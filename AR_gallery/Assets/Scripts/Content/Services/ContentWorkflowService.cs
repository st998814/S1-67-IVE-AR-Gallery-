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
        var request = new CreateContentRequestDto
        {
            contentId = Guid.NewGuid().ToString("N"),
            targetId = targetId ?? "",
            contentType = normalizedType,
            mediaUrl = mediaUrl ?? "",
            localPosition = new ApiVector3Dto(localPosition.x, localPosition.y, localPosition.z),
            localEuler = new ApiVector3Dto(localEuler.x, localEuler.y, localEuler.z),
            localScale = new ApiVector3Dto(localScale.x, localScale.y, localScale.z),
            meta = new ApiSyncMetaDto
            {
                schemaVersion = "v1",
                clientRequestId = Guid.NewGuid().ToString("N"),
                createdAtUtc = DateTime.UtcNow.ToString("o")
            }
        };

        return apiClient.CreateContent(request, onCompleted, timeoutSeconds);
    }

    public LocalImageSpawnResult SpawnImageLocal(
        MonoBehaviour runner,
        GameObject picturePrefab,
        string imageUrl,
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
