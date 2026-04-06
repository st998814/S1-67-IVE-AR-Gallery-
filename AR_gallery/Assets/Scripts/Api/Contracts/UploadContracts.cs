using System;

/// <summary>
/// Temporary contract assumptions (DEV-94 / Sub-task 1):
/// - Upload endpoint returns a stable public URL in `url`.
/// - If backend later wraps payload in an envelope, `UploadFileResponseDto` remains reusable as `data`.
/// </summary>
[Serializable]
public class UploadFileRequestDto
{
    /// <summary>Possible values: "poster_a.jpg", "clip.mp4", "note.txt".</summary>
    public string fileName;
    /// <summary>Possible values: "image/jpeg", "image/png", "video/mp4", "text/plain".</summary>
    public string mimeType;
    /// <summary>Possible values: non-empty byte array from selected local file.</summary>
    public byte[] fileBytes;
    /// <summary>Possible values: schemaVersion "v1", clientRequestId "req-123", createdAtUtc ISO-8601.</summary>
    public ApiSyncMetaDto meta = new ApiSyncMetaDto();
}

[Serializable]
public class UploadFileResponseDto
{
    /// <summary>Possible values: "https://cdn.example.com/uploads/poster_a.jpg".</summary>
    public string url;
    /// <summary>Possible values: same or normalized from request fileName.</summary>
    public string fileName;
    /// <summary>Possible values: "image/jpeg", "image/png", "video/mp4".</summary>
    public string mimeType;
    /// <summary>Possible values: > 0.</summary>
    public long sizeBytes;
    /// <summary>Possible values: ISO-8601 UTC timestamp, e.g. "2026-04-06T12:00:00Z".</summary>
    public string uploadedAtUtc;
}
