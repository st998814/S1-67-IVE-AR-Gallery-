# Semantic Transform Inspector and Placement Bounds
## Assignment : DEV-177 — AuthoringTool migration: semantic move/scale sliders, placement bounds, gizmo gating

---

## Documentation map

| Topic | Document |
|-------|----------|
| **Prerequisite** — interaction ownership, target vs content drag, scroll-scale split | [PlacementInteractionAndTransformAuthoring.md](PlacementInteractionAndTransformAuthoring.md) |
| **Sandbox pipeline** — RTG gizmo, target plane drag, front-side constraint | [../ObjectManagement/TargetCentricTransformAuthoring.md](../ObjectManagement/TargetCentricTransformAuthoring.md) |
| **Authoring UI shell** — top bar mode pills, inspector tabs, FAB | [AuthoringUILayout.md](AuthoringUILayout.md) |
| **Manual acceptance** — `TransformSandboxScene` + `AuthoringToolScene` checklist | [../Authoring/Transform/TRANSFORM_SANDBOX_ACCEPTANCE_AND_AUTHORING_MIGRATION.md](../Authoring/Transform/TRANSFORM_SANDBOX_ACCEPTANCE_AND_AUTHORING_MIGRATION.md) |

This document records the **AuthoringToolScene** refinement that replaces translate/scale gizmo handles with **semantic inspector sliders**, adds **TargetVisual-derived placement bounds**, and hardens **camera vs content transform** separation. It does not replace [PlacementInteractionAndTransformAuthoring.md](PlacementInteractionAndTransformAuthoring.md); that file remains the baseline for drag ownership and hierarchy rules.

---

## Outlines

### 1. Single write path for content transforms

All authoritative content **local** TRS updates in the authoring stack go through **`ContentTransformManipulator`**:

- **Semantic move** — one axis at a time via `PlacementBoundsCalculator.SemanticAxis` (Left/Right → local X, Up/Down → local Y, Closer/Further → local Z).
- **Uniform scale** — `TargetLocalTransformService` clamp range.
- **Gizmo rotate** — post-processed in `ApplyGizmoResult` after RTG drag (translate/scale gizmos are not shown; see §3).
- **Events** — `ContentTransformChanged` / `ContentTransformChangedDetailed` for UI sync, draft dirty flags, and workspace autosave hooks via **`AuthoringTransformCoordinator`**.

On **`AuthoringToolScene`**, **`AuthoringTransformCoordinator`** owns selection, gizmo, and **`ContentTransformManipulator`** as the single write path.

### 2. Placement bounds (TargetVisual XY + front-side Z)

**`PlacementBoundsService`** resolves limits per content instance:

- **XY (wall)** — absolute target-relative safe zone: **±0.75 m** left/right, **±0.50 m** up/down (anchor at **`TargetVisual`**, not card size). Floor/ceiling presets still scale from **`TargetVisual`**.
- **Z (wall)** — **0.05 m–1.00 m** in front of target plane (negative local Z: **-1.00** to **-0.05**). Uses posture **`minStandoffZ`** / **`depthMeters`**; aligns with **`FrontSideConstraint`** standoff.
- **Pure math** — **`PlacementBoundsCalculator`** in assembly **`ARGallery.Authoring.Core`** for EditMode tests without referencing `Assembly-CSharp`.

Sliders and manipulator calls clamp through the same service so UI and runtime rules stay aligned.

### 3. Gizmo modes vs scene handles

**`TransformGizmoController`** mode behavior in AuthoringTool:

| Mode | Scene handles | Authoring UI |
|------|----------------|--------------|
| **Move** | None (translate gizmo disabled) | Bottom panel: three semantic sliders |
| **Rotate** | RTG rotation gizmo | Bottom panel hidden |
| **Scale** | None (scale gizmo disabled) | Bottom panel: uniform scale slider |
| **Universal** | Hidden in UI; not offered in mode pills | — |

Keyboard shortcuts **1–3** still map to Move / Rotate / Scale when the content inspector is active.

### 4. Bottom-center manipulator panel (UI Toolkit)

A **non-collapsible** panel (`ManipulatorBottomPanel` in **`AuthoringUI.uxml`**) is anchored at the **bottom center** of the viewport:

- **Visible** only when the **Content** inspector tab is active, content is selected, and mode is **Move** or **Scale**.
- **Hidden** for **Rotate**, **Target** inspector, or empty selection.
- **`AuthoringUIManipulatorPanel`** binds sliders to **`ContentTransformManipulator`** and refreshes ranges from **`PlacementBoundsService`** / **`TargetLocalTransformService`**.
- Right inspector **Local X / Y / Z** and **Scale** fields are **read-only** for content during this flow; live values update while sliders move.

Mode pills in the top bar call **`SetManipulatorMode`** on **`AuthoringUIController`**, which forwards to **`TransformGizmoController.SetMode`**.

### 5. Camera input vs content coordinates

**`RuntimeCameraController`** WASD / scroll / look remain **camera-only**. **`AuthoringTransformCoordinator`** no longer applies keyboard nudges to content transforms (removed `enableKeyboardNudges` / WASD position paths).

Pressing movement keys while navigating must **not** change content **`localPosition`**.

### 6. No LMB drag for content position

Content shells under **`ContentRoot`** call **`DraggableObject.ConfigureForContentShell`**, which sets **`allowPositionDrag = false`**. LMB screen drag no longer moves content; **Move** uses bottom sliders only. **Target** **`TargetVisual`** keeps position drag (`ConfigurePositionDrag(true)`, `moveParentOnDrag`) and **`TargetMovementController`** plane drag.

### 7. Target inspector and orientation helper

- **Target** tab — **`TargetMovementController`** plane drag moves **`TargetRoot`**; content gizmo selection is cleared; mode pills and bottom panel are hidden.
- **Selection highlight** — pulsing edge bounds from **`AuthoringTransformCoordinator`**: content selection in Content mode, **`TargetVisual`** in Target mode.
- **Orientation axes** — **`WorkspaceOrientationHelper`** RGB triad under **`TargetRoot`** is **disabled by default** (`showOrientationHelper = false` on **`AuthoringWorkspaceEntry`**; coordinator also hides existing helpers on target context change). This removes the legacy “controller indicator” on the target.

---

## Modification

Paths are under `Assets/` unless noted.

### 1. Transform core — `Scripts/Authoring/Transform/`

| File | Description |
|------|-------------|
| **ContentTransformManipulator.cs** | Single write path: `SetSemanticAxis`, `SetUniformScale`, `ApplyGizmoResult`; fires transform-changed events. |
| **PlacementBoundsService.cs** | MonoBehaviour: `TryGetBoundsForContent`, `ClampLocalPosition`, `SetSemanticAxis`, `GetAxisRange`; wires `FrontSideConstraint` + `TargetVisual`. |
| **TransformGizmoController.cs** | `ResolveGizmoForMode` returns no gizmo for Translate/Scale; Rotate gizmo only; `HasActiveSceneGizmo` helper. |
| **AuthoringTransformCoordinator.cs** | Wires manipulator + bounds; selection highlight; syncs UI on gizmo changes; disables orientation helper on target context; applies `ConfigureForContentShell` on content list refresh. |
| **TransformInteractionCompositionRoot.cs** | Bootstraps `PlacementBoundsService` / `ContentTransformManipulator` when missing. |
| **TargetMovementController.cs** | Unchanged contract; target-plane drag when Target inspector active (see prerequisite doc). |

### 2. Testable bounds math — `Scripts/Authoring/Core/`

| File | Description |
|------|-------------|
| **PlacementBoundsCalculator.cs** | Static bounds snapshot, semantic axis enum, clamp/set-component helpers. |
| **ARGallery.Authoring.Core.asmdef** | `autoReferenced: true` assembly for EditMode tests. |

### 3. EditMode tests — `Tests/EditMode/`

| File | Description |
|------|-------------|
| **PlacementBoundsServiceTests.cs** | Pure calculator tests (XY from TargetVisual scale, Z negative-front, semantic axis helpers). |
| **ARGallery.Tests.EditMode.asmdef** | References `ARGallery.Authoring.Core` only (no `Assembly-CSharp` reference). |

### 4. Authoring UI — `Scripts/` + `UI/`

| File | Description |
|------|-------------|
| **AuthoringUIManipulatorPanel.cs** | Bottom panel bind/visibility, slider callbacks, `Focusable` focus check for spatial-field gating. |
| **AuthoringUIController.cs** | `BindManipulatorBottomPanel`, mode pill → gizmo mode, read-only inspector sync, manipulator refresh hooks. |
| **UI/UXML/AuthoringUI.uxml** | `ManipulatorBottomPanel`, `MoveControlsGroup`, `ScaleControlsGroup`, slider + value label rows. |
| **UI/USS/AuthoringUI.uss** | `.manipulator-bottom-panel` bottom-center layout and row styling. |

### 5. Workspace / scene — `Scripts/` + `Scenes/`

| File | Description |
|------|-------------|
| **AuthoringWorkspaceEntry.cs** | Default `showOrientationHelper = false`. |
| **Scenes/AuthoringToolScene.unity** | `TransformGizmoController` + coordinator wiring; orientation helper off in scene entry. |

### 6. Drag policy — `Scripts/`

| File | Description |
|------|-------------|
| **DraggableObject.cs** | `allowPositionDrag`, `ConfigurePositionDrag`, `ConfigureForContentShell` (content LMB move off). |
| **Content/Managers/RuntimeContentFactory.cs** | Applies content drag policy on pool acquire and factory create. |
| **Target/Managers/RuntimeImageTargetFactory.cs** | Target visual keeps `ConfigurePositionDrag(true)`. |

### 7. Camera — `Scripts/Camera/`

| File | Description |
|------|-------------|
| **RuntimeCameraController.cs** | Documented camera-only WASD; blocks when RTG / target drag / UI / draggable rules apply (unchanged gating model). |

---

## Interaction flow (AuthoringToolScene)

1. User selects **Content** tab and a child under **`ContentRoot`** → **`ObjectSelectionManager`** selection → coordinator highlight + inspector sync.
2. User chooses **Move** → bottom panel shows three sliders; dragging updates **`ContentTransformManipulator.SetSemanticAxis`** → bounds clamp + **`FrontSideConstraint`** → inspector XYZ labels refresh. LMB drag on content does not move it.
3. User chooses **Scale** → bottom panel shows uniform scale slider → **`SetUniformScale`** with service min/max.
4. User chooses **Rotate** → bottom panel hides → RTG rotation gizmo on selection; release applies **`ApplyGizmoResult`** (Rotate).
5. User chooses **Target** tab → content selection cleared, gizmo modes hidden, target plane drag available; orientation helper stays off.
6. User navigates with **WASD** → camera moves only; content **`localPosition`** unchanged.

---

## Validation (quick)

- **Move** — sliders appear bottom-center; content stays inside TargetVisual XY and front-side Z; inspector XYZ updates live; LMB drag does not reposition content.
- **Scale** — single slider; uniform scale only; no scale gizmo handles.
- **Rotate** — rotation gizmo only; no bottom panel.
- **Camera** — WASD does not nudge selected content local position.
- **Target** — no RGB orientation triad on **`TargetRoot`** after workspace load / target switch.

Full scene checklists: [TRANSFORM_SANDBOX_ACCEPTANCE_AND_AUTHORING_MIGRATION.md](../Authoring/Transform/TRANSFORM_SANDBOX_ACCEPTANCE_AND_AUTHORING_MIGRATION.md).

---

## Result

- **Move** and **Scale** authoring use **semantic bottom sliders** instead of translate/scale gizmo handles; **Rotate** keeps the RTG gizmo.
- Content placement is **clamped** to **TargetVisual** footprint and **front-side Z** through one manipulator + bounds service.
- **Camera WASD** is decoupled from content transform writes on the authoring coordinator path.
- **Target** orientation RGB helper is off by default and suppressed on target context changes.
- Bounds math is **unit-testable** via **`ARGallery.Authoring.Core`** without coupling EditMode tests to `Assembly-CSharp`.

---

## Related

- Interaction ownership and drag/scale binding baseline: [PlacementInteractionAndTransformAuthoring.md](PlacementInteractionAndTransformAuthoring.md).
- Original DEV-177 sandbox gizmo pipeline: [../ObjectManagement/TargetCentricTransformAuthoring.md](../ObjectManagement/TargetCentricTransformAuthoring.md).
- Workspace posture presets: [WorkspacePresetSystem.md](WorkspacePresetSystem.md) (DEV-129).
