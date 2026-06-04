# Authoring UI / layout

## Documentation map

| Topic | Document |
|-------|----------|
| **Current authoring shell** (viewport, FAB, inspector, switcher styling) | **This file** — sections *Current authoring shell* and *Authoring UI summary*. |
| **Layer 2 (removed from runtime; historical)** | [LocalWorkspacePersistent-Layer2.md](../Storage/LocalWorkspacePersistent-Layer2.md) |
| **Remote sync + backend + DB (Layer 3)** | [SyncingToPersistentStorage-Layer3.md](../Storage/SyncingToPersistentStorage-Layer3.md) |
| Placement / camera / transform behavior | [PlacementInteractionAndTransformAuthoring.md](PlacementInteractionAndTransformAuthoring.md), [RuntimeSceneCameraControl.md](RuntimeSceneCameraControl.md) |

---

## Scope completed (DEV-186 baseline)

This document originated as the DEV-186 implementation summary for `AuthoringToolScene`. The **current** authoring chrome is described in **Current authoring shell** below; earlier sections retain **historical** detail where still useful (left panel era, DEV-186 tasks).

---

## Current authoring shell (post–Layer 2 refresh)

Recent authoring chrome changes (source of truth for layout):

- **Left column removed** — former content-library column and toggle are gone; the center viewport expands horizontally.
- **Add control** — circular **+** FAB (`AddContentFabButton`) anchored **lower-left** of the authoring root; background matches panel tone (`AuthoringUI.uss`); invokes the same **`WebGLFileBrowser`** browse pipeline as legacy **Add Content** (`AuthoringUIController.OnBrowseButtonClicked`).
- **Right panel** — inspector remains (Target / Content tabs, spatial fields, optional target reference). **Content Library** list block was removed from this panel; hierarchy selection is driven by scene/coordinator flows when no list is present (controller guards null `ListView`).
- **Workspace switcher** — **DELETE** uses destructive styling (`switcher-action-danger` in `WorkspaceSwitcherUI.uss`; fallback styling in `WorkspaceSwitcherController` runtime UI). Optional **`backendApiBaseUrl`** triggers server **`DELETE /api/workspaces/{id}`** (see Layer 3 doc).

---

## Authoring UI summary (current)

| Area | Behavior |
|------|----------|
| **Top bar** | Workspace name, mode pills, Return, Save. |
| **Center** | 3D viewport (`CenterViewport`). |
| **Lower-left** | Circular **+** → same upload/browse intent as former Add Content. |
| **Right** | Collapsible inspector (Target/Content); no embedded content-library list. |
| **Switcher DELETE** | Red/danger styling; optional API delete before disk delete. |

### UI assets — `Assets/UI/`

| Asset | Role |
|-------|------|
| **UI/UXML/AuthoringUI.uxml** | Layout: top bar, center viewport, right inspector, FAB host. |
| **UI/USS/AuthoringUI.uss** | Panel tones, `.authoring-add-fab` circular add button. |
| **UI/UXML/WorkspaceSwitcherUI.uxml** | DELETE uses `.switcher-action-danger`. |
| **UI/USS/WorkspaceSwitcherUI.uss** | Danger button styles for DELETE. |

---

## Validation (UI shell)

- Authoring **+** opens file browser path consistent with `OnBrowseButtonClicked` / `UploadPurpose.Content`.
- Inspector and scene selection still update spatial fields when list UI is absent.
- Switcher DELETE presents danger styling and completes local delete (and server delete when configured).

---

## Known gaps / follow-ups

- Optional: restore a lightweight hierarchy UI elsewhere if product wants list-driven selection again without restoring the old left column.

---

## Historical: DEV-186 scene + layout foundation

- Authoring scene visual tone was moved to a clean dark editor-like style.
- UI shell **at the time** was restructured into:
  - top bar
  - left content panel (collapsible) — **since removed**; see *Current authoring shell*.
  - center 3D viewport
  - right inspector panel (collapsible)
- Side toggles remained visible in collapsed state.
- `Return` action navigates back to workspace switcher.

---

## Historical: left panel (content library)

> **Superseded:** the dedicated left content-library column was removed in the Layer 2 UI refresh. Selection is coordinator-driven without an embedded list.

Previously:

- Left side behaved as a runtime content library rather than old target-creation form.
- Supported add/upload flow and content list selection.
- Selection synced with scene transform selection.
- One-target/one-active-content behavior was preserved in runtime activation flow.
- Duplicate "saved" entry issue was addressed with one-time bootstrap + stricter stale cleanup logic.

---

## Historical: right panel (inspector)

- Removed old fields (type/media/youtube/text spawn) from right inspector scope.
- Kept spatial coordinates (`X/Y/Z`) for target/content contexts.
- Explicit inspector tabs:
  - `Target`
  - `Content`
- Context behavior:
  - `Content`: edits selected content transform
  - `Target`: edits active target transform and shows target-reference section
- Optional target-reference upload UI:
  - one reference per target
  - replacement supported
  - per-target preview/status in inspector
  - non-blocking local draft behavior (save-later)

---

## Historical: mode indicator visuals

- Top bar mode visualization uses active-state pills (Move/Rotate/Scale/Universal).
- Active styling follows current gizmo mode.
- Text `Mode:` label was removed per request; only pills remain.
- In target inspector mode, mode pills are hidden.

---

## Historical: interaction blocking / input conflict pass

- `picking-mode` usage across UXML was aligned for UI-vs-scene separation.
- `ObjectSelectionManager` and `RuntimeCameraController` were hardened to reliably resolve/use `AuthoringUIController` for UI blocking.
- Scene selection is blocked when pointer is over UI.
- Camera interaction is blocked when pointer is over UI / active manipulation state.

---

## Historical: selection feedback (TODO 6)

- Added textured-safe selection visualization in `AuthoringTransformCoordinator`.
- Replaced filled overlay behavior with edge-only bounds highlight to avoid obscuring content.
- Highlight follows mode context:
  - content mode: selected content
  - target mode: target visual area

---

## Historical: demo workspace side task

Predefined mock/demo workspaces were aligned to 3 contexts:

1. `Target on Wall`
2. `Target on Floor`
3. `Target on Ceiling`

Changes were applied in workspace provider/switcher integration so switcher options reflect these demo contexts.

Target image uses default visual behavior (backend image flow deferred).

---

## Historical: spawn position / front-side adjustments

To address "spawn behind target" and overlap behavior, spawn direction and front-side rules were aligned:

- front-side convention normalized to `Negative Local Z` in `FrontSideConstraint`
- spawn placement logic uses front-side sign derived from constraint
- additional clearance margin retained to reduce overlap risk

Note: Any remaining edge cases should be validated against exact active target hierarchy and posture transform chain at runtime.

---

## Key files touched (high impact)

- `Assets/UI/UXML/AuthoringUI.uxml`
- `Assets/UI/USS/AuthoringUI.uss`
- `Assets/Scripts/AuthoringUIController.cs`
- `Assets/Scripts/Authoring/Transform/AuthoringTransformCoordinator.cs`
- `Assets/Scripts/Authoring/Transform/ObjectSelectionManager.cs`
- `Assets/Scripts/Authoring/Transform/TargetMovementController.cs`
- `Assets/Scripts/Authoring/Transform/FrontSideConstraint.cs`
- `Assets/Scripts/Camera/RuntimeCameraController.cs`
- `Assets/Scripts/WorkspaceSwitcherController.cs`
- `Assets/Scripts/Workspace/Providers/MockWorkspaceProvider.cs`
- `Assets/Scripts/Workspace/Presets/WorkspacePresetLibrary.cs`
- `Assets/Scripts/Spawn/SpawnerManager.cs`
- `Assets/Scripts/AuthoringWorkspaceEntry.cs`

---

## Current status

- **Layout:** Current authoring shell is summarized in *Current authoring shell* and *Authoring UI summary*; persistence and sync are split into **Storage** docs (Layers 2–3).
- **DEV-186:** Required layout/system tasks from the original milestone are implemented; historical sections document pre-refresh behavior where it differs.
- **Demo workspaces:** Side demo workspace setup remains in place.
- **Spawn/front-side:** Iterated and aligned with current constraint direction conventions.
