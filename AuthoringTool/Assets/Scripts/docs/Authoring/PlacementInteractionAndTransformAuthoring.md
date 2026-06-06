# Placement Interaction and Transform Authoring
## Assignment : Runtime placement layer — interaction ownership, target vs content transforms (MWP)

---

## Outlines

### 1. Shared interaction ownership (camera vs drag)

At any time, a single **interaction owner** applies so camera navigation and object drag do not run together:

- **States** — `None`, `Camera`, `DraggingObject` (see `DraggableObject.InteractionOwner`).
- **Camera** — `RuntimeCameraController` acquires ownership while right-mouse look is active; releases on look end, disable, or blocked input.
- **Drag** — `DraggableObject` acquires drag ownership when a valid left-click drag starts; drag cannot start while camera owns input.
- **Consumers** — camera blocks when drag is active; **`AuthoringTransformCoordinator`** skips keyboard nudges while LMB drag is active so semantic sliders and drag do not fight.

Related: scene navigation details remain documented in [RuntimeSceneCameraControl.md](RuntimeSceneCameraControl.md).

### 2. Target vs content — movement and hierarchy rules

- **Target drag** — `TargetVisual` uses `DraggableObject` with `moveParentOnDrag = true`: the **target root** moves; `ContentRoot` and all child contents follow.
- **Content drag** — content prefabs use `DraggableObject` with `moveParentOnDrag = false` (default): only the **content instance** moves; the AR target root does not.
- **Plane hint (MWP)** — target setup keeps `lockLocalZ` on the drag transform so movement stays on an authoring plane; future pose-aware planes can replace this without changing the ownership model.

### 3. Drag vs scroll-scale binding (decoupled resolvers)

Scroll-while-drag scaling no longer shares the same transform resolution path as drag:

- **`ResolveDragTransform()`** — used only for position during drag (`moveParentOnDrag` → parent for targets).
- **`ResolveScaleTransform()`** — used only for uniform scroll scale (`scaleParentOnScroll` → parent for target uniform scale when configured).
- **Configuration** — `ConfigureDragBinding`, `ConfigureScaleBinding`, and `ConfigureConstraints` stay the MWP API; targets set scale-on-parent explicitly in factory code; content defaults scale the object that owns `DraggableObject` (usually the content shell).

### 4. Target scale vs content scale — what “decoupled” means

- **Independent operations** — scaling the **content** transform (container or selected content) changes only that object’s `localScale`. Scaling the **target root** (via target scroll binding) scales the whole subtree under that target, which is expected hierarchy behavior, not a shared scaler with content.
- **Visual coupling** — if the user scales the **target**, children under `ContentRoot` appear larger/smaller in world space because they are children of the target. That is normal; decoupling here means **different controls and transforms**, not that children ignore parent scale.

### 5. Model content: container vs mesh under `ContentBody`

For volumetric content (`ModelContentContainer` → `ContentBody` → imported mesh):

- **Authoring treats the container as the content transform** — `DraggableObject` and inspector-driven scale typically apply to **`ModelContentContainer` (clone)** under `ContentRoot`, not to deep mesh nodes (e.g. `Circle.012`).
- **Why** — the shell is stable for pooling, spawn identity, and loader lifecycle; uniform scale on the container scales the entire visual subtree consistently.
- **Mesh-only scale** is possible but discouraged for MWP unless explicitly designed (loader may rebuild children; pivots and pooling assumptions stay on the shell).

---

## Modification

Paths are under `Assets/Scripts/` unless noted.

### 1. Interaction ownership and drag/scale split — `Scripts/`

| File | Description |
|------|-------------|
| **DraggableObject.cs** | Static interaction owner (`None` / `Camera` / `DraggingObject`); `TryAcquireCameraInteraction` / `ReleaseCameraInteraction`; drag acquire/release on LMB; separate `ResolveDragTransform` vs `ResolveScaleTransform`; `ConfigureScaleBinding` for scroll-scale parent vs self. |
| **RuntimeCameraController.cs** | Acquires/releases camera interaction with RMB look; blocks all camera input while drag interaction is active; retains UI and RTG gating. |
| **AuthoringTransformCoordinator.cs** | Skips keyboard transform nudges while `DraggableObject.IsDraggingObjectInteractionActive` to avoid conflicts during LMB drag. |

### 2. Target factory wiring — `Target/Managers/`

| File | Description |
|------|-------------|
| **RuntimeImageTargetFactory.cs** | Target `TargetVisual` draggable: `ConfigureDragBinding(true)` (move parent/root); `ConfigureScaleBinding(true)` (uniform scroll scale on target root); constraints unchanged for MWP plane lock + scroll scale enable. |

### 3. Content scaling in practice (no new files)

| Mechanism | Description |
|-----------|-------------|
| **Scroll while dragging** | If `allowScrollScale` on the content’s `DraggableObject`, wheel adjusts uniform scale on `ResolveScaleTransform()` (typically the content shell, not the AR target). |
| **Keyboard** | **`AuthoringTransformCoordinator`** — semantic move/scale via inspector sliders; optional keyboard nudges when not in RTG drag and not in LMB drag ownership. |
| **UI** | `AuthoringUIController` scale field syncs to the bound authoring spatial target (selected content when applicable). |

---

## Result

- Camera navigation and LMB object drag are **mutually exclusive** at the interaction-owner level.
- Target drag still moves the **target root** and carries **ContentRoot** children; content drag moves **only** the content instance.
- **Scroll scale** uses a **dedicated scale transform** path, independent of drag transform resolution; target vs content bindings are configured separately.
- **Target root scale** vs **content shell scale** are separate operations; scaling the target still affects child world scale by hierarchy, which is expected.
- **Model** authoring intentionally scales/moves the **container** under `ContentRoot`; deep mesh nodes remain payload under `ContentBody`.

---
