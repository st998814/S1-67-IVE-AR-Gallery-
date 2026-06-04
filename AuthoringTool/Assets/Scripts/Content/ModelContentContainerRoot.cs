using UnityEngine;

/// <summary>
/// Marks the volumetric content prefab root and resolves the <c>ContentBody</c> child used as the
/// attach parent for runtime-loaded model hierarchies (ContentContainer → ContentBody → model).
/// </summary>
[DisallowMultipleComponent]
public class ModelContentContainerRoot : MonoBehaviour
{
    private Transform contentBodyCached;

    public void NotifyGlbLoadCompleted(bool success)
    {
        if (!success)
            return;

        SpatialMappingCoordinator mapping = FindFirstObjectByType<SpatialMappingCoordinator>();
        mapping?.RefreshForCurrentSelection();
    }

    /// <summary>Child that receives the loaded model root; falls back to this transform if missing.</summary>
    public Transform ContentBody
    {
        get
        {
            if (contentBodyCached == null)
                contentBodyCached = transform.Find("ContentBody");
            return contentBodyCached != null ? contentBodyCached : transform;
        }
    }
}
