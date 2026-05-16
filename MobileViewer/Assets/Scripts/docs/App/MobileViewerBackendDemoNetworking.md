# MobileViewer backend demo and networking

## Scope

This document describes how **MobileViewer** on a physical iPhone talks to the **Flask backend** on a Mac for live demos on this branch. It complements the Vuforia prototype notes in `MobileViewerVuforiaCloudInitialisation.md`, which cover cloud recognition and scene wiring; here the focus is **HTTP content loading** and **reachable API URLs**.

---

## What works on this branch

| Layer | Behavior |
|--------|----------|
| **Recognition** | Vuforia Cloud → `VuforiaCloudTargetController` → `TargetContentCoordinator` (unchanged flow). |
| **Content** | `HttpContentService` implements `IContentService` and calls `GET /api/mobileviewer/content/by-target/{targetKey}`. |
| **Rendering** | `ContentRenderer` shows authored **image** content (and legacy mock primitives where the API still returns them). Video/model paths are stubs per API doc. |
| **Authoring** | **AuthoringTool** on the same Mac uses `http://127.0.0.1:5050` against the backend; workspaces and targets you save there are what MobileViewer loads at runtime. |
| **Demo network** | **iPhone Personal Hotspot** is the recommended setup: Mac joins the phone’s Wi‑Fi, both sit on `172.20.10.x`, and the phone uses the **Mac’s hotspot IP** (not `127.0.0.1`). |

Contract reference: `docs/api/mobileviewer/MobileViewerContentRuntime.md`.

---

## End-to-end flow

1. User scans a Vuforia Cloud image target on the device.
2. `TargetContentCoordinator` receives **tracking found** with the Vuforia target id (or name) string.
3. `HttpContentService` requests:
   - `{baseApiUrl}/api/mobileviewer/content/by-target/{targetKey}`
4. Backend resolves `targetKey` (canonical target id or Vuforia id alias) and returns JSON mapped to `ContentData`.
5. `ContentRenderer` attaches content to the tracked target transform; on **tracking lost**, content is hidden and scanning resumes.
6. If the API returns **404**, the user sees a “no content” style message; other errors show a load-failure toast (see coordinator).

**Media URLs:** Responses may include absolute `mediaUrl` values (e.g. `/uploads/...`). The backend rewrites URLs that were stored with `PUBLIC_BASE_URL` to match the **Host** the phone used when calling the API, so localhost-stored links can still work when the client calls `http://<mac-ip>:5050`. See `backend/app.py` (content handler rewrite).

---

## Configuration

### 1. MobileViewer (Unity → device build)

**Component:** `HttpContentService` on the runtime coordinator object in `MobileViewerScene`.

| Inspector field | Purpose |
|-----------------|--------|
| **Base Api Url** | Backend root, **no trailing slash**. Must be reachable **from the phone**. |
| **Content Path** | Default `/api/mobileviewer/content/by-target/` (do not change unless the API changes). |
| **Api Key** | Optional header; leave empty for local demo. |
| **Log Requests / Responses** | Enable for Xcode/device debugging. |

**Important URL rules**

| Client | Use |
|--------|-----|
| **iPhone build** | Mac’s IP on the demo network, e.g. `http://172.20.10.2:5050` on iPhone hotspot. |
| **Unity Play Mode on Mac** | `http://127.0.0.1:5050` is fine (same machine as backend). |
| **Never on device** | `127.0.0.1` (points at the phone itself) or a stale home/office `192.168.x.x` from another network. |

After changing **Base Api Url**, save `MobileViewerScene` and rebuild/install to the device.

**Code default** (new components only): `HttpContentService.cs` defaults to `http://127.0.0.1:5050`; the **scene** serialized value overrides that for builds.

### 2. Backend (Mac)

**Docker (recommended):** from `backend/`:

```bash
make up
make backend-logs   # or: make logs
```

`backend/docker-compose.yml` already sets:

- `SERVER_HOST: 0.0.0.0` — API listens on all interfaces (required for phone → Mac).
- Port **5050** published to the host.

**Repo root `.env`** (optional overrides):

| Variable | Local Editor / same Mac | Phone demo (hotspot) |
|----------|-------------------------|----------------------|
| `SERVER_HOST` | `127.0.0.1` only if running `python backend/app.py` directly without Docker | Docker: already `0.0.0.0`. Local Flask: use `0.0.0.0` if the phone must connect. |
| `PUBLIC_BASE_URL` | `http://127.0.0.1:5050` is OK for AuthoringTool | Set to `http://<mac-hotspot-ip>:5050` if you want every **new** absolute upload URL to match the demo network without relying on rewrite. |

AuthoringTool on the Mac can keep using **`http://127.0.0.1:5050`**; only MobileViewer on the phone needs the LAN/hotspot IP.

**macOS firewall:** Allow inbound TCP **5050** for Python/Docker if the phone cannot connect.

### 3. iPhone Personal Hotspot (recommended demo)

1. Enable hotspot on the iPhone; connect the **Mac** to that Wi‑Fi.
2. On the Mac, find the Wi‑Fi IP (often **`172.20.10.2`**; phone is usually `.1` on that subnet). **System Settings → Network** — the address can change after reconnect.
3. Set MobileViewer **Base Api Url** to `http://<that-ip>:5050`.
4. Confirm from the Mac: `curl http://<that-ip>:5050/api/health` (or open in a browser).

Hotspot avoids most **guest Wi‑Fi client isolation** problems (devices on the same SSID cannot talk to each other). It does **not** remove the need for correct **Base Api Url**, **0.0.0.0** binding, and firewall rules.

### 4. AuthoringTool (same branch, same backend data)

Create or edit targets and content in the AuthoringTool workspace, ensure Vuforia registration succeeds, then scan the **same** cloud target on the phone. MobileViewer loads whatever is persisted for that target key via the runtime API.

---

## Quick validation checklist

- [ ] Backend up: `GET http://127.0.0.1:5050/api/health` on the Mac returns OK.
- [ ] Backend reachable from Mac via hotspot IP: `GET http://172.20.10.x:5050/api/health`.
- [ ] MobileViewer **Base Api Url** matches that IP (device build).
- [ ] Target has content in DB (AuthoringTool or API); scan target → toast **Loading** → **Content loaded** (or expected 404).
- [ ] Xcode/device logs: `[HttpContentService] GET http://...` shows the hotspot IP, not `127.0.0.1`.
- [ ] Image content: `mediaUrl` loads (check rewrite if URLs in JSON still show `127.0.0.1` but request used hotspot IP).

---

## Key files

| Path | Role |
|------|------|
| `MobileViewer/Assets/Scripts/Content/HttpContentService.cs` | HTTP `IContentService` implementation. |
| `MobileViewer/Assets/Scripts/AR/TargetContentCoordinator.cs` | Loads content on track, cancels on lost. |
| `MobileViewer/Assets/Scripts/Content/ContentRenderer.cs` | Renders API payload on target transform. |
| `MobileViewer/Assets/Scenes/MobileViewerScene.unity` | Serialized `baseApiUrl` and service references. |
| `backend/app.py` | Runtime content route, `PUBLIC_BASE_URL`, media URL rewrite. |
| `backend/docker-compose.yml` | `SERVER_HOST`, port publish, env defaults. |
| `docs/api/mobileviewer/MobileViewerContentRuntime.md` | API contract. |

---

## Troubleshooting

| Symptom | Likely cause |
|---------|----------------|
| Connection refused / timeout on phone | Wrong **Base Api Url**, backend bound only to `127.0.0.1`, or firewall blocking 5050. |
| API works on Mac, not on phone | Still using `127.0.0.1` or old `192.168.x.x` in the scene. |
| JSON OK but image missing | `mediaUrl` host not reachable; align `PUBLIC_BASE_URL` or ensure rewrite applies (request must use the same base the rewrite expects). |
| 404 on scan | No content row for that target key / Vuforia id not aliased in backend. |
| Worked yesterday on hotspot | Mac IP changed; update **Base Api Url** and optionally `PUBLIC_BASE_URL`. |

---

## Related docs

- `MobileViewerVuforiaCloudInitialisation.md` — Vuforia cloud bootstrap, coordinator architecture, mock-era prototype notes.
- `docs/api/mobileviewer/MobileViewerContentRuntime.md` — request/response fields.
- `backend/DEV_SETUP.md` — database, Docker, migrations, Vuforia env keys.
