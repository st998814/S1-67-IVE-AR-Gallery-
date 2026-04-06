using System;

[Serializable]
public class ApiResponseEnvelope<T>
{
    /// <summary>Possible values: true | false.</summary>
    public bool success;
    /// <summary>Possible values: "ok", "created", validation error message.</summary>
    public string message;
    /// <summary>Possible values: null, "DUPLICATE_ID", "VALIDATION_ERROR".</summary>
    public string errorCode;
    /// <summary>Possible values: request correlation id, e.g. "req-2026-04-06-001".</summary>
    public string requestId;
    /// <summary>Possible values: typed payload object for the operation.</summary>
    public T data;
}

[Serializable]
public class ApiVector3Dto
{
    /// <summary>Possible values: any float; typically authoring-space coordinate.</summary>
    public float x;
    /// <summary>Possible values: any float; typically authoring-space coordinate.</summary>
    public float y;
    /// <summary>Possible values: any float; typically authoring-space coordinate.</summary>
    public float z;

    public ApiVector3Dto()
    {
    }

    public ApiVector3Dto(float x, float y, float z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }
}

[Serializable]
public class ApiSyncMetaDto
{
    /// <summary>Possible values: "v1".</summary>
    public string schemaVersion = "v1";
    /// <summary>Possible values: GUID, "req-001", or any client-generated correlation id.</summary>
    public string clientRequestId;
    /// <summary>Possible values: ISO-8601 UTC timestamp, e.g. "2026-04-06T12:00:00Z".</summary>
    public string createdAtUtc;
}