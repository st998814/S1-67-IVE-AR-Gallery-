using UnityEngine;

/// <summary>
/// Resolves placement boundary limits for content on an AR target and clamps local positions.
/// XY extents come from <see cref="TargetVisual"/> scale; Z from <see cref="FrontSideConstraint"/>.
/// </summary>
public sealed class PlacementBoundsService : MonoBehaviour
{
    [SerializeField] private FrontSideConstraint frontSideConstraint;
    [Tooltip("Inset from TargetVisual edges along X and Y (ContentRoot-local).")]
    [SerializeField] private float edgeMargin = 0.02f;
    [Tooltip("Maximum distance content may extend from the target plane along local Z (meters).")]
    [SerializeField] private float maxDepthFromTarget = 2f;
    [Tooltip("Fallback half-extent when TargetVisual is missing (meters).")]
    [SerializeField] private float fallbackHalfExtent = 0.1f;

    [SerializeField] private Transform targetRoot;
    [SerializeField] private Transform contentRoot;

    public float EdgeMargin => edgeMargin;
    public float MaxDepthFromTarget => maxDepthFromTarget;

    public void Configure(FrontSideConstraint frontConstraintRef)
    {
        if (frontConstraintRef != null)
            frontSideConstraint = frontConstraintRef;
    }

    /// <summary>
    /// Repoints bounds resolution when the active authoring target changes.
    /// </summary>
    public void SetTargetContext(Transform newTargetRoot, Transform newContentRoot)
    {
        targetRoot = newTargetRoot;
        contentRoot = newContentRoot;
    }

    public bool TryGetBoundsForContent(Transform content, out PlacementBoundsCalculator.Snapshot bounds)
    {
        bounds = default;
        if (content == null)
            return false;

        if (!TryResolveTargetVisualScale(content, out Vector3 targetVisualLocalScale))
            targetVisualLocalScale = Vector3.one * (fallbackHalfExtent * 2f);

        if (!TryGetFrontZParameters(out float effectiveMinZ, out bool negativeFrontZ))
        {
            effectiveMinZ = 0.5f;
            negativeFrontZ = true;
        }

        bounds = PlacementBoundsCalculator.Compute(
            targetVisualLocalScale,
            edgeMargin,
            effectiveMinZ,
            negativeFrontZ,
            maxDepthFromTarget);

        return true;
    }

    public PlacementBoundsCalculator.AxisRange GetAxisRange(Transform content, PlacementBoundsCalculator.SemanticAxis axis)
    {
        if (!TryGetBoundsForContent(content, out PlacementBoundsCalculator.Snapshot bounds))
            return new PlacementBoundsCalculator.AxisRange(0f, 0f);

        return bounds.GetRange(axis);
    }

    public Vector3 ClampLocalPosition(Transform content, Vector3 localPosition)
    {
        if (content == null)
            return localPosition;

        if (!TryGetBoundsForContent(content, out PlacementBoundsCalculator.Snapshot bounds))
            return localPosition;

        return bounds.Clamp(localPosition);
    }

    public Vector3 SetSemanticAxis(Transform content, PlacementBoundsCalculator.SemanticAxis axis, float value)
    {
        if (content == null)
            return Vector3.zero;

        Vector3 localPosition = content.localPosition;
        localPosition = PlacementBoundsCalculator.SetAxisComponent(localPosition, axis, value);
        localPosition = ClampLocalPosition(content, localPosition);
        return localPosition;
    }

    private bool TryResolveTargetVisualScale(Transform content, out Vector3 targetVisualLocalScale)
    {
        targetVisualLocalScale = default;
        Transform root = ResolveTargetRoot(content);
        if (root == null)
            root = targetRoot;
        if (root == null)
            return false;

        Transform targetVisual = root.Find("TargetVisual");
        if (targetVisual == null)
            return false;

        targetVisualLocalScale = targetVisual.localScale;
        return targetVisualLocalScale.sqrMagnitude > 1e-8f;
    }

    private bool TryGetFrontZParameters(out float effectiveMinimumLocalZ, out bool negativeFrontLocalZ)
    {
        effectiveMinimumLocalZ = 0.5f;
        negativeFrontLocalZ = true;

        if (frontSideConstraint == null)
            frontSideConstraint = FindFirstObjectByType<FrontSideConstraint>();
        if (frontSideConstraint == null)
            return false;

        effectiveMinimumLocalZ = frontSideConstraint.EffectiveMinimumLocalZ;
        negativeFrontLocalZ = frontSideConstraint.FrontDirectionSign < 0f;
        return true;
    }

    private static Transform ResolveTargetRoot(Transform content)
    {
        if (content == null)
            return null;

        Transform current = content;
        while (current != null)
        {
            if (string.Equals(current.name, "ContentRoot", System.StringComparison.Ordinal))
                return current.parent;
            current = current.parent;
        }

        return content.parent != null ? content.parent.parent : null;
    }

    private void Awake()
    {
        if (frontSideConstraint == null)
            frontSideConstraint = FindFirstObjectByType<FrontSideConstraint>();

        if (targetRoot == null)
        {
            GameObject go = GameObject.Find("TargetRoot");
            if (go != null)
                targetRoot = go.transform;
        }

        if (contentRoot == null && targetRoot != null)
            contentRoot = targetRoot.Find("ContentRoot");
    }
}
