# DEV-186 Authoring UI/Layout 

## Scope Completed

This document summarizes the implementation and side-task adjustments completed for DEV-186 in `AuthoringToolScene`.

---

## 1) Scene + Layout Foundation

- Authoring scene visual tone was moved to a clean dark editor-like style.
- UI shell was restructured into:
  - top bar
  - left content panel (collapsible)
  - center 3D viewport
  - right inspector panel (collapsible)
- Side toggles remain visible in collapsed state.
- `Return` action is present for navigating back to workspace switcher.

---

## 2) Left Panel (Content Library)

- Left side now behaves as a runtime content library rather than old target-creation form.
- Supports add/upload flow and content list selection.
- Selection syncs with scene transform selection.
- One-target/one-active-content behavior was preserved in runtime activation flow.
- Duplicate "saved" entry issue was addressed with one-time bootstrap + stricter stale cleanup logic.

---

## 3) Right Panel (Inspector)

- Removed old fields (type/media/youtube/text spawn) from right inspector scope.
- Kept spatial coordinates (`X/Y/Z`) for target/content contexts.
- Added explicit inspector tabs:
  - `Target`
  - `Content`
- Context behavior:
  - `Content`: edits selected content transform
  - `Target`: edits active target transform and shows target-reference section
- Added optional target-reference upload UI:
  - one reference per target
  - replacement supported
  - per-target preview/status in inspector
  - non-blocking local draft behavior (save-later)

---

## 4) Mode Indicator Visuals

- Top bar mode visualization now uses active-state pills (Move/Rotate/Scale/Universal).
- Active styling follows current gizmo mode.
- Text `Mode:` label was removed per request; only pills remain.
- In target inspector mode, mode pills are hidden.

---

## 5) Interaction Blocking / Input Conflict Pass

- `picking-mode` usage across UXML was aligned for UI-vs-scene separation.
- `ObjectSelectionManager` and `RuntimeCameraController` were hardened to reliably resolve/use `AuthoringUIController` for UI blocking.
- Scene selection is blocked when pointer is over UI.
- Camera interaction is blocked when pointer is over UI / active manipulation state.

---

## 6) Selection Feedback (TODO 6)

- Added textured-safe selection visualization in `AuthoringTransformCoordinator`.
- Replaced filled overlay behavior with edge-only bounds highlight to avoid obscuring content.
- Highlight follows mode context:
  - content mode: selected content
  - target mode: target visual area

---

## 7) Demo Workspace Side Task

Predefined mock/demo workspaces were aligned to 3 contexts:

1. `Target on Wall`
2. `Target on Floor`
3. `Target on Ceiling`

Changes were applied in workspace provider/switcher integration so switcher options reflect these demo contexts.

Target image uses default visual behavior (backend image flow deferred).

---

## 8) Spawn Position / Front-Side Adjustments

To address "spawn behind target" and overlap behavior, spawn direction and front-side rules were aligned:

- front-side convention normalized to `Negative Local Z` in `FrontSideConstraint`
- spawn placement logic uses front-side sign derived from constraint
- additional clearance margin retained to reduce overlap risk

Note: Any remaining edge cases should be validated against exact active target hierarchy and posture transform chain at runtime.

---

## Key Files Touched (High Impact)

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

## Current Status

- DEV-186 required layout/system tasks are implemented.
- Side demo workspace setup is in place.
- Spawn/front-side behavior has been iterated and aligned with current constraint direction conventions.
