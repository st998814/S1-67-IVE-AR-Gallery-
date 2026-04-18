# Target API contract (v1)

Base path prefix: `/api`. See `common.md` for success/error conventions and shared types.

---

## `POST /api/targets`

Creates (or registers) a target. Aligns with `CreateTargetRequestDto` / `CreateTargetResponseDto` in Unity.

### Request

**Content-Type:** `application/json`

| Field | Type | Required | Description |
|--------|------|----------|-------------|
| `targetId` | string | yes | Canonical id (e.g. `poster-a`, `target-001`). |
| `targetName` | string | yes | Display name / internal title. |
| `displayLabel` | string | no | User-facing label; empty may fall back to `targetId` server-side. |
| `targetImageUrl` | string | no | Reference image URL, marker, or empty. |
| `localPosition` | object | yes | `ApiVector3Dto` — local position. |
| `localEuler` | object | yes | `ApiVector3Dto` — local Euler angles in **degrees**. |
| `localScale` | object | yes | `ApiVector3Dto` — local scale. |
| `meta` | object | no | `ApiSyncMetaDto`. |

### Response `200` / `201`

**Content-Type:** `application/json`

Body matches `CreateTargetResponseDto`:

| Field | Type | Description |
|--------|------|-------------|
| `targetId` | string | Canonical id (echoed or normalized). |
| `targetName` | string | Echoed or normalized name. |
| `displayLabel` | string | Normalized display label. |
| `status` | string | e.g. `created`, `accepted`, `failed`. |
| `createdAtUtc` | string | Server timestamp, ISO-8601 UTC. |

### Example — request

```json
{
  "targetId": "poster-a",
  "targetName": "Poster A",
  "displayLabel": "Main wall poster",
  "targetImageUrl": "https://cdn.example.com/uploads/poster_a.jpg",
  "localPosition": { "x": 0, "y": 0, "z": 0 },
  "localEuler": { "x": 0, "y": 90, "z": 0 },
  "localScale": { "x": 1, "y": 1, "z": 1 },
  "meta": {
    "schemaVersion": "v1",
    "clientRequestId": "req-target-001",
    "createdAtUtc": "2026-04-18T12:00:00Z"
  }
}
```

### Example — success response

```json
{
  "targetId": "poster-a",
  "targetName": "Poster A",
  "displayLabel": "Main wall poster",
  "status": "created",
  "createdAtUtc": "2026-04-18T12:00:01Z"
}
```

---

## `GET /api/targets`

Returns all targets (or the server-defined default list) for gallery / admin / sync.

### Request

No body. Query parameters are optional and backend-defined (e.g. pagination); if absent, document server behavior in backend readme—not duplicated here unless agreed.

### Response `200`

**Content-Type:** `application/json`

Body: **JSON array** of target summary objects (raw array, no envelope). Each element uses the same shape as `CreateTargetResponseDto`, optionally extended by the backend (e.g. `targetImageUrl`); clients should ignore unknown fields.

| Field | Type | Required on item | Description |
|--------|------|------------------|-------------|
| `targetId` | string | yes | Canonical id. |
| `targetName` | string | yes | Name. |
| `displayLabel` | string | no | Label. |
| `status` | string | no | Last-known lifecycle status if applicable. |
| `createdAtUtc` | string | no | ISO-8601 UTC. |
| `targetImageUrl` | string | no | If server stores it for list views. |

### Example — success response

```json
[
  {
    "targetId": "poster-a",
    "targetName": "Poster A",
    "displayLabel": "Main wall poster",
    "status": "accepted",
    "createdAtUtc": "2026-04-18T12:00:01Z",
    "targetImageUrl": "https://cdn.example.com/uploads/poster_a.jpg"
  }
]
```

---

## `DELETE /api/targets/{targetId}`

Deletes the target identified by path parameter `targetId`.

### Request

Path:

| Parameter | Description |
|-----------|-------------|
| `targetId` | Canonical target id (URL-encoded if needed). |

No body required.

### Response `200`

**Content-Type:** `application/json`

Minimal confirmation DTO:

| Field | Type | Description |
|--------|------|-------------|
| `targetId` | string | Deleted id. |
| `status` | string | e.g. `deleted`. |

### Example — success response

```json
{
  "targetId": "poster-a",
  "status": "deleted"
}
```

**Note:** If the backend prefers `204 No Content` with an empty body, that should be treated as success at the HTTP layer; the JSON shape above is the contract when a body is returned.

---

## Errors

Failures use the standardized error object from `common.md` and appropriate HTTP status (e.g. `404` if `targetId` not found on GET/DELETE, `409` on duplicate create if applicable).
