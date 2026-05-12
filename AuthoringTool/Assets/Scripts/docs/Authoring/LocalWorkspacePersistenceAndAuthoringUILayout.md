# Local Workspace Persistence and Authoring UI Layout (Refresh)

## Assignment : Durable local workspace state + authoring chrome iteration (MWP)

---

## Outlines

### 1. Local workspace snapshot model

Authoring workspaces can be persisted under `Application.persistentDataPath` as JSON snapshots plus referenced binary assets:

- **Snapshot DTO** — `WorkspaceSnapshot` aggregates workspace identity, target/content instances, and transform metadata suitable for round-trip rebuild.
- **Vector serialization** — `Vector3Data` (and related helpers) keep snapshot math stable and test-friendly independent of UnityEngine types in persistence layers.
- **Authored identity** — `AuthoredTargetInstance` / `AuthoredContentInstance` tag runtime objects so reconstruction and autosave can correlate scene instances with snapshot rows.

### 2. Disk layout and repositories

- **Paths** — centralized in `WorkspacePersistencePaths` (`workspace-index.json`, per-workspace `snapshot.json`, `assets/targets/`, `assets/contents/`).
- **Snapshot I/O** — `WorkspaceSnapshotRepository` reads/writes snapshot files per workspace id.
- **Asset blobs** — `WorkspaceAssetRepository` stores imported/uploaded bytes on disk and exposes paths for reload.
- **Registry** — `AuthoredObjectRegistry` tracks active authored instances for serialization and reconstruction orchestration.

### 3. Scene reconstruction and debounced autosave

- **Reconstruction** — `WorkspaceSceneReconstructor` rebuilds targets/content in scene using `TargetWorkflowService` / `SpawnerManager` alignment with snapshot and prefab resolution (`AuthoringUIController` prefab hooks where applicable).
- **Serialization** — `WorkspaceStateSerializer` maps registry + scene state into snapshot DTOs.
- **Autosave** — `WorkspaceAutoSaveService` debounces changes (`NotifyWorkspaceChanged`) and persists snapshot + assets without blocking every frame.

### 4. Workspace lifecycle (switcher, delete, session handoff)

- **Switcher merge** — `WorkspaceSwitcherController` merges provider/mock seeds with on-disk `workspace-index.json` so local workspaces appear alongside demo entries.
- **Deletion** — `WorkspaceDeletion.TryDeleteWorkspaceEverywhere` removes workspace folder and index entry consistently.
- **Session flush** — before leaving authoring (e.g. back to switcher), snapshot flush integrates with app flow so unsaved edits are written where appropriate (`AppFlowController` / `AuthoringUIController` navigation paths).

### 5. Authoring UI layout (current)

Recent authoring chrome changes:

- **Left column removed** — former content-library column and toggle are gone; center viewport expands horizontally.
- **Add control** — circular **+** FAB (`AddContentFabButton`) anchored **lower-left** of the authoring root; background matches panel tone (`AuthoringUI.uss`); invokes the same **`WebGLFileBrowser`** browse pipeline as legacy **Add Content** (`AuthoringUIController.OnBrowseButtonClicked`).
- **Right panel** — inspector remains (Target / Content tabs, spatial fields, optional target reference). **Content Library** list block was removed from this panel; hierarchy selection is driven by scene/coordinator flows when no list is present (controller guards null `ListView`).
- **Workspace switcher** — **DELETE** uses destructive styling (`switcher-action-danger` in `WorkspaceSwitcherUI.uss`; fallback styling in `WorkspaceSwitcherController` runtime UI).

Related historical context for interaction and camera rules remains in [AuthoringUILayout.md](AuthoringUILayout.md), [PlacementInteractionAndTransformAuthoring.md](PlacementInteractionAndTransformAuthoring.md), and [RuntimeSceneCameraControl.md](RuntimeSceneCameraControl.md). That older layout doc still describes prior left-panel behavior; treat **this document** as the source of truth for **current** authoring shell + persistence.

---

## Modification

Paths are under `Assets/Scripts/` unless noted.

### 1. Persistence core — `Workspace/Persistence/`

| File | Description |
|------|-------------|
| **WorkspacePersistencePaths.cs** | Canonical folder/file layout under `persistentDataPath`. |
| **WorkspaceSnapshot.cs** | Snapshot DTO shape for JSON persistence. |
| **WorkspaceSnapshotRepository.cs** | Load/save `snapshot.json` per workspace. |
| **WorkspaceAssetRepository.cs** | Target/content asset folders and blob I/O. |
| **AuthoredTargetInstance.cs** / **AuthoredContentInstance.cs** | Instance markers for authored entities. |
| **AuthoredObjectRegistry.cs** | Runtime registry of authored ids for save/rebuild. |
| **WorkspaceStateSerializer.cs** | Maps registry/scene → snapshot. |
| **WorkspaceSceneReconstructor.cs** | Snapshot → scene instantiation path. |
| **WorkspaceAutoSaveService.cs** | Debounced persistence notifications. |
| **WorkspaceDeletion.cs** | Deletes workspace artifacts on disk + index maintenance. |
| **WorkspacePersistenceBootstrap.cs** | Early `AuthoredObjectRegistry` ensure for authoring scene. |
| **WorkspaceAuthoredAttach.cs** | Attach/registry helpers as used by authoring prefabs (see usages in tree). |

### 2. Models and tests — `Target/Models/`, `Assets/Tests/`

| File | Description |
|------|-------------|
| **Target/Models/Vector3Data.cs** | Serializable vector helper for snapshots/tests. |
| **Tests/EditMode/Vector3DataTests.cs** | Unit coverage for vector persistence helpers. |

### 3. Authoring entry and UI integration

| File | Description |
|------|-------------|
| **AuthoringWorkspaceEntry.cs** | Loads/reconstructs workspace snapshot on scene entry; coordinates with session context. |
| **AuthoringUIController.cs** | Browse/FAB wiring, workspace guard enablement, persistence notify hooks, optional list wiring when `ContentLibraryList` absent. |
| **ContentTransformController.cs** | Drag/end paths that notify autosave where applicable (see implementation). |

### 4. App flow and switcher — `Scripts/`

| File | Description |
|------|-------------|
| **WorkspaceSwitcherController.cs** | Disk index merge, navigation to authoring, fallback UI including DELETE styling. |
| **AppFlowController.cs** / **WorkspaceSessionContext.cs** | Session payload and transitions (flush before clear on back-navigation). |

### 5. UI assets — `Assets/UI/`

| File | Description |
|------|-------------|
| **UI/UXML/AuthoringUI.uxml** | Layout: top bar, center viewport, right inspector, FAB host. |
| **UI/USS/AuthoringUI.uss** | Panel tones, `.authoring-add-fab` circular add button. |
| **UI/UXML/WorkspaceSwitcherUI.uxml** | DELETE uses `.switcher-action-danger`. |
| **UI/USS/WorkspaceSwitcherUI.uss** | Danger button styles for DELETE. |

### 6. Third-party browse integration — `Assets/Plugins/`

| File | Description |
|------|-------------|
| **FrostweepGames/WebGLFileBrowser/Scripts/WebGLFileBrowser.cs** | File picker used by authoring browse/add flows on WebGL/editor targets. |

---

## Persistence Flow (high level)

1. Authoring scene boots; `WorkspacePersistenceBootstrap` ensures `AuthoredObjectRegistry`.
2. `AuthoringWorkspaceEntry` applies session context and loads snapshot via repositories when a persisted workspace is opened.
3. `WorkspaceSceneReconstructor` instantiates targets/content per snapshot and registry rules.
4. Edits update transforms/content; services call `WorkspaceAutoSaveService.NotifyWorkspaceChanged()` (debounced).
5. Serializer writes snapshot; asset repository stores new blobs as needed.
6. On workspace delete, `WorkspaceDeletion` removes directory + index entry.
7. On return to switcher, session flush/clear ordering avoids dropping the last debounced snapshot (see `AuthoringUIController` / app-flow call sites).

---

## Authoring UI Summary (current)

| Area | Behavior |
|------|----------|
| **Top bar** | Workspace name, mode pills, Return, Save. |
| **Center** | 3D viewport (`CenterViewport`). |
| **Lower-left** | Circular **+** → same upload/browse intent as former Add Content. |
| **Right** | Collapsible inspector (Target/Content); no embedded content-library list. |
| **Switcher DELETE** | Red/danger styling. |

---

## Validation

- Open an existing local workspace: targets/content reconstruct without duplicate registry ids where contracts hold.
- Edit transforms/content: debounced save writes under expected workspace folder.
- Delete workspace from switcher: folder and index row removed.
- Authoring **+** opens file browser path consistent with `OnBrowseButtonClicked` / `UploadPurpose.Content`.
- Inspector and scene selection still update spatial fields when list UI is absent.

---

## Known Gaps / Follow-ups

- **AuthoringUILayout.md** still narrates an older shell (left content library). Prefer **this document** for current layout + persistence until that file is revised or superseded.
- Remote/API workspace sync remains outside local snapshot scope; provider replaces mock when integrated.
- Optional: restore a lightweight hierarchy UI elsewhere if product wants list-driven selection again without restoring the old left column.

---

## Result

- Workspaces gain **repeatable local persistence** (snapshot + assets + index) suitable for reload and deletion.
- Authoring UI is **simplified**: no left library column, **FAB add** for imports, inspector-only right panel, and **clear destructive styling** for workspace delete.
- Controller code paths tolerate **missing `ContentLibraryList`** while preserving coordinator-driven selection and spatial editing.
