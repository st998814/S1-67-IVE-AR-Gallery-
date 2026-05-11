using UnityEngine;

/// <summary>
/// Applies and normalizes object transforms in target-local space.
/// </summary>
public sealed class TargetLocalTransformService : MonoBehaviour
{
    [SerializeField] private float minUniformScale = 0.05f;
    [SerializeField] private float maxUniformScale = 8f;

    public float MinUniformScale => minUniformScale;
    public float MaxUniformScale => maxUniformScale;

    public void SetLocalPosition(Transform contentTransform, Vector3 localPosition)
    {
        if (contentTransform == null)
            return;
        contentTransform.localPosition = localPosition;
    }

    public void SetLocalRotation(Transform contentTransform, Quaternion localRotation)
    {
        if (contentTransform == null)
            return;
        contentTransform.localRotation = localRotation;
    }

    public void SetUniformLocalScale(Transform contentTransform, float scale)
    {
        if (contentTransform == null)
            return;
        float s = Mathf.Clamp(scale, minUniformScale, maxUniformScale);
        contentTransform.localScale = Vector3.one * s;
    }

    public void NormalizeUniformScale(Transform contentTransform)
    {
        if (contentTransform == null)
            return;

        Vector3 current = contentTransform.localScale;
        float average = (current.x + current.y + current.z) / 3f;
        SetUniformLocalScale(contentTransform, average);
    }
}
