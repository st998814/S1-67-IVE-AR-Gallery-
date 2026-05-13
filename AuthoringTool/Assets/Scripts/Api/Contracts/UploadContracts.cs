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
    public string mimeType; // mime = Multipurpose Internet Mail Extensions 
    /// <summary>Possible values: non-empty byte array from selected local file.</summary>
    public byte[] fileBytes;
    /// <summary>
    /// Optional multipart field for <c>POST /api/upload</c>: <c>content</c> (default), <c>target</c>, or <c>target_ref</c> — selects server folder under <c>uploads/</c>.
    /// </summary>
    public string uploadCategory = "";
    /// <summary>When <see cref="uploadCategory"/> is <c>target</c>, sent as form field <c>targetId</c> so the server uses one file name per target (avoids duplicate UUID suffixes).</summary>
    public string targetId = "";
    /// <summary>When <see cref="uploadCategory"/> is <c>content</c>, sent as <c>contentId</c> so re-sync overwrites the same stored file.</summary>
    public string contentId = "";
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
