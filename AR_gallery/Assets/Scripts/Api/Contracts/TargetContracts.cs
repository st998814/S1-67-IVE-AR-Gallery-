using System;

/// <summary>
/// Temporary contract assumptions (DEV-94 / Sub-task 1):
/// - Target creation will be POST-driven (`/api/targets` planned).
/// - `targetId` is the canonical key shared by Unity runtime and backend.
/// - Content association should always reference this same `targetId`.
/// </summary>
[Serializable]
public class CreateTargetRequestDto
{
    /// <summary>Possible values: "poster-a", "poster_b", "target-001".</summary>
    public string targetId;
    /// <summary>Possible values: "Poster A", "Brand Wall", "Target 01".</summary>
    public string targetName;
    /// <summary>Possible values: user-facing label; empty/null -> backend may fallback to targetId.</summary>
    public string displayLabel;
    /// <summary>Possible values: upload URL, YouTube URL, text payload marker, or empty string.</summary>
    public string targetImageUrl;

    // TargetVisual transform snapshot for server-side persistence.
    /// <summary>Possible values: local target position (x/y/z floats).</summary>
    public ApiVector3Dto localPosition;
    /// <summary>Possible values: local euler angles in degrees.</summary>
    public ApiVector3Dto localEuler;
    /// <summary>Possible values: local scale, e.g. (1,1,1) or (0.8,0.8,0.8).</summary>
    public ApiVector3Dto localScale;

    /// <summary>Possible values: schemaVersion "v1", clientRequestId, createdAtUtc ISO-8601.</summary>
    public ApiSyncMetaDto meta = new ApiSyncMetaDto();
}

[Serializable]
public class CreateTargetResponseDto
{
    /// <summary>Possible values: echoed canonical target id, e.g. "poster-a".</summary>
    public string targetId;
    /// <summary>Possible values: "Poster A".</summary>
    public string targetName;
    /// <summary>Possible values: display label from server normalization.</summary>
    public string displayLabel;
    /// <summary>Possible values: "created" | "accepted" | "failed".</summary>
    public string status;
    /// <summary>Possible values: ISO-8601 UTC timestamp.</summary>
    public string createdAtUtc;
}
