# Common API contract (v1)

Shared JSON shapes, HTTP conventions, ownership, and change workflow for Unity (frontend) and backend.

---

## HTTP response convention (v1)

**Success (2xx)**  
The response body is a **raw JSON object** (or JSON array where noted) matching the documented DTO for that endpoint. **No** outer `success` / `data` envelope is used in the HTTP contract.

**Error (4xx / 5xx)**  
The response body is a **standardized error object** (JSON). Clients use the HTTP status code plus this body for handling. Field names are camelCase.

```json
{
  "message": "Short human-readable explanation.",
  "errorCode": "VALIDATION_ERROR"
}
```

Optional keys backends may add (clients should tolerate unknown keys):

| Field | Type | Description |
|--------|------|-------------|
| `requestId` | string | Correlation id for logs (may echo `clientRequestId` from the request). |
| `details` | string or object | Extra validation or debug info (avoid leaking secrets). |

Example error codes (non-exhaustive; backend owns the canonical list): `VALIDATION_ERROR`, `NOT_FOUND`, `CONFLICT`, `UNAUTHORIZED`, `SERVER_ERROR`.

**Note:** Unity may wrap HTTP results in internal types (e.g. `ApiResult<T>`). Those wrappers are **not** part of the wire contract.

---

## Status field semantics

Some endpoints return both transport status and domain status fields.

Use them distinctly:

- HTTP status code indicates transport/request outcome.
- Body `status` indicates endpoint-level processing state (`created`, `accepted`, `deleted`, etc.).
- For cloud-target flow, use endpoint response fields (for example `vuforiaStatus`, `vuforiaTargetId`) plus HTTP status.

---

## `ApiVector3Dto` (Vector3)

Used for local position, Euler rotation (degrees), and scale in authoring/runtime snapshots.

| Field | Type | Description |
|--------|------|-------------|
| `x` | number | Float component. |
| `y` | number | Float component. |
| `z` | number | Float component. |

```json
{ "x": 0, "y": 1.2, "z": -0.05 }
```

---

## `ApiSyncMetaDto` (meta)

Optional on requests where noted; helps idempotency and tracing. The same JSON shape is reused across endpoints; **servers should ignore keys they do not persist** (extra fields are forward-compatible).

### Core fields (all endpoints that embed `meta`)

| Field | Type | Description |
|--------|------|-------------|
| `schemaVersion` | string | Contract version, e.g. `"v1"`. |
| `clientRequestId` | string | Client-generated correlation / idempotency hint. |
| `createdAtUtc` | string | ISO-8601 UTC timestamp when the client formed the request. |

### Optional content-oriented fields (primarily `POST /api/content`)

Used by the authoring client when persisting content so backends or CMS layers can store human-readable copy alongside transforms and `mediaUrl`. All are optional; empty string or omission is acceptable.

| Field | Type | Description |
|--------|------|-------------|
| `title` | string | Display title for the content instance. |
| `description` | string | Longer description or notes. |
| `textBody` | string | For `text` content: body text (may duplicate the text stored in `mediaUrl` depending on client; useful when `mediaUrl` is a marker or shortened). |
| `localContentId` | string | Client-local id (e.g. draft registry id). Distinct from canonical **`contentId`** in the POST body when both are sent; aids reconciliation and logs. |

### Example — tracing only (targets, uploads, minimal content)

```json
{
  "schemaVersion": "v1",
  "clientRequestId": "req-001",
  "createdAtUtc": "2026-04-18T12:00:00Z"
}
```

### Example — content save with extended meta

```json
{
  "schemaVersion": "v1",
  "clientRequestId": "req-content-001",
  "createdAtUtc": "2026-04-18T12:05:00Z",
  "title": "Welcome panel",
  "description": "Lobby AR copy",
  "textBody": "Tap to continue",
  "localContentId": "a1b2c3d4e5f64789"
}
```

---

## Naming conventions

- JSON property names: **camelCase** (matches Unity `JsonUtility` field names as serialized).
- Identifiers: **`targetId`**, **`contentId`** are canonical keys across APIs.
- Target publish lifecycle strings: `draft`, `publishing`, `published`, `failed`.
- Timestamps: **ISO-8601 UTC** (e.g. `2026-04-18T12:00:00Z`).
- Enum-like strings: lowercase with hyphens where already established (e.g. `poster-a`); `contentType` values such as `image`, `video`, `text`, `empty`, `model` as sent by the Unity authoring client (legacy rows or copy may say `model(3D)`).

---

## Data ownership

| Data | Owner | Notes |
|------|--------|--------|
| **Persistence, durability, backups** | Backend | Source of truth after accept. |
| **HTTP error shape and status codes** | Backend | Standardized error object on failure. |
| **Stable public URLs for uploaded media** | Backend | `UploadFileResponseDto.url`; CDN/hosting policy is server-side. |
| **Normalization** (trim labels, duplicate id policy, timestamps on records) | Backend | Server may echo or adjust fields; document divergences if any. |
| **Runtime / scene transforms** (local position, rotation, scale) | Frontend (authoring) | Client sends snapshots (`ApiVector3Dto`); backend stores if the product requires sync across devices. |
| **Client correlation** | Frontend | `clientRequestId` and optional `createdAtUtc` in `meta`. |
| **Optional content copy in `meta`** | Frontend proposes | `title`, `description`, `textBody`, `localContentId` on `POST /api/content`; backend may persist, echo on `GET`, or ignore. |
| **Proposed ids** (`targetId`, `contentId` on create) | Frontend proposes | Backend may accept as-is, normalize, or reject with `CONFLICT` / `VALIDATION_ERROR`. |

**Examples**

- Frontend sends `localPosition` / `localEuler` / `localScale` on target or content create; backend persists them for gallery restore.  
- Frontend uploads bytes via `POST /api/upload`; backend returns `url`; frontend sets `mediaUrl` on content to that `url` or uses an external URL (e.g. streaming link) without upload.  
- Backend returns `createdAtUtc` on create responses; frontend displays or logs but does not treat itself as authoritative over server clock.

---

## API contract collaboration workflow

1. **Draft** — Frontend or backend proposes contract changes in Markdown under `docs/api/` (same PR as DTO or OpenAPI change when applicable).  
2. **Review** — Both sides review field names, optional vs required fields, error codes, and ownership.  
3. **Merge** — Changes land via PR after approval.  
4. **Implement** — Client and server implement against the merged doc.  

**Rule:** Any change to public endpoints, request/response JSON shapes, status codes, or error semantics **must** include an update to the relevant file in `docs/api/` in the same PR when possible, or a follow-up PR immediately after.

---

## Related Unity DTO sources (implementation reference)

| Concept | C# types (approximate) |
|---------|-------------------------|
| Vector3 / meta | `ApiVector3Dto`, `ApiSyncMetaDto` in `ApiCommonContracts.cs` |
| Envelope | `ApiResponseEnvelope<T>` exists in code for legacy/other use; **v1 HTTP contract does not use it** for the endpoints in this folder. |

See `target.md`, `content.md`, and `upload.md` for endpoint-specific payloads.
