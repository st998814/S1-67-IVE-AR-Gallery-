# AuthoringTool ↔ Backend API contracts

This folder groups API contracts that are **primarily authored for the AuthoringTool Unity client** talking to the backend.

- Shared HTTP and DTO conventions live in `../common.md`.
- Core authoring endpoints are currently documented at the top level:
  - `../target.md` — target CRUD + Vuforia cloud registration.
  - `../content.md` — authoring content persistence and listing.
  - `../upload.md` — file upload for targets/content.

Future authoring-only or authoring-heavy endpoints should be documented here (or referenced from here) so it is clear which Unity surface they serve.

