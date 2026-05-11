using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Local-first content workflow for image content spawning/preview.
/// </summary>
public class ContentWorkflowService
{
    private readonly RuntimeContentFactory contentFactory = new RuntimeContentFactory();

    public class LocalImageSpawnResult
    {
        public bool success;
        public string message;
        public string contentType;
        public GameObject spawnedObject;
        public DraggableObject draggableObject;
    }

    public class LocalTextSpawnResult
    {
        public bool success;
        public string message;
        public string contentType;
        public GameObject spawnedObject;
        public DraggableObject draggableObject;
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
        if (apiClient == null)
        {
            onCompleted?.Invoke(ApiResult<CreateContentResponseDto>.Fail(
                ApiErrorCodes.Unknown,
                "CreateContent sync skipped: no API client available."));
            return null;
        }

        string normalizedType = NormalizeContentType(contentType);
        
        bool requiresMediaUrl = normalizedType == "image" || normalizedType == "video" || normalizedType == "model";
        if (requiresMediaUrl && string.IsNullOrWhiteSpace(mediaUrl))
        {
            onCompleted?.Invoke(ApiResult<CreateContentResponseDto>.Fail(
                ApiErrorCodes.ValidationError,
                $"CreateContent validation failed: mediaUrl is required for contentType '{normalizedType}'."));
            return null;
        }

        string renderKind = MapRenderKind(normalizedType);
        string assetFormat = MapAssetFormat(normalizedType, mediaUrl);

        var request = new CreateContentRequestDto
        {
            contentId = Guid.NewGuid().ToString("N"),
            targetId = targetId ?? "",
            contentType = normalizedType,
            mediaUrl = mediaUrl ?? "",
            localPosition = new ApiVector3Dto(localPosition.x, localPosition.y, localPosition.z),
            localEuler = new ApiVector3Dto(localEuler.x, localEuler.y, localEuler.z),
            localScale = new ApiVector3Dto(localScale.x, localScale.y, localScale.z),
            renderKind = renderKind,
            assetFormat = assetFormat,
            meta = new ApiSyncMetaDto
            {
                schemaVersion = "v1",
                clientRequestId = Guid.NewGuid().ToString("N"),
                createdAtUtc = DateTime.UtcNow.ToString("o")
            }
        };

        return apiClient.CreateContent(request, onCompleted, timeoutSeconds);
    }


    public bool ReleaseSpawnedContent(GameObject instance)
    {
        return contentFactory.ReleaseToPool(instance);
    }

    public LocalImageSpawnResult SpawnImageLocal(
        MonoBehaviour runner,
        GameObject picturePrefab,
        string imageUrl, // the url of the image is required for spawning the content
        string fileNameWithoutExt)
    {
        RuntimeContentFactory.ContentCreateResult created = contentFactory.CreateImageContent(picturePrefab);
        if (!created.success || created.instance == null)
        {
            return new LocalImageSpawnResult
            {
                success = false,
                message = created.message
            };
        }

        GameObject spawnedPicObj = created.instance;
        DraggableObject dragHandler = created.draggable;

        if (runner != null && !string.IsNullOrWhiteSpace(imageUrl))
            runner.StartCoroutine(ApplyTextureToObjectRoutine(spawnedPicObj, imageUrl));

        string label = string.IsNullOrWhiteSpace(fileNameWithoutExt) ? "Image" : $"Image ({fileNameWithoutExt})";
        return new LocalImageSpawnResult
        {
            success = true,
            message = "Local image content spawned.",
            contentType = label,
            spawnedObject = spawnedPicObj,
            draggableObject = dragHandler
        };
    }

    public LocalImageSpawnResult SpawnImageLocalFromBytes(
        GameObject picturePrefab,
        byte[] imageBytes,
        string fileNameWithoutExt)
    {
        RuntimeContentFactory.ContentCreateResult created = contentFactory.CreateImageContent(picturePrefab);
        if (!created.success || created.instance == null)
        {
            return new LocalImageSpawnResult
            {
                success = false,
                message = created.message
            };
        }

        GameObject spawnedPicObj = created.instance;
        DraggableObject dragHandler = created.draggable;

        if (imageBytes == null || imageBytes.Length == 0)
        {
            contentFactory.ReleaseToPool(spawnedPicObj);
            return new LocalImageSpawnResult
            {
                success = false,
                message = "Selected local image has no bytes."
            };
        }

        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
        if (!texture.LoadImage(imageBytes, markNonReadable: false))
        {
            contentFactory.ReleaseToPool(spawnedPicObj);
            UnityEngine.Object.Destroy(texture);
            return new LocalImageSpawnResult
            {
                success = false,
                message = "Failed to decode local image bytes."
            };
        }

        Renderer renderer = spawnedPicObj.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.mainTexture = texture;

        string label = string.IsNullOrWhiteSpace(fileNameWithoutExt) ? "Image" : $"Image ({fileNameWithoutExt})";
        return new LocalImageSpawnResult
        {
            success = true,
            message = "Local image content spawned from bytes.",
            contentType = label,
            spawnedObject = spawnedPicObj,
            draggableObject = dragHandler
        };
    }

    public LocalTextSpawnResult SpawnTextLocal(
        GameObject textPrefab,
        string textToDisplay)
    {
        RuntimeContentFactory.ContentCreateResult created = contentFactory.CreateTextContent(textPrefab, textToDisplay);
        if (!created.success || created.instance == null)
        {
            return new LocalTextSpawnResult
            {
                success = false,
                message = created.message
            };
        }

        return new LocalTextSpawnResult
        {
            success = true,
            message = "Local text content spawned.",
            contentType = "Text",
            spawnedObject = created.instance,
            draggableObject = created.draggable
        };
    }

    private IEnumerator ApplyTextureToObjectRoutine(GameObject objToTex, string url)
    {
        if (objToTex == null || string.IsNullOrWhiteSpace(url))
            yield break;

        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
        {
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Error loading image for preview: " + request.error);
                yield break;
            }

            Texture2D texture = ((DownloadHandlerTexture)request.downloadHandler).texture;
            Renderer renderer = objToTex.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.mainTexture = texture;
        }
    }

    private static string MapRenderKind(string normalizedType)
    {
        return normalizedType == "model" ? "volumetric" : "surface";
    }

    private static string MapAssetFormat(string normalizedType, string mediaUrl)
    {
        if (normalizedType != "model")
            return "";

        if (string.IsNullOrWhiteSpace(mediaUrl))
            return "";

        string trimmed = mediaUrl.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out Uri uri))
            return GetFormatFromPath(uri.AbsolutePath);

        return GetFormatFromPath(trimmed);
    }

    private static string GetFormatFromPath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        string ext = System.IO.Path.GetExtension(value).ToLowerInvariant();
        if (ext == ".glb")
            return "glb";
        if (ext == ".gltf")
            return "gltf";

        return ext.StartsWith(".") ? ext.Substring(1) : ext;
    }

    private static string NormalizeContentType(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "empty";

        string lower = value.Trim().ToLowerInvariant();
        if (lower.Contains("text"))
            return "text";
        if (lower.Contains("video") || lower.Contains("youtube"))
            return "video";
        if (lower.Contains("image") || lower.Contains("poster") || lower.Contains("picture"))
            return "image";
        return lower;
    }
}
