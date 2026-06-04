# Syncing to persistent storage (Layer 3)

**Layer 3** is now the durable source of truth: authored workspace state is persisted to the AR Gallery **backend** (Postgres + files under `uploads/`).

Authoring **UI** behavior is documented in **[AuthoringUILayout.md](../Authoring/AuthoringUILayout.md)**.

---

## Purpose

- Persist targets, content rows, and uploaded media on the server so TablePlus / API consumers see the same data as the authoring tool after sync.
- Support workspace-scoped records (`workspace_id` / `workspaceId`) and optional workspace row upsert on the server.
- On workspace delete from the switcher, cascade server-side data via **`DELETE /api/workspaces/<workspace_id>`**.

---

## Unity components

| Area | Role |
|------|------|
| **WorkspaceRemoteSyncService** | Coroutine-driven pass: sync targets then contents from **`AuthoredObjectRegistry`** when `RemoteDirty`; uses **`IApiClient`** (`HttpApiClient`). Uploads from **in-memory bytes** (`TargetImageBytes`, `TargetReferenceBytes`, `AssetBytes`) or existing **http(s) URLs** — no `persistentDataPath` reads. |
| **WorkspaceAutoSaveService** | Debounces edits → **`DebouncedWorkspaceChanged`** → schedules remote sync (no local `snapshot.json` write). |
| **HttpApiClient** | `POST /api/upload` (multipart `category`, optional **`targetId`** / **`contentId`** for stable file names), `POST /api/targets`, `POST /api/content`; multipart **`POST /api/targets/cloud`** where used. |
| **TargetWorkflowService** / **ContentWorkflowService** | Build JSON DTOs for create/update flows; target sync includes **`workspaceId`** / **`workspaceName`** from session. |
| **Workspace session** | `AppFlowController` / `WorkspaceSessionContext` supplies **`workspaceId`** for API payloads; optional **`targetImageBytes`** for new target setup (WebGL-safe). |
| **AuthoringWorkspaceEntry** | Opens workspace via **`GET /api/workspaces/{id}`** → in-memory **`WorkspaceSnapshot`** → **`WorkspaceSceneReconstructor.BeginRebuild`**. |
| **Back to switcher** | **`AuthoringUIController`** captures workspace id, **`SyncWorkspaceAndWait`**, then clears session (no disk flush). |

### WebGL / in-memory media

- Spawned content keeps upload bytes on **`AuthoredContentInstance.AssetBytes`** until sync uploads and sets **`MediaUrl`**.
- Target reference photos use **`AuthoredTargetInstance.TargetReferenceBytes`** (not disk under `assets/target_refs/`).
- Target trackable images use session/`TargetImageBytes` or **`TargetImageUrl`** after cloud create or upload.
- **`WorkspaceSceneReconstructor`** loads visuals from **URLs** in the backend payload only.

### Stable upload filenames (avoid duplicates)

- **Target:** multipart **`targetId`** with `category=target` → server stores **`{targetId}.{ext}`** and overwrites on re-sync (aligned with **`/api/targets/cloud`** naming).
- **Content:** multipart **`contentId`** with `category=content` → **`{contentId}.{ext}`** overwrite semantics.

---

## Backend (Flask)

| Endpoint | Notes |
|----------|------|
| **`GET /api/health`** | `postgresDatabase`, row counts — sanity check vs DB GUI (**database name** must match **`DB_NAME`**, e.g. `ive_ar_gallery`). |
| **`POST /api/upload`** | Multipart `file`, `category` (`content` \| `target` \| `target_ref`), optional **`targetId`** / **`contentId`**. |
| **`POST /api/targets`** | JSON create/update; **`workspaceId`** / optional **`workspaceName`**. |
| **`POST /api/targets/cloud`** | Vuforia + multipart image; **`workspaceId`** / **`workspaceName`**. |
| **`POST /api/targets/<targetId>/reference`** | Optional real-world placement reference photo → **`uploads/target_ref/{targetId}.{ext}`** and **`targets.target_reference_image_url`**. |
| **`POST /api/content`** | JSON; **`contentId`**, **`targetId`**, **`mediaUrl`**, transforms, meta. |
| **`DELETE /api/workspaces/<workspace_id>`** | Cascade: Vuforia cloud targets (`vuforia_target_id` per row), upload URLs + files under **`PUBLIC_BASE_URL`**, **`targets`** (contents cascade), **`workspaces`**. Response includes **`deletedVuforiaTargets`**. Reserved **`default`** workspace returns **403**. |

Environment: see repo **`.env.example`** (`DB_NAME`, `PUBLIC_BASE_URL`, Docker **`DOCKER_DB_PUBLISH`** when host Postgres already uses port **5432**).

---

## Operational notes

### Database GUI vs Docker Postgres

If **`psql` / TablePlus on `127.0.0.1:5432`** shows empty tables while **`/api/health`** shows counts, another Postgres instance often owns **5432**. Use the **published Docker port** (e.g. **`5433`** when **`DOCKER_DB_PUBLISH=5433`**) and database name **`ive_ar_gallery`** (or your **`DB_NAME`**).

### Workspace delete

With **`backendApiBaseUrl`** set on **`WorkspaceSwitcherController`**, the client treats backend delete as authoritative; **404** is treated as OK (never synced).

---

## Validation (Layer 3)

- After sync, **`GET /api/health`** counts match expectations; TablePlus connects to the **same** DB/host/port as the backend.
- Re-sync does not multiply **`uploads/target/`** / **`uploads/content/`** files for the same ids when **`targetId`** / **`contentId`** are sent.
- Deleting a workspace removes related Vuforia cloud targets, server rows, and upload files when the delete API succeeds.

---

## Related code (scripts)

- `AuthoringTool/Assets/Scripts/Workspace/Persistence/WorkspaceRemoteSyncService.cs`
- `AuthoringTool/Assets/Scripts/Api/Http/HttpApiClient.cs`
- `AuthoringTool/Assets/Scripts/WorkspaceSwitcherController.cs` (backend URL + delete flow)
- `backend/app.py` (routes above)
