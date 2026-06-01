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
    /// <summary>Optional real-world placement reference photo URL (from POST /api/targets/{id}/reference).</summary>
    public string targetReferenceImageUrl;
    /// <summary>Current contract field. Possible values: workspace key such as "default".</summary>
    public string workspaceId = "default";
    /// <summary>Optional human-readable name; backend upserts workspaces when missing.</summary>
    public string workspaceName = "";
    /// <summary>Current contract field. Possible values: positive meter value such as 0.2, 0.5, 1.0.</summary>
    public float physicalWidthM = 1.0f;
    /// <summary>Legacy field kept for backward compatibility with older clients/contracts.</summary>
    public float physicalWidth = 0.2f;
    /// <summary>Possible values: backend-normalized Vuforia cloud target name.</summary>
    public string vuforiaTargetName;

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
    public string targetReferenceImageUrl;
    /// <summary>Current contract field. Possible values: workspace key such as "default".</summary>
    public string workspaceId;
    /// <summary>Current contract field. Possible values: persisted physical width in meters.</summary>
    public float physicalWidthM;
    /// <summary>Legacy field kept for backward compatibility with older clients/contracts.</summary>
    public float physicalWidth;
    /// <summary>Possible values: cloud target name accepted by Vuforia conventions.</summary>
    public string vuforiaTargetName;
    /// <summary>Current cloud-create response field: Vuforia cloud target id.</summary>
    public string vuforiaTargetId;
    /// <summary>Current cloud-create response field: Vuforia result status, e.g. "TargetCreated".</summary>
    public string vuforiaStatus;
    /// <summary>Possible values: "created" | "accepted" | "failed".</summary>
    public string status;
    /// <summary>Possible values: ISO-8601 UTC timestamp.</summary>
    public string createdAtUtc;
}

[Serializable]
public class CreateCloudTargetRequestDto
{
    public string targetId;
    public string targetName;
    public string displayLabel;
    public string workspaceId = "default";
    public string workspaceName = "";
    public float width = 1.0f;
    public ApiVector3Dto localPosition = new ApiVector3Dto(0f, 0f, 0f);
    public ApiVector3Dto localEuler = new ApiVector3Dto(0f, 0f, 0f);
    public ApiVector3Dto localScale = new ApiVector3Dto(1f, 1f, 1f);
    public ApiSyncMetaDto meta = new ApiSyncMetaDto();
    public string fileName;
    public byte[] fileBytes;
}

[Serializable]
public class UploadTargetReferenceResponseDto
{
    public string targetId;
    public string targetReferenceImageUrl;
    public string status;
}
