# Target API contract (v1)

Base path prefix: `/api`. See `common.md` for success/error conventions and shared types.

---

## Frontend Target-Gate Semantics (Switcher -> TargetScene -> Authoring)

This contract supports a frontend scene gate where target creation and cloud publish are coupled in one submit flow.

Expected frontend sequence:

1. `POST /api/targets` (create/register draft target)
2. `POST /api/targets/{targetId}/publish` (attempt cloud publish immediately)
3. Proceed to authoring scene **only** when publish succeeds (`publishStatus = published` and `cloudTargetId` is non-empty).

If publish fails, frontend must stay in target scene and offer:

- retry publish
- cancel back to switcher

Image quality concerns are soft warnings on frontend and do not change this API-level gate policy.

---

## `POST /api/targets`

Creates (or registers) a target. Aligns with `CreateTargetRequestDto` / `CreateTargetResponseDto` in Unity.

### Request

**Content-Type:** `application/json`

| Field | Type | Required | Description |
|--------|------|----------|-------------|
| `targetId` | string | yes | Canonical id (e.g. `poster-a`, `target-001`). |
| `targetName` | string | yes | Internal technical name. |
| `displayLabel` | string | no | User-facing label; empty may fall back to `targetId` server-side. |
| `targetImageUrl` | string | no | Reference image URL, marker, or empty. |
| `physicalWidth` | number | yes | Physical width in meters, must be > 0. |
| `vuforiaTargetName` | string | no | Cloud-side target name hint (backend may normalize/fill). |
| `publishStatus` | string | no | Initial status; defaults to `draft`. |
| `publishIdempotencyKey` | string | no | Optional idempotency hint for publish chain. |
| `mappingVersion` | number | no | Mapping revision, default backend-defined (`1` typical). |
| `mappingChecksum` | string | no | Mapping integrity checksum (backend may generate/override). |
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
| `targetImageUrl` | string | Persisted target image URL. |
| `physicalWidth` | number | Persisted physical width in meters. |
| `vuforiaTargetName` | string | Persisted cloud-side name (empty if not yet resolved). |
| `cloudTargetId` | string | Cloud target id when published; empty otherwise. |
| `publishStatus` | string | `draft`, `publishing`, `published`, `failed`. |
| `publishIdempotencyKey` | string | Current/last publish idempotency key. |
| `lastSuccessfulPublishIdempotencyKey` | string | Key used for latest successful publish. |
| `lastPublishError` | string | Last publish failure detail. |
| `mappingVersion` | number | Mapping revision number. |
| `mappingChecksum` | string | Mapping checksum string. |
| `status` | string | e.g. `created`, `accepted`, `failed`. |
| `createdAtUtc` | string | Server timestamp, ISO-8601 UTC. |

### Example — request

```json
{
  "targetId": "poster-a",
  "targetName": "poster_main",
  "displayLabel": "Main wall poster",
  "targetImageUrl": "https://cdn.example.com/uploads/poster_a.jpg",
  "physicalWidth": 0.4,
  "vuforiaTargetName": "poster-a-main",
  "publishStatus": "draft",
  "publishIdempotencyKey": "req-target-001",
  "mappingVersion": 1,
  "mappingChecksum": "",
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
  "targetName": "poster_main",
  "displayLabel": "Main wall poster",
  "targetImageUrl": "https://cdn.example.com/uploads/poster_a.jpg",
  "physicalWidth": 0.4,
  "vuforiaTargetName": "poster-a-main",
  "cloudTargetId": "",
  "publishStatus": "draft",
  "publishIdempotencyKey": "req-target-001",
  "lastSuccessfulPublishIdempotencyKey": "",
  "lastPublishError": "",
  "mappingVersion": 1,
  "mappingChecksum": "",
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

## `GET /api/targets/{targetId}`

Returns one target by canonical `targetId`.

### Response `200`

Same object shape as target create response, including lifecycle/mapping fields.

### Response `404`

Standard error object from `common.md` (`NOT_FOUND`).

---

## `POST /api/targets/{targetId}/publish`

Attempts Vuforia cloud publish for an existing target. This endpoint is the frontend hard-gate checkpoint.

### Request

**Content-Type:** `application/json` (empty body allowed)

Optional fields:

| Field | Type | Required | Description |
|--------|------|----------|-------------|
| `publishIdempotencyKey` | string | no | Client idempotency key override/hint. |
| `meta` | object | no | `ApiSyncMetaDto`; `clientRequestId` may be used as idempotency hint. |

### Response `200` / `202`

Response body uses target response shape with lifecycle fields.

Frontend gating rules:

- Enter authoring only when `publishStatus == "published"` and `cloudTargetId` is non-empty.
- If response is accepted but still in `publishing`, frontend remains blocked and keeps polling/retrying policy in target scene.

### Response `502`

Publish failed at upstream provider. Body contains standard error object and may include failure details in `details`.

Frontend handling:

- Stay in target scene (hard block)
- Surface `lastPublishError` (if present via details)
- Offer retry or cancel

---

## `POST /api/targets/{targetId}/retry-publish`

Retries a failed/incomplete publish attempt.

### Behavior

- Backend generates or updates idempotency key for the new publish attempt.
- Response semantics match `/publish`.
- Frontend stays blocked from authoring until `published`.

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

Failures use the standardized error object from `common.md` and appropriate HTTP status.

Common target-flow cases:

- `400 VALIDATION_ERROR` (missing/invalid target fields)
- `404 NOT_FOUND` (unknown `targetId`)
- `409 CONFLICT` (publish in progress with conflicting idempotency semantics)
- `502 SERVER_ERROR` (upstream Vuforia publish failure)
