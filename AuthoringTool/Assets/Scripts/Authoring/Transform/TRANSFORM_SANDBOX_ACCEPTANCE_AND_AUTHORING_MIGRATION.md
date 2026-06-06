# TransformSandboxScene acceptance — AuthoringToolScene migration

This document records **static verification** of `TransformSandboxScene` for the target-centric gizmo pipeline (DEV‑177 style), **manual acceptance tests** to run in the Unity Editor, and the **AuthoringToolScene migration status**.

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

## Part C — AuthoringToolScene migration status

**Status: complete.** `AuthoringToolScene` uses **`AuthoringTransformCoordinator`** + **`TransformGizmoController`** + **`ObjectSelectionManager`** (not legacy `ContentTransformController`).

| Item | Status |
|------|--------|
| Transform pipeline on scene (`TransformGizmoController`, `AuthoringTransformCoordinator`, `ObjectSelectionManager`) | **Done** |
| `TargetSelectionManager` + `ImageTargetRoot` hierarchy for runtime targets | **Done** (active target drives `ObjectSelectionManager.contentRoot`) |
| Semantic inspector sliders via `ContentTransformManipulator` | **Done** |
| `RuntimeCameraController` input gating | **Done** |
| Workspace autosave hooks from transform changes | **Done** (via coordinator + registry) |

### Ongoing validation

- [ ] Repeat **Part B** checks in **`AuthoringToolScene`** with real UI (focus fields, panels).
- [ ] Regression: spawn/upload flows, workspace switcher round-trip, backend restore.

---

## Revision history

- **2026‑05‑04** — Static audit; sandbox `TargetMovementController` wired `contentRoot` + `planeAnchor`; acceptance + migration lists authored.
- **2026‑06‑04** — Migration marked complete; legacy `ContentTransformController` references removed from docs.
