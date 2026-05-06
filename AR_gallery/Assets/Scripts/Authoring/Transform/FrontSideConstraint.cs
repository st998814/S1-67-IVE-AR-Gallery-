using UnityEngine;

/// <summary>
/// Keeps content in front of the target plane by clamping local Z (relative to <see cref="Transform.parent"/>).
/// Optionally pushes content out of overlap with a target collider (kinematic overlap is otherwise allowed in Unity).
/// </summary>
public sealed class FrontSideConstraint : MonoBehaviour
{
    private enum FrontSideAxis
    {
        PositiveLocalZ = 0,
        NegativeLocalZ = 1
    }

    [Tooltip("Which local-Z direction counts as front side. Use NegativeLocalZ when your target faces the opposite direction.")]
    [SerializeField] private FrontSideAxis frontSideAxis = FrontSideAxis.NegativeLocalZ;

    [Tooltip("Base offset from the target plane along the chosen front side axis.")]
    [SerializeField] private float frontOffset = 0.5f;

    [Tooltip("Extra margin in local Z (e.g. half thickness of a 3D target cube along forward, in ContentRoot space).")]
    [SerializeField] private float additionalMinimumLocalZ;

    [Tooltip("If true, after Z clamp, separate content from a solid target collider using Physics.ComputePenetration.")]
    [SerializeField] private bool resolvePhysicsOverlap;

    [Tooltip("Collider on the target volume (e.g. Target cube). If null, tries to find one under TargetRoot excluding ContentRoot.")]
    [SerializeField] private Collider targetBlockingCollider;

    [SerializeField] private Transform targetRoot;
    [SerializeField] private Transform contentRoot;

    [SerializeField] private int maxDepenetrateIterations = 4;

    public float FrontOffset => frontOffset;
    public float EffectiveMinimumLocalZ => frontOffset + additionalMinimumLocalZ;
    public float FrontDirectionSign => frontSideAxis == FrontSideAxis.PositiveLocalZ ? 1f : -1f;

    private void Awake()
    {
        // Normalize to the project convention to avoid posture-dependent inversion.
        frontSideAxis = FrontSideAxis.NegativeLocalZ;

        if (targetRoot == null)
        {
            GameObject go = GameObject.Find("TargetRoot");
            if (go != null)
                targetRoot = go.transform;
        }

        if (contentRoot == null && targetRoot != null)
            contentRoot = targetRoot.Find("ContentRoot");

        if (targetBlockingCollider == null)
            targetBlockingCollider = FindFirstTargetColliderExcludingContent();
    }

    /// <summary>
    /// Repoints constraint resolution when the active authoring target changes (multi-target sessions).
    /// </summary>
    public void SetTargetContext(Transform newTargetRoot, Transform newContentRoot)
    {
        targetRoot = newTargetRoot;
        contentRoot = newContentRoot;
        targetBlockingCollider = FindFirstTargetColliderExcludingContent();
    }

    public bool Enforce(Transform contentTransform)
    {
        if (contentTransform == null)
            return false;

        float minZ = EffectiveMinimumLocalZ;
        bool changed = false;

        Vector3 localPos = contentTransform.localPosition;
        float clampedZ = ClampToFront(localPos.z, minZ);
        if (!Mathf.Approximately(clampedZ, localPos.z))
        {
            localPos.z = clampedZ;
            contentTransform.localPosition = localPos;
            changed = true;
        }

        if (resolvePhysicsOverlap && targetBlockingCollider != null && CanUseAsSolidBlockingCollider(targetBlockingCollider))
        {
            Collider contentCol = contentTransform.GetComponent<Collider>()
                ?? contentTransform.GetComponentInChildren<Collider>();
            if (contentCol != null && contentCol != targetBlockingCollider)
            {
                for (int i = 0; i < maxDepenetrateIterations; i++)
                {
                    if (!Physics.ComputePenetration(
                            targetBlockingCollider,
                            targetBlockingCollider.transform.position,
                            targetBlockingCollider.transform.rotation,
                            contentCol,
                            contentCol.transform.position,
                            contentCol.transform.rotation,
                            out Vector3 dir,
                            out float dist)
                        || dist <= 1e-5f)
                        break;

                    contentTransform.position += dir * dist;
                    changed = true;
                }
            }
        }

        localPos = contentTransform.localPosition;
        clampedZ = ClampToFront(localPos.z, minZ);
        if (!Mathf.Approximately(clampedZ, localPos.z))
        {
            localPos.z = clampedZ;
            contentTransform.localPosition = localPos;
            changed = true;
        }

        return changed;
    }

    private float ClampToFront(float localZ, float minOffset)
    {
        if (frontSideAxis == FrontSideAxis.PositiveLocalZ)
            return Mathf.Max(minOffset, localZ);

        return Mathf.Min(-minOffset, localZ);
    }

    private Collider FindFirstTargetColliderExcludingContent()
    {
        if (targetRoot == null)
            return null;

        Collider[] colliders = targetRoot.GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider c = colliders[i];
            if (c == null)
                continue;
            if (contentRoot != null && (c.transform == contentRoot || c.transform.IsChildOf(contentRoot)))
                continue;
            return c;
        }

        return null;
    }

    private static bool CanUseAsSolidBlockingCollider(Collider collider)
    {
        if (collider == null)
            return false;

        // Thin non-convex MeshCollider surfaces (e.g. a Quad target plane)
        // can produce unstable penetration corrections for this use case.
        if (collider is MeshCollider meshCollider && !meshCollider.convex)
            return false;

        return true;
    }
}
