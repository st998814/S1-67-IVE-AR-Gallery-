using System;
using System.IO;
using UnityEngine;

namespace ARGallery.Content
{
    /// <summary>
    /// Single entry for runtime content creation: infers render/media kind, routes to
    /// <see cref="ContentWorkflowService"/> for surface text/image, and reserves volumetric paths.
    /// API sync stays delegated to the workflow service without routing logic here.
    /// </summary>
    public class ContentCreationCoordinator
    {
        private readonly ContentWorkflowService workflow = new ContentWorkflowService();

        /// <summary>Unified spawn result after upload-driven creation (surface or future volumetric).</summary>
        public struct LocalContentSpawnOutcome
        {
            public bool success;
            public string message;
            public ContentRenderKind renderKind;
            public ContentMediaKind mediaKind;
            /// <summary>UI label, e.g. "Image (file)" or "Text".</summary>
            public string contentTypeLabel;
            public GameObject spawnedObject;
            public DraggableObject draggableObject;
        }

        public static ContentRenderKind GetRenderKind(ContentMediaKind mediaKind)
        {
            return mediaKind == ContentMediaKind.Model ? ContentRenderKind.Volumetric : ContentRenderKind.Surface;
        }

        /// <summary>Infers media kind from file name extension (upload or local path).</summary>
        public static ContentMediaKind InferMediaKindFromFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return ContentMediaKind.Image;

            string ext = Path.GetExtension(fileName).ToLowerInvariant();
            switch (ext)
            {
                case ".glb":
                case ".gltf":
                case ".fbx":
                case ".obj":
                    return ContentMediaKind.Model;
                case ".mp4":
                case ".webm":
                case ".mov":
                    return ContentMediaKind.Video;
                case ".png":
                case ".jpg":
                case ".jpeg":
                case ".webp":
                case ".gif":
                case ".bmp":
                    return ContentMediaKind.Image;
                default:
                    return ContentMediaKind.Image;
            }
        }

        /// <summary>Surface text — delegates to <see cref="ContentWorkflowService.SpawnTextLocal"/>.</summary>
        public ContentWorkflowService.LocalTextSpawnResult SpawnText(GameObject textPrefab, string textToDisplay)
        {
            return workflow.SpawnTextLocal(textPrefab, textToDisplay);
        }

        /// <summary>
        /// After a successful content upload, spawn matching runtime content.
        /// Image → picture prefab + texture; video/model paths reserved with clear outcomes.
        /// </summary>
        public LocalContentSpawnOutcome SpawnFromContentUpload(
            MonoBehaviour runner,
            GameObject picturePrefab,
            string uploadedUrl,
            string originalFileName)
        {
            ContentMediaKind kind = InferMediaKindFromFileName(originalFileName);

            switch (kind)
            {
                case ContentMediaKind.Image:
                    return SpawnSurfaceImageInternal(runner, picturePrefab, uploadedUrl, originalFileName, kind);

                case ContentMediaKind.Video:
                    return new LocalContentSpawnOutcome
                    {
                        success = false,
                        message = "Video surface spawn is not implemented yet; use image or extend the video prefab path.",
                        renderKind = ContentRenderKind.Surface,
                        mediaKind = kind
                    };

                case ContentMediaKind.Model:
                    return new LocalContentSpawnOutcome
                    {
                        success = false,
                        message = "Model volumetric spawn is not implemented yet (pipeline pending).",
                        renderKind = ContentRenderKind.Volumetric,
                        mediaKind = kind
                    };

                case ContentMediaKind.Text:
                    return new LocalContentSpawnOutcome
                    {
                        success = false,
                        message = "Text content is spawned via the text spawn action, not file upload.",
                        renderKind = ContentRenderKind.Surface,
                        mediaKind = kind
                    };

                default:
                    return new LocalContentSpawnOutcome
                    {
                        success = false,
                        message = $"Unhandled content media kind: {kind}",
                        renderKind = GetRenderKind(kind),
                        mediaKind = kind
                    };
            }
        }

        private LocalContentSpawnOutcome SpawnSurfaceImageInternal(
            MonoBehaviour runner,
            GameObject picturePrefab,
            string uploadedUrl,
            string originalFileName,
            ContentMediaKind mediaKind)
        {
            string baseName = string.IsNullOrWhiteSpace(originalFileName) ? "image" : Path.GetFileName(originalFileName);
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(baseName);

            ContentWorkflowService.LocalImageSpawnResult imageResult =
                workflow.SpawnImageLocal(runner, picturePrefab, uploadedUrl, fileNameWithoutExt);

            if (!imageResult.success || imageResult.spawnedObject == null)
            {
                return new LocalContentSpawnOutcome
                {
                    success = false,
                    message = imageResult.message,
                    renderKind = ContentRenderKind.Surface,
                    mediaKind = mediaKind
                };
            }

            return new LocalContentSpawnOutcome
            {
                success = true,
                message = imageResult.message,
                renderKind = ContentRenderKind.Surface,
                mediaKind = mediaKind,
                contentTypeLabel = imageResult.contentType,
                spawnedObject = imageResult.spawnedObject,
                draggableObject = imageResult.draggableObject
            };
        }

        public IApiRequestHandle SyncCreateContent(
            IApiClient apiClient,
            string contentType,
            Vector3 localPosition,
            Vector3 localEuler,
            Vector3 localScale,
            string mediaUrl,
            string targetId,
            Action<ApiResult<CreateContentResponseDto>> onCompleted,
            float timeoutSeconds = 20f)
        {
            return workflow.SyncCreateContent(
                apiClient,
                contentType,
                localPosition,
                localEuler,
                localScale,
                mediaUrl,
                targetId,
                onCompleted,
                timeoutSeconds);
        }
    }
}
