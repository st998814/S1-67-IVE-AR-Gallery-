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
    [SerializeField] private string targetEndpoint = "/api/targets";
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
        var handle = new CoroutineApiRequestHandle(this);
        Coroutine c = StartCoroutine(CreateTargetRoutine(request, onCompleted, timeoutSeconds, handle));
        handle.BindCoroutine(c);
        return handle;
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

    public IApiRequestHandle PublishTarget(
        string targetId,
        PublishTargetRequestDto request,
        Action<ApiResult<CreateTargetResponseDto>> onCompleted,
        float timeoutSeconds = 20f)
    {
        var handle = new CoroutineApiRequestHandle(this);
        Coroutine c = StartCoroutine(PublishTargetRoutine(targetId, request, isRetry: false, onCompleted, timeoutSeconds, handle));
        handle.BindCoroutine(c);
        return handle;
    }

    public IApiRequestHandle RetryPublishTarget(
        string targetId,
        PublishTargetRequestDto request,
        Action<ApiResult<CreateTargetResponseDto>> onCompleted,
        float timeoutSeconds = 20f)
    {
        var handle = new CoroutineApiRequestHandle(this);
        Coroutine c = StartCoroutine(PublishTargetRoutine(targetId, request, isRetry: true, onCompleted, timeoutSeconds, handle));
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

    private IEnumerator CreateTargetRoutine(
        CreateTargetRequestDto request,
        Action<ApiResult<CreateTargetResponseDto>> onCompleted,
        float timeoutSeconds,
        CoroutineApiRequestHandle handle)
    {
        if (request == null)
        {
            onCompleted?.Invoke(ApiResult<CreateTargetResponseDto>.Fail(ApiErrorCodes.ValidationError, "CreateTarget request is null"));
            handle.MarkDone();
            yield break;
        }

        string url = BuildUrl(targetEndpoint);
        string json = JsonUtility.ToJson(request);
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
                onCompleted?.Invoke(ApiResult<CreateTargetResponseDto>.Fail(ApiErrorCodes.Cancelled, "Request cancelled"));
                handle.MarkDone();
                yield break;
            }

            if (uwr.result != UnityWebRequest.Result.Success)
            {
                string err = $"CreateTarget failed: {uwr.error} HTTP {(long)uwr.responseCode}";
                onCompleted?.Invoke(ApiResult<CreateTargetResponseDto>.Fail(ApiErrorCodes.NetworkError, err, (int)uwr.responseCode));
                handle.MarkDone();
                yield break;
            }

            string body = uwr.downloadHandler != null ? uwr.downloadHandler.text : "";
            CreateTargetResponseDto parsed = JsonUtility.FromJson<CreateTargetResponseDto>(body);
            if (parsed == null || string.IsNullOrWhiteSpace(parsed.targetId))
            {
                onCompleted?.Invoke(ApiResult<CreateTargetResponseDto>.Fail(
                    ApiErrorCodes.ServerError,
                    "CreateTarget succeeded but response has no targetId.",
                    (int)uwr.responseCode));
                handle.MarkDone();
                yield break;
            }

            onCompleted?.Invoke(ApiResult<CreateTargetResponseDto>.Ok(parsed, "create target ok", (int)uwr.responseCode));
            handle.MarkDone();
        }
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

        if (string.IsNullOrWhiteSpace(request.contentId))
            request.contentId = Guid.NewGuid().ToString("N");
        if (string.IsNullOrWhiteSpace(request.contentType))
            request.contentType = "empty";
        if (request.mediaUrl == null)
            request.mediaUrl = "";

        string url = BuildUrl(contentEndpoint);
        string json = JsonUtility.ToJson(request);
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
            CreateContentResponseDto parsed = JsonUtility.FromJson<CreateContentResponseDto>(body);
            if (parsed == null || string.IsNullOrWhiteSpace(parsed.contentId))
            {
                onCompleted?.Invoke(ApiResult<CreateContentResponseDto>.Fail(
                    ApiErrorCodes.ServerError,
                    "CreateContent succeeded but response has no contentId.",
                    (int)uwr.responseCode));
                handle.MarkDone();
                yield break;
            }

            onCompleted?.Invoke(ApiResult<CreateContentResponseDto>.Ok(parsed, "create content ok", (int)uwr.responseCode));
            handle.MarkDone();
        }
    }

    [Serializable]
    private class ApiErrorBody
    {
        public string message;
        public string errorCode;
        public string details;
    }

    private static string ExtractServerErrorMessage(string body, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(body))
        {
            ApiErrorBody parsed = JsonUtility.FromJson<ApiErrorBody>(body);
            if (parsed != null && !string.IsNullOrWhiteSpace(parsed.message))
                return parsed.message;
        }
        return fallback;
    }

    private IEnumerator PublishTargetRoutine(
        string targetId,
        PublishTargetRequestDto request,
        bool isRetry,
        Action<ApiResult<CreateTargetResponseDto>> onCompleted,
        float timeoutSeconds,
        CoroutineApiRequestHandle handle)
    {
        if (string.IsNullOrWhiteSpace(targetId))
        {
            onCompleted?.Invoke(ApiResult<CreateTargetResponseDto>.Fail(ApiErrorCodes.ValidationError, "targetId is required"));
            handle.MarkDone();
            yield break;
        }

        string path = isRetry ? $"{targetEndpoint}/{targetId.Trim()}/retry-publish" : $"{targetEndpoint}/{targetId.Trim()}/publish";
        string url = BuildUrl(path);
        PublishTargetRequestDto payload = request ?? new PublishTargetRequestDto();
        string json = JsonUtility.ToJson(payload);
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
                onCompleted?.Invoke(ApiResult<CreateTargetResponseDto>.Fail(ApiErrorCodes.Cancelled, "Request cancelled"));
                handle.MarkDone();
                yield break;
            }

            string body = uwr.downloadHandler != null ? uwr.downloadHandler.text : "";
            if (uwr.result != UnityWebRequest.Result.Success)
            {
                string fallback = $"{(isRetry ? "RetryPublish" : "Publish")} failed: {uwr.error} HTTP {(long)uwr.responseCode}";
                string err = ExtractServerErrorMessage(body, fallback);
                onCompleted?.Invoke(ApiResult<CreateTargetResponseDto>.Fail(ApiErrorCodes.NetworkError, err, (int)uwr.responseCode));
                handle.MarkDone();
                yield break;
            }

            CreateTargetResponseDto parsed = JsonUtility.FromJson<CreateTargetResponseDto>(body);
            if (parsed == null || string.IsNullOrWhiteSpace(parsed.targetId))
            {
                onCompleted?.Invoke(ApiResult<CreateTargetResponseDto>.Fail(
                    ApiErrorCodes.ServerError,
                    "Publish succeeded but response has no targetId.",
                    (int)uwr.responseCode));
                handle.MarkDone();
                yield break;
            }

            onCompleted?.Invoke(ApiResult<CreateTargetResponseDto>.Ok(parsed, isRetry ? "retry publish ok" : "publish ok", (int)uwr.responseCode));
            handle.MarkDone();
        }
    }
}
