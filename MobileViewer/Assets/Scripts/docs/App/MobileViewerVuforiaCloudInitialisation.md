# MobileViewer Vuforia Cloud Initialisation

## Assignment : Ad-hoc implementation 

---

## Outlines

### 1. Prototype objective and boundaries

This implementation establishes a minimal iOS-focused AR runtime in `MobileViewer` using Vuforia Cloud Recognition.

- camera-based cloud image target recognition
- target identifier extraction from cloud results
- mock content lookup through replaceable service interface
- tracked-target-bound mock 3D rendering
- transient status toast messages for runtime visibility

Out-of-scope for this stage:

- production backend/API integration
- persistent content data pipeline
- advanced AR anchoring/occlusion/polish systems
- final UX polish and analytics instrumentation

### 2. Runtime architecture (separation of concerns)

Core responsibilities are split into modular components:

- **`VuforiaCloudTargetController`** - wraps Vuforia cloud callbacks, observer tracking lifecycle, and target status events.
- **`TargetContentCoordinator`** - orchestrates detected/tracked target flow into content loading and render/hide behavior.
- **`IContentService` / `MockContentService`** - content abstraction and mock implementation for backend-independent delivery.
- **`ContentRenderer`** - renders a simple mock 3D primitive for active tracked targets.
- **`MobileViewerStatusUI`** - provides toast-like status feedback to users.
- **`VuforiaSceneBootstrap`** - runtime cloud reco creation and Vuforia initialization wiring.

### 3. Recognition-to-render flow

1. Scene starts and `VuforiaSceneBootstrap` attempts to create `CloudRecoBehaviour`.
2. `VuforiaCloudTargetController` registers cloud handlers and waits in scanning state.
3. Cloud result arrives -> target identifier is extracted (property/field fallback).
4. Controller enables observer for result and monitors tracking status.
5. On tracking found, coordinator requests content from `IContentService`.
6. `MockContentService` resolves mock content by canonical target name (including alias mapping).
7. `ContentRenderer` renders a target-following primitive.
8. On tracking lost, coordinator cancels loading (if any), hides content, and resets to scanning.

### 4. UI and UX decisions

- Status UI uses toast-style queued messages (show/hide animation + 3s visibility).
- Toast anchor was moved lower from top edge to avoid iPhone notch overlap.
- Legacy blue content panel was disabled in runtime output; mock 3D object is primary visual output.
- Console status logs remain available for runtime debugging.

### 5. iOS readiness alignment

- Vuforia app license and cloud keys are wired via runtime bootstrap/inspector config.
- `MobileViewerScene` is the dedicated Vuforia prototype scene.
- AR Foundation iOS loader conflict was identified during build and treated as a setup constraint.
- Build/signing path was validated through Unity -> Xcode export workflow.

---

## Modification

Primary implementation changes under `MobileViewer/Assets/`.

### 1. AR flow and Vuforia integration

| File | Description |
|------|-------------|
| **Scripts/AR/VuforiaSceneBootstrap.cs** | Runtime Vuforia/cloud reco bootstrap with status reporting and initialization guarding. |
| **Scripts/AR/VuforiaCloudTargetController.cs** | Cloud callback registration, robust target extraction, observer attach/detach, tracking found/lost event emission. |
| **Scripts/AR/TargetContentCoordinator.cs** | Bridges controller events to content service and renderer; handles duplicate guards, cancellation, and lost-target reset. |

### 2. Content domain and mock data service

| File | Description |
|------|-------------|
| **Scripts/Content/ContentData.cs** | DTO for target-linked content payload used by renderer. |
| **Scripts/Content/IContentService.cs** | Service abstraction contract for target -> content resolution. |
| **Scripts/Content/MockContentService.cs** | Mock async content provider with alias mapping and sample targets (`ttest`, `demo-vuforia-target-2`, `desktop-mountain-target`). |

### 3. Rendering and UI

| File | Description |
|------|-------------|
| **Scripts/Content/ContentRenderer.cs** | Simple primitive render path (cube/capsule/sphere), target-following placement, hide-on-lost behavior, panel-disabled runtime output. |
| **Scripts/UI/MobileViewerStatusUI.cs** | Toast queue, tone mapping, animated status display, lowered top placement for notch-safe visibility. |
| **Prefabs/Content/ContentPanelPrefab.prefab** | Initial content panel prefab (kept as asset; runtime panel display disabled in current flow). |

### 4. Scene wiring

| File | Description |
|------|-------------|
| **Scenes/MobileViewerScene.unity** | Dedicated runtime scene containing AR camera, runtime coordinator object graph, and UI references. |

---

## Validation

- Cloud recognition callbacks are received when valid keys/license are configured.
- Known cloud target identifiers resolve to expected mock content entries.
- Content appears only while target is actively tracked and disappears on tracking loss.
- Status toast sequence reflects runtime progression (`Scanning -> Target detected -> Loading -> Content loaded`).
- iOS build/export path is functional after Xcode/signing and XR loader alignment.
- Script compile state is clean for current `MobileViewerStatusUI` (toast-only, no zoom code).

---

## Known Gaps / Follow-ups

- No backend-driven content service yet (mock-only).
- No persisted workspace/content authoring sync in this runtime.
- Cloud target alias map currently manual for UUID-style identifiers.
- Advanced AR UX (occlusion, stable anchors, interaction patterns) remains future work.
- Camera zoom feature was intentionally deferred and removed from current codebase.

---
