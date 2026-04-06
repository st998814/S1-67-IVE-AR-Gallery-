using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Temporary local mock implementation for DEV-94 Sub-task 3.
/// Provides predictable coroutine-based responses for upload/create-target/create-content.
/// </summary>
public class MockApiClient : MonoBehaviour, IApiClient
{
    [Header("Mock Timing")]
    [SerializeField] private float fixedLatencySeconds = 0.25f;

    [Header("Mock Upload")]
    [SerializeField] private string mockUploadBaseUrl = "https://mock.local/uploads";

    private readonly HashSet<string> createdTargetIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> createdContentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private int uploadSeq = 1;
    private int contentSeq = 1;
    // implementation of the IApiClient interface
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
    

    private IEnumerator UploadFileRoutine(
        UploadFileRequestDto request,
        Action<ApiResult<UploadFileResponseDto>> onCompleted,
        float timeoutSeconds,
        CoroutineApiRequestHandle handle)
    {
        Debug.Log($"[MockApiClient] UploadFile start file={request?.fileName}");
        yield return SimulateDelayOrTimeout(timeoutSeconds, handle, onCompleted, () =>
            ApiResult<UploadFileResponseDto>.Fail(ApiErrorCodes.Timeout, "Mock upload timed out"));

        if (handle.IsCancelled || handle.IsDone)
            yield break;

        string fileName = string.IsNullOrWhiteSpace(request?.fileName) ? $"mock_{uploadSeq++}.bin" : request.fileName.Trim();
        string baseUrl = string.IsNullOrWhiteSpace(mockUploadBaseUrl) ? "https://mock.local/uploads" : mockUploadBaseUrl.TrimEnd('/');
        var response = new UploadFileResponseDto
        {
            url = $"{baseUrl}/{fileName}",
            fileName = fileName,
            mimeType = string.IsNullOrWhiteSpace(request?.mimeType) ? "application/octet-stream" : request.mimeType,
            sizeBytes = request?.fileBytes != null ? request.fileBytes.LongLength : 0,
            uploadedAtUtc = DateTime.UtcNow.ToString("o")
        };

        Debug.Log($"[MockApiClient] UploadFile success url={response.url}");
        onCompleted?.Invoke(ApiResult<UploadFileResponseDto>.Ok(response, "mock upload ok"));
        handle.MarkDone();
    }

    private IEnumerator CreateTargetRoutine(
        CreateTargetRequestDto request,
        Action<ApiResult<CreateTargetResponseDto>> onCompleted,
        float timeoutSeconds,
        CoroutineApiRequestHandle handle)
    {
        Debug.Log($"[MockApiClient] CreateTarget start targetId={request?.targetId}");
        yield return SimulateDelayOrTimeout(timeoutSeconds, handle, onCompleted, () =>
            ApiResult<CreateTargetResponseDto>.Fail(ApiErrorCodes.Timeout, "Mock create-target timed out"));

        if (handle.IsCancelled || handle.IsDone)
            yield break;

        string targetId = string.IsNullOrWhiteSpace(request?.targetId) ? "new-target" : request.targetId.Trim();
        if (createdTargetIds.Contains(targetId))
        {
            string msg = $"Duplicate targetId in mock store: {targetId}";
            Debug.LogWarning($"[MockApiClient] CreateTarget rejected {msg}");
            onCompleted?.Invoke(ApiResult<CreateTargetResponseDto>.Fail(ApiErrorCodes.ValidationError, msg, 409));
            handle.MarkDone();
            yield break;
        }

        createdTargetIds.Add(targetId);
        var response = new CreateTargetResponseDto
        {
            targetId = targetId,
            targetName = string.IsNullOrWhiteSpace(request?.targetName) ? targetId : request.targetName,
            displayLabel = string.IsNullOrWhiteSpace(request?.displayLabel) ? targetId : request.displayLabel,
            status = "created",
            createdAtUtc = DateTime.UtcNow.ToString("o")
        };

        Debug.Log($"[MockApiClient] CreateTarget success targetId={response.targetId}");
        onCompleted?.Invoke(ApiResult<CreateTargetResponseDto>.Ok(response, "mock create-target ok", 201));
        handle.MarkDone();
    }

    private IEnumerator CreateContentRoutine(
        CreateContentRequestDto request,
        Action<ApiResult<CreateContentResponseDto>> onCompleted,
        float timeoutSeconds,
        CoroutineApiRequestHandle handle)
    {
        Debug.Log($"[MockApiClient] CreateContent start contentId={request?.contentId}, targetId={request?.targetId}");
        yield return SimulateDelayOrTimeout(timeoutSeconds, handle, onCompleted, () =>
            ApiResult<CreateContentResponseDto>.Fail(ApiErrorCodes.Timeout, "Mock create-content timed out"));

        if (handle.IsCancelled || handle.IsDone)
            yield break;

        string targetId = string.IsNullOrWhiteSpace(request?.targetId) ? "" : request.targetId.Trim();
        if (targetId.Length == 0)
        {
            const string msg = "targetId is required";
            Debug.LogWarning($"[MockApiClient] CreateContent rejected {msg}");
            onCompleted?.Invoke(ApiResult<CreateContentResponseDto>.Fail(ApiErrorCodes.ValidationError, msg, 400));
            handle.MarkDone();
            yield break;
        }

        string contentId = string.IsNullOrWhiteSpace(request?.contentId) ? $"content-{contentSeq++}" : request.contentId.Trim();
        if (createdContentIds.Contains(contentId))
        {
            string msg = $"Duplicate contentId in mock store: {contentId}";
            Debug.LogWarning($"[MockApiClient] CreateContent rejected {msg}");
            onCompleted?.Invoke(ApiResult<CreateContentResponseDto>.Fail(ApiErrorCodes.ValidationError, msg, 409));
            handle.MarkDone();
            yield break;
        }

        createdContentIds.Add(contentId);
        var response = new CreateContentResponseDto
        {
            contentId = contentId,
            targetId = targetId,
            status = "created",
            createdAtUtc = DateTime.UtcNow.ToString("o")
        };

        Debug.Log($"[MockApiClient] CreateContent success contentId={response.contentId}, targetId={response.targetId}");
        onCompleted?.Invoke(ApiResult<CreateContentResponseDto>.Ok(response, "mock create-content ok", 201));
        handle.MarkDone();
    }

    private IEnumerator SimulateDelayOrTimeout<TPayload>(
        float timeoutSeconds,
        CoroutineApiRequestHandle handle,
        Action<ApiResult<TPayload>> onCompleted,
        Func<ApiResult<TPayload>> timeoutFactory)
    {
        float elapsed = 0f;
        float timeout = timeoutSeconds <= 0f ? 20f : timeoutSeconds;
        while (elapsed < fixedLatencySeconds)
        {
            if (handle.IsCancelled)
            {
                onCompleted?.Invoke(ApiResult<TPayload>.Fail(ApiErrorCodes.Cancelled, "Request cancelled by caller"));
                handle.MarkDone();
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            if (elapsed > timeout)
            {
                onCompleted?.Invoke(timeoutFactory());
                handle.MarkDone();
                yield break;
            }
            yield return null;
        }
    }
}
