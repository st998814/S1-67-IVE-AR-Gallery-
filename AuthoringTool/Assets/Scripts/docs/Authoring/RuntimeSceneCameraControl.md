# Runtime Scene Camera Control
## Assignment : Runtime authoring scene camera control (MWP)

---

## Outlines

### 1. Minimal runtime navigation for authoring

The authoring scene now includes a lightweight runtime camera controller designed for inspection and placement validation:

- **Keyboard move** — supports `W/A/S/D` and arrow keys for planar movement.
- **Mouse look** — supports right-click hold + mouse drag for yaw/pitch camera rotation.
- **Scroll zoom** — supports wheel-based forward/back movement with bounded height.
- **Demo-first scope** — implementation targets stable MWP behavior rather than advanced cinematic camera features.

### 2. Input conflict safety with authoring workflows

Camera input processing is isolated from content interaction paths:

- **UI gate** — camera input is blocked while pointer is over authoring UI.
- **Gizmo gate** — camera input is blocked while Runtime Transform Gizmo is hovered or dragged.
- **Drag gate** — camera input is blocked during left-button object drag interactions.
- **Input separation** — right mouse is reserved for camera look; left mouse remains for object interaction flows.

### 3. Scene integration strategy

Integration is applied directly to the authoring runtime camera:

- Attach `RuntimeCameraController` to `Main Camera` in `AuthoringToolScene`.
- Keep existing camera stack untouched (`Camera`, `AudioListener`, `UniversalAdditionalCameraData`).
- Expose serialized tuning fields for demo iteration (`moveSpeed`, `lookSensitivity`, `zoomSpeed`, pitch/height clamps).

---

## Modification

### 1. New runtime camera controller — `Scripts/Camera/`

| File | Description |
|------|-------------|
| **RuntimeCameraController.cs** | New standalone runtime camera control script for authoring navigation. Implements keyboard movement, right-mouse look, scroll zoom, and conflict-safe input gating (UI/gizmo/drag). |

### 2. Scene hookup — `Scenes/`

| File | Description |
|------|-------------|
| **AuthoringToolScene.unity** | `Main Camera` updated to include `RuntimeCameraController` with demo-safe default parameters. |

### 3. Asset metadata

| File | Description |
|------|-------------|
| **RuntimeCameraController.cs.meta** | Unity metadata for the camera controller script used by scene component reference. |

---

## Result

- Authoring users can freely inspect the runtime scene using keyboard, mouse look, and scroll zoom.
- Camera controls no longer interfere with left-click object drag/select workflows.
- Authoring UI interactions and RTG manipulation remain stable during camera navigation.
- The implementation remains self-contained and suitable for demo-focused usage.

