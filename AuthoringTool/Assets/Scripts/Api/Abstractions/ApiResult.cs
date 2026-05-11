using System;

/// <summary>
/// The result of the API request. result = server response (success/fail) + custom message and status code
/// </summary>

[Serializable]
public class ApiResult<TPayload>
{
    public bool success;
    public string errorCode;
    public string message;
    public TPayload payload;
    public int statusCode;
    // if success
    public static ApiResult<TPayload> Ok(TPayload payload, string message = "ok", int statusCode = 200)
    {
        return new ApiResult<TPayload>
        {
            success = true,
            payload = payload,
            message = message,
            statusCode = statusCode
        };
    }
    // if fail
    public static ApiResult<TPayload> Fail(string errorCode, string message, int statusCode = 0)
    {
        return new ApiResult<TPayload>
        {
            success = false,
            errorCode = errorCode,
            message = message,
            statusCode = statusCode
        };
    }
}

public static class ApiErrorCodes
{
    public const string Cancelled = "CANCELLED";
    public const string Timeout = "TIMEOUT";
    public const string NetworkError = "NETWORK_ERROR";
    public const string ServerError = "SERVER_ERROR";
    public const string ValidationError = "VALIDATION_ERROR";
    public const string Unknown = "UNKNOWN";
}
