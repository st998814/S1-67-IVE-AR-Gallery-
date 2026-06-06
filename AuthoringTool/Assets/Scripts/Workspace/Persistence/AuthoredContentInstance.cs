using UnityEngine;

namespace ARGallery.Workspace.Persistence
{
    /// <summary>
    /// Attached to spawned content under a target ContentRoot. Upload payloads use in-memory bytes and/or <see cref="MediaUrl"/>.
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
        [HideInInspector] public byte[] AssetBytes;
        /// <summary>Legacy Layer 2 path; not written at runtime.</summary>
        public string AssetLocalPath = "";
        public string OriginalFileName = "";
        public string MediaUrl = "";
        public string AssetFormat = "";
        public bool IsUnsaved;
        public bool UploadPending;
        public bool PersistPending;

        /// <summary>Layer 3: persists with <see cref="ContentSnapshot.remoteDirty"/>.</summary>
        public bool RemoteDirty;
        /// <summary>ISO-8601 UTC; aligns with <see cref="ContentSnapshot.lastRemoteSyncedAtUtc"/>.</summary>
        public string LastRemoteSyncedAtUtc = "";

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
