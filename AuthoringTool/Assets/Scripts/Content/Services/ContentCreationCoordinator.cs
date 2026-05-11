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

        public struct LocalContentSpawnOutcome
        {
            public bool success;
            public string message;
            public ContentRenderKind renderKind;
            public ContentMediaKind mediaKind;
            public string contentTypeLabel;
            public GameObject spawnedObject;
            public DraggableObject draggableObject;
        }

        public static ContentRenderKind GetRenderKind(ContentMediaKind mediaKind)
        {
            return mediaKind == ContentMediaKind.Model ? ContentRenderKind.Volumetric : ContentRenderKind.Surface;
        }

        public static ContentMediaKind InferMediaKindFromFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return ContentMediaKind.Image;

            return MediaKindFromExtension(Path.GetExtension(fileName));
        }

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

        private static string GetUrlPathForExtension(string uploadedUrl)
        {
            if (string.IsNullOrWhiteSpace(uploadedUrl))
                return null;

            if (Uri.TryCreate(uploadedUrl.Trim(), UriKind.Absolute, out Uri uri))
                return uri.AbsolutePath;

            return uploadedUrl;
        }

        private static string GetExtensionForUpload(string originalFileName, string uploadedUrl)
        {
            string e = Path.GetExtension(originalFileName ?? "").ToLowerInvariant();
            if (!string.IsNullOrEmpty(e))
                return e;

            string path = GetUrlPathForExtension(uploadedUrl);
            return string.IsNullOrEmpty(path) ? "" : Path.GetExtension(path).ToLowerInvariant();
        }

        public ContentWorkflowService.LocalTextSpawnResult SpawnText(GameObject textPrefab, string textToDisplay)
        {
            return workflow.SpawnTextLocal(textPrefab, textToDisplay);
        }

        public LocalContentSpawnOutcome SpawnFromContentUpload(
            MonoBehaviour runner,
            GameObject picturePrefab,
            GameObject modelContainerPrefab,
            GameObject videoPrefab,
            string uploadedUrl,
            string originalFileName)
        {
            ContentMediaKind kind = InferMediaKindForContentUpload(originalFileName, uploadedUrl);

            // Safety net: if the URL clearly contains video formats but was missed
            if (kind == ContentMediaKind.Image && !string.IsNullOrEmpty(uploadedUrl))
            {
                string lowerUrl = uploadedUrl.ToLowerInvariant();
                if (lowerUrl.Contains(".mp4") || lowerUrl.Contains(".mov") || lowerUrl.Contains(".webm"))
                    kind = ContentMediaKind.Video;
            }

            switch (kind)
            {
                case ContentMediaKind.Image:
                    return SpawnSurfaceImageInternal(runner, picturePrefab, uploadedUrl, originalFileName, kind);

                case ContentMediaKind.Video:
                    // FIXED: Now properly routing to your existing helper method!
                    return SpawnSurfaceVideoInternal(runner, videoPrefab, uploadedUrl, originalFileName, kind);

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

        public LocalContentSpawnOutcome SpawnFromLocalFile(
            MonoBehaviour runner,
            GameObject picturePrefab,
            GameObject modelContainerPrefab,
            GameObject videoPrefab, 
            byte[] localFileBytes,
            string originalFileName,
            string localMimeType = "")
        {
            ContentMediaKind kind = InferMediaKindFromFileName(originalFileName);
            
            // THE CRITICAL FIX: The WebGL MIME Type Safety Net
            if (kind == ContentMediaKind.Image && !string.IsNullOrWhiteSpace(localMimeType))
            {
                string mime = localMimeType.ToLowerInvariant();
                if (IsLikelyModelMime(mime)) 
                    kind = ContentMediaKind.Model;
                else if (mime.Contains("video") || mime.Contains("mp4")) 
                    kind = ContentMediaKind.Video;
            }

            switch (kind)
            {
                case ContentMediaKind.Video:
                    if (videoPrefab == null)
                    {
                        return new LocalContentSpawnOutcome { success = false, message = "Video prefab is missing in the Coordinator call." };
                    }
                    
                    GameObject vObj = UnityEngine.Object.Instantiate(videoPrefab);
                    vObj.name = "Local_Video_Draft";
                    return new LocalContentSpawnOutcome { 
                        success = true, 
                        message = "Local video draft spawned.",
                        spawnedObject = vObj, 
                        draggableObject = vObj.GetComponent<DraggableObject>(),
                        renderKind = ContentRenderKind.Surface,
                        mediaKind = kind,
                        contentTypeLabel = "Video (Draft)"
                    };

                case ContentMediaKind.Image:
                    return SpawnSurfaceImageFromLocalBytes(picturePrefab, localFileBytes, originalFileName, kind);
                    
                case ContentMediaKind.Model:
                    return SpawnModelFromLocalBytes(runner, modelContainerPrefab, localFileBytes, originalFileName, kind);

                default:
                    return new LocalContentSpawnOutcome
                    {
                        success = false,
                        message = $"Local file spawn not supported for {kind}.",
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
                return new LocalContentSpawnOutcome { success = false, message = imageResult.message, renderKind = ContentRenderKind.Surface, mediaKind = mediaKind };
            }

            return new LocalContentSpawnOutcome { success = true, message = imageResult.message, renderKind = ContentRenderKind.Surface, mediaKind = mediaKind, contentTypeLabel = imageResult.contentType, spawnedObject = imageResult.spawnedObject, draggableObject = imageResult.draggableObject };
        }

        private LocalContentSpawnOutcome SpawnSurfaceVideoInternal(
            MonoBehaviour runner, 
            GameObject videoPrefab, 
            string uploadedUrl, 
            string originalFileName, 
            ContentMediaKind kind)
        {
            if (videoPrefab == null)
            {
                return new LocalContentSpawnOutcome { success = false, message = "Video prefab is missing in the Coordinator call.", renderKind = ContentRenderKind.Surface, mediaKind = kind };
            }

            GameObject spawned = UnityEngine.Object.Instantiate(videoPrefab);
            spawned.name = "Video_" + originalFileName;

            var vPlayer = spawned.GetComponent<UnityEngine.Video.VideoPlayer>();
            if (vPlayer != null)
            {
                vPlayer.source = UnityEngine.Video.VideoSource.Url;
                vPlayer.url = uploadedUrl;
                vPlayer.playOnAwake = true;
                vPlayer.Play();
            }

            return new LocalContentSpawnOutcome { success = true, message = "Video surface spawned successfully.", spawnedObject = spawned, draggableObject = spawned.GetComponent<DraggableObject>(), renderKind = ContentRenderKind.Surface, mediaKind = kind, contentTypeLabel = "Video" };
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
                return new LocalContentSpawnOutcome { success = false, message = "Runtime volumetric load supports .glb only.", renderKind = ContentRenderKind.Volumetric, mediaKind = mediaKind };

            if (modelContainerPrefab == null)
                return new LocalContentSpawnOutcome { success = false, message = "Assign model content container prefab.", renderKind = ContentRenderKind.Volumetric, mediaKind = mediaKind };
            
            GameObject instance = global::RuntimeContentPool.Shared.Acquire(RuntimeContentShellType.ModelShell, modelContainerPrefab);
            global::RuntimeContentPoolResetter.ResetForAcquire(instance, RuntimeContentShellType.ModelShell);

            ModelContentContainerRoot root = instance.GetComponent<ModelContentContainerRoot>();
            if (root == null) root = instance.AddComponent<ModelContentContainerRoot>();

            DraggableObject drag = instance.GetComponent<DraggableObject>();

            string baseName = !string.IsNullOrWhiteSpace(originalFileName) ? Path.GetFileName(originalFileName) : Path.GetFileName(GetUrlPathForExtension(uploadedUrl) ?? "");
            if (string.IsNullOrWhiteSpace(baseName)) baseName = "model";
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(baseName);

            ModelLoadService.BeginLoadGlb(runner, uploadedUrl, root, outcome =>
            {
                if (!outcome.success) Debug.LogError("[ModelLoadService] " + outcome.message);
            });

            return new LocalContentSpawnOutcome { success = true, message = "Model container spawned; GLB load started.", renderKind = ContentRenderKind.Volumetric, mediaKind = mediaKind, contentTypeLabel = $"Model ({fileNameWithoutExt})", spawnedObject = instance, draggableObject = drag };
        }

        private LocalContentSpawnOutcome SpawnSurfaceImageFromLocalBytes(
            GameObject picturePrefab,
            byte[] localFileBytes,
            string originalFileName,
            ContentMediaKind mediaKind)
        {
            string baseName = string.IsNullOrWhiteSpace(originalFileName) ? "image" : Path.GetFileName(originalFileName);
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(baseName);

            ContentWorkflowService.LocalImageSpawnResult imageResult =
                workflow.SpawnImageLocalFromBytes(picturePrefab, localFileBytes, fileNameWithoutExt);

            if (!imageResult.success || imageResult.spawnedObject == null)
                return new LocalContentSpawnOutcome { success = false, message = imageResult.message, renderKind = ContentRenderKind.Surface, mediaKind = mediaKind };

            return new LocalContentSpawnOutcome { success = true, message = imageResult.message, renderKind = ContentRenderKind.Surface, mediaKind = mediaKind, contentTypeLabel = imageResult.contentType, spawnedObject = imageResult.spawnedObject, draggableObject = imageResult.draggableObject };
        }

        private LocalContentSpawnOutcome SpawnModelFromLocalBytes(
            MonoBehaviour runner,
            GameObject modelContainerPrefab,
            byte[] localFileBytes,
            string originalFileName,
            ContentMediaKind mediaKind)
        {
            string ext = Path.GetExtension(originalFileName ?? "").ToLowerInvariant();
            if (ext != ".glb")
                return new LocalContentSpawnOutcome { success = false, message = "Runtime volumetric local load supports .glb only.", renderKind = ContentRenderKind.Volumetric, mediaKind = mediaKind };

            if (modelContainerPrefab == null)
                return new LocalContentSpawnOutcome { success = false, message = "Assign model container prefab.", renderKind = ContentRenderKind.Volumetric, mediaKind = mediaKind };

            GameObject instance = global::RuntimeContentPool.Shared.Acquire(RuntimeContentShellType.ModelShell, modelContainerPrefab);
            global::RuntimeContentPoolResetter.ResetForAcquire(instance, RuntimeContentShellType.ModelShell);

            ModelContentContainerRoot root = instance.GetComponent<ModelContentContainerRoot>();
            if (root == null) root = instance.AddComponent<ModelContentContainerRoot>();

            DraggableObject drag = instance.GetComponent<DraggableObject>();
            string baseName = string.IsNullOrWhiteSpace(originalFileName) ? "model.glb" : Path.GetFileName(originalFileName);
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(baseName);

            ModelLoadService.BeginLoadGlbBytes(runner, localFileBytes, baseName, root, outcome =>
            {
                if (!outcome.success) Debug.LogError("[ModelLoadService] " + outcome.message);
            });

            return new LocalContentSpawnOutcome { success = true, message = "Model container spawned; local GLB load started.", renderKind = ContentRenderKind.Volumetric, mediaKind = mediaKind, contentTypeLabel = $"Model ({fileNameWithoutExt})", spawnedObject = instance, draggableObject = drag };
        }

        private static bool IsLikelyModelMime(string mimeType)
        {
            if (string.IsNullOrWhiteSpace(mimeType))
                return false;

            string normalized = mimeType.Trim().ToLowerInvariant();
            return normalized.Contains("gltf") || normalized.Contains("glb") || normalized.Contains("model/");
        }

        public bool ReleaseSpawnedContent(GameObject instance)
        {
            return workflow.ReleaseSpawnedContent(instance);
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