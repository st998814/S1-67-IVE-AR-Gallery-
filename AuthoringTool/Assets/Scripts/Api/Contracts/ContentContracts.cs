using System;

/// <summary>
/// Temporary contract assumptions (DEV-94 / Sub-task 1):
/// - Content creation will be POST-driven (`/api/content` already exists).
/// - `targetId` is required to keep parent-child binding.
/// - `mediaUrl` can be either upload URL or external URL (e.g., YouTube).
/// </summary>
[Serializable]
public class CreateContentRequestDto
{
    /// <summary>Possible values: "content-001", GUID string, runtime generated id.</summary>
    public string contentId;
    /// <summary>Possible values: existing canonical target id, e.g. "poster-a".</summary>
    public string targetId;
    /// <summary>Possible values: "image" | "video" | "text" | "empty" |"model(3D)".</summary>
    public string contentType;
    /// <summary>Possible values: upload URL, YouTube URL, text payload marker, or empty string.</summary>
    public string mediaUrl;  // mediaUrl = UploadFileResponseDto.url

    /// <summary>Possible values: local content position (x/y/z floats).</summary>
    public ApiVector3Dto localPosition;
    /// <summary>Possible values: local euler angles in degrees.</summary>
    public ApiVector3Dto localEuler;
    /// <summary>Possible values: local scale, usually uniform.</summary>
    public ApiVector3Dto localScale;

    /// <summary>Render strategy: "surface" | "volumetric". Optional for backward compatibility.</summary>
    public string renderKind;
    /// <summary>Asset format hint (e.g. "glb"). Optional and used mainly for model content.</summary>
    public string assetFormat;

    /// <summary>Possible values: <see cref="ApiSyncMetaDto"/> (tracing + optional title, description, textBody, localContentId).</summary>
    public ApiSyncMetaDto meta = new ApiSyncMetaDto();
}

[Serializable]
public class CreateContentResponseDto
{
    /// <summary>Possible values: echoed/normalized content id.</summary>
    public string contentId;
    /// <summary>Possible values: associated target id.</summary>
    public string targetId;
    /// <summary>Possible values: "created" | "accepted" | "failed".</summary>
    public string status;
    /// <summary>Possible values: ISO-8601 UTC timestamp.</summary>
    public string createdAtUtc;
}