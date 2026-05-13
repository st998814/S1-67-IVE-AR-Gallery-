using System;
using FrostweepGames.Plugins.WebGLFileBrowser;
using ARGallery.Workspace.Persistence;
using UnityEngine;
using UnityEngine.UIElements;
using WorkspaceDomain = global::ARGallery.Workspace;

namespace ARGallery.AppFlow
{
    [RequireComponent(typeof(UIDocument))]
    public class TargetInstantiationUIController : MonoBehaviour
    {
        private const string WorkspaceNameInputName = "WorkspaceNameInput";
        private const string TargetNameInputName = "TargetNameInput";
        private const string TargetIdInputName = "TargetIdInput";
        private const string DisplayLabelInputName = "DisplayLabelInput";
        private const string TargetPostureDropdownName = "TargetPostureDropdown";
        private const string BrowseTargetImageButtonName = "BrowseTargetImageButton";
        private const string SelectedTargetImageLabelName = "SelectedTargetImageLabel";
        private const string PhysicalWidthInputName = "PhysicalWidthInput";
        private const string VuforiaTargetNameInputName = "VuforiaTargetNameInput";
        private const string SubmitButtonName = "SubmitTargetButton";
        private const string CancelButtonName = "CancelButton";
        private const string StatusLabelName = "StatusLabel";

        [SerializeField] private MonoBehaviour apiClientBehaviour;
        [SerializeField] private float createTargetTimeoutSeconds = 20f;

        private IApiClient apiClient;
        private TargetInstantiationSceneController sceneController;

        private TextField workspaceNameInput;
        private TextField targetNameInput;
        private TextField targetIdInput;
        private TextField displayLabelInput;
        private DropdownField targetPostureDropdown;
        private Button browseTargetImageButton;
        private Label selectedTargetImageLabel;
        private FloatField physicalWidthInput;
        private TextField vuforiaTargetNameInput;
        private Button submitButton;
        private Button cancelButton;
        private Label statusLabel;
        private File selectedTargetImageFile;

        private readonly WorkspaceAssetRepository workspaceAssetRepository = new WorkspaceAssetRepository();
        private readonly WorkspaceSnapshotRepository workspaceSnapshotRepository = new WorkspaceSnapshotRepository();

        private string lastTargetId = "";
        private bool isBusy;

        private void OnEnable()
        {
            sceneController = FindFirstObjectByType<TargetInstantiationSceneController>();
            apiClient = ResolveApiClient();
            EnsureFgFileBrowserPresent();

            UIDocument uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null || uiDocument.rootVisualElement == null)
            {
                Debug.LogError("TargetInstantiationUIController: UIDocument/root is missing.");
                return;
            }

            VisualElement root = uiDocument.rootVisualElement;
            EnsureFallbackUi(root);
            BindUi(root);
            ApplyWorkspaceNameFromSession();
            UpdateUiState();
            SetStatus("Create target to continue.");
        }

        private void ApplyWorkspaceNameFromSession()
        {
            if (workspaceNameInput == null)
                return;
            if (AppFlowController.TryGetWorkspaceSession(out WorkspaceSessionContext session) && session != null
                && !string.IsNullOrWhiteSpace(session.workspaceName))
                workspaceNameInput.SetValueWithoutNotify(session.workspaceName.Trim());
        }

        private void OnDisable()
        {
            if (submitButton != null) submitButton.clicked -= OnSubmitClicked;
            if (browseTargetImageButton != null) browseTargetImageButton.clicked -= OnBrowseTargetImageClicked;
            if (cancelButton != null) cancelButton.clicked -= OnCancelClicked;
            WebGLFileBrowser.FilesWereOpenedEvent -= OnFilesOpened;
        }

        private void BindUi(VisualElement root)
        {
            workspaceNameInput = root.Q<TextField>(WorkspaceNameInputName);
            targetNameInput = root.Q<TextField>(TargetNameInputName);
            targetIdInput = root.Q<TextField>(TargetIdInputName);
            displayLabelInput = root.Q<TextField>(DisplayLabelInputName);
            targetPostureDropdown = root.Q<DropdownField>(TargetPostureDropdownName);
            browseTargetImageButton = root.Q<Button>(BrowseTargetImageButtonName);
            selectedTargetImageLabel = root.Q<Label>(SelectedTargetImageLabelName);
            physicalWidthInput = root.Q<FloatField>(PhysicalWidthInputName);
            vuforiaTargetNameInput = root.Q<TextField>(VuforiaTargetNameInputName);
            submitButton = root.Q<Button>(SubmitButtonName);
            cancelButton = root.Q<Button>(CancelButtonName);
            statusLabel = root.Q<Label>(StatusLabelName);

            if (submitButton != null) submitButton.clicked += OnSubmitClicked;
            if (browseTargetImageButton != null) browseTargetImageButton.clicked += OnBrowseTargetImageClicked;
            if (cancelButton != null) cancelButton.clicked += OnCancelClicked;
            WebGLFileBrowser.FilesWereOpenedEvent -= OnFilesOpened;
            WebGLFileBrowser.FilesWereOpenedEvent += OnFilesOpened;
            ConfigurePostureDropdown();

            ApplyInputValueTextColor();
        }

        private void OnSubmitClicked()
        {
            if (isBusy)
                return;

            if (apiClient == null)
            {
                SetStatus("No API client available.");
                return;
            }

            string workspaceName = Safe(workspaceNameInput != null ? workspaceNameInput.value : "");
            string targetName = Safe(targetNameInput != null ? targetNameInput.value : "");
            string targetId = NormalizeTargetId(targetIdInput != null ? targetIdInput.value : "", targetName);
            string displayLabel = Safe(displayLabelInput != null ? displayLabelInput.value : "");
            string postureValue = Safe(targetPostureDropdown != null ? targetPostureDropdown.value : "");
            float physicalWidth = physicalWidthInput != null ? Mathf.Max(0f, physicalWidthInput.value) : 0f;

            if (string.IsNullOrWhiteSpace(workspaceName) || string.IsNullOrWhiteSpace(targetName) || string.IsNullOrWhiteSpace(displayLabel) || string.IsNullOrWhiteSpace(targetId) || string.IsNullOrWhiteSpace(postureValue) || postureValue == "Select..." || physicalWidth <= 0f || !HasValidSelectedTargetImage())
            {
                SetStatus("Missing required fields: workspace name, target name, display label, target posture, target image file, and physical width.");
                return;
            }

            AppFlowController.SetWorkspaceName(workspaceName);
            WorkspaceDomain.WorkspacePosture selectedPosture = ParsePosture(postureValue);

            if (targetIdInput != null)
                targetIdInput.SetValueWithoutNotify(targetId);

            isBusy = true;
            UpdateUiState();
            SetStatus("Creating cloud target...");

            string cloudWorkspaceId = "default";
            if (AppFlowController.TryGetWorkspaceSession(out WorkspaceSessionContext wsSession) && wsSession != null
                && !string.IsNullOrWhiteSpace(wsSession.workspaceId))
                cloudWorkspaceId = wsSession.workspaceId.Trim();

            CreateCloudTargetRequestDto request = new CreateCloudTargetRequestDto
            {
                targetId = targetId,
                targetName = targetName,
                displayLabel = displayLabel,
                workspaceId = cloudWorkspaceId,
                workspaceName = workspaceName,
                width = physicalWidth,
                localPosition = new ApiVector3Dto(0f, 0f, 0f),
                localEuler = new ApiVector3Dto(0f, 0f, 0f),
                localScale = new ApiVector3Dto(1f, 1f, 1f),
                fileName = ResolveSelectedFileName(),
                fileBytes = selectedTargetImageFile.data,
                meta = new ApiSyncMetaDto
                {
                    schemaVersion = "v1",
                    clientRequestId = Guid.NewGuid().ToString("N"),
                    createdAtUtc = DateTime.UtcNow.ToString("o")
                }
            };

            apiClient.CreateCloudTarget(request, result =>
            {
                if (result == null || !result.success || result.payload == null || string.IsNullOrWhiteSpace(result.payload.targetId))
                {
                    isBusy = false;
                    UpdateUiState();
                    SetStatus($"Create cloud target failed: {BuildResultMessage(result)}");
                    return;
                }

                lastTargetId = result.payload.targetId;
                AppFlowController.SetWorkspaceTargetImage(selectedTargetImageFile != null ? selectedTargetImageFile.data : null, ResolveSelectedFileName());
                AppFlowController.SetWorkspaceVuforiaTargetId(result.payload.vuforiaTargetId ?? "");
                SaveCreatedTargetToWorkspaceDraft(
                    result.payload,
                    workspaceName,
                    targetName,
                    displayLabel,
                    physicalWidth,
                    selectedPosture);
                isBusy = false;
                UpdateUiState();
                SetStatus("Target created. Entering authoring...");
                sceneController?.MarkReadyAndContinue(lastTargetId);
            }, createTargetTimeoutSeconds);
        }

        private void OnCancelClicked()
        {
            if (isBusy)
                return;
            sceneController?.CancelToSwitcher();
        }

        private void UpdateUiState(bool failureState = false)
        {
            if (submitButton != null)
            {
                submitButton.SetEnabled(!isBusy);
                submitButton.text = isBusy ? "Processing..." : "Create + Continue";
            }

            if (browseTargetImageButton != null)
                browseTargetImageButton.SetEnabled(!isBusy);

            if (workspaceNameInput != null)
                workspaceNameInput.SetEnabled(!isBusy);

            if (cancelButton != null)
                cancelButton.SetEnabled(!isBusy);
        }

        private void SetStatus(string message)
        {
            if (statusLabel != null)
                statusLabel.text = message ?? "";
            Debug.Log($"TargetInstantiationUIController: {message}");
        }

        private IApiClient ResolveApiClient()
        {
            IApiClient resolved = apiClientBehaviour as IApiClient;
            if (resolved != null)
                return resolved;

            HttpApiClient found = FindFirstObjectByType<HttpApiClient>();
            if (found != null)
            {
                apiClientBehaviour = found;
                return found;
            }

            HttpApiClient created = gameObject.AddComponent<HttpApiClient>();
            apiClientBehaviour = created;
            return created;
        }

        private static string BuildResultMessage(ApiResult<CreateTargetResponseDto> result)
        {
            if (result == null)
                return "No result";
            if (!string.IsNullOrWhiteSpace(result.message))
                return result.message;
            if (!string.IsNullOrWhiteSpace(result.errorCode))
                return result.errorCode;
            return "Unknown error";
        }

        private static string Safe(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "" : value.Trim();
        }

        private void OnBrowseTargetImageClicked()
        {
            if (isBusy)
                return;
            EnsureFgFileBrowserPresent();
            if (GameObject.Find("[FGFileBrowser]") == null)
            {
                SetStatus("File browser is not available in this scene.");
                return;
            }
            WebGLFileBrowser.OpenFilePanelWithFilters(".png,.jpg,.jpeg", false);
        }

        private void OnFilesOpened(File[] files)
        {
            if (files == null || files.Length == 0)
                return;
            File selected = files[0];
            if (selected == null || selected.data == null || selected.data.Length == 0)
            {
                SetStatus("Selected target image is empty.");
                return;
            }

            selectedTargetImageFile = selected;
            if (selectedTargetImageLabel != null)
                selectedTargetImageLabel.text = $"Selected: {ResolveSelectedFileName()}";
            ImportSelectedTargetImageToPersistentStorage();
            SetStatus("Target image selected. Ready to create.");
        }

        private bool HasValidSelectedTargetImage()
        {
            return selectedTargetImageFile != null && selectedTargetImageFile.data != null && selectedTargetImageFile.data.Length > 0;
        }

        private void ImportSelectedTargetImageToPersistentStorage()
        {
            if (!HasValidSelectedTargetImage())
                return;
            if (!AppFlowController.TryGetWorkspaceSession(out WorkspaceSessionContext session) || session == null || string.IsNullOrWhiteSpace(session.workspaceId))
            {
                Debug.LogWarning("TargetInstantiationUIController: no workspace session; skipped copying target image under persistentDataPath.");
                return;
            }

            string fileName = ResolveSelectedFileName();
            string sourcePath = "";
            if (selectedTargetImageFile.fileInfo != null && !string.IsNullOrWhiteSpace(selectedTargetImageFile.fileInfo.fullName))
                sourcePath = selectedTargetImageFile.fileInfo.fullName.Trim();

            if (!workspaceAssetRepository.TryImportTargetImage(session.workspaceId.Trim(), fileName, selectedTargetImageFile.data, sourcePath, out string relativePath, out string error))
            {
                Debug.LogWarning($"TargetInstantiationUIController: target image import failed: {error}");
                return;
            }

            AppFlowController.SetWorkspaceTargetImageLocalPath(relativePath);
        }

        private string ResolveSelectedFileName()
        {
            if (selectedTargetImageFile?.fileInfo == null)
                return "target.jpg";
            if (!string.IsNullOrWhiteSpace(selectedTargetImageFile.fileInfo.fullName))
                return System.IO.Path.GetFileName(selectedTargetImageFile.fileInfo.fullName.Trim());
            string baseName = string.IsNullOrWhiteSpace(selectedTargetImageFile.fileInfo.name) ? "target" : selectedTargetImageFile.fileInfo.name.TrimEnd('.');
            string ext = selectedTargetImageFile.fileInfo.extension ?? ".jpg";
            if (!ext.StartsWith("."))
                ext = "." + ext;
            return baseName + ext;
        }

        private void EnsureFgFileBrowserPresent()
        {
            if (GameObject.Find("[FGFileBrowser]") != null)
                return;
            GameObject prefab = Resources.Load<GameObject>("[FGFileBrowser]");
            if (prefab != null)
                Instantiate(prefab);
        }

        private void SaveCreatedTargetToWorkspaceDraft(
            CreateTargetResponseDto response,
            string workspaceDisplayName,
            string fallbackTargetName,
            string fallbackDisplayLabel,
            float fallbackPhysicalWidth,
            WorkspaceDomain.WorkspacePosture posture)
        {
            if (response == null || string.IsNullOrWhiteSpace(response.targetId))
                return;
            if (!AppFlowController.TryGetWorkspaceSession(out WorkspaceSessionContext session) || session == null || string.IsNullOrWhiteSpace(session.workspaceId))
                return;

            string resolvedWorkspaceName = Safe(workspaceDisplayName);
            if (string.IsNullOrWhiteSpace(resolvedWorkspaceName))
                resolvedWorkspaceName = string.IsNullOrWhiteSpace(session.workspaceName) ? session.workspaceId.Trim() : session.workspaceName.Trim();

            var draft = new WorkspaceDomain.WorkspaceDraftState
            {
                workspaceId = session.workspaceId.Trim(),
                workspaceName = resolvedWorkspaceName,
                schemaVersion = "v1",
                isDirty = true,
                localModifiedAtUtc = DateTime.UtcNow.ToString("o"),
                target = new WorkspaceDomain.TargetDraftState
                {
                    targetId = response.targetId,
                    targetName = string.IsNullOrWhiteSpace(response.targetName) ? fallbackTargetName : response.targetName,
                    displayLabel = string.IsNullOrWhiteSpace(response.displayLabel) ? fallbackDisplayLabel : response.displayLabel,
                    targetImageUrl = response.targetImageUrl ?? "",
                    physicalWidth = response.physicalWidthM > 0f ? response.physicalWidthM : fallbackPhysicalWidth,
                    posture = posture,
                    vuforiaTargetName = string.IsNullOrWhiteSpace(response.vuforiaTargetId) ? "" : response.vuforiaTargetId
                }
            };

            WorkspaceDomain.WorkspaceDataServices.LocalStore.UpdateWorkspace(draft, markDirty: true);

            string indexName = resolvedWorkspaceName;
            string thumb = string.IsNullOrWhiteSpace(session.thumbnailKey)
                ? (session.targetImageRelativePath ?? "")
                : session.thumbnailKey;
            workspaceSnapshotRepository.UpsertWorkspaceIndexEntry(session.workspaceId.Trim(), indexName, thumb);
        }

        private static string NormalizeTargetId(string targetIdInput, string fallbackName)
        {
            string source = string.IsNullOrWhiteSpace(targetIdInput) ? fallbackName : targetIdInput.Trim();
            source = source.ToLowerInvariant();
            System.Text.StringBuilder sb = new System.Text.StringBuilder(source.Length);
            bool lastWasDash = false;

            for (int i = 0; i < source.Length; i++)
            {
                char c = source[i];
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                {
                    sb.Append(c);
                    lastWasDash = false;
                }
                else if (c == '_' || c == '-' || c == ' ')
                {
                    if (!lastWasDash)
                    {
                        sb.Append('-');
                        lastWasDash = true;
                    }
                }
            }

            string normalized = sb.ToString().Trim('-');
            return normalized.Length == 0 ? "new-target" : normalized;
        }

        private static void EnsureFallbackUi(VisualElement root)
        {
            if (root.Q<Button>(SubmitButtonName) != null)
                return;

            root.Clear();
            root.style.flexGrow = 1f;
            root.style.paddingLeft = 24;
            root.style.paddingRight = 24;
            root.style.paddingTop = 20;
            root.style.paddingBottom = 20;
            root.style.backgroundColor = new Color(0.06f, 0.06f, 0.08f, 1f);

            Label title = new Label("Create the new workspace");
            title.style.fontSize = 24;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = Color.white;
            title.style.marginBottom = 12;
            root.Add(title);

            root.Add(MakeTextField(WorkspaceNameInputName, "Workspace Name (required)"));
            root.Add(MakeTextField(TargetNameInputName, "Target Name (required)"));
            root.Add(MakeTextField(DisplayLabelInputName, "Display Label (required)"));
            var postureDropdown = new DropdownField("Target Posture (required)", new System.Collections.Generic.List<string> { "Select...", "Wall", "Floor", "Ceiling" }, 0)
            {
                name = TargetPostureDropdownName
            };
            postureDropdown.style.marginBottom = 8;
            root.Add(postureDropdown);
            Button browseTargetImage = new Button { name = BrowseTargetImageButtonName, text = "Choose Target Image..." };
            browseTargetImage.style.marginBottom = 6;
            root.Add(browseTargetImage);
            Label selectedImage = new Label("No target image selected.") { name = SelectedTargetImageLabelName };
            selectedImage.style.color = new Color(0.8f, 0.9f, 1f, 1f);
            selectedImage.style.marginBottom = 8;
            root.Add(selectedImage);

            FloatField width = new FloatField("Physical Width (m, required)") { name = PhysicalWidthInputName, value = 0.2f };
            width.style.marginBottom = 8;
            root.Add(width);

            root.Add(MakeTextField(TargetIdInputName, "Target ID (optional, auto-generated)"));
            root.Add(MakeTextField(VuforiaTargetNameInputName, "Vuforia Target Name (optional)"));

            Label status = new Label("Status") { name = StatusLabelName };
            status.style.color = Color.white;
            status.style.marginTop = 8;
            status.style.marginBottom = 12;
            root.Add(status);

            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;

            Button submit = new Button { name = SubmitButtonName, text = "Create + Continue" };
            Button cancel = new Button { name = CancelButtonName, text = "Cancel" };
            cancel.style.marginLeft = 8;
            row.Add(submit);
            row.Add(cancel);
            root.Add(row);
        }

        private static TextField MakeTextField(string name, string label)
        {
            TextField tf = new TextField(label) { name = name };
            tf.style.marginBottom = 8;
            return tf;
        }

        private void ApplyInputValueTextColor()
        {
            ApplyBlackTextToInput(workspaceNameInput);
            ApplyBlackTextToInput(targetNameInput);
            ApplyBlackTextToInput(targetIdInput);
            ApplyBlackTextToInput(displayLabelInput);
            ApplyBlackTextToInput(targetPostureDropdown);
            ApplyBlackTextToInput(vuforiaTargetNameInput);
            ApplyBlackTextToInput(physicalWidthInput);
        }

        private static void ApplyBlackTextToInput(VisualElement field)
        {
            if (field == null)
                return;
            VisualElement input = field.Q(className: "unity-text-input");
            if (input != null)
                input.style.color = Color.black;
        }

        private void ConfigurePostureDropdown()
        {
            if (targetPostureDropdown == null)
                return;
            targetPostureDropdown.choices = new System.Collections.Generic.List<string> { "Select...", "Wall", "Floor", "Ceiling" };
            if (string.IsNullOrWhiteSpace(targetPostureDropdown.value))
                targetPostureDropdown.SetValueWithoutNotify("Select...");
        }

        private static WorkspaceDomain.WorkspacePosture ParsePosture(string value)
        {
            switch (Safe(value).ToLowerInvariant())
            {
                case "floor":
                    return WorkspaceDomain.WorkspacePosture.Floor;
                case "ceiling":
                    return WorkspaceDomain.WorkspacePosture.Ceiling;
                default:
                    return WorkspaceDomain.WorkspacePosture.Wall;
            }
        }
    }
}
