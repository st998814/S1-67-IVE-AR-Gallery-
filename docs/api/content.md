# Content API contract (v1)

Base path prefix: `/api`. See `common.md` for success/error conventions, `ApiVector3Dto`, and `ApiSyncMetaDto`.

**Relationship:** Every content resource is associated with exactly one **parent target** via **`targetId`**. That field is required on create and must reference an existing (or concurrently accepted) target per backend rules.

---

## `POST /api/content`

Creates content and binds it to a target. Aligns with `CreateContentRequestDto` / `CreateContentResponseDto` in Unity.

### Request

**Content-Type:** `application/json`

| Field | Type | Required | Description |
|--------|------|----------|-------------|
| `contentId` | string | yes | Client-proposed or generated id. |
| `targetId` | string | yes | Parent target canonical id. |
| `contentType` | string | yes | e.g. `image`, `video`, `text`, `empty`, `model(3D)`. |
| `mediaUrl` | string | no | Often `UploadFileResponseDto.url` after upload; may be external URL or empty for some types. |
| `localPosition` | object | yes | `ApiVector3Dto`. |
| `localEuler` | object | yes | `ApiVector3Dto` (degrees). |
| `localScale` | object | yes | `ApiVector3Dto`. |
| `renderKind` | string | no | e.g. `surface`, `volumetric`. |
| `assetFormat` | string | no | Hint, e.g. `glb` for models. |
| `meta` | object | no | `ApiSyncMetaDto`. |

### Response `200` / `201`

**Content-Type:** `application/json`

Body matches `CreateContentResponseDto`:

| Field | Type | Description |
|--------|------|-------------|
| `contentId` | string | Echoed or normalized id. |
| `targetId` | string | Associated target id. |
| `status` | string | e.g. `created`, `accepted`, `failed`. |
| `createdAtUtc` | string | ISO-8601 UTC. |

### Example — request

```json
{
  "contentId": "content-001",
  "targetId": "poster-a",
  "contentType": "image",
  "mediaUrl": "https://cdn.example.com/uploads/poster_a.jpg",
  "localPosition": { "x": 0, "y": 0.1, "z": 0 },
  "localEuler": { "x": 0, "y": 0, "z": 0 },
  "localScale": { "x": 0.5, "y": 0.5, "z": 0.5 },
  "renderKind": "surface",
  "assetFormat": "",
  "meta": {
    "schemaVersion": "v1",
    "clientRequestId": "req-content-001",
    "createdAtUtc": "2026-04-18T12:05:00Z"
  }
}
```

### Example — success response

```json
{
  "contentId": "content-001",
  "targetId": "poster-a",
  "status": "created",
  "createdAtUtc": "2026-04-18T12:05:01Z"
}
```

---

## `PATCH /api/content/{contentId}`

Partial update of an existing content record. **`contentId` in the path** is authoritative; do not send a conflicting `contentId` in the body.

### Request

**Content-Type:** `application/json`

Body: subset of create fields; **omit** keys that are unchanged. Typical patchable fields:

| Field | Type | Notes |
|--------|------|--------|
| `targetId` | string | Optional; if supported, moves content to another target. |
| `contentType` | string | Optional. |
| `mediaUrl` | string | Optional. |
| `localPosition` | object | Optional; full `ApiVector3Dto` replacement for position. |
| `localEuler` | object | Optional. |
| `localScale` | object | Optional. |
| `renderKind` | string | Optional. |
| `assetFormat` | string | Optional. |
| `meta` | object | Optional; merge or replace per backend policy. |

### Response `200`

**Content-Type:** `application/json`

Body: same shape as `CreateContentResponseDto` (current state after patch), or backend may return an extended object; clients tolerate unknown fields.

### Example — request

```json
{
  "localPosition": { "x": 0, "y": 0.2, "z": 0 },
  "mediaUrl": "https://cdn.example.com/uploads/replacement.jpg"
}
```

### Example — success response

```json
{
  "contentId": "content-001",
  "targetId": "poster-a",
  "status": "accepted",
  "createdAtUtc": "2026-04-18T12:05:01Z"
}
```

---

## Errors

Standard error body from `common.md`. Examples: `404` if `contentId` or referenced `targetId` is invalid; `VALIDATION_ERROR` for invalid `contentType` or transform data.

---

## Transform and target linkage

- **Target linkage:** `targetId` in the JSON body ties content to a target for both `POST` and (when provided) `PATCH`.  
- **Transform data:** `localPosition`, `localEuler`, and `localScale` describe the content instance relative to its parent/context as defined by the product (see authoring docs in Unity repo for scene semantics).  
- **Upload linkage:** When media is uploaded first, set `mediaUrl` to the `url` returned from `POST /api/upload` (see `upload.md`).
