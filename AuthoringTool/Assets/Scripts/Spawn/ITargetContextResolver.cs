using UnityEngine;

namespace ARGallery.Spawning
{
    /// <summary>
    /// Resolves target context for spawn workflows without UI dependencies.
    /// </summary>
    public interface ITargetContextResolver
    {
        GameObject GetActiveTarget();
        string ResolveTargetIdOrActive(string preferredTargetId);
        bool TryGetTarget(string preferredTargetId, out GameObject targetObject);
        bool TryGetContentRoot(string preferredTargetId, out Transform contentRoot);
    }
}
