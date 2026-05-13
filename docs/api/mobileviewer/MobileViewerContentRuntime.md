# MobileViewer runtime content API (v1)

## Scope

Contracts in this folder describe how the **MobileViewer** Unity client talks to the backend at runtime to:

- resolve a recognized Vuforia Cloud target into a canonical backend target, and
- fetch the content that should be rendered for that target on demand.

The HTTP conventions and shared DTO fragments follow `../common.md`.

---

## 1. Target resolution for Vuforia Cloud ids

### `GET /api/targets/resolve`

MobileViewer receives a **Vuforia Cloud target identifier** from `VuforiaCloudTargetController`.  
It calls the existing backend endpoint to resolve that id into a canonical `targetId` and display metadata.

See full field listing and examples in `../target.md` (`GET /api/targets/resolve`); MobileViewer uses a subset:

**Request**

- Query parameter: `vuforiaTargetId` — string, required.

**Success response (`200`) — fields relevant to MobileViewer**

| Field | Type | Description |
|--------|------|-------------|
| `targetId` | string | Canonical backend target id (used in subsequent content lookups). |
| `displayLabel` | string | Human-facing label for status/toast UI. |
| `targetImageUrl` | string | Optional reference image URL (debug/QA use). |
| `vuforiaTargetId` | string | Echoed Vuforia Cloud target id. |

**Error responses (`400` / `404`)**

- Standard error object from `common.md` with `VALIDATION_ERROR` for missing query param, or `NOT_FOUND` when the Vuforia id is unknown.

---

## 2. Fetching content for a resolved target (MobileViewer-specific)

MobileViewer uses a **runtime-optimized** content endpoint that returns at most one record for a given target key.  
This is a convenience wrapper over the more general authoring content APIs in `../content.md`.

### `GET /api/mobileviewer/content/by-target/{targetKey}`

Where `targetKey` is one of:

- the canonical backend `targetId` (e.g. `ttest`, `demo-vuforia-target-2`), or
- a Vuforia Cloud target id (UUID-style) that the backend aliases to a canonical target.

Backends may support both forms simultaneously; the lookup rules should be documented in backend code comments if they diverge from this doc.

**Request**

- Path parameter: `targetKey` — string, required.
- No body.

**Success response (`200`)**

- Body: a single JSON object representing **the content that MobileViewer should render for this target**.
- Field names are camelCase and map directly or trivially into the Unity `ContentData` DTO.

| Field | Type | Required | Description |
|--------|------|----------|-------------|
| `targetName` | string | yes | Canonical or display name for the target. Used for logging and status text. |
| `title` | string | no | Content title. |
| `description` | string | no | Longer description. |
| `contentType` | string | yes | Strict backend runtime type: `image`, `video`, `model`. |
| `mediaUrl` | string | no | URL for renderable media. Required in practice for `image`, `video`, and `model` runtime renderers. |
| `localPosition` | object | no | Optional `ApiVector3Dto` authored local position relative to target. |
| `localEuler` | object | no | Optional `ApiVector3Dto` authored local Euler rotation (degrees) relative to target. |
| `localScale` | object | no | Optional `ApiVector3Dto` authored local scale relative to target. |
| `color` | string | no | Hex color (`#RRGGBB` or `#RRGGBBAA`) for mock primitive tinting. |
| `displayLabel` | string | no | Label to show in UI; falls back to `targetName` when empty. |

Servers may include extra fields; MobileViewer should ignore unknown keys.

### Source content type constraints

Persisted content in backend is validated against the strict set:

- `image`
- `video`
- `model`

Current MobileViewer runtime behavior:

- image plane renderer
- video surface/player renderer: **stub for next step**
- `.glb` model renderer: **stub for next step**

**Example — success response**

```json
{
  "targetName": "ttest",
  "title": "Demo cube",
  "description": "Test content for ttest target.",
  "contentType": "cube",
  "color": "#FFAA33",
  "displayLabel": "Test Target"
}
```

**Not found (`404`)**

- Indicates that no runtime content is configured for this `targetKey`.
- Body: standard error object from `common.md` with `errorCode` typically `NOT_FOUND`.
- MobileViewer behavior:
  - show a toast such as `No content configured for this target`, and
  - continue scanning; no content is rendered.

**Other errors (`5xx` / network)**

- Treated as transient faults.
- MobileViewer behavior:
  - show a short error toast (`Loading content failed`),
  - log the `message` and `errorCode`,
  - allow retry on the next recognition of the same target.

---

## 3. MobileViewer client expectations and mapping

### 3.1 Request/response expectations

- **Transport**
  - HTTP over HTTPS in non-local environments.
  - JSON bodies follow camelCase naming from `common.md`.
- **Latency**
  - Endpoints are called **on demand** after recognition; backend should keep response times low enough that users see content within a reasonable delay.

### 3.2 Mapping to Unity types (approximate)

| Concept | Backend field(s) | Unity side (MobileViewer) |
|--------|-------------------|---------------------------|
| Recognized target | `vuforiaTargetId` | `VuforiaCloudTargetController` emits Vuforia id string. |
| Canonical target | `targetId`, `displayLabel` | Passed into the content service as lookup key and used in status UI text. |
| Content choice | first item in `GET /api/content?targetId=...` | Mapped into `ContentData` (`targetName`, `title`, `description`, `contentType`, etc.). |
| Rendered primitive | `contentType` | `ContentRenderer` picks primitive type (`cube`/`capsule`/`sphere`/future). |

The exact Unity C# DTOs and services are defined under `MobileViewer/Assets/Scripts/Content/` and `MobileViewer/Assets/Scripts/AR/`.

---

## 4. Future extensions (non-breaking)

Potential MobileViewer-specific endpoints or fields that can be added without breaking this contract:

- **Runtime-friendly content summaries**  
  e.g. `GET /api/mobileviewer/content-summary?vuforiaTargetId=...` returning a single lightweight object tailored to MobileViewer.

- **Per-target runtime config**  
  additional fields on `GET /api/targets/resolve` such as `runtimeStyle` or `runtimeVariant`.

All such changes should follow the process in `../common.md` (update docs + code in the same PR, keep existing fields stable).

