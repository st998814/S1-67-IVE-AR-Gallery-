using UnityEngine;

/// <summary>
/// Keeps content in front of the target plane by clamping local Z.
/// </summary>
public sealed class FrontSideConstraint : MonoBehaviour
{
    [SerializeField] private float frontOffset = 0.02f;

    public float FrontOffset => frontOffset;

    public bool Enforce(Transform contentTransform)
    {
        if (contentTransform == null)
            return false;

        Vector3 localPos = contentTransform.localPosition;
        float clampedZ = Mathf.Max(frontOffset, localPos.z);
        if (Mathf.Approximately(clampedZ, localPos.z))
            return false;

        localPos.z = clampedZ;
        contentTransform.localPosition = localPos;
        return true;
    }
}
