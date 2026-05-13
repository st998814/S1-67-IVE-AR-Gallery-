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

## 2. Fetching content for a resolved target

### `GET /api/content?targetId={targetId}`

Once MobileViewer has a canonical `targetId`, it retrieves content rows bound to that target using the existing content listing endpoint.

See full shape in `../content.md` (`GET /api/content`); MobileViewer uses a narrowed view to drive its simple mock-3D renderer.

**Request**

- Query parameter: `targetId` — string, required for this flow.
- No body.

**Success response (`200`)**

- Body: JSON array of content objects.
- For this prototype, MobileViewer expects **zero or more** rows and typically uses the **first** record matching simple criteria (for example, first `contentType` it recognizes).

Fields MobileViewer reads:

| Field | Type | Description |
|--------|------|-------------|
| `contentId` | string | Content identifier (for logging/analytics). |
| `targetId` | string | Must match the requested target id. |
| `contentType` | string | Mapped to mock primitive type (e.g. `cube`, `capsule`, `sphere`) or future richer renderers. |
| `meta.title` | string | Optional title used for debug/toast messaging. |
| `meta.description` | string | Optional description (debug use in logs). |

Other fields (`mediaUrl`, transforms) are available but not required by the current MobileViewer implementation.

**Empty result (`200` with `[]`)**

- MobileViewer treats this as “no content configured for this target” and may:
  - show a toast like `No content configured for {displayLabel}`, and
  - continue scanning.

**Error responses (`4xx` / `5xx`)**

- Standard error object from `common.md`; MobileViewer should:
  - log the message,
  - show a short error toast (`Loading content failed`), and
  - remain stable (continue scanning on next recognition).

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

