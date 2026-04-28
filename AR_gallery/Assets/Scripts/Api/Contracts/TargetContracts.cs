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
    /// <summary>Possible values: positive meter value such as 0.2, 0.5, 1.0.</summary>
    public float physicalWidth = 0.2f;
    /// <summary>Possible values: backend-normalized Vuforia cloud target name.</summary>
    public string vuforiaTargetName;
    /// <summary>Possible values: "draft" | "publishing" | "published" | "failed".</summary>
    public string publishStatus;
    /// <summary>Possible values: backend-managed idempotency key for publish operations.</summary>
    public string publishIdempotencyKey;
    /// <summary>Possible values: schema hash/version tag for local-cloud mapping integrity checks.</summary>
    public string mappingChecksum;
    /// <summary>Possible values: mapping version integer encoded as string (e.g. "1").</summary>
    public int mappingVersion = 1;

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
    /// <summary>Possible values: reference image URL echoed from backend record.</summary>
    public string targetImageUrl;
    /// <summary>Possible values: persisted physical width in meters.</summary>
    public float physicalWidth;
    /// <summary>Possible values: cloud target name accepted by Vuforia conventions.</summary>
    public string vuforiaTargetName;
    /// <summary>Possible values: Vuforia cloud target id when published, else empty string.</summary>
    public string cloudTargetId;
    /// <summary>Possible values: "draft" | "publishing" | "published" | "failed".</summary>
    public string publishStatus;
    /// <summary>Possible values: backend idempotency key for publish operations.</summary>
    public string publishIdempotencyKey;
    /// <summary>Possible values: latest successful publish idempotency key.</summary>
    public string lastSuccessfulPublishIdempotencyKey;
    /// <summary>Possible values: latest publish error message.</summary>
    public string lastPublishError;
    /// <summary>Possible values: integrity metadata hash/version for mapping reconciliation.</summary>
    public string mappingChecksum;
    /// <summary>Possible values: integer mapping revision.</summary>
    public int mappingVersion;
    /// <summary>Possible values: "created" | "accepted" | "failed".</summary>
    public string status;
    /// <summary>Possible values: ISO-8601 UTC timestamp.</summary>
    public string createdAtUtc;
}

[Serializable]
public class PublishTargetRequestDto
{
    /// <summary>Optional idempotency key hint for publish call.</summary>
    public string publishIdempotencyKey;
    /// <summary>Possible values: schemaVersion "v1", clientRequestId, createdAtUtc ISO-8601.</summary>
    public ApiSyncMetaDto meta = new ApiSyncMetaDto();
}
