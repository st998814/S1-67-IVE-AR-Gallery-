# Local workspace persistence (Layer 2, removed from runtime)

**Layer 2** previously stored durable local state under `Application.persistentDataPath` (`snapshot.json`, `workspace-index.json`, copied assets).

**Current authoring runtime (WebGL / Editor):** Layer 2 **disk writes and reads are removed**. Authoring uses:

- **In-memory** payloads on `AuthoredTargetInstance` / `AuthoredContentInstance` (`TargetImageBytes`, `TargetReferenceBytes`, `AssetBytes`)
- **Public URLs** after upload or backend restore (`TargetImageUrl`, `MediaUrl`, …)
- **Layer 3** — [SyncingToPersistentStorage-Layer3.md](SyncingToPersistentStorage-Layer3.md) (backend + `uploads/`)

Legacy disk repositories (`WorkspaceSnapshotRepository`, `WorkspaceAssetRepository`, `WorkspacePersistencePaths`) and `BeginRebuildFromDisk` were removed in Phase C legacy cleanup. DTO fields such as `assetLocalPath` remain on snapshot types for API compatibility but are not populated from local disk.

For **authoring chrome** (FAB, inspector, viewport), see **[AuthoringUILayout.md](../Authoring/AuthoringUILayout.md)**.

---

## Historical reference (pre-removal)

The sections below describe the original L2 design. Do not assume this behavior in current builds.

### 1. Local workspace snapshot model

- **Snapshot DTO** — `WorkspaceSnapshot` aggregates workspace identity, target/content instances, and transform metadata suitable for round-trip rebuild.
- **Vector serialization** — `Vector3Data` (and related helpers) keep snapshot math stable and test-friendly independent of UnityEngine types in persistence layers.
- **Authored identity** — `AuthoredTargetInstance` / `AuthoredContentInstance` tag runtime objects so reconstruction and autosave can correlate scene instances with snapshot rows.

### 2. Disk layout and repositories (legacy, removed)

- **Paths** — were centralized in `WorkspacePersistencePaths` (`workspace-index.json`, per-workspace `snapshot.json`, `assets/targets/`, `assets/contents/`).
- **Snapshot I/O** — `WorkspaceSnapshotRepository` read/wrote snapshot files per workspace id.
- **Asset blobs** — `WorkspaceAssetRepository` stored imported/uploaded bytes on disk and exposed paths for reload.
- **Registry** — `AuthoredObjectRegistry` tracks active authored instances for serialization and reconstruction orchestration.

### 3. Scene reconstruction and debounced autosave (legacy)

- **Reconstruction** — production path is backend GET → `WorkspaceSceneReconstructor.BeginRebuild(snapshot)`.
- **Serialization** — `WorkspaceStateSerializer` maps registry + scene state into snapshot DTOs (local path fields now emitted empty).
- **Autosave** — `WorkspaceAutoSaveService` now debounces **remote sync** only (see Layer 3 doc).

### 4. Workspace lifecycle (switcher, delete, session handoff)

- **Switcher** — lists workspaces from **backend API** (`WorkspaceSwitcherController`).
- **Deletion** — `DELETE /api/workspaces/<id>` from switcher; `WorkspaceDeletion.TryDeleteWorkspaceEverywhere` clears in-memory draft cache and session only.
- **Session** — `AppFlowController` / `WorkspaceSessionContext`; back navigation syncs to server then clears session (no snapshot flush).

---

## Related code (still present)

| File | Notes |
|------|--------|
| `WorkspaceDeletion.cs` | Clears in-memory draft cache, mock seed hide list, and active session |
| `WorkspaceSceneReconstructor.cs` | `BeginRebuild(snapshot)` from backend API |
| `WorkspaceSnapshot.cs` | In-memory DTOs for sync and restore |
| `LocalWorkspaceStore.cs` | Session-scoped draft cache across switcher visits |
