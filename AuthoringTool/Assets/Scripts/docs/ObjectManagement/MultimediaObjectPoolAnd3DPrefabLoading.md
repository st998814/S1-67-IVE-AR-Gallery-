≈

## Assignment : DEV-95 - Multimedia Object Pool and 3D Prefab Loading

---

## Outlines

### 1. Multimedia content creation architecture

The content runtime now supports both surface-based and volumetric content through a routed creation flow:

- **Entry routing** — `ContentCreationCoordinator` resolves media kind and routes to the correct runtime path (`image` / `video` / `text` / `model`).
- **Surface creation** — image/text still use the existing local-first workflow through `ContentWorkflowService` + `RuntimeContentFactory`.
- **Volumetric creation** — model uploads (`.glb`) create a dedicated model container and load payload asynchronously through `ModelLoadService`.
- **Sync separation** — local runtime creation remains primary; API persistence remains a separate `SyncCreateContent` step.

### 2. Runtime object structure and lifecycle

Runtime object handling is now shell-based and reuse-oriented:

- **3D container structure** — model content is spawned using `ModelContentContainer` (`ContentContainer` → `ContentBody`).
- **Object pooling** — shell instances are reused through runtime pooling instead of always instantiate/destroy.
- **Lifecycle reset** — pooled objects are reset on acquire/release; model shells clear all `ContentBody` children on release.
- **Safe pool release** — pooled instances carry a runtime tag that identifies shell type, so release does not rely on caller-supplied type.

### 3. 3D-capable sync contract

Content sync payload now carries minimal metadata for volumetric content while keeping `mediaUrl` canonical:

- `mediaUrl` remains the single canonical asset URL.
- `CreateContentRequestDto` adds:
  - `renderKind` (`surface` | `volumetric`)
  - `assetFormat` (for model payload hints such as `glb`)
- Existing target/content relation and transform payload remain unchanged (`targetId`, `localPosition`, `localEuler`, `localScale`).

---

## Modification

Summary of new or touched pieces for multimedia content creation, pooling, and 3D-capable sync. Paths are under `Assets/Scripts/` unless noted.

### 1. Content routing and orchestration — `Content/Services/`

| File | Description |
|------|-------------|
| **ContentCreationCoordinator.cs** | Unified content creation entry. Routes upload-driven creation by media/render kind. Handles model route (`.glb`) and delegates local surface spawning / sync to workflow service. |
| **ContentWorkflowService.cs** | Keeps local-first surface creation and sync responsibilities. Adds release-to-pool API and extends sync mapping (`renderKind`, `assetFormat`) while keeping `mediaUrl` canonical. |
| **ModelLoadService.cs** | Runtime GLB loading pipeline using glTFast: download, import, instantiate under model container `ContentBody`, and report load feedback. |

### 2. Runtime content management — `Content/`

| File | Description |
|------|-------------|
| **ContentRenderTypes.cs** | Defines render/media enums and runtime shell type keying used by pooling and routing. |
| **Managers/RuntimeContentFactory.cs** | Content shell factory with integrated runtime pooling, acquire/release lifecycle reset, and tag-based pool return safety. |
| **ModelContentContainerRoot.cs** | Resolves model attach transform (`ContentBody`) for volumetric payload placement. |

### 3. Authoring integration — `Scripts/`

| File | Description |
|------|-------------|
| **AuthoringUIController.cs** | Uses routed content creation flow, supports `.glb` browse/upload path, and resolves model container prefab fallback for runtime spawning. |

### 4. API contracts — `Api/Contracts/`

| File | Description |
|------|-------------|
| **ContentContracts.cs** | Extends `CreateContentRequestDto` with minimal 3D-capable metadata (`renderKind`, `assetFormat`) while preserving existing target-transform contract. |

### 5. Package / prefab assets

| File | Description |
|------|-------------|
| **Packages/manifest.json** | Adds glTFast dependency for runtime GLB loading. |
| **Assets/Resources/Prefabs/ModelContentContainer.prefab** | Dedicated volumetric content shell prefab (`ContentContainer` → `ContentBody`) for model payload attachment. |

---

## Result

- Content creation now supports routed multimedia entry (surface and volumetric paths).
- Runtime model content is loaded through a dedicated `.glb` pipeline and container structure.
- Runtime content lifecycle uses reusable pooled shells for better efficiency.
- Content sync remains local-first and now carries minimal, backward-compatible 3D metadata.

---

# Dynamic Resource Instantiation Controller
## Assignment :  DEV-96 - Dynamic Resource Instantiation Controller

## Outlines

### 1. Multimedia routing under unified spawner

- Keep multimedia entry unified through `SpawnerManager` as the UI-facing creation entry point.
- Delegate media-specific branching to existing coordinator logic (`ContentCreationCoordinator`) to avoid duplicated rules.
- Continue supporting current types (`image`, `text`, `model`) with extensible request-based routing.

### 2. Runtime lifecycle consistency (surface and volumetric)

- Ensure spawned objects are parented under target `ContentRoot` through spawner integration rules.
- Preserve existing pooling behavior from `RuntimeContentFactory` and `RuntimeContentPool` for reusable shell lifecycle.
- Keep volumetric model path unchanged (`ModelLoadService` + `ModelContentContainerRoot`) while integrating through unified spawn request flow.

### 3. Sync remains local-first and asynchronous

- Keep creation immediate in runtime scene (local-first).
- Trigger persistence via `BeginSyncCreateContent` / `BeginSyncCreateTarget` as non-blocking operations.
- Preserve resilience: API sync failure does not remove local runtime objects.

## Modification

### 1. Unified spawn layer — `Spawn/`

| File | Description |
|------|-------------|
| **SpawnContracts.cs** | Ticket-based spawn request/result models for unified multimedia creation API. |
| **SpawnerManager.cs** | Central spawner route for text/image/model with placement integration and delegated sync helpers. |
| **ISpawnerManager.cs** | Public contract for creation and non-blocking sync triggers. |
| **ITargetContextResolver.cs** / **TargetSelectionContextResolver.cs** | Active-target and `ContentRoot` resolution abstraction used during spawn integration. |

### 2. Integration updates — `Scripts/`

| File | Description |
|------|-------------|
| **AuthoringUIController.cs** | Updated to route create-target/spawn/sync actions through `ISpawnerManager`, reducing UI content-type branching. |
