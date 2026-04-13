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

            return MediaKindFromExtension(Path.GetExtension(fileName));
        }

        /// <summary>
        /// Infers kind for an upload using local file name and server URL. WebGL often omits extensions in
        /// <c>fileInfo.name</c>, so when the name is inconclusive (treated as image), the URL path is used (e.g. <c>.../Dimond.glb</c>).
        /// </summary>
        public static ContentMediaKind InferMediaKindForContentUpload(string originalFileName, string uploadedUrl)
        {
            ContentMediaKind fromName = InferMediaKindFromFileName(originalFileName);
            if (fromName != ContentMediaKind.Image)
                return fromName;

            string pathForExt = GetUrlPathForExtension(uploadedUrl);
            if (string.IsNullOrEmpty(pathForExt))
                return fromName;

            ContentMediaKind fromUrl = InferMediaKindFromFileName(pathForExt);
            return fromUrl != ContentMediaKind.Image ? fromUrl : fromName;
        }

        private static ContentMediaKind MediaKindFromExtension(string ext)
        {
            if (string.IsNullOrEmpty(ext))
                return ContentMediaKind.Image;

            switch (ext.ToLowerInvariant())
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

        /// <summary>Absolute URL path or last segment, for extension parsing when local file name has no suffix.</summary>
        private static string GetUrlPathForExtension(string uploadedUrl)
        {
            if (string.IsNullOrWhiteSpace(uploadedUrl))
                return null;

            if (Uri.TryCreate(uploadedUrl.Trim(), UriKind.Absolute, out Uri uri))
                return uri.AbsolutePath;

            return uploadedUrl;
        }

        /// <summary>Prefer extension from local file name; fall back to upload URL (e.g. WebGL strips <c>.glb</c> from name).</summary>
        private static string GetExtensionForUpload(string originalFileName, string uploadedUrl)
        {
            string e = Path.GetExtension(originalFileName ?? "").ToLowerInvariant();
            if (!string.IsNullOrEmpty(e))
                return e;

            string path = GetUrlPathForExtension(uploadedUrl);
            return string.IsNullOrEmpty(path) ? "" : Path.GetExtension(path).ToLowerInvariant();
        }

        /// <summary>Surface text — delegates to <see cref="ContentWorkflowService.SpawnTextLocal"/>.</summary>
        public ContentWorkflowService.LocalTextSpawnResult SpawnText(GameObject textPrefab, string textToDisplay)
        {
            return workflow.SpawnTextLocal(textPrefab, textToDisplay);
        }

        /// <summary>
        /// After a successful content upload, spawn matching runtime content.
        /// Image → picture prefab + texture; .glb → model container + <see cref="ModelLoadService"/>; video path reserved.
        /// </summary>
        /// <param name="picturePrefab">Surface image prefab (unused for volumetric .glb).</param>
        /// <param name="modelContainerPrefab">ContentContainer → ContentBody hierarchy; assign from authoring UI.</param>
        public LocalContentSpawnOutcome SpawnFromContentUpload(
            MonoBehaviour runner,
            GameObject picturePrefab,
            GameObject modelContainerPrefab,
            string uploadedUrl,
            string originalFileName)
        {
            ContentMediaKind kind = InferMediaKindForContentUpload(originalFileName, uploadedUrl);

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
                    return SpawnModelFromUpload(runner, modelContainerPrefab, uploadedUrl, originalFileName, kind);

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

        private LocalContentSpawnOutcome SpawnModelFromUpload(
            MonoBehaviour runner,
            GameObject modelContainerPrefab,
            string uploadedUrl,
            string originalFileName,
            ContentMediaKind mediaKind)
        {
            string ext = GetExtensionForUpload(originalFileName, uploadedUrl);
            if (ext != ".glb")
            {
                return new LocalContentSpawnOutcome
                {
                    success = false,
                    message = "Runtime volumetric load supports .glb only (glTFast).",
                    renderKind = ContentRenderKind.Volumetric,
                    mediaKind = mediaKind
                };
            }

            if (modelContainerPrefab == null)
            {
                return new LocalContentSpawnOutcome
                {
                    success = false,
                    message = "Assign the model content container prefab for 3D uploads.",
                    renderKind = ContentRenderKind.Volumetric,
                    mediaKind = mediaKind
                };
            }

            GameObject instance = UnityEngine.Object.Instantiate(modelContainerPrefab);
            ModelContentContainerRoot root = instance.GetComponent<ModelContentContainerRoot>();
            if (root == null)
                root = instance.AddComponent<ModelContentContainerRoot>();

            DraggableObject drag = instance.GetComponent<DraggableObject>();

            string baseName = !string.IsNullOrWhiteSpace(originalFileName)
                ? Path.GetFileName(originalFileName)
                : Path.GetFileName(GetUrlPathForExtension(uploadedUrl) ?? "");
            if (string.IsNullOrWhiteSpace(baseName))
                baseName = "model";
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(baseName);

            ModelLoadService.BeginLoadGlb(runner, uploadedUrl, root, outcome =>
            {
                if (!outcome.success)
                    Debug.LogError("[ModelLoadService] " + outcome.message);
            });

            return new LocalContentSpawnOutcome
            {
                success = true,
                message = "Model container spawned; GLB load started.",
                renderKind = ContentRenderKind.Volumetric,
                mediaKind = mediaKind,
                contentTypeLabel = $"Model ({fileNameWithoutExt})",
                spawnedObject = instance,
                draggableObject = drag
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
