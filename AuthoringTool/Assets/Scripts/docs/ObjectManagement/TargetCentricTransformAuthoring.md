# Target-Centric Transform Authoring (Runtime Gizmo Pipeline)
## Assignment : DEV-177 - Target-local gizmo, front-side constraint, and target plane interaction

---

## Outlines

### 1. Reusable scene-level transform pipeline

Introduce a small, composable set of components for **content** manipulation (gizmo) and **target** manipulation (plane drag) that can be wired in a sandbox or full authoring scene without `ContentTransformController`.

- **Selection** — `ObjectSelectionManager` raycasts for content under `ContentRoot`, single selection, optional clear on empty click, skips when the RTG gizmo is hovered.
- **Gizmo** — `TransformGizmoController` owns multiple RTG gizmos (translate, rotate, scale, universal), keyboard modes **1–4**, target-local post rules, and `FrontSideConstraint` integration.
- **Target motion** — `TargetMovementController` drags `TargetRoot` on a **posture plane**; hits on `ContentRoot` descendants are ignored so gizmo/selection own the content.
- **Wiring** — `TransformInteractionCompositionRoot` (optional) assigns `TargetRoot`, `ContentRoot`, camera, `targetPlaneAnchor`, and calls `Configure` / `ConfigureDependencies` on the above.

### 2. Target-local transform and front-side rules

- **`TargetLocalTransformService`** — Helpers for local TRS and **uniform scale** bounds.
- **`FrontSideConstraint`** — Clamps content **local Z** relative to the target so objects stay in front of the image plane; configurable **front axis** (`PositiveLocalZ` / `NegativeLocalZ`), optional depenetration (off by default for thin / non-convex colliders such as a quad).

### 3. Posture plane for target drag

- Drag uses a **mathematical plane** at press time: normal from `TargetRoot` (or optional **`planeAnchor`**, e.g. `TargetPlane` quad) and point on that plane, then **projected** movement each frame.
- **Precedence** on press: any **RTG** drag or gizmo **hover** blocks starting a target drag; app `TransformGizmoController.IsManipulating` is also checked.

### 4. Camera input isolation

- **`RuntimeCameraController`** blocks WASD, scroll zoom, and (when applicable) look when:
  1. An RTG gizmo is **dragged** or **hovered**
  2. **`TargetMovementController.IsTargetDragActive`**
  3. Legacy **`DraggableObject`** interaction
  4. Left button held over a **draggable** collider
- **Script order** — `TargetMovementController` runs early (`DefaultExecutionOrder(-100)`) so the static drag flag is set before the camera; the camera runs late (`DefaultExecutionOrder(2000)`) so RTG state is current in the same frame.

### 5. RTG runtime bootstrap

- **`RTGRuntimeBootstrap`** creates the `RTGApp` module graph when either `ContentTransformController` **or** `TransformGizmoController` is present, so sandbox scenes do not need a manual RTG menu init.

### 6. Sandbox-first delivery

- Primary validation scene: **`TransformSandboxScene`** (`TransformSystems` + `TargetRoot` / `TargetPlane` / `ContentRoot` hierarchy).
- **AuthoringToolScene** remains on **`ContentTransformController`** until migrated; see companion notes in `../Authoring/Transform/TRANSFORM_SANDBOX_ACCEPTANCE_AND_AUTHORING_MIGRATION.md`.

---

## Modification

Paths are under `Assets/Scripts/` unless noted.

### 1. Transform authoring core — `Authoring/Transform/`

| File | Description |
|------|-------------|
| **ObjectSelectionManager.cs** | Raycast selection for content under `ContentRoot`; RTG hover gate; `Configure(Camera, ContentRoot)`. |
| **TransformGizmoController.cs** | Multi-mode RTG binding, selection sync, uniform scale option, `FrontSideConstraint` hooks, transform-changed events for future persistence. |
| **TargetLocalTransformService.cs** | Local TRS helpers and uniform scale clamp range. |
| **FrontSideConstraint.cs** | Parent-local Z clamp toward “front” side; axis and optional overlap handling. |
| **TargetMovementController.cs** | Target-root plane drag; `contentRoot` / `planeAnchor`; RTG precedence; static **`IsTargetDragActive`**; early execution order. |
| **TransformInteractionCompositionRoot.cs** | Optional composition root: resolves anchors and wires dependency injection for selection, gizmo, target movement. |

### 2. Camera and RTG integration

| File | Description |
|------|-------------|
| **Camera/RuntimeCameraController.cs** | Scene interaction blocking order (RTG → target drag → draggable rules); late execution order for consistent gizmo state. |
| **RTGRuntimeBootstrap.cs** | Extends bootstrap trigger to include `TransformGizmoController` (in addition to `ContentTransformController`). |

### 3. Documentation and acceptance

| File | Description |
|------|-------------|
| **Authoring/Transform/TRANSFORM_SANDBOX_ACCEPTANCE_AND_AUTHORING_MIGRATION.md** (`Scripts/` tree) | Static audit, manual Play Mode checklist for `TransformSandboxScene`, and `AuthoringToolScene` migration checklist. |

---

## Interaction Flow (high level)

1. Play Mode starts; **`RTGRuntimeBootstrap`** ensures **`RTGApp`** exists when **`TransformGizmoController`** is in the scene.
2. User clicks content → **`ObjectSelectionManager`** selects → **`TransformGizmoController`** attaches the active work gizmo to the selection.
3. User drags gizmo handles → RTG updates transforms → **`FrontSideConstraint`** / local rules apply → events may fire for UI or future draft sync.
4. User clicks **wall / target collider** outside content → **`TargetMovementController`** begins plane drag; **`IsTargetDragActive`** gates the camera.
5. User hovers or drags gizmo → **`RuntimeCameraController`** does not apply navigation zoom/move for that frame window per blocking rules.

---

## Result

- Authors can manipulate **content** with a **3D gizmo** (modes 1–4) and **move the whole target** on its **wall plane** without coupling to legacy `ContentTransformController` in the sandbox path.
- **Front-side** depth behavior is enforced in target-local space with an explicit axis choice for different target facing conventions.
- **Camera** navigation does not fight **gizmo** or **target-plane** manipulation; ordering and RTG queries are aligned for stable gating.
- The pipeline is **reusable** via Inspector wiring or **`TransformInteractionCompositionRoot`**.
- **Persistence** (`LocalWorkspaceStore` / draft TRS) and **full AuthoringToolScene** cutover are **follow-up work**; transform-changed hooks are exposed for later wiring.

---

## Related

- Posture and camera presets at workspace entry: `../Authoring/WorkspacePresetSystem.md` (DEV-129).
- Spawn and hierarchy contracts for content: `DynamicResourceInstantiationController.md` (DEV-96).
