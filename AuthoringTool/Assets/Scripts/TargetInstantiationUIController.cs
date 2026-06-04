using System;
using System.Collections.Generic;
using System.Globalization;
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
        private const string TargetImagePreviewButtonName = "TargetImagePreviewButton";
        private const string TargetImagePreviewMediaName = "TargetImagePreviewMedia";
        private const string PhysicalWidthInputName = "PhysicalWidthInput";
        private const string VuforiaTargetNameInputName = "VuforiaTargetNameInput";
        private const string SubmitButtonName = "SubmitTargetButton";
        private const string PrevStepButtonName = "PrevStepButton";
        private const string NextStepButtonName = "NextStepButton";
        private const string StepTrackablePanelName = "StepTrackablePanel";
        private const string StepNamesPanelName = "StepNamesPanel";
        private const string StepDot0Name = "StepDot0";
        private const string StepDot1Name = "StepDot1";
        private const string StepSummaryLabelName = "StepSummaryLabel";
        private const string StatusLabelName = "StatusLabel";
        private const string StatusBannerName = "StatusBanner";
        private const string BackToSwitcherButtonName = "BackToSwitcherButton";
        private const string AdvancedToggleButtonName = "AdvancedToggleButton";
        private const string AdvancedSectionName = "AdvancedSection";
        private const string UseCustomDisplayLabelToggleName = "UseCustomDisplayLabelToggle";
        private const string VirtualKeyboardToggleButtonName = "VirtualKeyboardToggleButton";
        private const string VirtualKeyboardPanelName = "VirtualKeyboardPanel";
        private const string VirtualKeyboardTargetLabelName = "VirtualKeyboardTargetLabel";
        private const string VirtualKeyboardKeysContainerName = "VirtualKeyboardKeysContainer";

        private enum TargetSetupStatusKind
        {
            Idle,
            Info,
            Busy,
            Error,
            Success
        }

        private enum ActiveInputTarget
        {
            None,
            WorkspaceName,
            TargetName,
            TargetId,
            DisplayLabel,
            VuforiaTargetName,
            PhysicalWidth
        }

        [SerializeField] private MonoBehaviour apiClientBehaviour;
        [SerializeField] private float createTargetTimeoutSeconds = 120f;

        private IApiClient apiClient;
        private TargetInstantiationSceneController sceneController;

        private TextField workspaceNameInput;
        private TextField targetNameInput;
        private TextField targetIdInput;
        private TextField displayLabelInput;
        private DropdownField targetPostureDropdown;
        private Button targetImagePreviewButton;
        private VisualElement targetImagePreviewMedia;
        private TextField physicalWidthInput;
        private TextField vuforiaTargetNameInput;
        private Button submitButton;
        private Button prevStepButton;
        private Button nextStepButton;
        private Button backToSwitcherButton;
        private VisualElement stepTrackablePanel;
        private VisualElement stepNamesPanel;
        private VisualElement stepDot0;
        private VisualElement stepDot1;
        private Label stepSummaryLabel;
        private Button advancedToggleButton;
        private Label statusLabel;
        private VisualElement statusBanner;
        private VisualElement advancedSection;
        private Toggle useCustomDisplayLabelToggle;
        private Button virtualKeyboardToggleButton;
        private VisualElement virtualKeyboardPanel;
        private Label virtualKeyboardTargetLabel;
        private VisualElement virtualKeyboardKeysContainer;
        private readonly List<Button> virtualKeyboardButtons = new List<Button>();
        private File selectedTargetImageFile;
        private Texture2D previewTexture;


        private const int StepCount = 2;

        private string lastTargetId = "";
        private bool isBusy;
        private bool advancedExpanded;
        private bool keyboardVisible;
        private int currentStep;
        private ActiveInputTarget activeInputTarget = ActiveInputTarget.None;
        private string physicalWidthKeyboardBuffer = "0.2";

        private void OnEnable()
        {
            ConfigureWebGlKeyboardCapture();
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

            VisualElement screenRoot = root.Q<VisualElement>("TargetSetupRoot") ?? root;
            AppFlowWallpaper.Apply(screenRoot);

            BindUi(root);
            ApplyWorkspaceNameFromSession();
            SyncDerivedFieldsFromTargetName();
            RefreshDisplayLabelFieldVisibility();
            isBusy = false;
            currentStep = 0;
            RefreshWizardUi();
            UpdateUiState();
            SetStatus("Click the image area to upload, then use the arrow to continue.", TargetSetupStatusKind.Idle);
        }

        private static void ConfigureWebGlKeyboardCapture()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // Keep keyboard ownership on the WebGL canvas so UIToolkit fields remain responsive.
            WebGLInput.captureAllKeyboardInput = true;
#endif
        }

        private static void EnsureTextFieldFocusOnPointerDown(TextField field)
        {
            if (field == null)
                return;
            field.RegisterCallback<PointerDownEvent>(_ => field.schedule.Execute(field.Focus).ExecuteLater(0));
        }

        private void OnWorkspaceNameFocusIn(FocusInEvent _)
        {
            SetActiveInputTarget(ActiveInputTarget.WorkspaceName);
        }

        private void OnTargetNameFocusIn(FocusInEvent _)
        {
            SetActiveInputTarget(ActiveInputTarget.TargetName);
        }

        private void OnTargetIdFocusIn(FocusInEvent _)
        {
            SetActiveInputTarget(ActiveInputTarget.TargetId);
        }

        private void OnDisplayLabelFocusIn(FocusInEvent _)
        {
            SetActiveInputTarget(ActiveInputTarget.DisplayLabel);
        }

        private void OnVuforiaTargetNameFocusIn(FocusInEvent _)
        {
            SetActiveInputTarget(ActiveInputTarget.VuforiaTargetName);
        }

        private void OnPhysicalWidthFocusIn(FocusInEvent _)
        {
            SetActiveInputTarget(ActiveInputTarget.PhysicalWidth);
        }

        private void SetActiveInputTarget(ActiveInputTarget target)
        {
            activeInputTarget = target;
            if (target == ActiveInputTarget.PhysicalWidth && physicalWidthInput != null)
                physicalWidthKeyboardBuffer = Safe(physicalWidthInput.value);
            RefreshActiveInputVisuals();
            RefreshVirtualKeyboardUi();
        }

        private void RefreshActiveInputVisuals()
        {
            SetFieldActive(workspaceNameInput, activeInputTarget == ActiveInputTarget.WorkspaceName);
            SetFieldActive(targetNameInput, activeInputTarget == ActiveInputTarget.TargetName);
            SetFieldActive(targetIdInput, activeInputTarget == ActiveInputTarget.TargetId);
            SetFieldActive(displayLabelInput, activeInputTarget == ActiveInputTarget.DisplayLabel);
            SetFieldActive(vuforiaTargetNameInput, activeInputTarget == ActiveInputTarget.VuforiaTargetName);
            SetFieldActive(physicalWidthInput, activeInputTarget == ActiveInputTarget.PhysicalWidth);
        }

        private static void SetFieldActive(VisualElement field, bool isActive)
        {
            if (field == null)
                return;
            field.EnableInClassList("target-setup-field--active", isActive);
        }

        private void OnVirtualKeyboardToggleClicked()
        {
            keyboardVisible = !keyboardVisible;
            RefreshVirtualKeyboardUi();
        }

        private void BuildVirtualKeyboard()
        {
            if (virtualKeyboardKeysContainer == null || virtualKeyboardKeysContainer.childCount > 0)
                return;

            AddKeyboardRow(new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", ".", "-" });
            AddKeyboardRow(new[] { "q", "w", "e", "r", "t", "y", "u", "i", "o", "p" });
            AddKeyboardRow(new[] { "a", "s", "d", "f", "g", "h", "j", "k", "l" });
            AddKeyboardRow(new[] { "z", "x", "c", "v", "b", "n", "m" });
            AddKeyboardRow(new[] { "Space", "Backspace", "Clear", "Done" });
        }

        private void AddKeyboardRow(string[] keys)
        {
            if (virtualKeyboardKeysContainer == null || keys == null || keys.Length == 0)
                return;

            var row = new VisualElement();
            row.AddToClassList("target-setup-keyboard__row");
            virtualKeyboardKeysContainer.Add(row);

            for (int i = 0; i < keys.Length; i++)
            {
                string key = keys[i];
                Button button = new Button(() => OnVirtualKeyPressed(key)) { text = key };
                button.AddToClassList("target-setup-keyboard__key");
                if (string.Equals(key, "Space", StringComparison.Ordinal))
                    button.AddToClassList("target-setup-keyboard__key--extra-wide");
                else if (string.Equals(key, "Backspace", StringComparison.Ordinal) || string.Equals(key, "Clear", StringComparison.Ordinal) || string.Equals(key, "Done", StringComparison.Ordinal))
                    button.AddToClassList("target-setup-keyboard__key--wide");
                row.Add(button);
                virtualKeyboardButtons.Add(button);
            }
        }

        private void OnVirtualKeyPressed(string key)
        {
            if (isBusy || activeInputTarget == ActiveInputTarget.None || string.IsNullOrWhiteSpace(key))
                return;

            if (activeInputTarget == ActiveInputTarget.PhysicalWidth)
            {
                ApplyVirtualKeyToPhysicalWidth(key);
                return;
            }

            TextField target = GetActiveTextField();
            if (target == null)
                return;

            string value = target.value ?? "";
            switch (key)
            {
                case "Backspace":
                    if (value.Length > 0)
                        value = value.Substring(0, value.Length - 1);
                    break;
                case "Clear":
                    value = "";
                    break;
                case "Space":
                    value += " ";
                    break;
                case "Done":
                    target.Blur();
                    return;
                default:
                    value += key;
                    break;
            }

            target.value = value;
            target.Focus();
        }

        private TextField GetActiveTextField()
        {
            switch (activeInputTarget)
            {
                case ActiveInputTarget.WorkspaceName: return workspaceNameInput;
                case ActiveInputTarget.TargetName: return targetNameInput;
                case ActiveInputTarget.TargetId: return targetIdInput;
                case ActiveInputTarget.DisplayLabel: return displayLabelInput;
                case ActiveInputTarget.VuforiaTargetName: return vuforiaTargetNameInput;
                default: return null;
            }
        }

        private void ApplyVirtualKeyToPhysicalWidth(string key)
        {
            if (physicalWidthInput == null)
                return;

            string buffer = physicalWidthKeyboardBuffer ?? "";
            switch (key)
            {
                case "Backspace":
                    if (buffer.Length > 0)
                        buffer = buffer.Substring(0, buffer.Length - 1);
                    break;
                case "Clear":
                    buffer = "";
                    break;
                case "Done":
                    physicalWidthInput.Blur();
                    return;
                case "Space":
                    break;
                default:
                    if (key.Length == 1 && ((key[0] >= '0' && key[0] <= '9') || key[0] == '.'))
                    {
                        if (key[0] == '.' && buffer.Contains("."))
                            break;
                        buffer += key;
                    }
                    break;
            }

            physicalWidthKeyboardBuffer = buffer;
            physicalWidthInput.value = buffer;
            physicalWidthInput.Focus();
            RefreshStepSummary();
        }

        private void RefreshVirtualKeyboardUi()
        {
            if (virtualKeyboardPanel != null)
                virtualKeyboardPanel.EnableInClassList("target-setup-keyboard--hidden", !keyboardVisible);
            if (virtualKeyboardToggleButton != null)
            {
                virtualKeyboardToggleButton.text = "";
                virtualKeyboardToggleButton.tooltip = keyboardVisible ? "Hide keyboard" : "Show keyboard";
                virtualKeyboardToggleButton.EnableInClassList("target-setup-keyboard-fab--active", keyboardVisible);
            }
            if (virtualKeyboardTargetLabel != null)
                virtualKeyboardTargetLabel.text = "Typing in: " + ResolveActiveFieldLabel();

            bool hasTarget = activeInputTarget != ActiveInputTarget.None;
            for (int i = 0; i < virtualKeyboardButtons.Count; i++)
                virtualKeyboardButtons[i].SetEnabled(!isBusy && hasTarget);
        }

        private string ResolveActiveFieldLabel()
        {
            switch (activeInputTarget)
            {
                case ActiveInputTarget.WorkspaceName: return "Workspace name";
                case ActiveInputTarget.TargetName: return "Target name";
                case ActiveInputTarget.TargetId: return "Target ID";
                case ActiveInputTarget.DisplayLabel: return "Display label";
                case ActiveInputTarget.VuforiaTargetName: return "Vuforia target name";
                case ActiveInputTarget.PhysicalWidth: return "Physical width (m)";
                default: return "Select a field first";
            }
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
            if (targetImagePreviewButton != null) targetImagePreviewButton.clicked -= OnBrowseTargetImageClicked;
            if (prevStepButton != null) prevStepButton.clicked -= OnPreviousStepClicked;
            if (nextStepButton != null) nextStepButton.clicked -= OnNextStepClicked;
            if (backToSwitcherButton != null) backToSwitcherButton.clicked -= OnBackToSwitcherClicked;
            if (advancedToggleButton != null) advancedToggleButton.clicked -= OnAdvancedToggleClicked;
            if (virtualKeyboardToggleButton != null) virtualKeyboardToggleButton.clicked -= OnVirtualKeyboardToggleClicked;
            if (useCustomDisplayLabelToggle != null) useCustomDisplayLabelToggle.UnregisterValueChangedCallback(OnUseCustomDisplayLabelChanged);
            if (targetNameInput != null) targetNameInput.UnregisterValueChangedCallback(OnTargetNameChanged);
            if (workspaceNameInput != null) workspaceNameInput.UnregisterCallback<FocusInEvent>(OnWorkspaceNameFocusIn);
            if (targetNameInput != null) targetNameInput.UnregisterCallback<FocusInEvent>(OnTargetNameFocusIn);
            if (targetIdInput != null) targetIdInput.UnregisterCallback<FocusInEvent>(OnTargetIdFocusIn);
            if (displayLabelInput != null) displayLabelInput.UnregisterCallback<FocusInEvent>(OnDisplayLabelFocusIn);
            if (vuforiaTargetNameInput != null) vuforiaTargetNameInput.UnregisterCallback<FocusInEvent>(OnVuforiaTargetNameFocusIn);
            if (physicalWidthInput != null) physicalWidthInput.UnregisterCallback<FocusInEvent>(OnPhysicalWidthFocusIn);
            WebGLFileBrowser.FilesWereOpenedEvent -= OnFilesOpened;
        }

        private void OnDestroy()
        {
            if (previewTexture != null)
            {
                Destroy(previewTexture);
                previewTexture = null;
            }
        }

        private void BindUi(VisualElement root)
        {
            workspaceNameInput = root.Q<TextField>(WorkspaceNameInputName);
            targetNameInput = root.Q<TextField>(TargetNameInputName);
            targetIdInput = root.Q<TextField>(TargetIdInputName);
            displayLabelInput = root.Q<TextField>(DisplayLabelInputName);
            targetPostureDropdown = root.Q<DropdownField>(TargetPostureDropdownName);
            targetImagePreviewButton = root.Q<Button>(TargetImagePreviewButtonName);
            targetImagePreviewMedia = root.Q<VisualElement>(TargetImagePreviewMediaName);
            physicalWidthInput = root.Q<TextField>(PhysicalWidthInputName);
            vuforiaTargetNameInput = root.Q<TextField>(VuforiaTargetNameInputName);
            submitButton = root.Q<Button>(SubmitButtonName);
            prevStepButton = root.Q<Button>(PrevStepButtonName);
            nextStepButton = root.Q<Button>(NextStepButtonName);
            stepTrackablePanel = root.Q<VisualElement>(StepTrackablePanelName);
            stepNamesPanel = root.Q<VisualElement>(StepNamesPanelName);
            stepDot0 = root.Q<VisualElement>(StepDot0Name);
            stepDot1 = root.Q<VisualElement>(StepDot1Name);
            stepSummaryLabel = root.Q<Label>(StepSummaryLabelName);
            backToSwitcherButton = root.Q<Button>(BackToSwitcherButtonName);
            advancedToggleButton = root.Q<Button>(AdvancedToggleButtonName);
            advancedSection = root.Q<VisualElement>(AdvancedSectionName);
            statusBanner = root.Q<VisualElement>(StatusBannerName);
            statusLabel = root.Q<Label>(StatusLabelName);
            useCustomDisplayLabelToggle = root.Q<Toggle>(UseCustomDisplayLabelToggleName);
            virtualKeyboardToggleButton = root.Q<Button>(VirtualKeyboardToggleButtonName);
            virtualKeyboardPanel = root.Q<VisualElement>(VirtualKeyboardPanelName);
            virtualKeyboardTargetLabel = root.Q<Label>(VirtualKeyboardTargetLabelName);
            virtualKeyboardKeysContainer = root.Q<VisualElement>(VirtualKeyboardKeysContainerName);

            if (submitButton != null) submitButton.clicked += OnSubmitClicked;
            if (targetImagePreviewButton != null) targetImagePreviewButton.clicked += OnBrowseTargetImageClicked;
            if (prevStepButton != null) prevStepButton.clicked += OnPreviousStepClicked;
            if (nextStepButton != null) nextStepButton.clicked += OnNextStepClicked;
            if (backToSwitcherButton != null)
            {
                backToSwitcherButton.clicked += OnBackToSwitcherClicked;
                backToSwitcherButton.BringToFront();
            }
            if (advancedToggleButton != null) advancedToggleButton.clicked += OnAdvancedToggleClicked;
            if (virtualKeyboardToggleButton != null)
            {
                virtualKeyboardToggleButton.clicked += OnVirtualKeyboardToggleClicked;
                virtualKeyboardToggleButton.BringToFront();
            }
            if (useCustomDisplayLabelToggle != null)
                useCustomDisplayLabelToggle.RegisterValueChangedCallback(OnUseCustomDisplayLabelChanged);
            if (targetNameInput != null)
                targetNameInput.RegisterValueChangedCallback(OnTargetNameChanged);
            if (workspaceNameInput != null) workspaceNameInput.RegisterCallback<FocusInEvent>(OnWorkspaceNameFocusIn);
            if (targetNameInput != null) targetNameInput.RegisterCallback<FocusInEvent>(OnTargetNameFocusIn);
            if (targetIdInput != null) targetIdInput.RegisterCallback<FocusInEvent>(OnTargetIdFocusIn);
            if (displayLabelInput != null) displayLabelInput.RegisterCallback<FocusInEvent>(OnDisplayLabelFocusIn);
            if (vuforiaTargetNameInput != null) vuforiaTargetNameInput.RegisterCallback<FocusInEvent>(OnVuforiaTargetNameFocusIn);
            if (physicalWidthInput != null) physicalWidthInput.RegisterCallback<FocusInEvent>(OnPhysicalWidthFocusIn);
            EnsureTextFieldFocusOnPointerDown(workspaceNameInput);
            EnsureTextFieldFocusOnPointerDown(targetNameInput);
            EnsureTextFieldFocusOnPointerDown(targetIdInput);
            EnsureTextFieldFocusOnPointerDown(displayLabelInput);
            EnsureTextFieldFocusOnPointerDown(vuforiaTargetNameInput);
            EnsureTextFieldFocusOnPointerDown(physicalWidthInput);
            BuildVirtualKeyboard();
            RefreshVirtualKeyboardUi();
            WebGLFileBrowser.FilesWereOpenedEvent -= OnFilesOpened;
            WebGLFileBrowser.FilesWereOpenedEvent += OnFilesOpened;
            ConfigurePostureDropdown();

            ApplyInputValueTextColor();
            UpdateImagePreview();
        }

        private void OnBackToSwitcherClicked()
        {
            if (isBusy)
                return;
            sceneController?.CancelToSwitcher();
        }

        private void OnNextStepClicked()
        {
            if (isBusy || currentStep >= StepCount - 1)
                return;

            if (!TryValidateStep1(out string validationMessage))
            {
                SetStatus(validationMessage, TargetSetupStatusKind.Error);
                return;
            }

            currentStep++;
            RefreshWizardUi();
            SetStatus("Enter workspace and target names, then create.", TargetSetupStatusKind.Idle);
        }

        private void OnPreviousStepClicked()
        {
            if (isBusy || currentStep <= 0)
                return;

            currentStep--;
            RefreshWizardUi();
            SetStatus("Click the image area to upload, then use the arrow to continue.", TargetSetupStatusKind.Idle);
        }

        private void RefreshWizardUi()
        {
            bool onTrackable = currentStep == 0;

            if (stepTrackablePanel != null)
                stepTrackablePanel.EnableInClassList("target-setup-step-card--hidden", !onTrackable);
            if (stepNamesPanel != null)
                stepNamesPanel.EnableInClassList("target-setup-step-card--hidden", onTrackable);

            if (prevStepButton != null)
            {
                bool showPrev = currentStep > 0 && !isBusy;
                prevStepButton.EnableInClassList("target-setup-nav-arrow--hidden", !showPrev);
                prevStepButton.SetEnabled(showPrev);
            }

            if (nextStepButton != null)
            {
                bool showNext = currentStep < StepCount - 1 && !isBusy;
                nextStepButton.EnableInClassList("target-setup-nav-arrow--hidden", !showNext);
                nextStepButton.SetEnabled(showNext);
            }

            if (stepDot0 != null)
                stepDot0.EnableInClassList("target-setup-step-dot--active", currentStep == 0);
            if (stepDot1 != null)
                stepDot1.EnableInClassList("target-setup-step-dot--active", currentStep == 1);

            if (onTrackable)
            {
                if (activeInputTarget != ActiveInputTarget.PhysicalWidth)
                    SetActiveInputTarget(ActiveInputTarget.PhysicalWidth);
            }
            else
            {
                if (activeInputTarget == ActiveInputTarget.PhysicalWidth || activeInputTarget == ActiveInputTarget.None)
                    SetActiveInputTarget(ActiveInputTarget.WorkspaceName);
                workspaceNameInput?.schedule.Execute(() => workspaceNameInput.Focus()).ExecuteLater(0);
                RefreshStepSummary();
            }
        }

        private void RefreshStepSummary()
        {
            if (stepSummaryLabel == null)
                return;

            string fileName = HasValidSelectedTargetImage() ? ResolveSelectedFileName() : "—";
            string posture = Safe(targetPostureDropdown != null ? targetPostureDropdown.value : "");
            float width = ParsePhysicalWidthValue();
            stepSummaryLabel.text = $"Trackable: {fileName} · {posture} · {width:0.###} m wide";
        }

        private void OnAdvancedToggleClicked()
        {
            advancedExpanded = !advancedExpanded;
            RefreshAdvancedSection();
        }

        private void OnUseCustomDisplayLabelChanged(ChangeEvent<bool> evt)
        {
            RefreshDisplayLabelFieldVisibility();
        }

        private void OnTargetNameChanged(ChangeEvent<string> evt)
        {
            SyncDerivedFieldsFromTargetName();
        }

        private void RefreshAdvancedSection()
        {
            if (advancedSection != null)
                advancedSection.EnableInClassList("target-setup-advanced--expanded", advancedExpanded);
            if (advancedToggleButton != null)
                advancedToggleButton.text = advancedExpanded ? "Advanced \u25B4" : "Advanced \u25BE";
        }

        private void RefreshDisplayLabelFieldVisibility()
        {
            bool showCustom = useCustomDisplayLabelToggle != null && useCustomDisplayLabelToggle.value;
            if (displayLabelInput == null)
                return;

            displayLabelInput.EnableInClassList("target-setup-field--conditional--visible", showCustom);
            displayLabelInput.style.display = showCustom ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void SyncDerivedFieldsFromTargetName()
        {
            string targetName = Safe(targetNameInput != null ? targetNameInput.value : "");
            if (targetIdInput != null && !string.IsNullOrWhiteSpace(targetName))
                targetIdInput.SetValueWithoutNotify(NormalizeTargetId(targetIdInput.value, targetName));

            if (useCustomDisplayLabelToggle != null && !useCustomDisplayLabelToggle.value && displayLabelInput != null && !string.IsNullOrWhiteSpace(targetName))
                displayLabelInput.SetValueWithoutNotify(targetName);
        }

        private void OnSubmitClicked()
        {
            if (isBusy)
                return;

            if (apiClient == null)
            {
                SetStatus("No API client available.", TargetSetupStatusKind.Error);
                return;
            }

            string workspaceName = Safe(workspaceNameInput != null ? workspaceNameInput.value : "");
            string targetName = Safe(targetNameInput != null ? targetNameInput.value : "");
            string targetId = NormalizeTargetId(targetIdInput != null ? targetIdInput.value : "", targetName);
            bool useCustomDisplayLabel = useCustomDisplayLabelToggle != null && useCustomDisplayLabelToggle.value;
            string displayLabel = useCustomDisplayLabel
                ? Safe(displayLabelInput != null ? displayLabelInput.value : "")
                : targetName;
            if (string.IsNullOrWhiteSpace(displayLabel))
                displayLabel = targetName;
            if (useCustomDisplayLabel && string.IsNullOrWhiteSpace(displayLabel))
            {
                SetStatus("Enter a display label or disable the custom label option.", TargetSetupStatusKind.Error);
                return;
            }

            string postureValue = Safe(targetPostureDropdown != null ? targetPostureDropdown.value : "");
            float physicalWidth = ParsePhysicalWidthValue();
            bool hasTargetImage = HasValidSelectedTargetImage();

            if (!TryValidateStep1(out string step1Message))
            {
                SetStatus(step1Message, TargetSetupStatusKind.Error);
                return;
            }

            if (!TryValidateStep2(workspaceName, targetName, useCustomDisplayLabel, displayLabel, out string step2Message))
            {
                SetStatus(step2Message, TargetSetupStatusKind.Error);
                return;
            }

            AppFlowController.SetWorkspaceName(workspaceName);
            WorkspaceDomain.WorkspacePosture selectedPosture = ParsePosture(postureValue);

            if (targetIdInput != null)
                targetIdInput.SetValueWithoutNotify(targetId);

            isBusy = true;
            UpdateUiState();
            SetStatus("Creating cloud target… This may take up to a minute for large images.", TargetSetupStatusKind.Busy);

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
                    SetStatus(BuildResultMessage(result), TargetSetupStatusKind.Error);
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
                SetStatus("Target created. Opening authoring…", TargetSetupStatusKind.Success);
                sceneController?.MarkReadyAndContinue(lastTargetId);
            }, createTargetTimeoutSeconds);
        }

        private void UpdateUiState()
        {
            bool enabled = !isBusy;

            if (submitButton != null)
            {
                submitButton.SetEnabled(enabled);
                submitButton.text = isBusy ? "Creating target…" : "Create target & continue";
            }

            if (targetImagePreviewButton != null)
                targetImagePreviewButton.SetEnabled(enabled);
            if (workspaceNameInput != null) workspaceNameInput.SetEnabled(enabled);
            if (targetNameInput != null) targetNameInput.SetEnabled(enabled);
            if (targetIdInput != null) targetIdInput.SetEnabled(enabled);
            if (displayLabelInput != null) displayLabelInput.SetEnabled(enabled);
            if (targetPostureDropdown != null) targetPostureDropdown.SetEnabled(enabled);
            if (physicalWidthInput != null) physicalWidthInput.SetEnabled(enabled);
            if (vuforiaTargetNameInput != null) vuforiaTargetNameInput.SetEnabled(enabled);
            if (useCustomDisplayLabelToggle != null) useCustomDisplayLabelToggle.SetEnabled(enabled);
            if (advancedToggleButton != null) advancedToggleButton.SetEnabled(enabled);
            if (backToSwitcherButton != null) backToSwitcherButton.SetEnabled(enabled);
            if (prevStepButton != null) prevStepButton.SetEnabled(enabled && currentStep > 0);
            if (nextStepButton != null) nextStepButton.SetEnabled(enabled && currentStep < StepCount - 1);
            if (virtualKeyboardToggleButton != null) virtualKeyboardToggleButton.SetEnabled(enabled);

            RefreshWizardUi();
            RefreshVirtualKeyboardUi();
        }

        private bool TryValidateStep1(out string validationMessage)
        {
            string postureValue = Safe(targetPostureDropdown != null ? targetPostureDropdown.value : "");
            float physicalWidth = ParsePhysicalWidthValue();
            return TryValidateStep1Fields(postureValue, physicalWidth, HasValidSelectedTargetImage(), out validationMessage);
        }

        private static bool TryValidateStep1Fields(string postureValue, float physicalWidth, bool hasTargetImage, out string validationMessage)
        {
            var missing = new System.Collections.Generic.List<string>();

            if (!hasTargetImage) missing.Add("trackable image");
            if (string.IsNullOrWhiteSpace(postureValue)) missing.Add("placement");
            if (physicalWidth <= 0f) missing.Add("physical width");

            if (missing.Count == 0)
            {
                validationMessage = null;
                return true;
            }

            validationMessage = "Missing: " + string.Join(", ", missing) + ".";
            return false;
        }

        private static bool TryValidateStep2(string workspaceName, string targetName, bool useCustomDisplayLabel, string displayLabel, out string validationMessage)
        {
            var missing = new System.Collections.Generic.List<string>();

            if (string.IsNullOrWhiteSpace(workspaceName)) missing.Add("workspace name");
            if (string.IsNullOrWhiteSpace(targetName)) missing.Add("target name");
            if (useCustomDisplayLabel && string.IsNullOrWhiteSpace(displayLabel)) missing.Add("display label");

            if (missing.Count == 0)
            {
                validationMessage = null;
                return true;
            }

            validationMessage = "Missing: " + string.Join(", ", missing) + ".";
            return false;
        }

        private void SetStatus(string message, TargetSetupStatusKind kind = TargetSetupStatusKind.Info)
        {
            if (statusLabel != null)
                statusLabel.text = message ?? "";

            if (statusBanner != null)
            {
                statusBanner.EnableInClassList("target-setup-status--idle", kind == TargetSetupStatusKind.Idle);
                statusBanner.EnableInClassList("target-setup-status--info", kind == TargetSetupStatusKind.Info);
                statusBanner.EnableInClassList("target-setup-status--busy", kind == TargetSetupStatusKind.Busy);
                statusBanner.EnableInClassList("target-setup-status--error", kind == TargetSetupStatusKind.Error);
                statusBanner.EnableInClassList("target-setup-status--success", kind == TargetSetupStatusKind.Success);
            }

            Debug.Log($"TargetInstantiationUIController: {message}");
        }

        private void UpdateImagePreview()
        {
            if (targetImagePreviewMedia == null)
                return;

            if (previewTexture != null)
            {
                Destroy(previewTexture);
                previewTexture = null;
            }

            bool hasImage = HasValidSelectedTargetImage();
            if (targetImagePreviewButton != null)
            {
                targetImagePreviewButton.EnableInClassList("target-setup-image-picker--empty", !hasImage);
                targetImagePreviewButton.EnableInClassList("target-setup-image-picker--has-image", hasImage);
            }

            if (!hasImage)
            {
                targetImagePreviewMedia.style.backgroundImage = new StyleBackground(StyleKeyword.None);
                return;
            }

            previewTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!previewTexture.LoadImage(selectedTargetImageFile.data, markNonReadable: false))
            {
                Destroy(previewTexture);
                previewTexture = null;
                targetImagePreviewMedia.style.backgroundImage = new StyleBackground(StyleKeyword.None);
                if (targetImagePreviewButton != null)
                {
                    targetImagePreviewButton.EnableInClassList("target-setup-image-picker--empty", true);
                    targetImagePreviewButton.EnableInClassList("target-setup-image-picker--has-image", false);
                }
                return;
            }

            targetImagePreviewMedia.style.backgroundImage = Background.FromTexture2D(previewTexture);
            targetImagePreviewMedia.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
            targetImagePreviewMedia.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
            targetImagePreviewMedia.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
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
                return "No response from server.";

            if (string.Equals(result.errorCode, "VUFORIA_TIMEOUT", StringComparison.OrdinalIgnoreCase))
                return "Vuforia registration timed out. Try a smaller JPG/PNG or check your network.";

            if (string.Equals(result.errorCode, "VUFORIA_ERROR", StringComparison.OrdinalIgnoreCase))
                return string.IsNullOrWhiteSpace(result.message)
                    ? "Vuforia registration failed."
                    : result.message;

            if (!string.IsNullOrWhiteSpace(result.message))
                return result.message;

            if (!string.IsNullOrWhiteSpace(result.errorCode))
                return result.errorCode;

            return "Could not create cloud target.";
        }

        private static string Safe(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "" : value.Trim();
        }

        private float ParsePhysicalWidthValue()
        {
            string raw = Safe(physicalWidthInput != null ? physicalWidthInput.value : "");
            if (string.IsNullOrWhiteSpace(raw))
                return 0f;
            if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
                return 0f;
            return Mathf.Max(0f, parsed);
        }

        private void OnBrowseTargetImageClicked()
        {
            if (isBusy)
                return;
            EnsureFgFileBrowserPresent();
            if (GameObject.Find("[FGFileBrowser]") == null)
            {
                SetStatus("File browser is not available in this scene.", TargetSetupStatusKind.Error);
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
                SetStatus("Selected image is empty.", TargetSetupStatusKind.Error);
                return;
            }

            selectedTargetImageFile = selected;
            UpdateImagePreview();
            if (currentStep == 0)
                SetStatus("Image ready. Use the arrow to continue.", TargetSetupStatusKind.Info);
            else
                RefreshStepSummary();
        }

        private bool HasValidSelectedTargetImage()
        {
            return selectedTargetImageFile != null && selectedTargetImageFile.data != null && selectedTargetImageFile.data.Length > 0;
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
            Button imagePicker = new Button { name = TargetImagePreviewButtonName, text = "Choose Target Image..." };
            imagePicker.style.marginBottom = 8;
            imagePicker.style.height = 120;
            root.Add(imagePicker);

            TextField width = new TextField("Physical Width (m, required)") { name = PhysicalWidthInputName, value = "0.2" };
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
            row.Add(submit);
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
            targetPostureDropdown.choices = new System.Collections.Generic.List<string> { "Wall", "Floor", "Ceiling" };
            if (string.IsNullOrWhiteSpace(targetPostureDropdown.value)
                || string.Equals(targetPostureDropdown.value, "Select...", StringComparison.Ordinal))
                targetPostureDropdown.SetValueWithoutNotify("Wall");
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
