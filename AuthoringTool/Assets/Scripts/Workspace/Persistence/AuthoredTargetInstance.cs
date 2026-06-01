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
        /// <summary>Relative path under workspace folder for optional placement reference photo.</summary>
        public string TargetReferenceLocalPath = "";
        /// <summary>Public URL after last successful reference upload (Layer 3).</summary>
        public string TargetReferenceImageUrl = "";
        public string OriginalFileName = "";
        public string TargetReferenceOriginalFileName = "";
        /// <summary>Layer 3: reference image needs backend upload.</summary>
        public bool TargetReferenceRemoteDirty;

        /// <summary>Printed target width in meters (Vuforia / runtime convention). Drives TargetVisual quad scale.</summary>
        public float PhysicalWidthM = 0.2f;

        /// <summary>Layer 3: persists with <see cref="TargetSnapshot.remoteDirty"/>.</summary>
        public bool RemoteDirty;
        /// <summary>ISO-8601 UTC; aligns with <see cref="TargetSnapshot.lastRemoteSyncedAtUtc"/>.</summary>
        public string LastRemoteSyncedAtUtc = "";

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
