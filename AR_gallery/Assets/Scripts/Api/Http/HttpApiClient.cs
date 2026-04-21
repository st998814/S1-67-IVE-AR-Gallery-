using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Real API client for localhost backend.
/// Upload uses real endpoint.
/// </summary>
public class HttpApiClient : MonoBehaviour, IApiClient
{
    [Header("Backend")]
    [SerializeField] private string baseUrl = "http://127.0.0.1:5050";
    [SerializeField] private string uploadEndpoint = "/api/upload";
    [SerializeField] private string contentEndpoint = "/api/content";

    public IApiRequestHandle UploadFile(
        UploadFileRequestDto request,
        Action<ApiResult<UploadFileResponseDto>> onCompleted,
        float timeoutSeconds = 20f)
    {
        var handle = new CoroutineApiRequestHandle(this);
        Coroutine c = StartCoroutine(UploadFileRoutine(request, onCompleted, timeoutSeconds, handle));
        handle.BindCoroutine(c);
        return handle;
    }

    public IApiRequestHandle CreateTarget(
        CreateTargetRequestDto request,
        Action<ApiResult<CreateTargetResponseDto>> onCompleted,
        float timeoutSeconds = 20f)
    {
        onCompleted?.Invoke(ApiResult<CreateTargetResponseDto>.Fail(
            ApiErrorCodes.Unknown,
            "HttpApiClient.CreateTarget not wired yet. Implement /api/targets."));
        return null;
    }

    public IApiRequestHandle CreateContent(
        CreateContentRequestDto request,
        Action<ApiResult<CreateContentResponseDto>> onCompleted,
        float timeoutSeconds = 20f)
    {
        var handle = new CoroutineApiRequestHandle(this);
        Coroutine c = StartCoroutine(CreateContentRoutine(request, onCompleted, timeoutSeconds, handle));
        handle.BindCoroutine(c);
        return handle;
    }

    private IEnumerator UploadFileRoutine(
        UploadFileRequestDto request,
        Action<ApiResult<UploadFileResponseDto>> onCompleted,
        float timeoutSeconds,
        CoroutineApiRequestHandle handle)
    {
        if (request == null || request.fileBytes == null || request.fileBytes.Length == 0)
        {
            onCompleted?.Invoke(ApiResult<UploadFileResponseDto>.Fail(ApiErrorCodes.ValidationError, "fileBytes is empty"));
            handle.MarkDone();
            yield break;
        }

        string url = BuildUrl(uploadEndpoint);
        var form = new WWWForm();
        string fileName = string.IsNullOrWhiteSpace(request.fileName) ? "upload.bin" : request.fileName;
        form.AddBinaryData("file", request.fileBytes, fileName, string.IsNullOrWhiteSpace(request.mimeType) ? "application/octet-stream" : request.mimeType);

        using (UnityWebRequest uwr = UnityWebRequest.Post(url, form))
        {
            uwr.timeout = Mathf.Max(1, Mathf.RoundToInt(timeoutSeconds <= 0f ? 20f : timeoutSeconds));
            yield return uwr.SendWebRequest();

            if (handle.IsCancelled)
            {
                onCompleted?.Invoke(ApiResult<UploadFileResponseDto>.Fail(ApiErrorCodes.Cancelled, "Request cancelled"));
                handle.MarkDone();
                yield break;
            }

            if (uwr.result != UnityWebRequest.Result.Success)
            {
                string err = $"Upload failed: {uwr.error} HTTP {(long)uwr.responseCode}";
                onCompleted?.Invoke(ApiResult<UploadFileResponseDto>.Fail(ApiErrorCodes.NetworkError, err, (int)uwr.responseCode));
                handle.MarkDone();
                yield break;
            }

            string body = uwr.downloadHandler != null ? uwr.downloadHandler.text : "";
            UploadFileResponseDto parsed = JsonUtility.FromJson<UploadFileResponseDto>(body);
            if (parsed == null || string.IsNullOrWhiteSpace(parsed.url))
            {
                onCompleted?.Invoke(ApiResult<UploadFileResponseDto>.Fail(
                    ApiErrorCodes.ServerError,
                    "Upload succeeded but response has no url.",
                    (int)uwr.responseCode));
                handle.MarkDone();
                yield break;
            }

            if (string.IsNullOrWhiteSpace(parsed.fileName))
                parsed.fileName = fileName;
            if (string.IsNullOrWhiteSpace(parsed.mimeType))
                parsed.mimeType = request.mimeType;
            if (parsed.uploadedAtUtc == null || parsed.uploadedAtUtc.Length == 0)
                parsed.uploadedAtUtc = DateTime.UtcNow.ToString("o");

            onCompleted?.Invoke(ApiResult<UploadFileResponseDto>.Ok(parsed, "upload ok", (int)uwr.responseCode));
            handle.MarkDone();
        }
    }

    private string BuildUrl(string endpoint)
    {
        string basePart = string.IsNullOrWhiteSpace(baseUrl) ? "http://127.0.0.1:5050" : baseUrl.TrimEnd('/');
        string endPart = string.IsNullOrWhiteSpace(endpoint) ? "/api/upload" : endpoint.Trim();
        if (!endPart.StartsWith("/"))
            endPart = "/" + endPart;
        return basePart + endPart;
    }

    [Serializable]
    private class LegacyCreateContentRequestBody
    {
        public string ContentType;
        public float PosX;
        public float PosY;
        public float PosZ;
        public float Scale;
        public string MediaURL;
        public string TargetId;
    }

    [Serializable]
    private class LegacyCreateContentResponseBody
    {
        public int id;
        public string message;
    }

    private IEnumerator CreateContentRoutine(
        CreateContentRequestDto request,
        Action<ApiResult<CreateContentResponseDto>> onCompleted,
        float timeoutSeconds,
        CoroutineApiRequestHandle handle)
    {
        if (request == null)
        {
            onCompleted?.Invoke(ApiResult<CreateContentResponseDto>.Fail(ApiErrorCodes.ValidationError, "CreateContent request is null"));
            handle.MarkDone();
            yield break;
        }

        var legacyBody = new LegacyCreateContentRequestBody
        {
            ContentType = string.IsNullOrWhiteSpace(request.contentType) ? "empty" : request.contentType,
            PosX = request.localPosition != null ? request.localPosition.x : 0f,
            PosY = request.localPosition != null ? request.localPosition.y : 0f,
            PosZ = request.localPosition != null ? request.localPosition.z : 0f,
            Scale = request.localScale != null ? request.localScale.x : 1f,
            MediaURL = request.mediaUrl ?? "",
            TargetId = request.targetId ?? ""
        };

        string url = BuildUrl(contentEndpoint);
        string json = JsonUtility.ToJson(legacyBody);
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);

        using (UnityWebRequest uwr = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            uwr.uploadHandler = new UploadHandlerRaw(bodyRaw);
            uwr.downloadHandler = new DownloadHandlerBuffer();
            uwr.SetRequestHeader("Content-Type", "application/json");
            uwr.timeout = Mathf.Max(1, Mathf.RoundToInt(timeoutSeconds <= 0f ? 20f : timeoutSeconds));
            yield return uwr.SendWebRequest();

            if (handle.IsCancelled)
            {
                onCompleted?.Invoke(ApiResult<CreateContentResponseDto>.Fail(ApiErrorCodes.Cancelled, "Request cancelled"));
                handle.MarkDone();
                yield break;
            }

            if (uwr.result != UnityWebRequest.Result.Success)
            {
                string err = $"CreateContent failed: {uwr.error} HTTP {(long)uwr.responseCode}";
                onCompleted?.Invoke(ApiResult<CreateContentResponseDto>.Fail(ApiErrorCodes.NetworkError, err, (int)uwr.responseCode));
                handle.MarkDone();
                yield break;
            }

            string body = uwr.downloadHandler != null ? uwr.downloadHandler.text : "";
            LegacyCreateContentResponseBody parsed = null;
            try { parsed = JsonUtility.FromJson<LegacyCreateContentResponseBody>(body); }
            catch { parsed = null; }

            var response = new CreateContentResponseDto
            {
                contentId = !string.IsNullOrWhiteSpace(request.contentId)
                    ? request.contentId
                    : (parsed != null && parsed.id > 0 ? parsed.id.ToString() : Guid.NewGuid().ToString("N")),
                targetId = request.targetId ?? "",
                status = "created",
                createdAtUtc = DateTime.UtcNow.ToString("o")
            };

            onCompleted?.Invoke(ApiResult<CreateContentResponseDto>.Ok(response, "create content ok", (int)uwr.responseCode));
            handle.MarkDone();
        }
    }
}
