# API Contracts (Unity-side)

This folder defines request/response DTOs 

## Goals

- Keep Unity UI/runtime independent from concrete HTTP code.
- Standardize field naming before backend endpoints are finalized.
- Provide a clear handoff contract for backend collaboration.
- Use coroutine-first async pattern for Unity integration.

## Async pattern decision (Unity)

- Pattern: `Coroutine` + callback completion.
- Rationale:
  - better fit with Unity main-thread lifecycle and UI updates
  - direct compatibility with `UnityWebRequest`
  - simpler cancellation through request handles
- Abstractions added in `Scripts/Api/Abstractions`:
  - `IApiClient`
  - `IApiRequestHandle`
  - `ApiResult<TPayload>`
  - `CoroutineApiRequestHandle`

## Result / cancellation conventions

- Result wrapper fields:
  - `success`
  - `errorCode`
  - `message`
  - `payload`
  - `statusCode`
- Standard error codes:
  - `CANCELLED`
  - `TIMEOUT`
  - `NETWORK_ERROR`
  - `SERVER_ERROR`
  - `VALIDATION_ERROR`
  - `UNKNOWN`
- Every API method supports:
  - timeout parameter (`timeoutSeconds`)
  - cancellation via returned `IApiRequestHandle`

## Field naming conventions

- Use `camelCase` JSON-compatible field names.
- Canonical IDs:
  - `targetId`
  - `contentId`
- Media field:
  - `mediaUrl`

## Current DTO groups

- `UploadContracts.cs`
  - `UploadFileRequestDto`
  - `UploadFileResponseDto`
- `TargetContracts.cs`
  - `CreateTargetRequestDto`
  - `CreateTargetResponseDto`
- `ContentContracts.cs`
  - `CreateContentRequestDto`
  - `CreateContentResponseDto`
- `ApiCommonContracts.cs`
  - `ApiResponseEnvelope<T>`
  - `ApiVector3Dto`
  - `ApiSyncMetaDto`

## Field value examples

- `contentType`: `image` | `video` | `text` | `empty`
- `targetId`: `poster-a`, `poster-b`, `target-001`
- `contentId`: `content-001`, `0d7f33c1-8e15-4b1e-9a3e-a6bc...`
- `mediaUrl`:
  - image/video: `https://cdn.example.com/uploads/poster_a.jpg`
  - video external: `https://youtu.be/abcd1234`
  - text: plain text payload marker or empty string
- `status`: `created` | `accepted` | `failed`
- `schemaVersion`: `v1`
- `createdAtUtc` / `uploadedAtUtc`: ISO-8601 UTC (`2026-04-06T12:00:00Z`)

### Upload request/response examples

```json
{
  "fileName": "poster_a.jpg",
  "mimeType": "image/jpeg",
  "fileBytes": "<raw-bytes>",
  "meta": {
    "schemaVersion": "v1",
    "clientRequestId": "req-001",
    "createdAtUtc": "2026-04-06T12:00:00Z"
  }
}
```

```json
{
  "url": "https://cdn.example.com/uploads/poster_a.jpg",
  "fileName": "poster_a.jpg",
  "mimeType": "image/jpeg",
  "sizeBytes": 532112,
  "uploadedAtUtc": "2026-04-06T12:00:03Z"
}
```

## Temporary assumptions

- Target creation endpoint is planned (POST `/api/targets`).
- Content creation uses POST `/api/content`.
- Upload endpoint returns at least `url`.
- Full fetch/rebuild endpoints are out of current scope.
