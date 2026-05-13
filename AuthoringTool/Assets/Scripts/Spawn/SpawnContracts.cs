using UnityEngine;

namespace ARGallery.Spawning
{
    /// <summary>
    /// High-level spawn request category.
    /// </summary>
    public enum SpawnRequestKind
    {
        Content,
        Target
    }

    /// <summary>
    /// Semantic content category used by spawn routing.
    /// </summary>
    public enum SpawnContentType
    {
        Image,
        Text,
        Model,
        Video
    }

    /// <summary>
    /// Rendering route hint for spawned content.
    /// </summary>
    public enum SpawnRenderKind
    {
        Surface,
        Volumetric
    }

    /// <summary>
    /// Optional local transform override applied after parenting.
    /// </summary>
    public struct SpawnTransformData
    {
        public Vector3 localPosition;
        public Vector3 localEuler;
        public Vector3 localScale;
    }

    /// <summary>
    /// UI-agnostic request model for runtime content creation.
    /// </summary>
    public class SpawnRequest
    {
        public SpawnContentType contentType = SpawnContentType.Image;
        public string mediaUrl = "";
        public string textPayload = "";
        public string targetId = "";
        public string originalFileName = "";
        public byte[] localFileBytes;
        public string localMimeType = "";
        public bool isLocalDraft;
        public bool hasTransformOverride;
        public SpawnTransformData transformOverride;

        /// <summary>
        /// Optional route hint. If empty, manager derives from content type.
        /// </summary>
        public string renderKind = "";

        /// <summary>
        /// When set, <see cref="ISpawnerManager.BeginSyncCreateContent"/> uses this as POST <c>contentId</c> for stable upserts.
        /// If empty, resolved from <see cref="ARGallery.Workspace.Persistence.AuthoredContentInstance.ServerContentId"/> on the spawned transform, then a new GUID.
        /// </summary>
        public string contentIdOverride = "";
    }

    /// <summary>
    /// UI-agnostic request model for runtime target creation.
    /// </summary>
    public class SpawnTargetRequest
    {
        public string targetName = "";
        public string targetId = "";
        public string displayLabel = "";
        public string targetImageUrl = "";
    }

    /// <summary>
    /// Unified content creation result shape for caller usage.
    /// </summary>
    public struct SpawnContentResult
    {
        public bool success;
        public string message;
        public GameObject spawnedObject;
        public DraggableObject draggableObject;
        public SpawnContentType contentType;
        public SpawnRenderKind renderKind;
    }

    /// <summary>
    /// Unified target creation result shape for caller usage.
    /// </summary>
    public struct SpawnTargetResult
    {
        public bool success;
        public bool isDuplicate;
        public int duplicateIndex;
        public string targetId;
        public string message;
        public GameObject targetObject;
    }
}
