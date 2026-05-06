using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ARGallery.AppFlow
{
    [RequireComponent(typeof(UIDocument))]
    public class TargetInstantiationUIController : MonoBehaviour
    {
        private const string TargetNameInputName = "TargetNameInput";
        private const string TargetIdInputName = "TargetIdInput";
        private const string DisplayLabelInputName = "DisplayLabelInput";
        private const string TargetImageUrlInputName = "TargetImageUrlInput";
        private const string PhysicalWidthInputName = "PhysicalWidthInput";
        private const string VuforiaTargetNameInputName = "VuforiaTargetNameInput";
        private const string SubmitButtonName = "SubmitTargetButton";
        private const string RetryButtonName = "RetryPublishButton";
        private const string CancelButtonName = "CancelButton";
        private const string StatusLabelName = "StatusLabel";

        [SerializeField] private MonoBehaviour apiClientBehaviour;
        [SerializeField] private float createTargetTimeoutSeconds = 20f;

        private IApiClient apiClient;
        private TargetInstantiationSceneController sceneController;

        private TextField targetNameInput;
        private TextField targetIdInput;
        private TextField displayLabelInput;
        private TextField targetImageUrlInput;
        private FloatField physicalWidthInput;
        private TextField vuforiaTargetNameInput;
        private Button submitButton;
        private Button retryButton;
        private Button cancelButton;
        private Label statusLabel;

        private string lastTargetId = "";
        private bool isBusy;

        private void OnEnable()
        {
            sceneController = FindFirstObjectByType<TargetInstantiationSceneController>();
            apiClient = ResolveApiClient();

            UIDocument uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null || uiDocument.rootVisualElement == null)
            {
                Debug.LogError("TargetInstantiationUIController: UIDocument/root is missing.");
                return;
            }

            VisualElement root = uiDocument.rootVisualElement;
            EnsureFallbackUi(root);
            BindUi(root);
            UpdateUiState();
            SetStatus("Create target to continue.");
        }

        private void OnDisable()
        {
            if (submitButton != null) submitButton.clicked -= OnSubmitClicked;
            if (retryButton != null) retryButton.clicked -= OnRetryClicked;
            if (cancelButton != null) cancelButton.clicked -= OnCancelClicked;
        }

        private void BindUi(VisualElement root)
        {
            targetNameInput = root.Q<TextField>(TargetNameInputName);
            targetIdInput = root.Q<TextField>(TargetIdInputName);
            displayLabelInput = root.Q<TextField>(DisplayLabelInputName);
            targetImageUrlInput = root.Q<TextField>(TargetImageUrlInputName);
            physicalWidthInput = root.Q<FloatField>(PhysicalWidthInputName);
            vuforiaTargetNameInput = root.Q<TextField>(VuforiaTargetNameInputName);
            submitButton = root.Q<Button>(SubmitButtonName);
            retryButton = root.Q<Button>(RetryButtonName);
            cancelButton = root.Q<Button>(CancelButtonName);
            statusLabel = root.Q<Label>(StatusLabelName);

            if (submitButton != null) submitButton.clicked += OnSubmitClicked;
            if (retryButton != null) retryButton.clicked += OnRetryClicked;
            if (cancelButton != null) cancelButton.clicked += OnCancelClicked;
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

            string targetName = Safe(targetNameInput != null ? targetNameInput.value : "");
            string targetId = NormalizeTargetId(targetIdInput != null ? targetIdInput.value : "", targetName);
            string displayLabel = Safe(displayLabelInput != null ? displayLabelInput.value : "");
            string targetImageUrl = Safe(targetImageUrlInput != null ? targetImageUrlInput.value : "");
            float physicalWidth = physicalWidthInput != null ? Mathf.Max(0f, physicalWidthInput.value) : 0f;
            string vuforiaTargetName = Safe(vuforiaTargetNameInput != null ? vuforiaTargetNameInput.value : "");

            if (string.IsNullOrWhiteSpace(targetName) || string.IsNullOrWhiteSpace(targetId) || string.IsNullOrWhiteSpace(targetImageUrl) || physicalWidth <= 0f)
            {
                SetStatus("Missing required fields: target name, target id, target image URL, physical width.");
                return;
            }

            if (targetIdInput != null)
                targetIdInput.SetValueWithoutNotify(targetId);

            if (!LooksLikeImageUrl(targetImageUrl))
                SetStatus("Warning: image URL format looks unusual. Continue with upload gate policy.");

            isBusy = true;
            UpdateUiState();
            SetStatus("Creating target...");

            CreateTargetRequestDto request = new CreateTargetRequestDto
            {
                targetId = targetId,
                targetName = targetName,
                displayLabel = string.IsNullOrWhiteSpace(displayLabel) ? targetName : displayLabel,
                targetImageUrl = targetImageUrl,
                workspaceId = "default",
                physicalWidthM = physicalWidth,
                physicalWidth = physicalWidth,
                vuforiaTargetName = vuforiaTargetName,
                localPosition = new ApiVector3Dto(0f, 0f, 0f),
                localEuler = new ApiVector3Dto(0f, 0f, 0f),
                localScale = new ApiVector3Dto(1f, 1f, 1f),
                meta = new ApiSyncMetaDto
                {
                    schemaVersion = "v1",
                    clientRequestId = Guid.NewGuid().ToString("N"),
                    createdAtUtc = DateTime.UtcNow.ToString("o")
                }
            };

            apiClient.CreateTarget(request, result =>
            {
                if (result == null || !result.success || result.payload == null || string.IsNullOrWhiteSpace(result.payload.targetId))
                {
                    isBusy = false;
                    UpdateUiState();
                    SetStatus($"Create target failed: {BuildResultMessage(result)}");
                    return;
                }

                lastTargetId = result.payload.targetId;
                isBusy = false;
                UpdateUiState();
                SetStatus("Target created. Entering authoring...");
                sceneController?.MarkReadyAndContinue(lastTargetId);
            }, createTargetTimeoutSeconds);
        }

        private void OnRetryClicked()
        {
            if (isBusy || apiClient == null || string.IsNullOrWhiteSpace(lastTargetId))
                return;

            isBusy = true;
            UpdateUiState();
            SetStatus("Legacy publish retry flow removed. Submit a new target request instead.");
            isBusy = false;
            UpdateUiState(failureState: false);
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

            if (retryButton != null)
                retryButton.style.display = failureState ? DisplayStyle.Flex : DisplayStyle.None;

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

        private static bool LooksLikeImageUrl(string url)
        {
            string lower = Safe(url).ToLowerInvariant();
            return lower.EndsWith(".png") || lower.EndsWith(".jpg") || lower.EndsWith(".jpeg") || lower.EndsWith(".webp") || lower.Contains("/uploads/");
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

            Label title = new Label("Target Setup");
            title.style.fontSize = 24;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = Color.white;
            title.style.marginBottom = 12;
            root.Add(title);

            root.Add(MakeTextField(TargetNameInputName, "Target Name (required)"));
            root.Add(MakeTextField(TargetIdInputName, "Target ID (required)"));
            root.Add(MakeTextField(DisplayLabelInputName, "Display Label (optional)"));
            root.Add(MakeTextField(TargetImageUrlInputName, "Target Image URL (required)"));

            FloatField width = new FloatField("Physical Width (m, required)") { name = PhysicalWidthInputName, value = 0.2f };
            width.style.marginBottom = 8;
            root.Add(width);

            root.Add(MakeTextField(VuforiaTargetNameInputName, "Vuforia Target Name (optional)"));

            Label status = new Label("Status") { name = StatusLabelName };
            status.style.color = Color.white;
            status.style.marginTop = 8;
            status.style.marginBottom = 12;
            root.Add(status);

            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;

            Button submit = new Button { name = SubmitButtonName, text = "Create + Publish + Continue" };
            Button retry = new Button { name = RetryButtonName, text = "Retry Publish" };
            Button cancel = new Button { name = CancelButtonName, text = "Cancel" };
            retry.style.marginLeft = 8;
            cancel.style.marginLeft = 8;

            retry.style.display = DisplayStyle.None;
            row.Add(submit);
            row.Add(retry);
            row.Add(cancel);
            root.Add(row);
        }

        private static TextField MakeTextField(string name, string label)
        {
            TextField tf = new TextField(label) { name = name };
            tf.style.marginBottom = 8;
            return tf;
        }
    }
}
