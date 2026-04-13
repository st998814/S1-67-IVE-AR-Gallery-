# Object Creation Workflow

## Assignment : DEV-94 - Refactor Unity API Client Workflow 

---

## Outlines

### 1. Module integration (UI, scene objects, network)

The authoring stack is split into clear responsibilities and dependencies:

- **UI layer** — `AuthoringUIController` (UI Toolkit) drives user actions: file browse, create target, spawn content, and save. It does not embed HTTP details; it resolves an `IApiClient` and calls workflow services.
- **Workflow services** — `UploadWorkflowService` (file → URL), `TargetWorkflowService` (targets), and `ContentWorkflowService` (content) encapsulate local-first steps and API sync. They depend on DTOs (`UploadContracts`, `TargetContracts`, `ContentContracts`) and return or accept `ApiResult<T>` via callbacks.
- **Runtime object layer** — `RuntimeImageTargetFactory` builds each target’s hierarchy (`ArImageTarget`, visual, `ContentRoot`). `RuntimeContentFactory` instantiates image/text prefabs and attaches `DraggableObject`. `TargetSelectionManager` tracks targets and the active target for parenting content.
- **Network abstraction** — `IApiClient` defines upload/create operations with `IApiRequestHandle` for cancellation. `HttpApiClient` is the concrete `UnityWebRequest`-based implementation against a configurable backend base URL.


### 2. Local-first workflow and runtime/API data flow

User-visible behavior follows **local-first**: create and edit in the scene immediately, then **sync** to the server when a client is available. Failures on sync do not discard local objects unless the product explicitly does so.

**Targets**

- User supplies target name/id and optionally a target image. If an image is chosen, the UI uploads via `UploadWorkflowService` → `IApiClient.UploadFile` → backend; the returned URL can be applied to the target visual (`ApplyTargetImageFromUrl`).
- **Create Target** runs `TargetWorkflowService.CreateAndRegisterLocal`: factory creates the GameObject, `TargetSelectionManager` registers it. Optionally `SyncCreateTarget` sends metadata (and transforms) via `CreateTarget`; the callback receives `ApiResult<CreateTargetResponseDto>` while the local target remains if sync fails.

**Content**

- User picks content type, paths/URLs, and transform. Media may be uploaded first (`UploadSelectedFile` → `ApiResult<UploadFileResponseDto>`).
- **Spawn** uses `ContentWorkflowService` (`SpawnImageLocal` / `SpawnTextLocal`) and `RuntimeContentFactory`; content is parented under the active target’s content root.
- **Save** calls `SyncCreateContent` with ids, transforms, and `mediaUrl`; the server is updated through `CreateContent` and `ApiResult<CreateContentResponseDto>`, with the same local-first tolerance on failure.

---

## Modification

Summary of new or touched pieces for the local-first + API workflow. Paths are under `Assets/Scripts/`.

### 1. API abstractions — `Api/Abstractions/`

| File | Description |
|------|-------------|
| **ApiResult.cs** | Generic success/failure wrapper for API calls: `payload`, HTTP-style `statusCode`, `errorCode`, and static `Ok` / `Fail` helpers. Includes **ApiErrorCodes** constants (network, timeout, validation, etc.). |
| **CoroutineApiRequestHandle.cs** | **IApiRequestHandle** implementation that binds a Unity **Coroutine**, exposes **Cancel** (stops the coroutine), and tracks **IsDone** / **IsCancelled**. |
| **IApiClient.cs** | Coroutine-oriented API surface: **UploadFile**, **CreateTarget**, **CreateContent** — each returns a handle and invokes a completion callback with a typed **ApiResult** payload. |
| **IApiRequestHandle.cs** | Minimal contract to cancel in-flight work and observe completion (**IsDone**, **IsCancelled**, **Cancel**). |

### 2. Request/response DTOs — `Api/Contracts/`

| File | Description |
|------|-------------|
| **ApiCommonContracts.cs** | Shared JSON-serializable types: generic **ApiResponseEnvelope**, **ApiVector3Dto**, **ApiSyncMetaDto** (schema version, client request id, timestamps). |
| **ContentContracts.cs** | **CreateContentRequestDto** / **CreateContentResponseDto** — content id, **targetId**, type, **mediaUrl**, local transform + meta for POST `/api/content`. |
| **TargetContracts.cs** | **CreateTargetRequestDto** / **CreateTargetResponseDto** — **targetId**, name, label, **targetImageUrl**, local transform + meta for planned POST targets API. |
| **UploadContracts.cs** | **UploadFileRequestDto** (file name, MIME, **fileBytes**, meta) and **UploadFileResponseDto** (**url**, size, timestamps) for `/api/upload`. |

### 3. HTTP client — `Api/Http/`

| File | Description |
|------|-------------|
| **HttpApiClient.cs** | **MonoBehaviour** implementing **IApiClient**: real **UnityWebRequest** upload to configurable base URL; **CreateTarget** / **CreateContent** stubs until backend routes are wired. |

### 4. Content runtime — `Content/`

| File | Description |
|------|-------------|
| **Managers/RuntimeContentFactory.cs** | Instantiates image/text prefabs, applies text to **TextMesh** / **TextMeshPro**, returns **DraggableObject** + success metadata. |
| **Services/ContentWorkflowService.cs** | Local-first content flow: **SpawnImageLocal** / **SpawnTextLocal**, then **SyncCreateContent** via **IApiClient** with validation (e.g. media URL for image/video). |

### 5. Target runtime — `Target/`

| File | Description |
|------|-------------|
| **Managers/RuntimeImageTargetFactory.cs** | Builds per-target hierarchy under **ImageTargetRoot**: **ArImageTarget**, placeholder, **TargetVisual**, **ContentRoot**. |
| **Services/TargetWorkflowService.cs** | Local-first target flow: **CreateAndRegisterLocal**, **ApplyTargetImageFromUrl**, **SyncCreateTarget** — coordinates factory, **TargetSelectionManager**, and API. |

### 6. Authoring UI — `Scripts/` (modified)

| File | Description |
|------|-------------|
| **AuthoringUIController.cs** | UI Toolkit authoring panel: wires browse/save/create-target, resolves **IApiClient**, and delegates to **UploadWorkflowService**, **TargetWorkflowService**, and **ContentWorkflowService** for the full local-first workflow. |

---

## Related

- Follow-up ticket doc: `MultimediaObjectPoolAnd3DPrefabLoading.md` for render branching, GLB loading, pooling, and 3D sync metadata refinements.
