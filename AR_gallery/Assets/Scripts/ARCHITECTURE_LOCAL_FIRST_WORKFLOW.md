# Local-First Architecture and Workflow

This document describes:

1. Module integration for network/object/UI layers.
2. User-facing workflow and runtime/API data flow.

---

## Suggested Name and Placement

- **Name**: `ARCHITECTURE_LOCAL_FIRST_WORKFLOW.md`
- **Placement**: `Assets/Scripts/`

Rationale:
- This scope spans `Api`, `Target`, `Content`, and `AuthoringUIController`.
- Keeping it at `Assets/Scripts/` makes it discoverable for Unity frontend contributors.
- It stays close to implementation without being buried in a single module subfolder.

---

## 1) Modules Integration (UML)

```mermaid
classDiagram
direction LR

class AuthoringUIController {
  +OnBrowseButtonClicked()
  +OnCreateTargetButtonClicked()
  +OnSaveButtonClicked()
}

class UploadWorkflowService {
  +UploadSelectedFile(...)
}

class TargetWorkflowService {
  +CreateAndRegisterLocal(...)
  +ApplyTargetImageFromUrl(...)
  +SyncCreateTarget(...)
}

class ContentWorkflowService {
  +SpawnImageLocal(...)
  +SpawnTextLocal(...)
  +SyncCreateContent(...)
}

class RuntimeImageTargetFactory {
  +CreateTarget(...)
}

class RuntimeContentFactory {
  +CreateImageContent(...)
  +CreateTextContent(...)
}

class TargetSelectionManager {
  +AddTarget(...)
  +SetActiveTarget(...)
  +GetActiveTarget()
}

class IApiClient {
  <<interface>>
  +UploadFile(...)
  +CreateTarget(...)
  +CreateContent(...)
}

class HttpApiClient {
  +UploadFile(...)
  +CreateTarget(...)
  +CreateContent(...)
}

class UploadContracts
class TargetContracts
class ContentContracts
class ApiResult~T~
class IApiRequestHandle

class BackendAPI {
  +POST /api/upload
  +POST /api/targets (planned/adapter)
  +POST /api/content
}

AuthoringUIController --> UploadWorkflowService
AuthoringUIController --> TargetWorkflowService
AuthoringUIController --> ContentWorkflowService
AuthoringUIController --> TargetSelectionManager

TargetWorkflowService --> RuntimeImageTargetFactory
TargetWorkflowService --> TargetSelectionManager
ContentWorkflowService --> RuntimeContentFactory

UploadWorkflowService --> IApiClient
TargetWorkflowService --> IApiClient
ContentWorkflowService --> IApiClient

IApiClient <|.. HttpApiClient
HttpApiClient --> BackendAPI

UploadWorkflowService ..> UploadContracts
TargetWorkflowService ..> TargetContracts
ContentWorkflowService ..> ContentContracts
UploadWorkflowService ..> ApiResult~T~
TargetWorkflowService ..> ApiResult~T~
ContentWorkflowService ..> ApiResult~T~
UploadWorkflowService ..> IApiRequestHandle
TargetWorkflowService ..> IApiRequestHandle
ContentWorkflowService ..> IApiRequestHandle
```

---

## 2) User Workflow and Data Flow (UML)

```mermaid
sequenceDiagram
autonumber
actor User
participant UI as AuthoringUIController
participant UWS as UploadWorkflowService
participant TWS as TargetWorkflowService
participant CWS as ContentWorkflowService
participant TS as TargetSelectionManager
participant TF as RuntimeImageTargetFactory
participant CF as RuntimeContentFactory
participant API as IApiClient/HttpApiClient
participant BE as Backend API

%% Target flow
User->>UI: Input target name/id + optional target image
opt Upload target image
  UI->>UWS: UploadSelectedFile(target image)
  UWS->>API: UploadFile(request DTO)
  API->>BE: POST /api/upload
  BE-->>API: Upload URL
  API-->>UWS: ApiResult<UploadFileResponseDto>
  UWS-->>UI: targetImageUrl
end

User->>UI: Click Create Target
UI->>TWS: CreateAndRegisterLocal(...)
TWS->>TF: CreateTarget(...)
TF-->>TWS: targetObject
TWS->>TS: AddTarget(targetObject, active=true)
TWS-->>UI: LocalCreateResult

opt targetImageUrl exists
  UI->>TWS: ApplyTargetImageFromUrl(targetObject, url)
end

UI->>TWS: SyncCreateTarget(...)
TWS->>API: CreateTarget(request DTO)
API->>BE: POST target metadata
BE-->>API: response
API-->>TWS: ApiResult<CreateTargetResponseDto>
TWS-->>UI: sync callback (local target kept on failure)

%% Content flow
User->>UI: Upload content file or input text
opt Upload media file
  UI->>UWS: UploadSelectedFile(content file)
  UWS->>API: UploadFile(request DTO)
  API->>BE: POST /api/upload
  BE-->>API: media URL
  API-->>UWS: ApiResult<UploadFileResponseDto>
  UWS-->>UI: mediaUrl
end

UI->>CWS: SpawnImageLocal(...) or SpawnTextLocal(...)
CWS->>CF: CreateImageContent(...) or CreateTextContent(...)
CF-->>CWS: contentObject
CWS-->>UI: local spawn result
UI->>TS: GetActiveTarget()
UI->>UI: Parent content under Target/ContentRoot

User->>UI: Click Save
UI->>CWS: SyncCreateContent(...)
CWS->>API: CreateContent(request DTO)
API->>BE: POST /api/content
BE-->>API: response
API-->>CWS: ApiResult<CreateContentResponseDto>
CWS-->>UI: sync callback (local content kept on failure)
```

