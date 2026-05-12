using UnityEngine;

namespace ARGallery.Workspace.Persistence
{
    /// <summary>
    /// Attached to an authored image-target root so it can be round-tripped through <see cref="WorkspaceSnapshot"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AuthoredTargetInstance : MonoBehaviour
    {
        [Tooltip("Stable workspace-scoped id (e.g. matches logical target / session id).")]
        public string LocalTargetId = "";

        public string ServerTargetId = "";
        public string VuforiaTargetId = "";
        public string TargetName = "";
        /// <summary>Relative path under workspace folder, e.g. assets/targets/&lt;id&gt;.png</summary>
        public string TargetImageLocalPath = "";
        public string OriginalFileName = "";

        private void Awake()
        {
            AuthoredObjectRegistry.RegisterTarget(this);
        }

        private void OnDestroy()
        {
            AuthoredObjectRegistry.UnregisterTarget(this);
        }
    }
}
