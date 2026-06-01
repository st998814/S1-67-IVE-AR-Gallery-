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
    [SerializeField] private string cloudTargetEndpoint = "/api/targets/cloud";
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

    public IApiRequestHandle CreateCloudTarget(
        CreateCloudTargetRequestDto request,
        Action<ApiResult<CreateTargetResponseDto>> onCompleted,
        float timeoutSeconds = 25f)
    {
        var handle = new CoroutineApiRequestHandle(this);
        Coroutine c = StartCoroutine(CreateCloudTargetRoutine(request, onCompleted, timeoutSeconds, handle));
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

    public IApiRequestHandle UploadTargetReference(
        string targetId,
        UploadFileRequestDto request,
        Action<ApiResult<UploadTargetReferenceResponseDto>> onCompleted,
        float timeoutSeconds = 20f)
    {
        var handle = new CoroutineApiRequestHandle(this);
        Coroutine c = StartCoroutine(UploadTargetReferenceRoutine(targetId, request, onCompleted, timeoutSeconds, handle));
        handle.BindCoroutine(c);
        return handle;
    }

    private IEnumerator UploadTargetReferenceRoutine(
        string targetId,
        UploadFileRequestDto request,
        Action<ApiResult<UploadTargetReferenceResponseDto>> onCompleted,
        float timeoutSeconds,
        CoroutineApiRequestHandle handle)
    {
        if (string.IsNullOrWhiteSpace(targetId))
        {
            onCompleted?.Invoke(ApiResult<UploadTargetReferenceResponseDto>.Fail(ApiErrorCodes.ValidationError, "targetId is empty"));
            handle.MarkDone();
            yield break;
        }

        if (request == null || request.fileBytes == null || request.fileBytes.Length == 0)
        {
            onCompleted?.Invoke(ApiResult<UploadTargetReferenceResponseDto>.Fail(ApiErrorCodes.ValidationError, "fileBytes is empty"));
            handle.MarkDone();
            yield break;
        }

        string tid = targetId.Trim();
        string url = BuildUrl($"{targetEndpoint}/{Uri.EscapeDataString(tid)}/reference");
        var form = new WWWForm();
        string fileName = string.IsNullOrWhiteSpace(request.fileName) ? "reference.jpg" : request.fileName.Trim();
        if (string.IsNullOrEmpty(System.IO.Path.GetExtension(fileName)))
            fileName += ".jpg";
        string mimeType = string.IsNullOrWhiteSpace(request.mimeType) || request.mimeType == "application/octet-stream"
            ? GuessImageMimeType(fileName)
            : request.mimeType;
        form.AddBinaryData("file", request.fileBytes, fileName, mimeType);

        using (UnityWebRequest uwr = UnityWebRequest.Post(url, form))
        {
            uwr.timeout = Mathf.Max(1, Mathf.RoundToInt(timeoutSeconds <= 0f ? 20f : timeoutSeconds));
            yield return uwr.SendWebRequest();

            if (handle.IsCancelled)
            {
                onCompleted?.Invoke(ApiResult<UploadTargetReferenceResponseDto>.Fail(ApiErrorCodes.Cancelled, "Request cancelled"));
                handle.MarkDone();
                yield break;
            }

            if (uwr.result != UnityWebRequest.Result.Success)
            {
                string err = $"Target reference upload failed: {uwr.error} HTTP {(long)uwr.responseCode}";
                onCompleted?.Invoke(ApiResult<UploadTargetReferenceResponseDto>.Fail(ApiErrorCodes.NetworkError, err, (int)uwr.responseCode));
                handle.MarkDone();
                yield break;
            }

            if (IsNonSuccessHttpStatus(uwr, out int httpStatus))
            {
                string errorBody = uwr.downloadHandler != null ? uwr.downloadHandler.text : "";
                string fallback = $"Target reference upload failed: HTTP {httpStatus}";
                string errMsg = ExtractServerErrorMessage(errorBody, fallback);
                string errCode = ExtractServerErrorCode(errorBody, ApiErrorCodes.ServerError);
                onCompleted?.Invoke(ApiResult<UploadTargetReferenceResponseDto>.Fail(errCode, errMsg, httpStatus));
                handle.MarkDone();
                yield break;
            }

            string body = uwr.downloadHandler != null ? uwr.downloadHandler.text : "";
            UploadTargetReferenceResponseDto parsed = JsonUtility.FromJson<UploadTargetReferenceResponseDto>(body);
            if (parsed == null)
                parsed = new UploadTargetReferenceResponseDto();

            if (string.IsNullOrWhiteSpace(parsed.targetReferenceImageUrl))
            {
                var generic = JsonUtility.FromJson<CreateTargetResponseDto>(body);
                if (generic != null && !string.IsNullOrWhiteSpace(generic.targetReferenceImageUrl))
                {
                    parsed.targetId = generic.targetId;
                    parsed.targetReferenceImageUrl = generic.targetReferenceImageUrl;
                    parsed.status = generic.status;
                }
            }

            if (string.IsNullOrWhiteSpace(parsed.targetReferenceImageUrl))
            {
                onCompleted?.Invoke(ApiResult<UploadTargetReferenceResponseDto>.Fail(
                    ApiErrorCodes.ServerError,
                    "Target reference upload succeeded but response has no targetReferenceImageUrl.",
                    (int)uwr.responseCode));
                handle.MarkDone();
                yield break;
            }

            if (string.IsNullOrWhiteSpace(parsed.targetId))
                parsed.targetId = tid;

            onCompleted?.Invoke(ApiResult<UploadTargetReferenceResponseDto>.Ok(parsed, "target reference ok", (int)uwr.responseCode));
            handle.MarkDone();
        }
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
        string cat = string.IsNullOrWhiteSpace(request.uploadCategory) ? "content" : request.uploadCategory.Trim().ToLowerInvariant();
        if (cat != "content" && cat != "target" && cat != "target_ref")
            cat = "content";
        form.AddField("category", cat);
        if ((cat == "target" || cat == "target_ref") && !string.IsNullOrWhiteSpace(request.targetId))
            form.AddField("targetId", request.targetId.Trim());
        if (cat == "content" && !string.IsNullOrWhiteSpace(request.contentId))
            form.AddField("contentId", request.contentId.Trim());
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

            if (IsNonSuccessHttpStatus(uwr, out int httpStatus))
            {
                string errorBody = uwr.downloadHandler != null ? uwr.downloadHandler.text : "";
                string fallback = $"Upload failed: HTTP {httpStatus}";
                string errMsg = ExtractServerErrorMessage(errorBody, fallback);
                string errCode = ExtractServerErrorCode(errorBody, ApiErrorCodes.ServerError);
                onCompleted?.Invoke(ApiResult<UploadFileResponseDto>.Fail(errCode, errMsg, httpStatus));
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
                string errorBody = uwr.downloadHandler != null ? uwr.downloadHandler.text : "";
                string fallback = $"CreateTarget failed: {uwr.error} HTTP {(long)uwr.responseCode}";
                string err = ExtractServerErrorMessage(errorBody, fallback);
                string code = ExtractServerErrorCode(errorBody, ApiErrorCodes.NetworkError);
                onCompleted?.Invoke(ApiResult<CreateTargetResponseDto>.Fail(code, err, (int)uwr.responseCode));
                handle.MarkDone();
                yield break;
            }

            if (IsNonSuccessHttpStatus(uwr, out int httpStatus))
            {
                string errorBody = uwr.downloadHandler != null ? uwr.downloadHandler.text : "";
                string fallback = $"CreateTarget failed: HTTP {httpStatus}";
                string err = ExtractServerErrorMessage(errorBody, fallback);
                string code = ExtractServerErrorCode(errorBody, ApiErrorCodes.ServerError);
                onCompleted?.Invoke(ApiResult<CreateTargetResponseDto>.Fail(code, err, httpStatus));
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
                string errorBody = uwr.downloadHandler != null ? uwr.downloadHandler.text : "";
                string fallback = $"CreateContent failed: {uwr.error} HTTP {(long)uwr.responseCode}";
                string err = ExtractServerErrorMessage(errorBody, fallback);
                string code = ExtractServerErrorCode(errorBody, ApiErrorCodes.NetworkError);
                onCompleted?.Invoke(ApiResult<CreateContentResponseDto>.Fail(code, err, (int)uwr.responseCode));
                handle.MarkDone();
                yield break;
            }

            if (IsNonSuccessHttpStatus(uwr, out int httpStatus))
            {
                string errorBody = uwr.downloadHandler != null ? uwr.downloadHandler.text : "";
                string fallback = $"CreateContent failed: HTTP {httpStatus}";
                string err = ExtractServerErrorMessage(errorBody, fallback);
                string code = ExtractServerErrorCode(errorBody, ApiErrorCodes.ServerError);
                onCompleted?.Invoke(ApiResult<CreateContentResponseDto>.Fail(code, err, httpStatus));
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

    private IEnumerator CreateCloudTargetRoutine(
        CreateCloudTargetRequestDto request,
        Action<ApiResult<CreateTargetResponseDto>> onCompleted,
        float timeoutSeconds,
        CoroutineApiRequestHandle handle)
    {
        if (request == null || request.fileBytes == null || request.fileBytes.Length == 0)
        {
            onCompleted?.Invoke(ApiResult<CreateTargetResponseDto>.Fail(ApiErrorCodes.ValidationError, "Cloud target image file is required."));
            handle.MarkDone();
            yield break;
        }

        string url = BuildUrl(cloudTargetEndpoint);
        var form = new WWWForm();
        string targetId = string.IsNullOrWhiteSpace(request.targetId) ? Guid.NewGuid().ToString("N") : request.targetId.Trim();
        string targetName = string.IsNullOrWhiteSpace(request.targetName) ? targetId : request.targetName.Trim();
        string fileName = string.IsNullOrWhiteSpace(request.fileName) ? "target.jpg" : request.fileName.Trim();
        string mimeType = GuessImageMimeType(fileName);

        form.AddField("targetId", targetId);
        form.AddField("targetName", targetName);
        form.AddField("displayLabel", string.IsNullOrWhiteSpace(request.displayLabel) ? targetName : request.displayLabel.Trim());
        form.AddField("workspaceId", string.IsNullOrWhiteSpace(request.workspaceId) ? "default" : request.workspaceId.Trim());
        form.AddField("workspaceName", request.workspaceName != null ? request.workspaceName.Trim() : "");
        form.AddField("width", Mathf.Max(0.01f, request.width).ToString(System.Globalization.CultureInfo.InvariantCulture));
        form.AddField("localPosition", JsonUtility.ToJson(request.localPosition ?? new ApiVector3Dto(0f, 0f, 0f)));
        form.AddField("localEuler", JsonUtility.ToJson(request.localEuler ?? new ApiVector3Dto(0f, 0f, 0f)));
        form.AddField("localScale", JsonUtility.ToJson(request.localScale ?? new ApiVector3Dto(1f, 1f, 1f)));
        form.AddField("meta", JsonUtility.ToJson(request.meta ?? new ApiSyncMetaDto()));
        form.AddBinaryData("file", request.fileBytes, fileName, mimeType);

        using (UnityWebRequest uwr = UnityWebRequest.Post(url, form))
        {
            uwr.timeout = Mathf.Max(1, Mathf.RoundToInt(timeoutSeconds <= 0f ? 25f : timeoutSeconds));
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
                string fallback = $"CreateCloudTarget failed: {uwr.error} HTTP {(long)uwr.responseCode}";
                string err = ExtractServerErrorMessage(body, fallback);
                string code = ExtractServerErrorCode(body, ApiErrorCodes.NetworkError);
                onCompleted?.Invoke(ApiResult<CreateTargetResponseDto>.Fail(code, err, (int)uwr.responseCode));
                handle.MarkDone();
                yield break;
            }

            if (IsNonSuccessHttpStatus(uwr, out int httpStatus))
            {
                string fallback = $"CreateCloudTarget failed: HTTP {httpStatus}";
                string err = ExtractServerErrorMessage(body, fallback);
                string code = ExtractServerErrorCode(body, ApiErrorCodes.ServerError);
                onCompleted?.Invoke(ApiResult<CreateTargetResponseDto>.Fail(code, err, httpStatus));
                handle.MarkDone();
                yield break;
            }

            CreateTargetResponseDto parsed = JsonUtility.FromJson<CreateTargetResponseDto>(body);
            if (parsed == null || string.IsNullOrWhiteSpace(parsed.targetId))
            {
                onCompleted?.Invoke(ApiResult<CreateTargetResponseDto>.Fail(
                    ApiErrorCodes.ServerError,
                    "CreateCloudTarget succeeded but response has no targetId.",
                    (int)uwr.responseCode));
                handle.MarkDone();
                yield break;
            }

            onCompleted?.Invoke(ApiResult<CreateTargetResponseDto>.Ok(parsed, "create cloud target ok", (int)uwr.responseCode));
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

    /// <summary>
    /// <see cref="UnityWebRequest.Result.Success"/> does not always imply HTTP 2xx on every platform;
    /// reject non-2xx so error JSON is not mis-parsed as a success DTO.
    /// </summary>
    private static bool IsNonSuccessHttpStatus(UnityWebRequest uwr, out int statusCode)
    {
        statusCode = uwr != null ? (int)uwr.responseCode : 0;
        long code = statusCode;
        return code < 200 || code > 299;
    }

    private static ApiErrorBody ParseErrorBody(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;
        return JsonUtility.FromJson<ApiErrorBody>(body);
    }

    private static string ExtractServerErrorMessage(string body, string fallback)
    {
        ApiErrorBody parsed = ParseErrorBody(body);
        if (parsed != null && !string.IsNullOrWhiteSpace(parsed.message))
            return parsed.message;
        return fallback;
    }

    private static string ExtractServerErrorCode(string body, string fallback)
    {
        ApiErrorBody parsed = ParseErrorBody(body);
        if (parsed != null && !string.IsNullOrWhiteSpace(parsed.errorCode))
            return parsed.errorCode;
        return fallback;
    }

    private static string GuessImageMimeType(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "image/jpeg";
        string lower = fileName.ToLowerInvariant();
        if (lower.EndsWith(".png"))
            return "image/png";
        if (lower.EndsWith(".jpg") || lower.EndsWith(".jpeg"))
            return "image/jpeg";
        return "application/octet-stream";
    }

}
