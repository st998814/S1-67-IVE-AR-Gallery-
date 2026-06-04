# Target API contract (v1)

Base path prefix: `/api`. See `common.md` for success/error conventions and shared types.

This file documents the **currently implemented** target endpoints in `backend/app.py`.

---

## `POST /api/targets`

Creates or updates a target record directly from JSON.

### Request

**Content-Type:** `application/json`

| Field | Type | Required | Description |
|--------|------|----------|-------------|
| `targetId` | string | yes | Canonical id (e.g. `poster-a`, `target-001`). |
| `targetName` | string | yes | Internal/technical name. |
| `displayLabel` | string | no | User-facing label; falls back to `targetId` when empty. |
| `targetImageUrl` | string | no | Previously uploaded target image URL. |
| `targetReferenceImageUrl` | string | no | Optional placement reference URL (see `POST …/reference`). |
| `workspaceId` | string | no | Workspace id. Defaults to `default`. Must exist in `workspaces`. |
| `physicalWidthM` | number | no | Physical width in meters. Defaults to `1.0`. |
| `localPosition` | object | yes | `ApiVector3Dto` — local position. |
| `localEuler` | object | yes | `ApiVector3Dto` — local Euler angles in degrees. |
| `localScale` | object | yes | `ApiVector3Dto` — local scale. Defaults to `{1,1,1}` when omitted. |
| `meta` | object | no | `ApiSyncMetaDto` (see `common.md`). Typically tracing fields only; content-specific keys such as `title` or `localContentId` may appear if a client reuses the same DTO — backends should tolerate and ignore unknown keys. |

### Response `200` / `201`

**Content-Type:** `application/json`

| Field | Type | Description |
|--------|------|-------------|
| `targetId` | string | Canonical target id. |
| `targetName` | string | Stored target name. |
| `displayLabel` | string | Stored display label. |
| `targetImageUrl` | string | Stored image URL. |
| `status` | string | `created` or `accepted`. |
| `createdAtUtc` | string | ISO-8601 UTC timestamp. |

### Example — request

```json
{
  "targetId": "poster-a",
  "targetName": "poster_main",
  "displayLabel": "Main wall poster",
  "targetImageUrl": "http://127.0.0.1:5050/uploads/poster_a.jpg",
  "workspaceId": "default",
  "physicalWidthM": 0.4,
  "localPosition": { "x": 0, "y": 0, "z": 0 },
  "localEuler": { "x": 0, "y": 90, "z": 0 },
  "localScale": { "x": 1, "y": 1, "z": 1 },
  "meta": { "schemaVersion": "v1" }
}
```

### Example — success response

```json
{
  "targetId": "poster-a",
  "targetName": "poster_main",
  "displayLabel": "Main wall poster",
  "targetImageUrl": "http://127.0.0.1:5050/uploads/poster_a.jpg",
  "status": "created",
  "createdAtUtc": "2026-05-06T10:26:13.296729Z"
}
```

---

## `POST /api/targets/cloud`

Uploads a target image, registers it in Vuforia Cloud Targets, and persists the target row.

### Request

**Content-Type:** `multipart/form-data`

| Part / Field | Type | Required | Description |
|--------------|------|----------|-------------|
| `file` | file | yes | Target image (`png`, `jpg`, `jpeg`). |
| `targetId` | string | yes | Canonical target id. |
| `targetName` | string | no | Defaults to `targetId` if omitted. |
| `displayLabel` | string | no | Defaults to `targetId` if omitted. |
| `workspaceId` | string | no | Defaults to `default`. Must exist in `workspaces`. |
| `width` | number | no | Physical width in meters for Vuforia and DB `physical_width_m`. Defaults to `VUFORIA_TARGET_WIDTH` (`1.0` fallback). |
| `localPosition` | string JSON | no | Stringified `ApiVector3Dto`. Defaults to zero vector. |
| `localEuler` | string JSON | no | Stringified `ApiVector3Dto`. Defaults to zero vector. |
| `localScale` | string JSON | no | Stringified `ApiVector3Dto`. Defaults to one vector. |
| `meta` | string JSON | no | Stringified object; defaults to `{ "schemaVersion": "v1" }`. |

### Response `200` / `201`

**Content-Type:** `application/json`

| Field | Type | Description |
|--------|------|-------------|
| `targetId` | string | Canonical target id. |
| `targetName` | string | Stored target name. |
| `displayLabel` | string | Stored display label. |
| `targetImageUrl` | string | Backend URL of uploaded image. |
| `status` | string | `created` or `accepted`. |
| `createdAtUtc` | string | ISO-8601 UTC timestamp. |
| `vuforiaTargetId` | string | Vuforia Cloud target id. |
| `vuforiaStatus` | string | Vuforia result code (e.g. `TargetCreated`). |

### Example — success response

```json
{
  "targetId": "wire-verify-1778063171",
  "targetName": "wire-verify-1778063171",
  "displayLabel": "Wire Verify",
  "targetImageUrl": "http://127.0.0.1:5050/uploads/verify-7de1d972.png",
  "status": "created",
  "createdAtUtc": "2026-05-06T10:26:13.296729Z",
  "vuforiaTargetId": "ac359e111d82418785dbf0f6a1312564",
  "vuforiaStatus": "TargetCreated"
}
```

---

## `POST /api/targets/{targetId}/reference`

Uploads an optional **real-world placement reference** photo (how the printed target sits in the environment). This is separate from the Vuforia trackable image.

### Request

**Content-Type:** `multipart/form-data`

| Part / Field | Type | Required | Description |
|--------------|------|----------|-------------|
| `file` | file | yes | Reference photo (`png`, `jpg`, `jpeg`, `gif`, `webp`, …). |
| `targetId` | string | yes | Path segment — canonical target id (must already exist in `targets`). |

File is stored at **`uploads/target_ref/{targetId}.{ext}`** (overwrites on re-upload). The row field **`target_reference_image_url`** is updated.

Alternative: `POST /api/upload` with `category=target_ref` and multipart `targetId`, then `POST /api/targets` with `targetReferenceImageUrl` — the dedicated route above performs both steps.

### Response `200`

| Field | Type | Description |
|--------|------|-------------|
| `targetId` | string | Canonical target id. |
| `targetReferenceImageUrl` | string | Public URL under `/uploads/target_ref/`. |
| `status` | string | `accepted`. |

### Errors

| HTTP | When |
|------|------|
| `404` | Target id not found. |
| `415` | Disallowed file extension. |

---

### Backend configuration

The backend accepts either variable set below for Vuforia credentials:

| Variable | Description |
|----------|-------------|
| `VUFORIA_ACCESS_KEY` or `VUFORIA_SERVER_ACCESS_KEY` | Vuforia database access key. |
| `VUFORIA_SECRET_KEY` or `VUFORIA_SERVER_SECRET_KEY` | Vuforia database secret key. |
| `VUFORIA_HOST` or `VUFORIA_BASE_URL` | Vuforia base URL. Defaults to `https://vws.vuforia.com`. |
| `VUFORIA_TARGET_WIDTH` | Default width when `width` is omitted. |

---

## `GET /api/targets`

Returns all persisted targets as a raw JSON array.

### Response `200`

Each item currently includes:

| Field | Type | Description |
|--------|------|-------------|
| `targetId` | string | Canonical id. |
| `targetName` | string | Name. |
| `displayLabel` | string | Label. |
| `targetImageUrl` | string | Stored image URL. |
| `status` | string | Lifecycle status. |
| `createdAtUtc` | string | ISO-8601 UTC timestamp. |
| `vuforiaTargetId` | string | Vuforia Cloud target id, empty for non-cloud/manual targets. |
| `vuforiaStatus` | string | Last Vuforia result/status, empty for non-cloud/manual targets. |

### Example — success response

```json
[
  {
    "targetId": "poster-a",
    "targetName": "Poster A",
    "displayLabel": "Main wall poster",
    "targetImageUrl": "http://127.0.0.1:5050/uploads/poster_a.jpg",
    "status": "accepted",
    "createdAtUtc": "2026-05-06T10:26:13.296729Z",
    "vuforiaTargetId": "ac359e111d82418785dbf0f6a1312564",
    "vuforiaStatus": "TargetCreated"
  }
]
```

---

## `GET /api/targets/resolve`

Resolves a Vuforia Cloud target id into the backend's canonical target record. This is useful after MobileViewer receives a Vuforia recognition id and needs to call `GET /api/content?targetId=...`.

### Request

| Query parameter | Type | Required | Description |
|-----------------|------|----------|-------------|
| `vuforiaTargetId` | string | yes | Exact Vuforia Cloud target id stored on the target row. |

### Response `200`

Returns the same target summary shape as `GET /api/targets` items.

### Example

```http
GET /api/targets/resolve?vuforiaTargetId=ac359e111d82418785dbf0f6a1312564
```

```json
{
  "targetId": "poster-a",
  "targetName": "Poster A",
  "displayLabel": "Main wall poster",
  "targetImageUrl": "http://127.0.0.1:5050/uploads/poster_a.jpg",
  "status": "accepted",
  "createdAtUtc": "2026-05-06T10:26:13.296729Z",
  "vuforiaTargetId": "ac359e111d82418785dbf0f6a1312564",
  "vuforiaStatus": "TargetCreated"
}
```

### Response `400` / `404`

Standard error object from `common.md` with `VALIDATION_ERROR` for missing `vuforiaTargetId`, or `NOT_FOUND` when no target stores that Vuforia id.

---

## `DELETE /api/targets/{targetId}`

Deletes one target by id.

### Response `200`

```json
{
  "targetId": "poster-a",
  "status": "deleted"
}
```

### Response `404`

Standard error object from `common.md` with `NOT_FOUND`.

---

## Not in current implementation

These routes are **not** implemented in the current backend and should not be called:

- `GET /api/targets/{targetId}`
- `POST /api/targets/{targetId}/publish`
- `POST /api/targets/{targetId}/retry-publish`

---

## Errors

Failures use the standard error object from `common.md`.

Common target-flow cases:

- `400 VALIDATION_ERROR` (missing/invalid fields)
- `404 NOT_FOUND` (unknown resource)
- `415 VALIDATION_ERROR` (unsupported cloud target image type)
- `500 SERVER_ERROR` (database failure)
- `502 VUFORIA_ERROR` (upstream Vuforia failure)
