# Spatial Mapping System (DEV-134)
## Assignment : Physical World ↔ Virtual Placement Mapping System (Authoring Scene)

---

## Outlines

### 1. Posture-aware placement boundaries

Authoring placement limits are now driven by workspace posture presets, not only implicit `TargetVisual` scale.

- **Posture source** — `WorkspacePreset.placementBoundary` from `WorkspacePresetLibrary` (`Wall`, `Floor`, `Ceiling`).
- **Pure math** — `PlacementBoundsCalculator` builds ContentRoot-local axis ranges and eight box corners.
- **Runtime service** — `PlacementBoundsService` resolves bounds from `TargetVisual`, active preset, and `FrontSideConstraint` Z rules.
- **Wall safe zone** — `PlacementBoundaryPreset.WallDefault` uses absolute half-extents (±0.75 m left/right, ±0.50 m up/down, 0.05 m–1.00 m in front) so the target acts as a spatial anchor rather than a tight card-sized box.
- **Clamp path unchanged** — `ContentTransformManipulator` remains the only writer; sliders and gizmo still clamp through the same service.

### 2. Semantic real-world distance labels

Slider and inspector values no longer show raw `localPosition` floats as the primary UX.

- **Formatter** — `SemanticDistanceFormatter` outputs phrases such as `25 cm right`, `10 cm up`, `54 cm in front of target`.
- **Bottom manipulator** — `AuthoringUIManipulatorPanel` value labels call the formatter per semantic axis.
- **Right inspector** — `AuthoringUIController` shows semantic offset rows for content selection; default content inspector no longer leads with raw Local X/Y/Z fields.

### 3. Placement boundary visualization (corner-only)

The editable region is shown as a lightweight limit cue, not a second projection volume.

- **Visualizer** — `PlacementSpaceVisualizer` draws **red corner brackets** only (three axis-aligned legs per corner, 24 lines total).
- **No full box** — twelve-edge wireframe, front-plane grid, and diagonal corner accents were removed after user feedback.
- **Visual hierarchy** — boundary alpha and line width stay below the holographic projection so content and target remain dominant.
- **Parent space** — guides attach under the target root (`PlacementBoundaryVisual`) and follow target-relative bounds from `PlacementBoundsService.TryGetPlacementVolumeVisualBounds`.

### 4. Holographic target-to-content projection (primary spatial cue)

The original orange target→content line and later RGB axis legs were replaced by a single primary relationship indicator.

- **Indicator** — `HolographicProjectionIndicator` builds a frustum from the target face toward the selected content bounds (solid frustum edges, dashed face rings, optional scan-line fill mesh).
- **Shader** — `AuthoringHologramProjection.shader` (URP + built-in fallback): scrolling stripe alpha mask, rim, subtle pulse; glitch disabled by default.
- **Read-only** — updates on selection and `ContentTransformChanged`; does not modify placement data, physics, or backend snapshots.
- **Removed** — `SpatialMappingIndicatorRenderer` (RGB axis projections) deleted per UX request.

### 5. Spatial mapping coordinator and scene wiring

A dedicated coordinator owns authoring-only visuals separately from content rendering.

- **Component** — `SpatialMappingCoordinator` on `AuthoringTransformSystems` in `AuthoringToolScene`.
- **Responsibilities** — placement boundary refresh, holographic projection attach/hide, camera-distance line sizing, target/layout change detection.
- **Events** — `ActiveTargetChanged`, `ContentSelectionChanged`, `ContentTransformChanged`, plus `LateUpdate` refresh while content is selected.
- **Entry hook** — `AuthoringWorkspaceEntry` calls `RefreshPlacementVolume()` after posture preset apply.

### 6. Target-aligned bounds and interaction cleanup

Bounds and guides were aligned to the physical target anchor and de-cluttered from duplicate box visuals.

- **Volume centering** — `PlacementBoundsService` and `PlacementBoundsCalculator.ConvertSnapshotLocalSpace` support target-root visual bounds when `TargetVisual` is offset from `ContentRoot`.
- **Front standoff** — `FrontSideConstraint.frontOffset` default reduced to `0.05 m` so preset near-limit (5 cm) is not overridden by enforce logic.
- **Selection wireframe removed** — large blue selection-bounds box removed from `AuthoringTransformCoordinator`; material highlight on selected renderers retained.

### 7. EditMode test coverage

| Test file | Covers |
|-----------|--------|
| **Tests/EditMode/PlacementBoundsServiceTests.cs** | Preset multipliers, absolute Wall safe zone, corner fill, target-context bounds |
| **Tests/EditMode/SemanticDistanceFormatterTests.cs** | Axis wording, cm/m thresholds, combined offset strings |

Run: Unity **Window → General → Test Runner → EditMode**.

---

## Modification

Paths are under `Assets/Scripts/` unless noted.

### 1. Placement boundary domain (Core + Transform)

| File | Description |
|------|-------------|
| **Authoring/Core/PlacementBoundaryPreset.cs** | Posture boundary struct: scale multipliers, depth, margin, optional min standoff, optional absolute XY half-extents; `WallDefault` safe zone. |
| **Authoring/Core/PlacementBoundsCalculator.cs** | ContentRoot-local range and corner math; preset-aware `Compute`; `ConvertSnapshotLocalSpace` for target-root visualization. |
| **Authoring/Transform/PlacementBoundsService.cs** | Resolves bounds/clamp ranges from `TargetVisual` + active preset; `SetPosture`, `SetTargetContext`, `TryGetPlacementVolumeVisualBounds`. |
| **Authoring/Transform/FrontSideConstraint.cs** | Default `frontOffset` `0.05 m` so near-plane preset limits apply correctly. |
| **Authoring/Transform/ContentTransformManipulator.cs** | Unchanged write path; continues to clamp via `PlacementBoundsService`. |

### 2. Workspace preset integration

| File | Description |
|------|-------------|
| **Workspace/Presets/WorkspacePresetModels.cs** | `WorkspacePlacementBoundaryPreset` and `WorkspacePreset.placementBoundary`. |
| **Workspace/Presets/WorkspacePresetLibrary.cs** | Per-posture `PlacementBoundaryPreset` table (`WallDefault`, Floor, Ceiling variants). |
| **AuthoringWorkspaceEntry.cs** | Applies workspace preset; refreshes `SpatialMappingCoordinator` placement boundary after posture apply. |

### 3. Semantic distance UI

| File | Description |
|------|-------------|
| **Authoring/Core/SemanticDistanceFormatter.cs** | Human-readable offset strings (cm/m, left/right, up/down, in front/behind target). |
| **AuthoringUIManipulatorPanel.cs** | Slider value labels use formatter instead of raw metres. |
| **AuthoringUIController.cs** | Content inspector semantic placement offset rows. |
| **UI/UXML/AuthoringUI.uxml** | Inspector layout aligned with semantic offset presentation. |

### 4. Spatial mapping visuals (new folder)

| File | Description |
|------|-------------|
| **Authoring/SpatialMapping/SpatialMappingCoordinator.cs** | Owns boundary + hologram lifecycle; inspector colors for boundary corners and hologram projection. |
| **Authoring/SpatialMapping/PlacementSpaceVisualizer.cs** | Red corner-only boundary brackets (no wireframe box). |
| **Authoring/SpatialMapping/HolographicProjectionIndicator.cs** | Target-to-content holographic frustum lines and fill mesh. |
| **Authoring/SpatialMapping/AuthoringBoundsVisualUtility.cs** | Renderer bounds and face-corner helpers for frustum anchoring. |
| **Authoring/SpatialMapping/AuthoringLineVisualUtility.cs** | Shared `LineRenderer` materials (`Unlit/Color`, optional dash texture). |
| **Authoring/SpatialMapping/AuthoringHologramMaterialUtility.cs** | Runtime hologram material and stripe alpha mask setup. |
| **Authoring/SpatialMapping/Shaders/AuthoringHologramProjection.shader** | URP-compatible transparent hologram fill shader. |

### 5. Transform coordinator cleanup

| File | Description |
|------|-------------|
| **Authoring/Transform/AuthoringTransformCoordinator.cs** | Removed selection-bounds wireframe overlay; kept selection material highlight. |

### 6. Scene and project maintenance

| File | Description |
|------|-------------|
| **Scenes/AuthoringToolScene.unity** | `SpatialMappingCoordinator` on `AuthoringTransformSystems`; `PlacementBoundsService` wiring; boundary/hologram inspector defaults. |
| **../Assembly-CSharp.csproj** | Includes spatial mapping scripts for IDE resolution. |

### 7. Tests

| File | Description |
|------|-------------|
| **Tests/EditMode/PlacementBoundsServiceTests.cs** | Boundary preset and calculator integration tests. |
| **Tests/EditMode/SemanticDistanceFormatterTests.cs** | Formatter wording and unit thresholds. |

### 8. Removed during DEV-134 (superseded or cut)

| File | Description |
|------|-------------|
| **Authoring/SpatialMapping/SpatialMappingIndicatorRenderer.cs** | Deleted — RGB axis legs and arrow heads on target; replaced by holographic projection-only UX. |

---

## Spatial Mapping Flow

1. Authoring scene loads target with `ContentRoot` / `TargetVisual` hierarchy.
2. `AuthoringWorkspaceEntry` applies workspace posture preset (target rotation, camera, placement boundary).
3. `PlacementBoundsService` stores active `PlacementBoundaryPreset` and target context.
4. `SpatialMappingCoordinator.RefreshPlacementVolume()` builds boundary snapshot and shows red corner brackets under target root.
5. User selects content under `ContentRoot`.
6. Coordinator attaches `HolographicProjectionIndicator` and refreshes frustum on transform changes and `LateUpdate`.
7. User moves content via bottom sliders or gizmo — `ContentTransformManipulator` clamps position; boundary corners and hologram update; semantic labels refresh.
8. Save/reload — `localPosition` in workspace snapshot unchanged; guides rebuild after scene reconstruct.

---

## Visual Hierarchy (as shipped)

| Layer | When visible | Role |
|-------|----------------|------|
| Content + target meshes | Always | Primary scene subjects |
| Holographic projection | Content selected | **Primary** — explains target-relative content placement |
| Red boundary corners | Active target | **Secondary** — allowed movement limits only |
| Selection material tint | Object selected | Interaction feedback (no placement wireframe) |

---

## Result

- Authoring placement is bounded by posture-aware presets with a relaxed Wall safe zone anchored on the target, not a postage-stamp card box.
- The scene communicates left/right, up/down, and closer/further through semantic slider labels and a holographic target→content projection.
- Placement limits are shown as subtle red corner brackets without a full collision-style wireframe volume.
- RGB axis indicators and the old direct relationship line were removed to reduce visual noise.
- All bounds and guides use ContentRoot-local space consistent with saved `localPosition`; runtime MobileViewer and snapshot schema are unchanged.
- EditMode tests cover boundary math and semantic formatting for regression safety.

---

## Out of scope

- Runtime AR / MobileViewer rendering changes
- Room reconstruction, physics colliders, multi-room navigation
- Changing workspace `snapshot.json` transform schema (still stores raw local TRS)
- Backend placement export format changes

---

## Manual acceptance checklist

1. Open `AuthoringToolScene` with a Wall workspace — red corner brackets mark the movement limits around the target.
2. Select content — cyan holographic frustum connects target face to content; no RGB axis lines on the target.
3. Move content with bottom sliders — brackets stay fixed; hologram and labels (`cm` / `m` phrases) update live.
4. Switch Floor/Ceiling mock workspace — boundary size and camera posture change per preset table.
5. Save/reload workspace — `localPosition` unchanged in snapshot; boundary and hologram reappear after reconstruct.

---

## Related docs

- [SemanticTransformInspectorAuthoring.md](SemanticTransformInspectorAuthoring.md) — semantic sliders and `ContentTransformManipulator`
- [WorkspacePresetSystem.md](WorkspacePresetSystem.md) — posture presets and scene entry (DEV-129)
- [PlacementInteractionAndTransformAuthoring.md](PlacementInteractionAndTransformAuthoring.md) — interaction ownership
