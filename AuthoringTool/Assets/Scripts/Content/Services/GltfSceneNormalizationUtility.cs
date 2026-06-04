using UnityEngine;

namespace ARGallery.Content
{
    /// <summary>
    /// Fixes degenerate glTF hierarchies that produce extreme bounds (stretched hologram lines) after reload.
    /// </summary>
    public static class GltfSceneNormalizationUtility
    {
        private const float MaxReasonableExtentMeters = 25f;

        public static void SanitizeLoadedHierarchy(Transform root)
        {
            if (root == null)
                return;

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
                return;

            Bounds combined = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    combined.Encapsulate(renderers[i].bounds);
            }

            if (combined.extents.magnitude <= MaxReasonableExtentMeters)
                return;

            Debug.LogWarning(
                $"[GltfSceneNormalization] GLB bounds are extreme ({combined.extents.magnitude:F1}m). " +
                "Resetting loaded child transforms under ContentBody.");

            Transform contentBody = root;
            if (root.GetComponent<ModelContentContainerRoot>() != null)
            {
                ModelContentContainerRoot container = root.GetComponent<ModelContentContainerRoot>();
                contentBody = container.ContentBody != null ? container.ContentBody : root;
            }

            for (int i = 0; i < contentBody.childCount; i++)
            {
                Transform child = contentBody.GetChild(i);
                if (child == null)
                    continue;

                child.localPosition = Vector3.zero;
                child.localRotation = Quaternion.identity;
                child.localScale = Vector3.one;
            }
        }
    }
}
