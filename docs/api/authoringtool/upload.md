# Upload API contract (v1)

Base path: **`POST /api/upload`**. See `common.md` for success/error conventions.

Upload supplies raw file bytes; the backend stores the file and returns a **stable URL** the client uses elsewhere (e.g. `mediaUrl` on `POST /api/content`, or `targetImageUrl` on targets).

In the current authoring flow, upload is generally a **Save-time persistence step** for unresolved local drafts (not a required immediate step at file-selection time).

---

## Request

**Content-Type:** `multipart/form-data`

### Parts

| Part name | Type | Required | Description |
|-----------|------|----------|-------------|
| `file` | file (binary) | yes | File bytes. Filename and content-type should be set on this part (browser / Unity `WWWForm.AddBinaryData` behavior). |

Optional parts (only if backend and client agree to support them in a later revision):

| Part name | Type | Required | Description |
|-----------|------|----------|-------------|
| `category` | string | no | `content` (default), `target`, or `target_ref` — selects subdirectory under `uploads/` (`content/`, `target/`, `target_ref/`). |
| `targetId` | string | no | When `category` is `target`, pass the canonical target id so the server stores **`{targetId}.{ext}`** and overwrites on re-sync (avoids a second file like `ttest-<uuid>.jpg` when `/api/targets/cloud` already wrote `ttest.jpg`). |
| `contentId` | string | no | When `category` is `content`, pass the canonical **`contentId`** (same as `POST /api/content`) so the server stores **`{contentId}.{ext}`** and overwrites on re-sync instead of duplicate `large-47-<uuid>.jpg` files. |
| `meta` | string (JSON) | no | Stringified `ApiSyncMetaDto` for tracing (see `common.md`). May include the full DTO shape; upload handlers should only require tracing fields and ignore unused keys. |

**Unity reference:** `HttpApiClient` sends **`category`**, optional **`targetId`** / **`contentId`** when set on `UploadFileRequestDto`, then **`file`** (remote sync sets ids for deterministic paths).

Conceptual mapping from `UploadFileRequestDto` (C#) to wire:

| DTO field | Wire |
|-----------|------|
| `fileBytes` | Binary content of part `file`. |
| `fileName` | Original filename on part `file`. |
| `mimeType` | Content-Type of part `file`. |
| `uploadCategory` | Maps to multipart `category`: `content` \| `target` \| `target_ref` (Unity client). |
| `targetId` | Maps to multipart `targetId` when category is `target`. |
| `contentId` | Maps to multipart `contentId` when category is `content`. |
| `meta` | Optional `meta` part if both sides implement it. |

---

## Response `200` / `201`

**Content-Type:** `application/json`

Body is a **raw JSON** object matching `UploadFileResponseDto` (no envelope):

| Field | Type | Description |
|--------|------|-------------|
| `url` | string | Public HTTPS URL for the stored object (required for client success handling). |
| `fileName` | string | Echoed or normalized filename. |
| `mimeType` | string | Echoed or detected MIME type. |
| `sizeBytes` | number | Stored size in bytes. |
| `uploadedAtUtc` | string | ISO-8601 UTC. |

### Example — success response

```json
{
  "url": "https://cdn.example.com/uploads/poster_a.jpg",
  "fileName": "poster_a.jpg",
  "mimeType": "image/jpeg",
  "sizeBytes": 532112,
  "uploadedAtUtc": "2026-04-18T12:00:03Z"
}
```

---

## Media URL usage

1. Client calls **`POST /api/upload`** with `multipart/form-data` and part **`file`**.  
2. On success, client reads **`url`** from the JSON body.  
3. Client passes that value as **`mediaUrl`** when persisting content on Save (`content.md`), or as **`targetImageUrl`** / other URL fields on targets (`target.md`) when applicable.  
4. Backends may return CDN URLs, signed URLs, or long-lived public URLs; **contract minimum** is a string usable in subsequent JSON requests and at runtime.

External media (e.g. hosted video links) may skip upload and set `mediaUrl` directly per content contract.

---

## Save-driven flow notes

- Local-first authoring can spawn/edit objects from local files before any backend call.
- On Save, the client uploads unresolved local assets and then persists content metadata through `POST /api/content`.
- For non-text content, `mediaUrl` is expected to be canonical/resolved by the time `POST /api/content` is sent.

---

## Errors

Failures return the standardized error JSON from `common.md` with a non-2xx HTTP status (e.g. `413` payload too large, `415` unsupported type, `400` validation).
