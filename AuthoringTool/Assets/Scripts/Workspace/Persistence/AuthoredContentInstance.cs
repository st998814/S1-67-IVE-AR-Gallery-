using UnityEngine;

namespace ARGallery.Workspace.Persistence
{
    /// <summary>
    /// Attached to spawned content under a target ContentRoot for snapshot serialization.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AuthoredContentInstance : MonoBehaviour
    {
        public string LocalContentId = "";
        public string ServerContentId = "";
        public string TargetId = "";
        /// <summary>Snapshot vocabulary: image | text | video | model | unknown</summary>
        public string ContentType = "unknown";
        /// <summary>e.g. surface | volumetric</summary>
        public string RenderKind = "";
        public string Title = "";
        public string Description = "";
        public string TextBody = "";
        public string AssetLocalPath = "";
        public string OriginalFileName = "";
        public string MediaUrl = "";
        public string AssetFormat = "";
        public bool IsUnsaved;
        public bool UploadPending;
        public bool PersistPending;

        private void Awake()
        {
            AuthoredObjectRegistry.RegisterContent(this);
        }

        private void OnDestroy()
        {
            AuthoredObjectRegistry.UnregisterContent(this);
        }
    }
}
