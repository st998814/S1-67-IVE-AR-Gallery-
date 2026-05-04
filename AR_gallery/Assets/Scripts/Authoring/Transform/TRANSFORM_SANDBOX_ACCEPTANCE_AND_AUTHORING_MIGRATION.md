# TransformSandboxScene acceptance — AuthoringToolScene migration

This document records **static verification** of `TransformSandboxScene` for the target-centric gizmo pipeline (DEV‑177 style), **manual acceptance tests** to run in the Unity Editor, and a **migration checklist** for `AuthoringToolScene`.

---

## Part A — Static verification (`TransformSandboxScene`)

Executed against scene YAML + scripts (no Play Mode).

| Check | Result | Notes |
|--------|--------|--------|
| `RuntimeCameraController` on Main Camera | **PASS** | Present; input gating uses RTG + `TargetMovementController.IsTargetDragActive`. |
| RTG bootstrap | **PASS** | `RTGRuntimeBootstrap` creates `RTGApp` when `TransformGizmoController` exists (no scene asset reference required). |
| `TransformSystems` holds core pipeline | **PASS** | `ObjectSelectionManager`, `TransformGizmoController`, `TargetLocalTransformService`, `FrontSideConstraint`; refs wired among themselves. |
| `ObjectSelectionManager` | **PASS** | `raycastCamera` → Main Camera; `contentRoot` → `ContentRoot`. |
| `TargetMovementController` on `TargetRoot` | **PASS** (after fix) | **`contentRoot`** and **`planeAnchor`** assigned to `ContentRoot` and **`TargetPlane`** so content clicks are excluded from target-plane drag and the drag plane matches the quad. Previously `contentRoot` was unset in YAML (runtime `Find` partially masked this). |
| Hierarchy | **PASS** | `WorkspaceRoot` → `TargetRoot` → `TargetPlane` → `ContentRoot` → `Cube`; quad `MeshCollider` on `TargetPlane`. |
| `TransformInteractionCompositionRoot` | **N/A** | Not used in sandbox; references are set directly on components (optional: add for parity with authoring). |

**Outstanding static gaps (non-blocking if Awake Find succeeds)**

- `FrontSideConstraint` YAML only serializes `frontOffset`; other fields use defaults / runtime `Find("TargetRoot")`. Confirm in Inspector: **`frontSideAxis`**, **`additionalMinimumLocalZ`**, **`targetBlockingCollider`** for your mesh thickness.

---

## Part B — Manual acceptance checklist (Play Mode, `TransformSandboxScene` only)

Run these once after opening the scene and saving assets.

### Selection and gizmo

1. **Empty click** — Click sky/ground with no hit: selection clears if `clearSelectionOnEmptyClick` is on.
2. **Select cube** — Left-click `Cube`: gizmo appears on selection; modes **1–4** switch translate / rotate / scale / universal per `TransformGizmoController`.
3. **Gizmo manipulates content** — Drag handles: cube moves/rotates/scales; **`FrontSideConstraint`** keeps content on the correct side of the wall (adjust axis/offsets if content pops behind the plane).
4. **Uniform scale** — Scale mode respects uniform scale policy when enabled.

### Target plane drag

5. **Drag empty wall** — Click-drag **`TargetPlane`** mesh (not cube): **`TargetRoot`** slides on the posture plane; **`Cube`** moves with it (parent under target).
6. **Content blocks target drag** — Click-drag **Cube**: target does **not** move; gizmo/selection behavior wins per setup.
7. **`planeAnchor`** — With `TargetPlane` assigned as anchor, drag feel stays aligned with the quad normal (verify after scene fix).

### Camera isolation

8. **Scroll / WASD / QE** — During **gizmo hover**, **gizmo drag**, or **target drag**, camera move/zoom/look must **not** apply (see `RuntimeCameraController.IsBlockedBySceneInteraction` precedence).
9. **Right-mouse look** — Still works when **not** blocked; ends cleanly after block release.

### Regression

10. **No RTG errors** — Console free of RTG init errors; `RTGApp` appears at runtime.

---

## Part C — Migration checklist (`AuthoringToolScene`)

Authoring today uses **`ContentTransformController`** + **`TargetSelectionManager`** + **`ImageTargetRoot`**. Sandbox uses **`ObjectSelectionManager`** + **`TransformGizmoController`** + **`TargetRoot`** naming. Migrate in phases.

### Scene wiring

- [ ] Add (or duplicate from sandbox) a **`TransformSystems`**-style GameObject with: `ObjectSelectionManager`, `TransformGizmoController`, `TargetLocalTransformService`, `FrontSideConstraint`.
- [ ] Optionally add **`TransformInteractionCompositionRoot`** and assign: **`targetRoot`** (see below), **`contentRoot`**, **`mainCamera`**, **`targetPlaneAnchor`** (quad/wall transform if different from pivot), plus component refs — reduces duplicate Inspector wiring.
- [ ] Map **`ImageTargetRoot`** → **`targetRoot`** for `TargetMovementController`, `FrontSideConstraint`, and plane drag. Update **`FrontSideConstraint`** / **`GameObject.Find("TargetRoot")`** usage: either rename a runtime helper transform for sandbox parity or assign **`targetRoot`** / **`contentRoot`** explicitly in Inspector (recommended for authoring).
- [ ] Ensure **`ContentRoot`** exists under each visible authoring target and matches **`ObjectSelectionManager.contentRoot`** when that target is active (multi-target: align with `TargetSelectionManager.ActiveTargetChanged` — may require a small bridge script or reconfigure selection when the active target index changes).
- [ ] Wire **`TargetMovementController`**: `targetRoot`, `contentRoot`, `raycastCamera`, `gizmoController`, **`planeAnchor`** (wall quad), `targetMask`.
- [ ] Keep **`RuntimeCameraController`** on the authoring camera (same blocking rules as sandbox).

### Legacy controller

- [ ] **`ContentTransformController`**: disable or remove after **`TransformGizmoController`** + **`ObjectSelectionManager`** cover selection, gizmo modes, and **`AuthoringUIController.OnContentSelectedInScene`** / spatial sync. Port any calls like **`SelectContentTransform`**, **`RefreshContentList`**, upload hooks to the new selection API or thin adapter.
- [ ] Confirm only one system owns RTG “work” gizmo instances to avoid duplicate gizmos (legacy creates a universal gizmo in code).

### UI / product

- [ ] **`AuthoringUIController`**: verify transform fields still sync when selection changes (was driven by `ContentTransformController`).
- [ ] Presets / workspace: **`WorkspacePresetLibrary`** affects camera + target rotation — after migration, verify target-plane drag and **`FrontSideConstraint`** match preset posture (wall vs floor).

### Validation

- [ ] Repeat **Part B** checks in **`AuthoringToolScene`** with real UI (focus fields, panels).
- [ ] Regression: tracker targets, spawn/upload flows, switching AR targets from dropdown.

---

## Revision history

- **2026‑05‑04** — Static audit; sandbox `TargetMovementController` wired `contentRoot` + `planeAnchor`; acceptance + migration lists authored.
