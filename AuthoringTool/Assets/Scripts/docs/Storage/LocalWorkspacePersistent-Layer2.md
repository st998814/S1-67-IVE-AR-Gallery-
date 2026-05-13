# Local workspace persistence (Layer 2)

**Layer 2** is durable **local** workspace state under `Application.persistentDataPath`: JSON snapshots, binary assets, workspace index, debounced autosave, and disk deletion (including **`snapshot.json`**). It does **not** include remote/API sync—that is **[SyncingToPersistentStorage-Layer3.md](SyncingToPersistentStorage-Layer3.md)** (Layer 3).

For **authoring chrome** (FAB, inspector, viewport), see **[AuthoringUILayout.md](../Authoring/AuthoringUILayout.md)**.

---

## Assignment (MWP context)

Durable local workspace state suitable for round-trip reload; authoring UI shell iteration lives in the Authoring UI doc above.

---

## Outlines

### 1. Local workspace snapshot model

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
- **Deletion (local)** — `WorkspaceDeletion.TryDeleteWorkspaceEverywhere` removes the workspace folder (**including `snapshot.json`**), updates the index, clears draft cache, and clears session when it matches. **Backend cascade** is Layer 3 / API (see Layer 3 doc and `DELETE /api/workspaces/<id>` before local delete when the switcher is configured with **`backendApiBaseUrl`**).
- **Session flush** — before leaving authoring (e.g. back to switcher), snapshot flush integrates with app flow so unsaved edits are written where appropriate (`AppFlowController` / `AuthoringUIController` navigation paths).

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
| **WorkspaceDeletion.cs** | Local workspace artifacts + index; backend delete orchestrated from switcher before this when API is configured. |
| **WorkspacePersistenceBootstrap.cs** | Early `AuthoredObjectRegistry` ensure for authoring scene. |
| **WorkspaceAuthoredAttach.cs** | Attach/registry helpers as used by authoring prefabs (see usages in tree). |

### 2. Models and tests — `Target/Models/`, `Assets/Tests/`

| File | Description |
|------|-------------|
| **Target/Models/Vector3Data.cs** | Serializable vector helper for snapshots/tests. |
| **Tests/EditMode/Vector3DataTests.cs** | Unit coverage for vector persistence helpers. |

### 3. Authoring entry (persistence hooks)

| File | Description |
|------|-------------|
| **AuthoringWorkspaceEntry.cs** | Loads/reconstructs workspace snapshot on scene entry; coordinates with session context. |
| **AuthoringUIController.cs** | Persistence notify hooks, workspace guards; UI layout detail in **AuthoringUILayout.md**. |
| **ContentTransformController.cs** | Drag/end paths that notify autosave where applicable (see implementation). |

### 4. App flow and switcher — `Scripts/`

| File | Description |
|------|-------------|
| **WorkspaceSwitcherController.cs** | Disk index merge, navigation to authoring; optional backend workspace delete before local delete (`backendApiBaseUrl`). UI styling for DELETE in Authoring UI doc. |
| **AppFlowController.cs** / **WorkspaceSessionContext.cs** | Session payload and transitions (flush before clear on back-navigation). |

### 5. Third-party browse integration — `Assets/Plugins/`

| File | Description |
|------|-------------|
| **FrostweepGames/WebGLFileBrowser/Scripts/WebGLFileBrowser.cs** | File picker used by authoring browse/add flows on WebGL/editor targets. |

---

## Persistence flow (high level)

1. Authoring scene boots; `WorkspacePersistenceBootstrap` ensures `AuthoredObjectRegistry`.
2. `AuthoringWorkspaceEntry` applies session context and loads snapshot via repositories when a persisted workspace is opened.
3. `WorkspaceSceneReconstructor` instantiates targets/content per snapshot and registry rules.
4. Edits update transforms/content; services call `WorkspaceAutoSaveService.NotifyWorkspaceChanged()` (debounced).
5. Serializer writes snapshot; asset repository stores new blobs as needed.
6. On workspace delete (local path), `WorkspaceDeletion` removes directory + index entry after optional API delete (switcher).
7. On return to switcher, session flush/clear ordering avoids dropping the last debounced snapshot (see `AuthoringUIController` / app-flow call sites).

---

## Validation (Layer 2)

- Open an existing local workspace: targets/content reconstruct without duplicate registry ids where contracts hold.
- Edit transforms/content: debounced save writes under expected workspace folder.
- Delete workspace from switcher: folder and index row removed; with backend configured, server rows cleaned first.
- Inspector and scene selection still update spatial fields when list UI is absent (see Authoring UI doc).

---

## Known gaps / follow-ups

- Remote/API workspace sync is **Layer 3** — see **SyncingToPersistentStorage-Layer3.md**.
- Optional: restore a lightweight hierarchy UI elsewhere if product wants list-driven selection again without restoring the old left column.

---

## Result

- Workspaces gain **repeatable local persistence** (snapshot + assets + index) suitable for reload and deletion.
- Controller code paths tolerate **missing `ContentLibraryList`** while preserving coordinator-driven selection and spatial editing.
