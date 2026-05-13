using ARGallery.Workspace.Persistence;
using UnityEngine;

/// <summary>
/// Scales the authoring <see cref="RuntimeImageTargetFactory"/> quad so one scene unit matches
/// real-world meters (same convention as Vuforia target width). Default Unity quads are 1×1 unit;
/// this sets localScale to (physicalWidthM, physicalWidthM × aspect, 1).
/// </summary>
public static class TargetVisualPhysicalLayout
{
    public static void Apply(Transform targetVisual, float physicalWidthMeters, Texture textureOrNull)
    {
        if (targetVisual == null || physicalWidthMeters <= 1e-5f)
            return;

        float heightOverWidth = 1f;
        if (textureOrNull != null && textureOrNull.width > 0)
            heightOverWidth = (float)textureOrNull.height / textureOrNull.width;

        targetVisual.localScale = new Vector3(physicalWidthMeters, physicalWidthMeters * heightOverWidth, 1f);
    }

    public static void ApplyFromTargetRoot(GameObject targetRoot, Texture textureOrNull)
    {
        if (targetRoot == null)
            return;

        Transform visual = targetRoot.transform.Find("TargetVisual");
        if (visual == null)
            return;

        var auth = targetRoot.GetComponent<AuthoredTargetInstance>();
        float w = auth != null && auth.PhysicalWidthM > 1e-5f ? auth.PhysicalWidthM : 0.2f;
        Apply(visual, w, textureOrNull);
    }
}
