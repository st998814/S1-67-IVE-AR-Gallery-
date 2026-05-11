# Dynamic Resource Instantiation Controller
## Assignment :  DEV-96 - Dynamic Resource Instantiation Controller

## Outlines

### 1. Unified object-creation entry point

- Introduce `ISpawnerManager` / `SpawnerManager` as the single runtime creation API between UI and object workflows.
- Keep request/response contracts UI-agnostic via `SpawnRequest`, `SpawnTargetRequest`, `SpawnContentResult`, and `SpawnTargetResult`.
- Use `ITargetContextResolver` + `TargetSelectionContextResolver` to resolve active target context and `ContentRoot` without UI coupling.

### 2. Routed content creation with existing services

- Route text creation through `ContentCreationCoordinator.SpawnText`.
- Route image/model upload creation through `ContentCreationCoordinator.SpawnFromContentUpload` (which keeps model loading via `ModelLoadService`).
- Preserve existing pooling/lifecycle internals by delegating to current content workflow/factory systems.

### 3. Runtime hierarchy integration

- Move parenting + placement to `SpawnerManager` so spawned objects are integrated into target `ContentRoot` consistently.
- Align upload-driven content to `TargetVisual` and apply a small forward offset to reduce z-fighting.
- Keep text spawn placement at `ContentRoot` origin by default.
- Support optional local transform override (`SpawnTransformData`) after default placement.

### 4. Non-blocking sync as separate concern

- Keep local-first creation immediate (`CreateContent`, `CreateTarget`).
- Expose dedicated async sync helpers (`BeginSyncCreateContent`, `BeginSyncCreateTarget`) that delegate to existing sync workflows.
- Maintain failure-tolerant behavior: sync failure does not break or remove local runtime objects.

## Modification

### 1. Spawner contracts and abstractions — `Spawn/`

| File | Description |
|------|-------------|
| **SpawnContracts.cs** | Ticket-level spawn request/result models for unified runtime creation and transform override support. |
| **ISpawnerManager.cs** | Unified creation/sync interface (`CreateContent`, `CreateTarget`, `BeginSyncCreateContent`, `BeginSyncCreateTarget`). |
| **ITargetContextResolver.cs** | Target context abstraction to resolve active target and `ContentRoot` independently from UI implementation. |
| **TargetSelectionContextResolver.cs** | Unity-backed resolver implementation using `TargetSelectionManager`. |

### 2. Spawner orchestration — `Spawn/SpawnerManager.cs`

| File | Description |
|------|-------------|
| **SpawnerManager.cs** | Centralized routing for text/image/model creation, hierarchy integration (parenting/alignment), optional transform override handling, and non-blocking sync delegation. |

### 3. UI integration — `AuthoringUIController.cs`

| File | Description |
|------|-------------|
| **AuthoringUIController.cs** | Refactored to use `ISpawnerManager` for create-target, text spawn, upload spawn, and sync trigger paths, removing direct creation branching in UI actions. |
