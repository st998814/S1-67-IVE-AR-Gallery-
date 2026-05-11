# Authoring App Flow Initialization

## Assignment : DEV-131 - Application-Level Authoring Flow Initialization

---

## Outlines

### 1. End-to-end app flow bootstrapping

This ticket establishes the minimal runtime application flow for the authoring tool:

- `LandingScene` -> `WorkspaceSwitcherScene` -> `AuthoringToolScene`.
- Navigation is explicit and deterministic through shared app-flow entry points.
- Scene transitions are guarded to avoid duplicate input during navigation.

### 2. Workspace as single target-context (1:1)

Workspace meaning for this stage is normalized to:

- **1 workspace = 1 target-context + bound content state**.
- Entering authoring from switcher activates (or creates) the workspace target directly.
- No additional target-selection intent is required from users at scene entry.

### 3. Transition consistency and UX continuity

- Introduce a reusable fade-to-black transition service used across app-flow interactions.
- Keep flow interactions stable under rapid button clicks via transition in-progress guards.
- Ensure transitions remain non-destructive to runtime editing state management.

### 4. Minimal interaction completeness for app flow

- Add reverse navigation from authoring back to workspace switcher.
- Clear active workspace session on back-navigation to avoid stale context carry-over.
- Preserve existing authoring save/create/edit behavior while app-level flow is extended.

---

## Modification

### 1. App flow core — `Assets/Scripts/`

| File | Description |
|------|-------------|
| **AppFlowController.cs** | Central app-flow constants and helpers for scene names, workspace session set/get/clear, and new workspace session creation. |
| **WorkspaceSessionContext.cs** | Session payload model passed across scenes (`workspaceId`, `workspaceName`, `targetId`, `isNewWorkspace`, optional thumbnail key). |
| **SceneTransitionService.cs** | Reusable fade transition service (`FadeOut -> LoadScene -> FadeIn`) with input blocking and duplicate-trigger protection. |

### 2. Landing scene entry — `Assets/Scenes/`, `Assets/UI/`, `Assets/Scripts/`

| File | Description |
|------|-------------|
| **Scenes/LandingScene.unity** | New landing scene containing `UIDocument` and app entry UI host. |
| **UI/UXML/LandingUI.uxml** | Landing UI Toolkit markup for product title and start action. |
| **Scripts/LandingController.cs** | Binds landing UI start action to app-flow transition into workspace switcher; includes runtime UI fallback when UXML binding is unavailable. |

### 3. Workspace switcher scene — `Assets/Scenes/`, `Assets/UI/`, `Assets/Scripts/`

| File | Description |
|------|-------------|
| **Scenes/WorkspaceSwitcherScene.unity** | New workspace switcher scene with `UIDocument` host. |
| **UI/UXML/WorkspaceSwitcherUI.uxml** | Switcher UI Toolkit layout for horizontal card strip, arrows, and `NEW`/`EDIT` actions. |
| **Scripts/WorkspaceSwitcherController.cs** | Mock workspace list, arrow-based selection, selected-card focus behavior (scale/opacity), center snap animation, and entry routing to authoring scene. |

### 4. Authoring scene workspace entry adapter — `Assets/Scripts/`, `Assets/Scenes/`

| File | Description |
|------|-------------|
| **Scripts/AuthoringWorkspaceEntry.cs** | Consumes workspace session on authoring scene load; resolves target by id and creates missing target when needed to maintain 1:1 workspace-target entry contract. |
| **Scenes/AuthoringToolScene.unity** | Updated to attach `AuthoringWorkspaceEntry` so workspace context is applied automatically at scene start. |

### 5. App-flow interaction completion in authoring UI — `Assets/UI/`, `Assets/Scripts/`

| File | Description |
|------|-------------|
| **UI/UXML/AuthoringUI.uxml** | Added `BackToSwitcherButton` in authoring panel. |
| **Scripts/AuthoringUIController.cs** | Wires `BackToSwitcherButton` click to clear workspace context and transition back to `WorkspaceSwitcherScene`. |

### 6. Build scene registration and order — `ProjectSettings/`

| File | Description |
|------|-------------|
| **ProjectSettings/EditorBuildSettings.asset** | Registers and orders scenes as `LandingScene` -> `WorkspaceSwitcherScene` -> `AuthoringToolScene` (with `SampleScene` disabled for this flow). |

### 7. Minimal visual-system uplift for app screens — `Assets/UI/`

| File | Description |
|------|-------------|
| **UI/USS/AppTheme.uss** | Shared token-based style system (color palette, spacing, radius, reusable layout/button/card classes). |
| **UI/USS/LandingUI.uss** | Landing-specific style refinements aligned to minimal futuristic visual direction. |
| **UI/USS/WorkspaceSwitcherUI.uss** | Switcher-specific style hooks for carousel, actions, and reusable card states. |
| **UI/UXML/LandingUI.uxml** / **UI/UXML/WorkspaceSwitcherUI.uxml** | Updated to consume reusable USS classes with minimal structural change. |

---

## Validation

- Scene load targets are present and enabled in build settings.
- Transition source/target scene names match app-flow constants.
- Landing start action transitions to switcher with fade guard.
- Switcher `NEW`/`EDIT` actions transition to authoring with workspace context assignment.
- Authoring workspace entry adapter resolves/creates target context for selected workspace.
- Authoring includes back-navigation action to return to switcher and clear session context.

---

## Known Gaps / Follow-ups

- Workspace list is currently mock/in-memory; no durable workspace registry persistence yet.
- Runtime-save checkpointing and persistent-sync save are not yet unified under a dual-mode save policy.
- Additional app-flow interactions and UI polish (beyond this ticket baseline) should be tracked in follow-up tickets.
