using System;

/// <summary>
/// Coroutine-first API abstraction for Unity runtime usage.
/// Methods start async work internally and report completion through callbacks.
/// </summary>
public interface IApiClient
{
    IApiRequestHandle UploadFile(
        UploadFileRequestDto request,
        Action<ApiResult<UploadFileResponseDto>> onCompleted, // callback function
        float timeoutSeconds = 20f);

    IApiRequestHandle CreateTarget(
        CreateTargetRequestDto request,
        Action<ApiResult<CreateTargetResponseDto>> onCompleted,
        float timeoutSeconds = 20f);

    IApiRequestHandle CreateContent(
        CreateContentRequestDto request,
        Action<ApiResult<CreateContentResponseDto>> onCompleted,
        float timeoutSeconds = 20f);
}