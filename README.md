# IVE AR Gallery

An application suite for **IVE Research Centre** that lets researchers author augmented-reality gallery content at physical locations (corridors, walls, displays) and lets visitors view that content on mobile AR devices.

Researchers use a **web-based authoring tool** to create workspaces, register image targets, upload media (images, video, 3D models), and position content in 3D. A **Flask REST API** with **PostgreSQL** stores workspaces, targets, content metadata, and uploaded files. An **AR mobile app** uses **Vuforia Cloud** image recognition to load and render authored content in situ when a visitor scans a target.

The prototype is designed for deployment on the **IVE network at Mawson Lakes** and can be extended to additional buildings over time.

---

## System architecture

```
┌─────────────────────┐     REST API      ┌──────────────────────────┐
│   AuthoringTool     │ ────────────────► │  Backend (Flask + Postgres) │
│  Unity WebGL /      │                   │  Port 5050                  │
│  Unity Editor       │ ◄──────────────── │  uploads/ + DB              │
└─────────────────────┘                   └─────────────┬────────────┘
                                                          │
                        GET /api/mobileviewer/content/…   │
                                                          ▼
                                              ┌─────────────────────┐
                                              │   MobileViewer      │
                                              │   iOS / Android     │
                                              │   Vuforia Cloud AR  │
                                              └─────────────────────┘
```

| Component | Folder | Role |
|-----------|--------|------|
| **Authoring tool** | `AuthoringTool/` | Create/edit workspaces, targets, and content; save to backend |
| **Backend API** | `backend/` | REST API, file uploads, Vuforia target registration, mobile runtime payload |
| **AR viewer** | `MobileViewer/` | Scan cloud targets; render image, video, and GLB content |
| **API contracts** | `docs/api/` | HTTP request/response documentation |

**Delivery priority (as specified):** authoring tool → backend/API → mobile AR viewer. Headset support (e.g. Meta Quest 3) is planned as a later stage; the current AR viewer targets **iOS and Android** phones.

---

## Prerequisites

| Tool | Version / notes |
|------|-----------------|
| **Docker Desktop** | Recommended for backend + database |
| **Unity** | **6000.3.11f1** (both `AuthoringTool/` and `MobileViewer/`) |
| **Xcode** | iOS device builds |
| **Android Studio / JDK** | Android device builds |
| **Vuforia Engine** | Via Unity packages; cloud credentials for target registration |


---

## Quick start

### 1. Backend

From the repository root:

```bash
cd backend
make up
```

This starts PostgreSQL and the Flask API, runs migrations, and exposes the API at **http://127.0.0.1:5050**.

Verify:

```bash
curl http://127.0.0.1:5050/api/health
make backend-logs   # follow API logs
```

Stop:

```bash
make down
```

Full setup options (local Flask, migrations, tests): see [backend/DEV_SETUP.md](backend/DEV_SETUP.md).

**Optional environment:** create a `.env` file at the **repository root** (not committed). Common variables:

| Variable | Purpose |
|----------|---------|
| `VUFORIA_ACCESS_KEY` / `VUFORIA_SECRET_KEY` | Vuforia Cloud target registration |
| `PUBLIC_BASE_URL` | Upload URL base (use Mac LAN IP for phone demos) |
| `DOCKER_DB_PUBLISH` | Host Postgres port if 5432 is already in use |

---

### 2. Authoring tool

Open **`AuthoringTool/`** in Unity **6000.3.11f1**.

The authoring tool can be run **either in Unity Editor or as a WebGL web app**.

#### Option A — Unity Editor (development)

1. Start the backend (`make up`).
2. Open **`Assets/Scenes/LandingScene.unity`**.
3. Press **Play**.
4. Default API URL: **`http://127.0.0.1:5050`**.

Flow: Landing → workspace switcher → target setup → authoring. Create a workspace, register a target image, add content (image / video / GLB), adjust placement, and save to the server.

#### Option B — WebGL (web app)

1. **File → Build Settings → WebGL** → build to a folder (e.g. `AuthoringTool/Builds/WebGL`).
2. Serve the build locally:

   ```bash
   cd AuthoringTool/Builds/WebGL
   python3 -m http.server 8080
   ```

3. Open **http://localhost:8080** in a browser.
4. The backend must be reachable at **`http://127.0.0.1:5050`** on the same machine (or configure the scene API URL if deployed elsewhere).

---

### 3. MobileViewer (iOS / Android)

Open **`MobileViewer/`** in Unity **6000.3.11f1**.

#### Configure backend URL (important for device builds)

On a **physical phone**, `127.0.0.1` points at the phone itself, not your Mac.

1. Open **`Assets/Scenes/MobileViewerScene.unity`**.
2. Select the object with **`HttpContentService`**.
3. Set **Base Api Url** to your Mac's address on the demo network, e.g. `http://172.20.10.2:5050` (iPhone Personal Hotspot is a common demo setup).
4. Save the scene and rebuild.

Detailed networking steps: [MobileViewer/Assets/Scripts/docs/App/MobileViewerBackendDemoNetworking.md](MobileViewer/Assets/Scripts/docs/App/MobileViewerBackendDemoNetworking.md).

#### Build and run

| Platform | Steps |
|----------|--------|
| **iOS** | File → Build Settings → iOS → Build → open generated Xcode project → run on device |
| **Android** | File → Build Settings → Android → Build And Run (or export Gradle project) |

Entry scene: **`MobileViewerScene.unity`**.

---

## Repository layout

```
.
├── AuthoringTool/          Unity project — web authoring (WebGL + Editor)
├── MobileViewer/           Unity project — iOS / Android AR viewer
├── backend/                Flask API, Docker, migrations, tests
├── docs/api/               REST API contracts
└── README.md               This file
```

---

## Documentation

| Topic | Location |
|-------|----------|
| Backend setup & migrations | [backend/DEV_SETUP.md](backend/DEV_SETUP.md) |
| Backend tests | [backend/TESTING_INSTRUCTION.md](backend/TESTING_INSTRUCTION.md) |
| Mobile ↔ backend networking | [MobileViewer/Assets/Scripts/docs/App/MobileViewerBackendDemoNetworking.md](MobileViewer/Assets/Scripts/docs/App/MobileViewerBackendDemoNetworking.md) |
| Mobile viewer runtime API | [docs/api/mobileviewer/MobileViewerContentRuntime.md](docs/api/mobileviewer/MobileViewerContentRuntime.md) |
| Authoring API overview | [docs/api/authoringtool/README.md](docs/api/authoringtool/README.md) |
| Shared API conventions | [docs/api/common.md](docs/api/common.md) |

---



## Known limitations (prototype)

- **MobileViewer on device** requires a reachable backend URL (LAN / hotspot IP), not `127.0.0.1`.
- **Headset AR** (e.g. Meta Quest 3) is not included in this prototype; mobile phone AR is the current viewer.
- **GLB / 3D models** use runtime URP Lit material fallbacks on WebGL and mobile builds where glTF shader graphs are stripped.
- **Vuforia Cloud** credentials and target registration are required for new physical targets.
- Upload files under `backend/uploads/` are gitignored; demo data must be created via the authoring tool or API.


