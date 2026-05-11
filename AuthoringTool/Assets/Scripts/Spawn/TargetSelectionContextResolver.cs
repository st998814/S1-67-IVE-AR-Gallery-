using UnityEngine;

namespace ARGallery.Spawning
{
    /// <summary>
    /// Unity-backed resolver that uses TargetSelectionManager to resolve active or explicit target context.
    /// </summary>
    public class TargetSelectionContextResolver : ITargetContextResolver
    {
        private TargetSelectionManager targetSelectionManager;

        public TargetSelectionContextResolver(TargetSelectionManager targetSelectionManager = null)
        {
            this.targetSelectionManager = targetSelectionManager;
        }

        public GameObject GetActiveTarget()
        {
            TargetSelectionManager manager = ResolveTargetSelectionManager();
            return manager != null ? manager.GetActiveTarget() : null;
        }

        public string ResolveTargetIdOrActive(string preferredTargetId)
        {
            if (!string.IsNullOrWhiteSpace(preferredTargetId))
                return preferredTargetId.Trim();

            TargetSelectionManager manager = ResolveTargetSelectionManager();
            if (manager == null || manager.TargetCount == 0)
                return "";

            int activeIndex = Mathf.Clamp(manager.ActiveTargetIndex, 0, manager.TargetCount - 1);
            return manager.GetTargetId(activeIndex);
        }

        public bool TryGetTarget(string preferredTargetId, out GameObject targetObject)
        {
            targetObject = null;
            TargetSelectionManager manager = ResolveTargetSelectionManager();
            if (manager == null || manager.TargetCount == 0)
                return false;

            string resolvedTargetId = ResolveTargetIdOrActive(preferredTargetId);
            if (!string.IsNullOrWhiteSpace(resolvedTargetId))
            {
                int indexById = manager.FindTargetIndexById(resolvedTargetId);
                if (indexById >= 0)
                {
                    targetObject = manager.GetTargetAt(indexById);
                    return targetObject != null;
                }
            }

            targetObject = manager.GetActiveTarget();
            return targetObject != null;
        }

        public bool TryGetContentRoot(string preferredTargetId, out Transform contentRoot)
        {
            contentRoot = null;
            if (!TryGetTarget(preferredTargetId, out GameObject targetObject) || targetObject == null)
                return false;

            contentRoot = targetObject.transform.Find("ContentRoot");
            return contentRoot != null;
        }

        private TargetSelectionManager ResolveTargetSelectionManager()
        {
            if (targetSelectionManager != null)
                return targetSelectionManager;

            targetSelectionManager = Object.FindFirstObjectByType<TargetSelectionManager>();
            if (targetSelectionManager != null)
                return targetSelectionManager;

            TargetSelectionManager[] candidates = Resources.FindObjectsOfTypeAll<TargetSelectionManager>();
            foreach (TargetSelectionManager candidate in candidates)
            {
                if (candidate == null || !candidate.gameObject.scene.IsValid())
                    continue;

                targetSelectionManager = candidate;
                break;
            }

            return targetSelectionManager;
        }
    }
}
