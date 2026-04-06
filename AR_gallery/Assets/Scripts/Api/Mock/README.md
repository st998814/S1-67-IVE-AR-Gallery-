# Mock API Client

`MockApiClient` is a temporary local implementation of `IApiClient` for DEV-94 sub-task 3.

## Behavior

- Coroutine-based async flow with fixed latency.
- Predictable success responses for:
  - upload file
  - create target
  - create content
- Validation behavior:
  - duplicate `targetId` -> validation error
  - duplicate `contentId` -> validation error
  - missing `targetId` on content -> validation error
- Supports timeout and cancellation through `IApiRequestHandle`.

## Usage

1. Add `MockApiClient` component to a scene object (e.g. `NetworkManager`).
2. Inject/reference it where `IApiClient` is needed in UI/runtime flow.
3. Keep this implementation active until real backend endpoints are ready.
