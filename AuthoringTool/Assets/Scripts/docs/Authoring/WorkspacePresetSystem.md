# Workspace Preset System (DEV-129)
## Assignment : Authoring scene posture-driven preset initialization (MWP)

---

## Outlines

### 1. Posture-driven workspace initialization

The authoring scene now initializes from workspace posture rather than surface reconstruction.

- **Posture source** — `TargetDraftState.posture` in workspace draft data.
- **Supported postures** — `Wall`, `Floor`, `Ceiling`.
- **Deterministic mapping** — posture maps to target rotation and camera preset through a single preset library.

### 2. Target orientation preset behavior

Preset target orientation is applied to the active workspace target root:

- **Wall** -> `0,0,0` (vertical)
- **Floor** -> `90,0,0` on X (horizontal, facing up)
- **Ceiling** -> `-90,0,0` on X (horizontal, facing down)

This keeps authored content target-local while preserving existing `ContentRoot` parenting rules.

### 3. Camera preset behavior and reset support

Per-posture camera presets are defined as local offsets from the active target:

- initial local position offset
- look-at local offset
- additional tilt to avoid flat orthogonal framing

`RuntimeCameraController` now exposes apply/reset pose APIs and synchronizes internal yaw/pitch after external pose apply to prevent first-look snapping.

### 4. Minimal target-centric hierarchy compatibility

Existing authoring systems still depend on direct-child lookup contracts:

- `activeTarget.Find("ContentRoot")`
- `contentRoot.parent.Find("TargetVisual")`

`AuthoringWorkspaceEntry` now performs compatibility normalization before preset apply:

- recover or create direct `ContentRoot`
- recover/promote `TargetPlane` to `TargetVisual` or create `TargetVisual` when missing

This preserves spawn/selection/save stability while moving toward posture-based scene setup.

### 5. Optional orientation helper

A lightweight orientation helper is available and toggleable from `AuthoringWorkspaceEntry`:

- helper root under active target
- subtle X/Y/Z axis cubes
- colliders removed so helper does not interfere with interaction

---

## Modification

Paths are under `Assets/Scripts/` unless noted.

### 1. Workspace data and posture model

| File | Description |
|------|-------------|
| **Workspace/Models/WorkspaceDraftModels.cs** | Introduced `WorkspacePosture` enum and draft models (`WorkspaceDraftState`, `TargetDraftState`, `ContentDraftState`) used by authoring initialization. |
| **Workspace/Providers/IWorkspaceProvider.cs** | Added frontend-side workspace data abstraction (`GetWorkspace`, `SaveWorkspace`, `GetAvailableWorkspaces`). |
| **Workspace/Providers/MockWorkspaceProvider.cs** | Added stable mock workspaces with posture-specific targets (`Wall`, `Floor`, `Ceiling`) for backend-independent authoring flow. |
| **Workspace/Store/LocalWorkspaceStore.cs** | Added in-memory workspace cache preserving unsaved local draft state across workspace switching. |
| **Workspace/WorkspaceDataServices.cs** | Added static composition point for provider + local store (mock-first default, backend-replaceable later). |

### 2. Preset domain and library

| File | Description |
|------|-------------|
| **Workspace/Presets/WorkspacePresetModels.cs** | Added preset domain structs for target orientation, camera preset, interaction constraints, and aggregated `WorkspacePreset`. |
| **Workspace/Presets/WorkspacePresetLibrary.cs** | Added deterministic posture-to-preset mapping for target rotation and camera offsets/tilt. |

### 3. Camera integration updates

| File | Description |
|------|-------------|
| **Camera/RuntimeCameraController.cs** | Added `RuntimeCameraPose`, `ApplyPose(...)`, `TryResetToLastAppliedPose()`, `ResetToStartupPose()`, plus internal yaw/pitch sync after external pose apply. |

### 4. Authoring entry preset flow integration

| File | Description |
|------|-------------|
| **AuthoringWorkspaceEntry.cs** | Switched startup to workspace provider flow; resolved target and applied posture preset on all branches (existing, duplicate, created); applied target orientation and camera preset deterministically at scene entry. |

### 5. Orientation helper and compatibility guard

| File | Description |
|------|-------------|
| **Workspace/Presets/WorkspaceOrientationHelper.cs** | Added toggleable, non-intrusive orientation axis helper under target root. |
| **AuthoringWorkspaceEntry.cs** | Added helper toggle fields and helper application in preset flow; added hierarchy compatibility normalization for `ContentRoot` / `TargetVisual` contracts used by existing runtime systems. |

### 6. IDE project include maintenance

| File | Description |
|------|-------------|
| **../Assembly-CSharp.csproj** | Added workspace/preset script includes so IDE module resolution recognizes newly introduced namespaces and files. |

---

## Preset Application Flow

1. Authoring scene starts.
2. Load workspace draft through `WorkspaceDataServices` provider/store.
3. Resolve workspace target (`activate existing` or `create missing`).
4. Normalize hierarchy compatibility (`ContentRoot` / `TargetVisual`).
5. Read posture from draft target.
6. Apply preset target rotation.
7. Apply preset camera pose through `RuntimeCameraController`.
8. Apply optional orientation helper state.

---

## Result

- Workspace posture is now part of authoring initialization and directly drives target orientation and camera framing.
- Authoring scene startup is deterministic and repeatable across reloads for `Wall/Floor/Ceiling` posture cases.
- Camera preset apply/reset is now stable and does not break runtime look controls.
- Existing spawn/selection/save flows remain compatible through `TargetVisual`/`ContentRoot` contract preservation.
- Backend workspace endpoints remain out of scope; mock-first provider architecture keeps future `HttpWorkspaceProvider` integration low-impact.

---
