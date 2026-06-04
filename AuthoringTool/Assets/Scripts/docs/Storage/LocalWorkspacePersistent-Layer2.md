# Local workspace persistence (Layer 2, removed from runtime)

**Layer 2** previously stored durable local state under `Application.persistentDataPath` (`snapshot.json`, `workspace-index.json`, copied assets).

**Current authoring runtime (WebGL / Editor):** Layer 2 **disk writes and reads are disabled**. Authoring uses:

- **In-memory** payloads on `AuthoredTargetInstance` / `AuthoredContentInstance` (`TargetImageBytes`, `TargetReferenceBytes`, `AssetBytes`)
- **Public URLs** after upload or backend restore (`TargetImageUrl`, `MediaUrl`, …)
- **Layer 3** — [SyncingToPersistentStorage-Layer3.md](SyncingToPersistentStorage-Layer3.md) (backend + `uploads/`)

Legacy types (`WorkspaceSnapshotRepository`, `WorkspaceAssetRepository`, path fields on DTOs) remain in the tree for reference and optional cleanup of old folders on workspace delete; they are **not** used on the hot path.

For **authoring chrome** (FAB, inspector, viewport), see **[AuthoringUILayout.md](../Authoring/AuthoringUILayout.md)**.

---

## Historical reference (pre-removal)

The sections below describe the original L2 design. Do not assume this behavior in current builds.

### 1. Local workspace snapshot model

- **Snapshot DTO** — `WorkspaceSnapshot` aggregates workspace identity, target/content instances, and transform metadata suitable for round-trip rebuild.
- **Vector serialization** — `Vector3Data` (and related helpers) keep snapshot math stable and test-friendly independent of UnityEngine types in persistence layers.
- **Authored identity** — `AuthoredTargetInstance` / `AuthoredContentInstance` tag runtime objects so reconstruction and autosave can correlate scene instances with snapshot rows.

### 2. Disk layout and repositories (legacy)

- **Paths** — centralized in `WorkspacePersistencePaths` (`workspace-index.json`, per-workspace `snapshot.json`, `assets/targets/`, `assets/contents/`).
- **Snapshot I/O** — `WorkspaceSnapshotRepository` reads/writes snapshot files per workspace id.
- **Asset blobs** — `WorkspaceAssetRepository` stores imported/uploaded bytes on disk and exposes paths for reload.
- **Registry** — `AuthoredObjectRegistry` tracks active authored instances for serialization and reconstruction orchestration.

### 3. Scene reconstruction and debounced autosave (legacy)

- **Reconstruction** — `WorkspaceSceneReconstructor.BeginRebuildFromDisk` (disabled); production path is backend GET → `BeginRebuild(snapshot)`.
- **Serialization** — `WorkspaceStateSerializer` maps registry + scene state into snapshot DTOs (local path fields now emitted empty).
- **Autosave** — `WorkspaceAutoSaveService` now debounces **remote sync** only (see Layer 3 doc).

### 4. Workspace lifecycle (switcher, delete, session handoff)

- **Switcher** — lists workspaces from **backend API** (`WorkspaceSwitcherController`).
- **Deletion** — `DELETE /api/workspaces/<id>` from switcher; optional `WorkspaceDeletion` may remove legacy on-disk folders.
- **Session** — `AppFlowController` / `WorkspaceSessionContext`; back navigation syncs to server then clears session (no snapshot flush).

---

## Related code (still present, not hot path)

| File | Notes |
|------|--------|
| `WorkspacePersistencePaths.cs` | Path helpers |
| `WorkspaceSnapshotRepository.cs` | Disk snapshot I/O (unused on hot path) |
| `WorkspaceAssetRepository.cs` | Disk asset copy (unused on hot path) |
| `WorkspaceDeletion.cs` | May delete legacy workspace folders |
| `WorkspaceSceneReconstructor.cs` | `BeginRebuild(snapshot)` from API; `BeginRebuildFromDisk` obsolete |
