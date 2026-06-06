using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;
using System.Collections.Generic;
using ARGallery.Content;
#if UNITY_WEBGL || UNITY_EDITOR
using FrostweepGames.Plugins.WebGLFileBrowser;
#endif
using ARGallery.Spawning;
using ARGallery.AppFlow;
using ARGallery.Workspace.Persistence;
using System;
using UnityEngine.InputSystem;

public class AuthoringUIController : MonoBehaviour
{
    public GameObject videoPrefab;
    public StyleSheet mobileStyleSheet;

    [SerializeField] private TargetSelectionManager targetSelectionManager;
    [SerializeField] private AuthoringTransformCoordinator authoringTransformCoordinator;
    [SerializeField] private TransformGizmoController transformGizmoController;

    // --- NEW: Prefab Templates (Drag these in the Inspector) ---
    public GameObject picturePrefab;
    public GameObject textPrefab;
    [Tooltip("Optional override. If empty, loads from Resources: Prefabs/ModelContentContainer.")]
    public GameObject modelContentContainerPrefab;

    private const string ModelContentContainerResourcesPath = "Prefabs/ModelContentContainer";
    
    // --- UI Fields ---
    private TextField contentTypeInput;
    private FloatField scaleInput;
    private VisualElement contentPlacementOffsetSection;
    private VisualElement targetPositionSection;
    private Label posLeftRightOffsetLabel;
    private Label posUpDownOffsetLabel;
    private Label posCloserFurtherOffsetLabel;
    private Label posLeftRightRowLabel;
    private Label posUpDownRowLabel;
    private Label posCloserFurtherRowLabel;
    private Label targetPosXLabel;
    private Label targetPosYLabel;
    private Label targetPosZLabel;
    private TextField filePathInput;
    private TextField youtubeUrlInput;
    private DropdownField imageTargetDropdown;
    private TextField createTargetNameInput;
    private TextField createTargetIdInput;
    private TextField createTargetImageUrlInput;
    private Button browseTargetImageButton;
    private Button createTargetButton;

    /// <summary>为 true 时忽略下拉回调，避免与 <see cref="TargetSelectionManager.ActiveTargetChanged"/> 互相触发。</summary>
    private bool suppressTargetDropdownCallbacks;
    
    // --- NEW: Text Spawning Fields ---
    private TextField spawningTextInput;
    private Button spawnTextButton;
    
    // --- TARGET REFERENCE (Target inspector only) ---
    private VisualElement targetReferenceContainer;
    private Button browseTargetReferenceButton;
    private Label targetReferenceStatusLabel;
    private Image targetReferencePreviewImage;
    private Button inspectorTargetTabButton;
    private Button inspectorContentTabButton;

    private Button browseButton, saveButton;
    private Button addContentButton;
    private Button backToSwitcherButton;
    private Button leftPanelToggleButton;
    private Button rightPanelToggleButton;
    private VisualElement leftPanelBody;
    private VisualElement rightPanelBody;
    private Label workspaceNameLabel;
    private Label modeIndicatorLabel;
    private VisualElement topBarModeGroup;
    private VisualElement modeMovePill;
    private VisualElement modeRotatePill;
    private VisualElement modeScalePill;
    private VisualElement modeUniversalPill;
    private VisualElement contentLibraryPanel;
    private bool isLeftPanelExpanded;
    private bool isRightPanelExpanded;

    // --- TASK 6: 新增 UI 变量 ---
    private VisualElement _loadingOverlay;
    private VisualElement _errorToast;
    private Label _errorLabel;
    private Coroutine _errorToastCoroutine;
    private Coroutine _loadingHideRoutine;
    private float _loadingShownAt;
    private VisualElement _syncStatusToast;
    private Label _syncStatusTitle;
    private Label _syncStatusMessage;
    private Button _syncToastDismiss;
    private Label _physicalSizeLabel;
    private Coroutine _syncToastHideRoutine;
    private WorkspaceRemoteSyncService _boundRemoteSyncService;
    // ----------------------------

    // Track the object that is currently "active" in the UI (being dragged)
    private DraggableObject activeDraggedObject;
    /// <summary>当前与面板坐标/缩放绑定的 Transform（含无 DraggableObject 的 Cube 等）。</summary>
    private Transform authoringSpatialTarget;
    private readonly Dictionary<DraggableObject, ContentDraftState> contentDraftsByDraggable = new Dictionary<DraggableObject, ContentDraftState>();
    private readonly Dictionary<Transform, ContentDraftState> contentDraftsByTransform = new Dictionary<Transform, ContentDraftState>();
    private ContentDraftState activeContentDraft;
    private UIDocument uiDocument;

    /// <summary>为 true 时忽略 FloatField 回调，避免从脚本写 UI 时反向改 Transform。</summary>
    private bool suppressSpatialUiCallbacks;

    [SerializeField] private MonoBehaviour apiClientBehaviour;
    [SerializeField] private float createTargetTimeoutSeconds = 20f;
    [SerializeField] private float uploadTimeoutSeconds = 20f;
    [SerializeField] private float createContentTimeoutSeconds = 20f;
    private bool isSaveInProgress;
    private IApiClient apiClient;
    private readonly TargetWorkflowService targetWorkflowService = new TargetWorkflowService();
    private readonly UploadWorkflowService uploadWorkflowService = new UploadWorkflowService();
    private ISpawnerManager spawnerManager;
    private string pendingTargetImageUrl = "";
    private UploadPurpose pendingUploadPurpose = UploadPurpose.Content;
    
    private readonly Dictionary<string, TargetReferenceDraft> targetReferencesByTargetId = new Dictionary<string, TargetReferenceDraft>();
    private string pendingTargetReferenceTargetId;
    private InspectorMode inspectorMode = InspectorMode.Target;
    private TransformGizmoController.GizmoMode _lastKnownGizmoMode = TransformGizmoController.GizmoMode.Translate;
    private readonly AuthoringUIManipulatorPanel _manipulatorPanel = new AuthoringUIManipulatorPanel();
    private PlacementBoundsService _placementBoundsService;

    private const string AddButtonAddIcon = "+";
    private const string AddButtonReplaceIcon = "↻";

    private enum InspectorMode
    {
        Target,
        Content
    }

    private sealed class TargetReferenceDraft
    {
        public byte[] bytes;
        public string fileName;
        public bool isUnsaved;
        public Texture2D previewTexture;
    }

    private enum UploadPurpose
    {
        Content,
        TargetImage,
        TargetReference
    }

    private bool IsWorkspaceReadyForAuthoring(bool showBlockedMessage)
    {
        if (!AppFlowController.TryGetWorkspaceSession(out WorkspaceSessionContext workspace) || workspace == null)
            return true;

        if (workspace.IsReadyForAuthoring())
            return true;

        if (showBlockedMessage)
        {
            ShowError("Target setup is pending. Complete target instantiation before authoring.");
            Debug.LogWarning("AuthoringUIController: blocked action while workspace setup is pending.");
        }

        return false;
    }

    private void RefreshWorkspaceGuardUiState()
    {
        bool ready = IsWorkspaceReadyForAuthoring(showBlockedMessage: false);
        if (saveButton != null) saveButton.SetEnabled(ready);
        if (browseButton != null) browseButton.SetEnabled(ready);
        if (addContentButton != null) addContentButton.SetEnabled(ready);
        if (spawnTextButton != null) spawnTextButton.SetEnabled(ready);
        if (createTargetButton != null) createTargetButton.SetEnabled(ready);
        if (browseTargetImageButton != null) browseTargetImageButton.SetEnabled(ready);
        if (browseTargetReferenceButton != null) browseTargetReferenceButton.SetEnabled(ready);
    }

    private sealed class ContentDraftState
    {
        public string draftId;
        public SpawnContentType contentType;
        public DraggableObject draggableObject;
        public Transform contentTransform;
        public string targetId;

        // Local source fields are prepared for deferred upload/save pipeline.
        public string localFileName;
        public byte[] localFileBytes;
        public string localMimeType;
        public string localObjectUrl;
        public string textPayload;

        public string mediaUrl;
        public bool isUnsaved;
        public bool uploadPending;
        public bool persistPending;
        public string lastError;
    }

    void OnEnable()
    {
        uiDocument = GetComponent<UIDocument>();
        var root = uiDocument.rootVisualElement;

        // Basic Fields
        contentTypeInput = root.Q<TextField>("ContentTypeInput");
        contentPlacementOffsetSection = root.Q<VisualElement>("ContentPlacementOffsetSection");
        targetPositionSection = root.Q<VisualElement>("TargetPositionSection");
        posLeftRightOffsetLabel = root.Q<Label>("PosLeftRightOffsetLabel");
        posUpDownOffsetLabel = root.Q<Label>("PosUpDownOffsetLabel");
        posCloserFurtherOffsetLabel = root.Q<Label>("PosCloserFurtherOffsetLabel");
        posLeftRightRowLabel = root.Q<Label>("PosLeftRightRowLabel");
        posUpDownRowLabel = root.Q<Label>("PosUpDownRowLabel");
        posCloserFurtherRowLabel = root.Q<Label>("PosCloserFurtherRowLabel");
        targetPosXLabel = root.Q<Label>("TargetPosXLabel");
        targetPosYLabel = root.Q<Label>("TargetPosYLabel");
        targetPosZLabel = root.Q<Label>("TargetPosZLabel");
        scaleInput = root.Q<FloatField>("ScaleInput");
        filePathInput = root.Q<TextField>("FilePathInput");
        youtubeUrlInput = root.Q<TextField>("YoutubeUrlInput");
        imageTargetDropdown = root.Q<DropdownField>("ImageTargetDropdown");
        createTargetNameInput = root.Q<TextField>("CreateTargetNameInput");
        createTargetIdInput = root.Q<TextField>("CreateTargetIdInput");
        createTargetImageUrlInput = root.Q<TextField>("CreateTargetImageUrlInput");
        browseTargetImageButton = root.Q<Button>("BrowseTargetImageButton");
        createTargetButton = root.Q<Button>("CreateTargetButton");
        
        // NEW: Text Spawning UI elements
        spawningTextInput = root.Q<TextField>("SpawningTextInput");
        spawnTextButton = root.Q<Button>("SpawnTextButton");
        
        // Target Reference (right panel target inspector only)
        targetReferenceContainer = root.Q<VisualElement>("TargetReferenceContainer");
        browseTargetReferenceButton = root.Q<Button>("BrowseTargetReferenceButton");
        targetReferenceStatusLabel = root.Q<Label>("TargetReferenceStatusLabel");
        targetReferencePreviewImage = root.Q<Image>("TargetReferencePreviewImage");
        inspectorTargetTabButton = root.Q<Button>("InspectorTargetTabButton");
        inspectorContentTabButton = root.Q<Button>("InspectorContentTabButton");
        
        browseButton = root.Q<Button>("BrowseButton");
        addContentButton = root.Q<Button>("AddContentFabButton") ?? root.Q<Button>("AddContentButton");
        saveButton = root.Q<Button>("SaveButton");
        backToSwitcherButton = root.Q<Button>("BackToSwitcherButton");
        leftPanelToggleButton = root.Q<Button>("LeftPanelToggle");
        rightPanelToggleButton = root.Q<Button>("RightPanelToggle");
        leftPanelBody = root.Q<VisualElement>("LeftPanelBody");
        rightPanelBody = root.Q<VisualElement>("RightPanelBody");
        workspaceNameLabel = root.Q<Label>("WorkspaceNameLabel");
        modeIndicatorLabel = root.Q<Label>("ModeIndicatorLabel");
        topBarModeGroup = root.Q<VisualElement>("TopBarModeGroup");
        modeMovePill = root.Q<VisualElement>("ModeMovePill");
        modeRotatePill = root.Q<VisualElement>("ModeRotatePill");
        modeScalePill = root.Q<VisualElement>("ModeScalePill");
        modeUniversalPill = root.Q<VisualElement>("ModeUniversalPill");
        contentLibraryPanel = root.Q<VisualElement>("ContentLibraryPanel")
            ?? root.Q<ListView>("ContentLibraryList")?.parent
            ?? root.Q<ListView>("ContentHierarchyList")?.parent;
        HideContentLibraryPanel();

        // --- TASK 6: 获取并初始化 Loading 和 Error 元素 ---
        _loadingOverlay = root.Q<VisualElement>("loading-overlay");
        _errorToast = root.Q<VisualElement>("error-toast");
        _errorLabel = root.Q<Label>("error-label");

        _syncStatusToast = root.Q<VisualElement>("SyncStatusToast");
        _syncStatusTitle = root.Q<Label>("SyncStatusTitle");
        _syncStatusMessage = root.Q<Label>("SyncStatusMessage");
        _syncToastDismiss = root.Q<Button>("SyncToastDismiss");
        _physicalSizeLabel = root.Q<Label>("PhysicalSizeLabel");

        HideLoading();
        HideErrorToast();
        HideSyncStatusToast();
        if (_syncToastDismiss != null)
            _syncToastDismiss.clicked += OnSyncToastDismissClicked;

        // Event Listeners
        if (browseButton != null) browseButton.clicked += OnBrowseButtonClicked;
        if (addContentButton != null) addContentButton.clicked += OnBrowseButtonClicked;
        if (browseTargetImageButton != null) browseTargetImageButton.clicked += OnBrowseTargetImageButtonClicked;
        if (saveButton != null)
        {
            saveButton.clicked += OnSaveButtonClicked;
            saveButton.BringToFront();
            ResetSaveFabFeedback();
        }
        if (backToSwitcherButton != null)
        {
            backToSwitcherButton.clicked += OnBackToSwitcherButtonClicked;
            backToSwitcherButton.BringToFront();
        }
        if (createTargetButton != null) createTargetButton.clicked += OnCreateTargetButtonClicked;
        if (leftPanelToggleButton != null) leftPanelToggleButton.clicked += OnLeftPanelToggleClicked;
        if (rightPanelToggleButton != null) rightPanelToggleButton.clicked += OnRightPanelToggleClicked;
        if (inspectorTargetTabButton != null) inspectorTargetTabButton.clicked += OnInspectorTargetTabClicked;
        if (inspectorContentTabButton != null) inspectorContentTabButton.clicked += OnInspectorContentTabClicked;

        if (createTargetImageUrlInput != null && string.IsNullOrWhiteSpace(createTargetImageUrlInput.value))
            createTargetImageUrlInput.value = "No target image selected";
        if (workspaceNameLabel != null)
            workspaceNameLabel.text = ResolveWorkspaceDisplayName();
        if (modeIndicatorLabel != null)
            modeIndicatorLabel.text = "Mode: Move";
        InitializePanelCollapsedState();
        
        // NEW: Event Listener for spawning text
        if (spawnTextButton != null) spawnTextButton.clicked += OnSpawnTextButtonClicked;

        // Target reference upload (non-blocking)
        if (browseTargetReferenceButton != null) browseTargetReferenceButton.clicked += OnBrowseTargetReferenceButtonClicked;

        // NEW: Listen for when the user selects a file in the browser
        #if UNITY_WEBGL || UNITY_EDITOR
        WebGLFileBrowser.FilesWereOpenedEvent += OnFilesOpened;
        #endif

        RegisterSpatialFieldCallbacks();

        targetSelectionManager = ResolveTargetSelectionManager();
        authoringTransformCoordinator = ResolveAuthoringTransformCoordinator();
        if (transformGizmoController == null)
            transformGizmoController = FindFirstObjectByType<TransformGizmoController>();
        _placementBoundsService = FindFirstObjectByType<PlacementBoundsService>();
        BindManipulatorBottomPanel(root);
        apiClient = ResolveApiClient();
        spawnerManager = BuildSpawnerManager();

        RefreshImageTargetDropdownChoices();
        if (imageTargetDropdown != null)
            imageTargetDropdown.RegisterValueChangedCallback(OnImageTargetDropdownChanged);
        if (targetSelectionManager != null)
            targetSelectionManager.ActiveTargetChanged += OnManagerActiveTargetChanged;
        if (authoringTransformCoordinator != null)
        {
            authoringTransformCoordinator.ContentSelectionChanged += OnCoordinatorContentSelectionChanged;
            authoringTransformCoordinator.ContentListChanged += OnCoordinatorContentListChanged;
        }
        RefreshWorkspaceGuardUiState();

        // Right panel inspector mode:
        // - if a content object is selected -> content inspector
        // - otherwise -> target inspector
        if (authoringSpatialTarget == null)
            ApplyInspectorModeTarget();
        else
            ApplyInspectorModeContent();

        BindRemoteSyncToastService();
        UpdateAddContentButtonIcon();
    }

    void OnDisable()
    {
        UnregisterSpatialFieldCallbacks();
        #if UNITY_WEBGL || UNITY_EDITOR
        WebGLFileBrowser.FilesWereOpenedEvent -= OnFilesOpened;
        #endif

        if (browseButton != null) browseButton.clicked -= OnBrowseButtonClicked;
        if (addContentButton != null) addContentButton.clicked -= OnBrowseButtonClicked;
        if (browseTargetImageButton != null) browseTargetImageButton.clicked -= OnBrowseTargetImageButtonClicked;
        if (saveButton != null) saveButton.clicked -= OnSaveButtonClicked;
        if (backToSwitcherButton != null) backToSwitcherButton.clicked -= OnBackToSwitcherButtonClicked;
        if (spawnTextButton != null) spawnTextButton.clicked -= OnSpawnTextButtonClicked;
        if (createTargetButton != null) createTargetButton.clicked -= OnCreateTargetButtonClicked;
        if (leftPanelToggleButton != null) leftPanelToggleButton.clicked -= OnLeftPanelToggleClicked;
        if (rightPanelToggleButton != null) rightPanelToggleButton.clicked -= OnRightPanelToggleClicked;
        if (browseTargetReferenceButton != null) browseTargetReferenceButton.clicked -= OnBrowseTargetReferenceButtonClicked;
        if (inspectorTargetTabButton != null) inspectorTargetTabButton.clicked -= OnInspectorTargetTabClicked;
        if (inspectorContentTabButton != null) inspectorContentTabButton.clicked -= OnInspectorContentTabClicked;

        if (imageTargetDropdown != null)
            imageTargetDropdown.UnregisterValueChangedCallback(OnImageTargetDropdownChanged);
        if (targetSelectionManager != null)
            targetSelectionManager.ActiveTargetChanged -= OnManagerActiveTargetChanged;
        if (authoringTransformCoordinator != null)
        {
            authoringTransformCoordinator.ContentSelectionChanged -= OnCoordinatorContentSelectionChanged;
            authoringTransformCoordinator.ContentListChanged -= OnCoordinatorContentListChanged;
        }

        if (_syncToastDismiss != null)
            _syncToastDismiss.clicked -= OnSyncToastDismissClicked;
        UnbindRemoteSyncToastService();

        foreach (TargetReferenceDraft draft in targetReferencesByTargetId.Values)
        {
            if (draft != null && draft.previewTexture != null)
                Destroy(draft.previewTexture);
        }
        targetReferencesByTargetId.Clear();
    }

    private string ResolveWorkspaceDisplayName()
    {
        if (AppFlowController.TryGetWorkspaceSession(out WorkspaceSessionContext workspace)
            && workspace != null
            && !string.IsNullOrWhiteSpace(workspace.workspaceName))
        {
            return workspace.workspaceName.Trim();
        }

        return "Authoring";
    }

    private static void NotifyWorkspacePersistenceChanged()
    {
        WorkspaceAutoSaveService autoSave = UnityEngine.Object.FindFirstObjectByType<WorkspaceAutoSaveService>();
        if (autoSave != null)
            autoSave.NotifyWorkspaceChanged();
        else
            Debug.LogWarning("[WorkspacePersistence] NotifyWorkspacePersistenceChanged: WorkspaceAutoSaveService not found.");
    }

    /// <summary>Used by workspace persistence (<c>WorkspaceSceneReconstructor</c>) to align spawn prefabs with authoring.</summary>
    public GameObject ResolvePersistencePicturePrefab() => picturePrefab;

    public GameObject ResolvePersistenceTextPrefab() => textPrefab;

    public GameObject ResolvePersistenceVideoPrefab() => videoPrefab;

    public GameObject ResolvePersistenceModelContainerPrefab() => GetModelContentContainerPrefab();

    private void InitializePanelCollapsedState()
    {
        isLeftPanelExpanded = false;
        isRightPanelExpanded = false;
        ApplyPanelState();
    }

    private void OnLeftPanelToggleClicked()
    {
        isLeftPanelExpanded = !isLeftPanelExpanded;
        ApplyPanelState();
    }

    private void OnRightPanelToggleClicked()
    {
        isRightPanelExpanded = !isRightPanelExpanded;
        ApplyPanelState();
    }

    private void ApplyPanelState()
    {
        if (leftPanelBody != null)
        {
            leftPanelBody.EnableInClassList("panel--expanded", isLeftPanelExpanded);
            leftPanelBody.EnableInClassList("panel--collapsed", !isLeftPanelExpanded);
            leftPanelBody.style.display = isLeftPanelExpanded ? DisplayStyle.Flex : DisplayStyle.None;
        }
        if (rightPanelBody != null)
        {
            rightPanelBody.EnableInClassList("panel--expanded", isRightPanelExpanded);
            rightPanelBody.EnableInClassList("panel--collapsed", !isRightPanelExpanded);
            rightPanelBody.style.display = isRightPanelExpanded ? DisplayStyle.Flex : DisplayStyle.None;
        }
        if (leftPanelToggleButton != null)
            leftPanelToggleButton.text = isLeftPanelExpanded ? "<" : ">";
        if (rightPanelToggleButton != null)
            rightPanelToggleButton.text = isRightPanelExpanded ? ">" : "<";
    }

    private void HideContentLibraryPanel()
    {
        if (contentLibraryPanel != null)
            contentLibraryPanel.style.display = DisplayStyle.None;

        var list = uiDocument != null
            ? uiDocument.rootVisualElement.Q<ListView>("ContentLibraryList")
                ?? uiDocument.rootVisualElement.Q<ListView>("ContentHierarchyList")
            : null;
        if (list != null)
            list.style.display = DisplayStyle.None;
    }

    private void OnCoordinatorContentSelectionChanged(Transform _)
    {
        _manipulatorPanel.RefreshVisibilityAndValues();
        UpdateAddContentButtonIcon();
    }

    private void OnCoordinatorContentListChanged()
    {
        UpdateAddContentButtonIcon();
    }

    private void UpdateAddContentButtonIcon()
    {
        if (addContentButton == null)
            return;

        bool hasContent = authoringTransformCoordinator != null
            && authoringTransformCoordinator.GetActiveContentEntries() != null
            && authoringTransformCoordinator.GetActiveContentEntries().Count > 0;

        addContentButton.text = hasContent ? AddButtonReplaceIcon : AddButtonAddIcon;
    }

    private ContentReplacementContext ClearActiveTargetContentForReplace()
    {
        authoringTransformCoordinator ??= ResolveAuthoringTransformCoordinator();
        spawnerManager ??= BuildSpawnerManager();
        string targetId = GetActiveTargetIdForSave();
        ContentReplacementContext ctx = ContentReplacementService.ClearActiveTargetContent(
            authoringTransformCoordinator,
            spawnerManager,
            RemoveDraftForTransform,
            () => activeContentDraft = null);
        if (string.IsNullOrWhiteSpace(ctx.targetId) && !string.IsNullOrWhiteSpace(targetId))
            ctx.targetId = targetId;
        return ctx;
    }

    private SpawnContentResult ReplaceActiveTargetContent(SpawnRequest request)
    {
        if (request == null)
        {
            return new SpawnContentResult { success = false, message = "SpawnRequest is null." };
        }

        if (!IsWorkspaceReadyForAuthoring(showBlockedMessage: true))
            return new SpawnContentResult { success = false, message = "Workspace not ready for authoring." };

        spawnerManager ??= BuildSpawnerManager();
        ContentReplacementContext ctx = ClearActiveTargetContentForReplace();
        if (string.IsNullOrWhiteSpace(request.targetId))
            request.targetId = GetActiveTargetIdForSave();

        ContentReplacementService.ApplyContextToSpawnRequest(request, ctx, request.contentType);

        SpawnContentResult outcome = spawnerManager.CreateContent(request);
        if (!outcome.success || outcome.spawnedObject == null)
        {
            ShowError(string.IsNullOrWhiteSpace(outcome.message)
                ? "Content spawn failed."
                : $"Content spawn failed: {outcome.message}");
            return outcome;
        }

        ContentReplacementService.ApplyAuthoredIdentityAfterReplace(outcome.spawnedObject.transform, ctx);
        authoringTransformCoordinator?.SelectContentTransform(outcome.spawnedObject.transform, syncAuthoringUi: false);
        FindFirstObjectByType<SpatialMappingCoordinator>()?.RefreshForCurrentSelection();
        NotifyWorkspacePersistenceChanged();
        UpdateAddContentButtonIcon();
        return outcome;
    }

    private void RemoveDraftForTransform(Transform tr)
    {
        if (tr == null)
            return;

        if (contentDraftsByTransform.TryGetValue(tr, out ContentDraftState byTransform) && byTransform != null)
        {
            if (byTransform.draggableObject != null)
                contentDraftsByDraggable.Remove(byTransform.draggableObject);
            contentDraftsByTransform.Remove(tr);
            if (ReferenceEquals(activeContentDraft, byTransform))
                activeContentDraft = null;
            return;
        }

        DraggableObject draggable = tr.GetComponent<DraggableObject>();
        if (draggable != null)
            contentDraftsByDraggable.Remove(draggable);
    }

    private bool TryDeleteSelectedAuthoredContent()
    {
        if (authoringSpatialTarget == null)
            return false;

        AuthoredContentInstance ci = authoringSpatialTarget.GetComponent<AuthoredContentInstance>();
        if (ci == null)
            return false;

        Transform contentTransform = authoringSpatialTarget;
        GameObject contentObject = contentTransform.gameObject;

        AuthoredObjectRegistry.UnregisterContent(ci);
        RemoveDraftForTransform(contentTransform);

        authoringTransformCoordinator ??= ResolveAuthoringTransformCoordinator();
        authoringTransformCoordinator?.ClearContentSelection(syncAuthoringUi: false);
        ClearAuthoringSpatialSelection();

        spawnerManager ??= BuildSpawnerManager();
        if (spawnerManager == null || !spawnerManager.ReleaseSpawnedContent(contentObject))
            Destroy(contentObject);

        authoringTransformCoordinator?.RefreshActiveContentList();
        FindFirstObjectByType<SpatialMappingCoordinator>()?.RefreshForCurrentSelection();
        NotifyWorkspacePersistenceChanged();
        UpdateAddContentButtonIcon();
        return true;
    }

    // --- TASK 6: 用于独立测试的 Update 方法 ---
    private void Update()
{
    // 按 L 键切换 Loading 状态
    if (Keyboard.current != null && Keyboard.current.lKey.wasPressedThisFrame)
    {
        if (_loadingOverlay != null && _loadingOverlay.style.display == DisplayStyle.None)
            ShowLoading();
        else
            HideLoading();
    }

    // 按 E 键触发报错
    if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
    {
        ShowError("Upload Failed! \n HTTP 500 Internal Server Error\n"+ System.DateTime.Now.ToString("HH:mm:ss"));
    }

    // Delete: release pooled shells or destroy; unregister from sync registry.
    if (Keyboard.current != null && Keyboard.current.deleteKey.wasPressedThisFrame)
        TryDeleteSelectedAuthoredContent();

    // Keep inspector coordinates synced when moving target/content via 3D interaction.
    SyncSpatialInspectorRealtime();
    SyncModeIndicatorLabel();
    _manipulatorPanel.RefreshVisibilityAndValues();
}

    private void SyncSpatialInspectorRealtime()
    {
        // Avoid fighting user typing.
        if (IsAnySpatialFieldFocused())
            return;

        // If the panel elements are missing (right panel trimmed), do nothing.
        if (!HasPlacementOffsetInspectorUi())
            return;

        // Content mode: refresh from selected content transform.
        if (authoringSpatialTarget != null)
        {
            SyncTransformToInspector(authoringSpatialTarget);
            return;
        }

        // Target mode: refresh from active target.
        if (targetSelectionManager == null)
            targetSelectionManager = ResolveTargetSelectionManager();

        SyncTargetToInspector();
    }

    private bool IsAnySpatialFieldFocused()
    {
        if (uiDocument == null || uiDocument.rootVisualElement == null)
            return false;

        var panel = uiDocument.rootVisualElement.panel;
        var focusedElement = panel != null && panel.focusController != null ? panel.focusController.focusedElement : null;

        return _manipulatorPanel.IsManipulatorSliderFocused(focusedElement);
    }

    public bool IsTargetInspectorActive()
    {
        return inspectorMode == InspectorMode.Target;
    }

    private void SyncModeIndicatorLabel()
    {
        if (transformGizmoController == null)
            return;

        // Target inspector does not manipulate content gizmo mode.
        // Hide the mode label in target context.
        if (inspectorMode == InspectorMode.Target)
        {
            if (modeIndicatorLabel != null && modeIndicatorLabel.style.display != DisplayStyle.None)
                modeIndicatorLabel.style.display = DisplayStyle.None;
            if (topBarModeGroup != null && topBarModeGroup.style.display != DisplayStyle.None)
                topBarModeGroup.style.display = DisplayStyle.None;
            SetModePillActive(TransformGizmoController.GizmoMode.Translate, false);
            SetModePillActive(TransformGizmoController.GizmoMode.Rotate, false);
            SetModePillActive(TransformGizmoController.GizmoMode.Scale, false);
            SetModePillActive(TransformGizmoController.GizmoMode.Universal, false);
            _manipulatorPanel.RefreshVisibilityAndValues();
            return;
        }

        if (modeIndicatorLabel != null && modeIndicatorLabel.style.display != DisplayStyle.Flex)
            modeIndicatorLabel.style.display = DisplayStyle.Flex;
        if (topBarModeGroup != null && topBarModeGroup.style.display != DisplayStyle.Flex)
            topBarModeGroup.style.display = DisplayStyle.Flex;

        TransformGizmoController.GizmoMode current = transformGizmoController.CurrentMode;
        if (current != _lastKnownGizmoMode)
        {
            _lastKnownGizmoMode = current;
            if (modeIndicatorLabel != null)
                modeIndicatorLabel.text = "Mode: " + GetModeDisplayName(current);
        }

        SetModePillActive(TransformGizmoController.GizmoMode.Translate, current == TransformGizmoController.GizmoMode.Translate);
        SetModePillActive(TransformGizmoController.GizmoMode.Rotate, current == TransformGizmoController.GizmoMode.Rotate);
        SetModePillActive(TransformGizmoController.GizmoMode.Scale, current == TransformGizmoController.GizmoMode.Scale);
        SetModePillActive(TransformGizmoController.GizmoMode.Universal, current == TransformGizmoController.GizmoMode.Universal);
        _manipulatorPanel.RefreshVisibilityAndValues();
    }

    private void SetModePillActive(TransformGizmoController.GizmoMode mode, bool active)
    {
        VisualElement pill = null;
        switch (mode)
        {
            case TransformGizmoController.GizmoMode.Translate:
                pill = modeMovePill;
                break;
            case TransformGizmoController.GizmoMode.Rotate:
                pill = modeRotatePill;
                break;
            case TransformGizmoController.GizmoMode.Scale:
                pill = modeScalePill;
                break;
            case TransformGizmoController.GizmoMode.Universal:
                pill = modeUniversalPill;
                break;
        }

        if (pill != null)
            pill.EnableInClassList("button--active", active);
    }

    private static string GetModeDisplayName(TransformGizmoController.GizmoMode mode)
    {
        switch (mode)
        {
            case TransformGizmoController.GizmoMode.Translate:
                return "Move";
            case TransformGizmoController.GizmoMode.Rotate:
                return "Rotate";
            case TransformGizmoController.GizmoMode.Scale:
                return "Scale";
            case TransformGizmoController.GizmoMode.Universal:
                return "Universal";
            default:
                return "Move";
        }
    }
    // ----------------------------------------

    // ==========================================
    // --- TASK 6: 全局 Loading 遮罩与 Error 弹窗逻辑 ---
    // ==========================================
    public void ShowLoading()
    {
        if (_loadingOverlay == null) return;
        if (_loadingHideRoutine != null) { StopCoroutine(_loadingHideRoutine); _loadingHideRoutine = null; }
        _loadingShownAt = Time.realtimeSinceStartup;
        _loadingOverlay.style.display = DisplayStyle.Flex;
    }

    public void HideLoading()
    {
        if (_loadingOverlay == null) return;
        float elapsed = Time.realtimeSinceStartup - _loadingShownAt;
        float remaining = 1.5f - elapsed;
        if (remaining > 0f)
        {
            if (_loadingHideRoutine != null) StopCoroutine(_loadingHideRoutine);
            _loadingHideRoutine = StartCoroutine(HideLoadingAfterDelay(remaining));
        }
        else
        {
            _loadingOverlay.style.display = DisplayStyle.None;
        }
    }

    private IEnumerator HideLoadingAfterDelay(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        if (_loadingOverlay != null)
            _loadingOverlay.style.display = DisplayStyle.None;
        _loadingHideRoutine = null;
    }

    public void ShowError(string errorMessage)
    {
        if (_errorToast == null || _errorLabel == null) return;

        _errorLabel.text = errorMessage;
        _errorToast.style.display = DisplayStyle.Flex;

        if (_errorToastCoroutine != null)
        {
            StopCoroutine(_errorToastCoroutine);
        }
        
        _errorToastCoroutine = StartCoroutine(HideErrorToastAfterDelay(3f));
    }

    private void HideErrorToast()
    {
        if (_errorToast != null)
            _errorToast.style.display = DisplayStyle.None;
    }

    private IEnumerator HideErrorToastAfterDelay(float delaySeconds)
    {
        yield return new WaitForSeconds(delaySeconds);
        HideErrorToast();
    }

    private void BindRemoteSyncToastService()
    {
        UnbindRemoteSyncToastService();

        WorkspaceRemoteSyncService svc = ResolveRemoteSyncService();
        if (svc == null)
            return;

        _boundRemoteSyncService = svc;
        _boundRemoteSyncService.RemoteSyncToastChanged += OnRemoteSyncToastChanged;
    }

    private void UnbindRemoteSyncToastService()
    {
        CancelSyncToastHideRoutine();
        if (_boundRemoteSyncService != null)
        {
            _boundRemoteSyncService.RemoteSyncToastChanged -= OnRemoteSyncToastChanged;
            _boundRemoteSyncService = null;
        }

        HideSyncStatusToast();
    }

    private WorkspaceRemoteSyncService ResolveRemoteSyncService()
    {
        return remoteSyncService != null ? remoteSyncService : FindFirstObjectByType<WorkspaceRemoteSyncService>();
    }

    private void OnSyncToastDismissClicked()
    {
        CancelSyncToastHideRoutine();
        HideSyncStatusToast();
    }

    private void OnRemoteSyncToastChanged(WorkspaceRemoteSyncToastKind kind, string message)
    {
        if (kind == WorkspaceRemoteSyncToastKind.Debouncing)
            return;

        if (kind == WorkspaceRemoteSyncToastKind.Syncing)
            ShowLoading();
        else if (kind == WorkspaceRemoteSyncToastKind.Synced || kind == WorkspaceRemoteSyncToastKind.Failed || kind == WorkspaceRemoteSyncToastKind.Skipped)
            HideLoading();

        if (_syncStatusToast == null || _syncStatusTitle == null || _syncStatusMessage == null)
            return;

        CancelSyncToastHideRoutine();

        ApplyRemoteSyncToastStyle(kind);

        if (kind == WorkspaceRemoteSyncToastKind.Synced)
        {
            _syncStatusTitle.style.display = DisplayStyle.None;
            _syncStatusTitle.text = "";
            _syncStatusMessage.text = string.IsNullOrWhiteSpace(message)
                ? "Workspace synchronized with the server."
                : message;
            _syncStatusMessage.style.fontSize = 13;
            _syncStatusMessage.style.unityFontStyleAndWeight = FontStyle.Bold;
        }
        else
        {
            _syncStatusTitle.style.display = DisplayStyle.Flex;
            _syncStatusTitle.text = TitleForRemoteSyncToast(kind);
            _syncStatusMessage.text = string.IsNullOrWhiteSpace(message) ? " " : message;
            _syncStatusMessage.style.fontSize = 11;
            _syncStatusMessage.style.unityFontStyleAndWeight = FontStyle.Normal;
        }

        _syncStatusToast.RemoveFromClassList("sync-toast--hidden");
        _syncStatusToast.style.display = DisplayStyle.Flex;

        float hideDelay = RemoteSyncToastAutoHideSeconds(kind);
        if (hideDelay > 0f)
            _syncToastHideRoutine = StartCoroutine(HideSyncStatusToastAfterDelay(hideDelay));
    }

    private static string TitleForRemoteSyncToast(WorkspaceRemoteSyncToastKind kind)
    {
        switch (kind)
        {
            case WorkspaceRemoteSyncToastKind.Debouncing:
                return "Sync scheduled";
            case WorkspaceRemoteSyncToastKind.Syncing:
                return "Syncing…";
            case WorkspaceRemoteSyncToastKind.Synced:
                return "Saved to server";
            case WorkspaceRemoteSyncToastKind.Failed:
                return "Sync failed";
            case WorkspaceRemoteSyncToastKind.Skipped:
                return "Sync skipped";
            default:
                return "Sync status";
        }
    }

    private static float RemoteSyncToastAutoHideSeconds(WorkspaceRemoteSyncToastKind kind)
    {
        switch (kind)
        {
            case WorkspaceRemoteSyncToastKind.Debouncing:
                return 0f;
            case WorkspaceRemoteSyncToastKind.Syncing:
                return 0f;
            case WorkspaceRemoteSyncToastKind.Synced:
                return 3f;
            case WorkspaceRemoteSyncToastKind.Failed:
                return 8f;
            case WorkspaceRemoteSyncToastKind.Skipped:
                return 5f;
            default:
                return 4f;
        }
    }

    private void ApplyRemoteSyncToastStyle(WorkspaceRemoteSyncToastKind kind)
    {
        if (_syncStatusToast == null)
            return;

        _syncStatusToast.RemoveFromClassList("sync-toast--debouncing");
        _syncStatusToast.RemoveFromClassList("sync-toast--syncing");
        _syncStatusToast.RemoveFromClassList("sync-toast--success");
        _syncStatusToast.RemoveFromClassList("sync-toast--failed");
        _syncStatusToast.RemoveFromClassList("sync-toast--skipped");
        _syncStatusToast.RemoveFromClassList("sync-toast--success-banner");

        switch (kind)
        {
            case WorkspaceRemoteSyncToastKind.Debouncing:
                _syncStatusToast.AddToClassList("sync-toast--debouncing");
                break;
            case WorkspaceRemoteSyncToastKind.Syncing:
                _syncStatusToast.AddToClassList("sync-toast--syncing");
                break;
            case WorkspaceRemoteSyncToastKind.Synced:
                _syncStatusToast.AddToClassList("sync-toast--success");
                _syncStatusToast.AddToClassList("sync-toast--success-banner");
                break;
            case WorkspaceRemoteSyncToastKind.Failed:
                _syncStatusToast.AddToClassList("sync-toast--failed");
                break;
            case WorkspaceRemoteSyncToastKind.Skipped:
                _syncStatusToast.AddToClassList("sync-toast--skipped");
                break;
        }
    }

    private void HideSyncStatusToast()
    {
        if (_syncStatusToast == null)
            return;

        _syncStatusToast.AddToClassList("sync-toast--hidden");
        _syncStatusToast.style.display = DisplayStyle.None;
        _syncStatusToast.RemoveFromClassList("sync-toast--debouncing");
        _syncStatusToast.RemoveFromClassList("sync-toast--syncing");
        _syncStatusToast.RemoveFromClassList("sync-toast--success");
        _syncStatusToast.RemoveFromClassList("sync-toast--failed");
        _syncStatusToast.RemoveFromClassList("sync-toast--skipped");
        _syncStatusToast.RemoveFromClassList("sync-toast--success-banner");
    }

    private void CancelSyncToastHideRoutine()
    {
        if (_syncToastHideRoutine != null)
        {
            StopCoroutine(_syncToastHideRoutine);
            _syncToastHideRoutine = null;
        }
    }

    private IEnumerator HideSyncStatusToastAfterDelay(float delaySeconds)
    {
        yield return new WaitForSeconds(delaySeconds);
        _syncToastHideRoutine = null;
        HideSyncStatusToast();
    }

    private void UpdatePhysicalSizeLabel(Transform t)
    {
        if (_physicalSizeLabel == null) return;
        if (t == null) { _physicalSizeLabel.text = ""; return; }
        Vector3 s = t.localScale;
        int wMm = Mathf.RoundToInt(Mathf.Abs(s.x) * 1000f);
        int hMm = Mathf.RoundToInt(Mathf.Abs(s.y) * 1000f);
        int dMm = Mathf.RoundToInt(Mathf.Abs(s.z) * 1000f);
        _physicalSizeLabel.text = $"Physical size: {wMm}mm × {hMm}mm × {dMm}mm";
    }
    // ==========================================

    // dropdown manu maneger
    private void RefreshImageTargetDropdownChoices()
    {
        if (imageTargetDropdown == null)
            return;

        targetSelectionManager = ResolveTargetSelectionManager();

        var choices = new List<string>();
        if (targetSelectionManager == null || targetSelectionManager.TargetCount == 0)
        {
            choices.Add("— 无 AR Target —");
            imageTargetDropdown.choices = choices;
            imageTargetDropdown.SetEnabled(false);
            imageTargetDropdown.SetValueWithoutNotify(choices[0]);
            return;
        }

        imageTargetDropdown.SetEnabled(true);
        for (int i = 0; i < targetSelectionManager.TargetCount; i++)
            choices.Add(targetSelectionManager.GetTargetDisplayName(i));

        imageTargetDropdown.choices = choices;
        int idx = Mathf.Clamp(targetSelectionManager.ActiveTargetIndex, 0, choices.Count - 1);
        imageTargetDropdown.SetValueWithoutNotify(choices[idx]);
    }

    private void OnImageTargetDropdownChanged(ChangeEvent<string> evt)
    {
        if (suppressTargetDropdownCallbacks || targetSelectionManager == null || imageTargetDropdown == null)
            return;

        int idx = imageTargetDropdown.choices.IndexOf(evt.newValue);
        if (idx < 0)
            return;

        suppressTargetDropdownCallbacks = true;
        try
        {
            targetSelectionManager.SetActiveTarget(idx);
        }
        finally
        {
            suppressTargetDropdownCallbacks = false;
        }
    }

    private void OnManagerActiveTargetChanged(int index)
    {
        UpdateAddContentButtonIcon();
        if (suppressTargetDropdownCallbacks || imageTargetDropdown == null || targetSelectionManager == null)
            return;
        if (targetSelectionManager.TargetCount == 0)
            return;
        if (index < 0 || index >= imageTargetDropdown.choices.Count)
            return;

        suppressTargetDropdownCallbacks = true;
        try
        {
            imageTargetDropdown.SetValueWithoutNotify(imageTargetDropdown.choices[index]);
        }
        finally
        {
            suppressTargetDropdownCallbacks = false;
        }

        // Target switched: refresh right-panel target reference and target coordinates
        // when inspector is currently in target mode.
        if (inspectorMode == InspectorMode.Target)
            ApplyInspectorModeTarget();

        UpdateAddContentButtonIcon();
    }

    private void OnInspectorTargetTabClicked()
    {
        inspectorMode = InspectorMode.Target;
        authoringTransformCoordinator?.ClearContentSelection(syncAuthoringUi: true);
        ApplyInspectorModeTarget();
    }

    private void OnInspectorContentTabClicked()
    {
        inspectorMode = InspectorMode.Content;

        Transform selected = authoringTransformCoordinator != null
            ? authoringTransformCoordinator.GetSelectedContentTransform()
            : null;

        if (selected != null)
        {
            OnContentSelectedInScene(selected);
            return;
        }

        IReadOnlyList<Transform> entries = authoringTransformCoordinator != null
            ? authoringTransformCoordinator.GetActiveContentEntries()
            : null;

        if (entries != null && entries.Count > 0 && entries[0] != null)
        {
            authoringTransformCoordinator.SelectContentTransform(entries[0], syncAuthoringUi: true);
            return;
        }

        // No content to switch to, keep target inspector visible.
        inspectorMode = InspectorMode.Target;
        ApplyInspectorModeTarget();
        ShowError("No content found under active target.");
    }

    private string GetActiveTargetIdForSave()
    {
        if (targetSelectionManager == null || targetSelectionManager.TargetCount == 0)
            return "";
        return targetSelectionManager.GetTargetId(targetSelectionManager.ActiveTargetIndex);
    }

    private static void ResolveSessionWorkspaceForApi(out string workspaceId, out string workspaceName)
    {
        workspaceId = "default";
        workspaceName = "";
        if (AppFlowController.TryGetWorkspaceSession(out WorkspaceSessionContext session) && session != null)
        {
            if (!string.IsNullOrWhiteSpace(session.workspaceId))
                workspaceId = session.workspaceId.Trim();
            if (!string.IsNullOrWhiteSpace(session.workspaceName))
                workspaceName = session.workspaceName.Trim();
        }
    }

    /// <summary>Create and register a new runtime target from UI inputs.</summary>
    private void OnCreateTargetButtonClicked()
    {
        if (!IsWorkspaceReadyForAuthoring(showBlockedMessage: true))
            return;

        targetSelectionManager = ResolveTargetSelectionManager();
        apiClient = ResolveApiClient();
        spawnerManager ??= BuildSpawnerManager();

        if (targetSelectionManager == null)
        {
            Debug.LogError("AuthoringUIController: TargetSelectionManager is missing.");
            return;
        }

        string targetName = createTargetNameInput != null ? createTargetNameInput.value : "";
        string normalizedName = string.IsNullOrWhiteSpace(targetName) ? "NewTarget" : targetName.Trim();
        string targetIdInput = createTargetIdInput != null ? createTargetIdInput.value : "";
        string normalizedTargetId = NormalizeTargetId(targetIdInput, normalizedName);
        string displayLabel = normalizedName;

        if (createTargetIdInput != null)
            createTargetIdInput.SetValueWithoutNotify(normalizedTargetId);

        ResolveSessionWorkspaceForApi(out string wsId, out string wsName);

        var localResult = spawnerManager.CreateTarget(new SpawnTargetRequest
        {
            targetName = normalizedName,
            targetId = normalizedTargetId,
            displayLabel = displayLabel,
            targetImageUrl = GetTargetImageUrlForCreateTarget(),
            workspaceId = wsId,
            workspaceName = wsName
        });

        if (!localResult.success)
        {
            ShowCreateTargetFeedback(localResult.message, isError: true);
            if (localResult.isDuplicate && localResult.duplicateIndex >= 0)
            {
                Debug.LogWarning($"AuthoringUIController: rejected duplicate targetId '{normalizedTargetId}'.");
                targetSelectionManager.SetActiveTarget(localResult.duplicateIndex);
                RefreshImageTargetDropdownChoices();

                // Duplicate target creation: still allow updating the existing target visual with the uploaded target texture.
                GameObject existingTarget = targetSelectionManager.GetTargetAt(localResult.duplicateIndex);
                ApplyPendingTargetImageToTarget(existingTarget);
                NotifyWorkspacePersistenceChanged();
            }
            return;
        }
        RefreshImageTargetDropdownChoices();

        int activeIndex = targetSelectionManager.ActiveTargetIndex;
        if (activeIndex >= 0)
            targetSelectionManager.SetActiveTarget(activeIndex);

        ShowCreateTargetFeedback($"Created: {normalizedTargetId}", isError: false);
        
        // get the target image url
        string targetImageUrl = GetTargetImageUrlForCreateTarget();

        targetWorkflowService.ApplyTargetImageFromUrl(this, localResult.targetObject, targetImageUrl);
        NotifyWorkspacePersistenceChanged();

        // create target  , save to database
        spawnerManager.BeginSyncCreateTarget(
            apiClient,
            new SpawnTargetRequest
            {
                targetName = normalizedName,
                targetId = normalizedTargetId,
                displayLabel = displayLabel,
                targetImageUrl = targetImageUrl,
                workspaceId = wsId,
                workspaceName = wsName
            },
            localResult.targetObject,
            OnCreateTargetSyncCompleted,
            createTargetTimeoutSeconds);
    }

    private void ApplyPendingTargetImageToTarget(GameObject targetObject)
    {
        string targetImageUrl = GetTargetImageUrlForCreateTarget();
        if (string.IsNullOrWhiteSpace(targetImageUrl) || targetObject == null)
            return;

        targetWorkflowService.ApplyTargetImageFromUrl(this, targetObject, targetImageUrl);
    }

    private TargetSelectionManager ResolveTargetSelectionManager()
    {
        if (targetSelectionManager != null)
            return targetSelectionManager;

        targetSelectionManager = FindFirstObjectByType<TargetSelectionManager>();
        if (targetSelectionManager != null)
            return targetSelectionManager;

        TargetSelectionManager[] candidates = Resources.FindObjectsOfTypeAll<TargetSelectionManager>();
        foreach (TargetSelectionManager candidate in candidates)
        {
            if (candidate == null || !candidate.gameObject.scene.IsValid())
                continue;
            targetSelectionManager = candidate;
            break;
        }

        return targetSelectionManager;
    }

    private AuthoringTransformCoordinator ResolveAuthoringTransformCoordinator()
    {
        if (authoringTransformCoordinator != null)
            return authoringTransformCoordinator;

        authoringTransformCoordinator = FindFirstObjectByType<AuthoringTransformCoordinator>();
        if (authoringTransformCoordinator == null)
        {
            var all = Resources.FindObjectsOfTypeAll<AuthoringTransformCoordinator>();
            foreach (AuthoringTransformCoordinator c in all)
            {
                if (c != null && c.gameObject.scene.IsValid())
                {
                    authoringTransformCoordinator = c;
                    break;
                }
            }
        }

        return authoringTransformCoordinator;
    }

    private IApiClient ResolveApiClient()
    {
        if (apiClient != null)
            return apiClient;

        if (apiClientBehaviour != null)
        {
            apiClient = apiClientBehaviour as IApiClient;
            if (apiClient == null)
                Debug.LogWarning("AuthoringUIController: apiClientBehaviour does not implement IApiClient.");
        }

        if (apiClient == null)
        {
            HttpApiClient http = FindFirstObjectByType<HttpApiClient>();
            if (http != null)
            {
                apiClient = http;
                apiClientBehaviour = http;
                return apiClient;
            }
        }

        if (apiClient == null)
        {
            HttpApiClient created = gameObject.AddComponent<HttpApiClient>();
            apiClient = created;
            apiClientBehaviour = created;
            Debug.LogWarning("AuthoringUIController: No API client found in scene. Added HttpApiClient to this GameObject.");
        }

        return apiClient;
    }

    private string GetTargetImageUrlForCreateTarget()
    {
        return string.IsNullOrWhiteSpace(pendingTargetImageUrl) ? "" : pendingTargetImageUrl.Trim();
    }

    private void OnCreateTargetSyncCompleted(ApiResult<CreateTargetResponseDto> result)
    {
        if (result != null && result.success && result.payload != null && !string.IsNullOrWhiteSpace(result.payload.targetId))
        {
            string tid = result.payload.targetId.Trim();
            AuthoredObjectRegistry registry = AuthoredObjectRegistry.Instance;
            if (registry != null)
            {
                foreach (AuthoredTargetInstance t in registry.GetTargetsOrdered())
                {
                    if (t == null)
                        continue;
                    if (string.Equals(t.ServerTargetId, tid, StringComparison.Ordinal)
                        || string.Equals(t.LocalTargetId, tid, StringComparison.Ordinal))
                    {
                        t.RemoteDirty = false;
                        t.LastRemoteSyncedAtUtc = DateTime.UtcNow.ToString("o");
                        break;
                    }
                }
            }

            Debug.Log($"CreateTarget sync success: {tid}");
            return;
        }

        string code = result != null ? result.errorCode : ApiErrorCodes.Unknown;
        string message = result != null ? result.message : "No result";
        Debug.LogWarning($"CreateTarget sync failed (local target kept): [{code}] {message}");
    }

    /// <summary>
    /// Normalize the target id.
    /// </summary>
    private string NormalizeTargetId(string targetIdInput, string fallbackName)
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
                continue;
            }

            if (c == '_' || c == '-' || c == ' ')
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

    /// <summary>
    /// Show the create target feedback.
    /// </summary>
    private void ShowCreateTargetFeedback(string message, bool isError)
    {
        if (createTargetButton == null)
            return;

        string original = "Create Target";
        createTargetButton.text = message;
        createTargetButton.schedule.Execute(() => { createTargetButton.text = original; }).StartingIn(isError ? 2600 : 1600);
    }

    private void RegisterSpatialFieldCallbacks()
    {
        if (scaleInput != null)
            scaleInput.RegisterValueChangedCallback(OnScaleFloatFieldChanged);
    }

    private void UnregisterSpatialFieldCallbacks()
    {
        if (scaleInput != null)
            scaleInput.UnregisterValueChangedCallback(OnScaleFloatFieldChanged);
    }

    private void OnScaleFloatFieldChanged(ChangeEvent<float> _)
    {
        if (suppressSpatialUiCallbacks || authoringSpatialTarget == null || scaleInput == null)
            return;

        ContentTransformManipulator manipulator = ResolveContentManipulator();
        if (manipulator != null)
            manipulator.SetUniformScale(authoringSpatialTarget, scaleInput.value);
        else
            authoringSpatialTarget.localScale = Vector3.one * Mathf.Max(0.01f, scaleInput.value);

        MarkActiveDraftDirty();
        _manipulatorPanel.RefreshVisibilityAndValues();
    }

    /// <summary>用于场景点击选中 / Gizmo 拖拽后，把 Transform 写回面板（位置 + 均匀缩放）。</summary>
    public void SyncTransformToInspector(Transform target)
{
    if (target == null || !HasPlacementOffsetInspectorUi())
        return;

    Vector3 lp = target.localPosition;
    SemanticAxisMapping.PlacementPosture posture = ResolvePlacementPosture();
    ApplyPlacementOffsetRowLabels(posture);
    SemanticDistanceFormatter.FormatOffsets(posture, lp, out string leftRight, out string upDown, out string closerFurther);
    SetLabelText(posLeftRightOffsetLabel, leftRight);
    SetLabelText(posUpDownOffsetLabel, upDown);
    SetLabelText(posCloserFurtherOffsetLabel, closerFurther);

    if (scaleInput != null)
    {
        suppressSpatialUiCallbacks = true;
        try
        {
            scaleInput.value = (float)System.Math.Round(target.localScale.x, 2);
        }
        finally
        {
            suppressSpatialUiCallbacks = false;
        }
    }

    UpdatePhysicalSizeLabel(target);
    _manipulatorPanel.RefreshVisibilityAndValues();
}
    private void ApplyInspectorModeContent()
    {
        inspectorMode = InspectorMode.Content;
        if (targetReferenceContainer != null)
            targetReferenceContainer.style.display = DisplayStyle.None;
        UpdatePlacementInspectorSectionVisibility();
        UpdateInspectorModeTabVisualState();
        _manipulatorPanel.RefreshVisibilityAndValues();
    }

    private void ApplyInspectorModeTarget()
    {
        inspectorMode = InspectorMode.Target;
        if (targetReferenceContainer != null)
            targetReferenceContainer.style.display = DisplayStyle.Flex;

        UpdatePlacementInspectorSectionVisibility();
        SyncTargetToInspector();
        RefreshTargetReferenceUiForActiveTarget();
        UpdateTargetReferenceStatusLabel(showUploadingText: false);
        UpdateInspectorModeTabVisualState();
        _manipulatorPanel.RefreshVisibilityAndValues();
    }

    private void UpdateInspectorModeTabVisualState()
    {
        bool isTarget = inspectorMode == InspectorMode.Target;
        if (inspectorTargetTabButton != null)
            inspectorTargetTabButton.EnableInClassList("button--active", isTarget);
        if (inspectorContentTabButton != null)
            inspectorContentTabButton.EnableInClassList("button--active", !isTarget);
    }

    private void SyncTargetToInspector()
    {
        if (!HasTargetPositionInspectorUi())
            return;

        if (targetSelectionManager == null)
            targetSelectionManager = ResolveTargetSelectionManager();

        Vector3 lp = Vector3.zero;
        GameObject activeTarget = targetSelectionManager != null ? targetSelectionManager.GetActiveTarget() : null;
        if (activeTarget != null)
            lp = activeTarget.transform.localPosition;

        SetLabelText(targetPosXLabel, SemanticDistanceFormatter.FormatTargetAxisComponent('X', lp.x));
        SetLabelText(targetPosYLabel, SemanticDistanceFormatter.FormatTargetAxisComponent('Y', lp.y));
        SetLabelText(targetPosZLabel, SemanticDistanceFormatter.FormatTargetAxisComponent('Z', lp.z));

        if (scaleInput != null)
        {
            suppressSpatialUiCallbacks = true;
            try
            {
                scaleInput.value = 1;
            }
            finally
            {
                suppressSpatialUiCallbacks = false;
            }
        }

        GameObject sizeTarget = targetSelectionManager != null ? targetSelectionManager.GetActiveTarget() : null;
        UpdatePhysicalSizeLabel(sizeTarget != null ? sizeTarget.transform : null);
    }

    private void UpdatePlacementInspectorSectionVisibility()
    {
        bool isTarget = inspectorMode == InspectorMode.Target;
        if (contentPlacementOffsetSection != null)
            contentPlacementOffsetSection.style.display = isTarget ? DisplayStyle.None : DisplayStyle.Flex;
        if (targetPositionSection != null)
            targetPositionSection.style.display = isTarget ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private bool HasPlacementOffsetInspectorUi()
    {
        return posLeftRightOffsetLabel != null
            && posUpDownOffsetLabel != null
            && posCloserFurtherOffsetLabel != null;
    }

    private bool HasTargetPositionInspectorUi()
    {
        return targetPosXLabel != null && targetPosYLabel != null && targetPosZLabel != null;
    }

    private static void SetLabelText(Label label, string text)
    {
        if (label != null)
            label.text = text;
    }

    private void UpdateTargetReferenceStatusLabel(bool showUploadingText)
    {
        if (targetReferenceStatusLabel == null)
            return;

        if (showUploadingText)
        {
            targetReferenceStatusLabel.text = "Uploading...";
            // Keep it neutral/primary-ish while uploading.
            targetReferenceStatusLabel.style.color = new StyleColor(new Color32(229, 231, 235, 255));
            return;
        }

        AuthoredTargetInstance authored = ResolveAuthoredTargetForActiveSelection();
        TargetReferenceDraft activeDraft = GetActiveTargetReferenceDraft();
        if (activeDraft != null && activeDraft.bytes != null && activeDraft.isUnsaved)
        {
            string suffix = string.IsNullOrWhiteSpace(activeDraft.fileName) ? "" : $" ({activeDraft.fileName})";
            targetReferenceStatusLabel.text = $"Saved locally — sync to upload{suffix}";
            targetReferenceStatusLabel.style.color = new StyleColor(new Color32(220, 53, 69, 255));
            return;
        }

        if (authored != null && authored.TargetReferenceRemoteDirty)
        {
            targetReferenceStatusLabel.text = "Saved locally — sync to upload";
            targetReferenceStatusLabel.style.color = new StyleColor(new Color32(220, 53, 69, 255));
            return;
        }

        if (authored != null && !string.IsNullOrWhiteSpace(authored.TargetReferenceImageUrl))
        {
            targetReferenceStatusLabel.text = "Uploaded";
            targetReferenceStatusLabel.style.color = new StyleColor(new Color32(34, 197, 94, 255));
            return;
        }

        if (authored != null && authored.TargetReferenceBytes != null && authored.TargetReferenceBytes.Length > 0)
        {
            targetReferenceStatusLabel.text = "Ready to sync";
            targetReferenceStatusLabel.style.color = new StyleColor(new Color32(229, 231, 235, 255));
            return;
        }

        targetReferenceStatusLabel.text = "Not uploaded yet";
        targetReferenceStatusLabel.style.color = new StyleColor(new Color32(107, 114, 128, 255));
    }

    private void RefreshTargetReferenceUiForActiveTarget()
    {
        if (targetReferencePreviewImage == null)
            return;

        EnsureTargetReferenceDraftHydratedFromAuthored();

        TargetReferenceDraft activeDraft = GetActiveTargetReferenceDraft();
        if (activeDraft == null || activeDraft.previewTexture == null)
        {
            targetReferencePreviewImage.image = null;
            targetReferencePreviewImage.style.display = DisplayStyle.None;
            return;
        }

        targetReferencePreviewImage.image = activeDraft.previewTexture;
        targetReferencePreviewImage.style.display = DisplayStyle.Flex;
    }

    private TargetReferenceDraft GetActiveTargetReferenceDraft()
    {
        string targetId = GetActiveTargetIdForSave();
        if (string.IsNullOrWhiteSpace(targetId))
            return null;

        targetReferencesByTargetId.TryGetValue(targetId, out TargetReferenceDraft draft);
        return draft;
    }

    private void SetOrReplaceTargetReferenceDraft(string targetId, byte[] bytes, string fileName)
    {
        if (string.IsNullOrWhiteSpace(targetId) || bytes == null || bytes.Length == 0)
            return;

        if (targetReferencesByTargetId.TryGetValue(targetId, out TargetReferenceDraft existing) && existing != null && existing.previewTexture != null)
            Destroy(existing.previewTexture);

        var draft = new TargetReferenceDraft
        {
            bytes = bytes,
            fileName = fileName ?? "",
            isUnsaved = true,
            previewTexture = CreatePreviewTexture(bytes)
        };

        AuthoredTargetInstance authored = ResolveAuthoredTargetById(targetId);
        if (authored != null && !authored.TargetReferenceRemoteDirty && !string.IsNullOrWhiteSpace(authored.TargetReferenceImageUrl))
            draft.isUnsaved = false;

        targetReferencesByTargetId[targetId] = draft;
        RefreshTargetReferenceUiForActiveTarget();
    }

    private static Texture2D CreatePreviewTexture(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
            return null;

        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        bool loaded = texture.LoadImage(bytes, markNonReadable: false);
        if (!loaded)
        {
            Destroy(texture);
            return null;
        }

        return texture;
    }

    /// <summary>场景里选中 ContentRoot 下的物体时调用，与 Gizmo / 保存逻辑对齐。</summary>
    public void OnContentSelectedInScene(Transform contentTransform)
    {
        if (contentTransform == null)
            return;

        authoringSpatialTarget = contentTransform;
        activeDraggedObject = contentTransform.GetComponent<DraggableObject>();

        ApplyInspectorModeContent();
        SyncTransformToInspector(contentTransform);
        activeContentDraft = ResolveDraftForSelection(contentTransform, activeDraggedObject);
        if (activeContentDraft != null)
        {
            string draftMediaValue = activeContentDraft.contentType == SpawnContentType.Text
                ? activeContentDraft.textPayload
                : activeContentDraft.mediaUrl;
            ApplyUrlToMediaFields(draftMediaValue);
        }
    }

    /// <summary>切换 Target 后若无选中内容，清空坐标绑定，避免仍在改「已隐藏目标」上的 Transform。</summary>
    public void ClearAuthoringSpatialSelection()
    {
        authoringSpatialTarget = null;
        activeDraggedObject = null;
        activeContentDraft = null;
        UpdatePhysicalSizeLabel(null);
        if (inspectorMode == InspectorMode.Target)
            ApplyInspectorModeTarget();

        UpdateAddContentButtonIcon();
    }

    public bool IsPointerOverAuthoringUi(Vector2 screenPosition)
    {
        return AuthoringUiPickHelper.IsOverUiDocument(uiDocument, screenPosition);
    }

    public Transform TryGetActiveContentRoot()
    {
        targetSelectionManager = ResolveTargetSelectionManager();
        if (targetSelectionManager == null)
            return null;

        GameObject active = targetSelectionManager.GetActiveTarget();
        if (active == null)
            return null;

        return active.transform.Find("ContentRoot");
    }

    [Tooltip("相对墙面沿法线微移，减轻与 TargetVisual 灰框 Z-fighting")]
    [SerializeField] private float spawnForwardOffsetFromWall = 0.008f;
    [SerializeField] private WorkspaceRemoteSyncService remoteSyncService;

    /// <summary>Inspector slot, or <see cref="Resources"/> at <see cref="ModelContentContainerResourcesPath"/>.</summary>
    private GameObject GetModelContentContainerPrefab()
    {
        if (modelContentContainerPrefab != null)
            return modelContentContainerPrefab;

        GameObject loaded = Resources.Load<GameObject>(ModelContentContainerResourcesPath);
        if (loaded == null)
            Debug.LogWarning(
                "Assign Model Content Container Prefab, or place the prefab at Resources/" + ModelContentContainerResourcesPath + ".prefab");
        return loaded;
    }

    private ISpawnerManager BuildSpawnerManager()
    {
        targetSelectionManager = ResolveTargetSelectionManager();
        ITargetContextResolver resolver = new TargetSelectionContextResolver(targetSelectionManager);
        
        // Ensure videoPrefab is passed here!
        return new SpawnerManager(
            this, 
            picturePrefab, 
            textPrefab, 
            GetModelContentContainerPrefab(), 
            videoPrefab, 
            resolver, 
            contentCoordinator: null, 
            targetWorkflowService: targetWorkflowService, 
            forwardOffsetFromWall: spawnForwardOffsetFromWall);
    }

    // --- NEW: Text Spawning ---
    void OnSpawnTextButtonClicked()
    {
        if (!IsWorkspaceReadyForAuthoring(showBlockedMessage: true))
            return;

        if (spawningTextInput == null)
            return;

        string textToDisplay = spawningTextInput.value;

        SpawnContentResult localResult = ReplaceActiveTargetContent(new SpawnRequest
        {
            contentType = SpawnContentType.Text,
            textPayload = textToDisplay,
            targetId = GetActiveTargetIdForSave()
        });
        if (!localResult.success || localResult.spawnedObject == null)
            return;

        if (localResult.draggableObject != null)
        {
            RegisterTextDraft(localResult.draggableObject, textToDisplay);
            SetActiveAuthoringObject(localResult.draggableObject, textToDisplay, "Text");
        }
        else if (localResult.spawnedObject != null)
        {
            RegisterTextDraftForTransform(localResult.spawnedObject.transform, textToDisplay);
            SetActiveAuthoringTransform(localResult.spawnedObject.transform, textToDisplay, "Text");
        }
    }

    void OnBrowseButtonClicked()
    {
        if (!IsWorkspaceReadyForAuthoring(showBlockedMessage: true))
            return;

        pendingUploadPurpose = UploadPurpose.Content;
        #if UNITY_WEBGL || UNITY_EDITOR
        WebGLFileBrowser.OpenFilePanelWithFilters(".png,.jpg,.jpeg,.glb,.mp4,.mov,.webm", false);
        #endif
    }



    void OnBrowseTargetImageButtonClicked()
    {
        if (!IsWorkspaceReadyForAuthoring(showBlockedMessage: true))
            return;

        pendingUploadPurpose = UploadPurpose.TargetImage;
        if (createTargetImageUrlInput != null)
            createTargetImageUrlInput.value = "Uploading target image...";
        #if UNITY_WEBGL || UNITY_EDITOR
        WebGLFileBrowser.OpenFilePanelWithFilters(".png,.jpg", false);
        #endif
    }

    private void OnBrowseTargetReferenceButtonClicked()
    {
        if (!IsWorkspaceReadyForAuthoring(showBlockedMessage: true))
            return;

        pendingTargetReferenceTargetId = GetActiveTargetIdForSave();
        if (string.IsNullOrWhiteSpace(pendingTargetReferenceTargetId))
        {
            ShowError("No active target selected for target reference.");
            return;
        }

        pendingUploadPurpose = UploadPurpose.TargetReference;
        UpdateTargetReferenceStatusLabel(showUploadingText: true);

        #if UNITY_WEBGL || UNITY_EDITOR
        WebGLFileBrowser.OpenFilePanelWithFilters(".png,.jpg,.jpeg", false);
        #endif
    }

    // This runs automatically when an image is selected
    private void OnFilesOpened(FrostweepGames.Plugins.WebGLFileBrowser.File[] files)
    {
        if (!IsWorkspaceReadyForAuthoring(showBlockedMessage: true))
            return;

        if (files == null || files.Length == 0)
            return;

        var selectedFile = files[0];
        bool isTargetReferenceUpload = pendingUploadPurpose == UploadPurpose.TargetReference;
        if (isTargetReferenceUpload)
        {
            HandleTargetReferenceFileSelected(selectedFile);
            pendingUploadPurpose = UploadPurpose.Content;
            return;
        }

        bool isTargetImageUpload = pendingUploadPurpose == UploadPurpose.TargetImage;
        if (isTargetImageUpload)
        {
            if (createTargetImageUrlInput != null)
                createTargetImageUrlInput.value = "Uploading target image...";

            apiClient = ResolveApiClient();
            uploadWorkflowService.UploadSelectedFile(
                selectedFile,
                apiClient,
                result => OnTargetImageUploadCompleted(result, selectedFile),
                uploadTimeoutSeconds);

            pendingUploadPurpose = UploadPurpose.Content;
            return;
        }

        // Default: content library local draft + optional immediate spawn.
        SpawnLocalContentFromFileSelection(selectedFile);
        pendingUploadPurpose = UploadPurpose.Content;
    }

    private void HandleTargetReferenceFileSelected(FrostweepGames.Plugins.WebGLFileBrowser.File selectedFile)
    {
        if (string.IsNullOrWhiteSpace(pendingTargetReferenceTargetId))
        {
            UpdateTargetReferenceStatusLabel(showUploadingText: false);
            return;
        }

        if (selectedFile == null || selectedFile.data == null || selectedFile.data.Length == 0)
        {
            pendingTargetReferenceTargetId = null;
            UpdateTargetReferenceStatusLabel(showUploadingText: false);
            return;
        }

        string targetId = pendingTargetReferenceTargetId.Trim();
        string fileName = selectedFile.fileInfo != null ? selectedFile.fileInfo.name : "";
        if (!TryPersistTargetReferenceLocally(targetId, selectedFile.data, fileName, out string error))
        {
            ShowError(string.IsNullOrWhiteSpace(error) ? "Could not save target reference locally." : error);
            pendingTargetReferenceTargetId = null;
            UpdateTargetReferenceStatusLabel(showUploadingText: false);
            return;
        }

        SetOrReplaceTargetReferenceDraft(targetId, selectedFile.data, fileName);
        pendingTargetReferenceTargetId = null;
        UpdateTargetReferenceStatusLabel(showUploadingText: false);
        RequestWorkspaceSnapshotSave();
    }

    private bool TryPersistTargetReferenceLocally(string targetId, byte[] bytes, string fileName, out string errorMessage)
    {
        errorMessage = null;
        if (bytes == null || bytes.Length == 0)
        {
            errorMessage = "Reference image is empty.";
            return false;
        }

        if (!AppFlowController.TryGetWorkspaceSession(out WorkspaceSessionContext session) || session == null
            || string.IsNullOrWhiteSpace(session.workspaceId))
        {
            errorMessage = "No active workspace session.";
            return false;
        }

        AuthoredTargetInstance authored = ResolveAuthoredTargetById(targetId);
        if (authored == null)
        {
            errorMessage = "Active target not found.";
            return false;
        }

        authored.TargetReferenceBytes = PersistenceByteUtility.CloneBytes(bytes);
        authored.TargetReferenceLocalPath = "";
        authored.TargetReferenceOriginalFileName = fileName ?? "";
        authored.TargetReferenceRemoteDirty = true;
        authored.RemoteDirty = true;
        return true;
    }

    private AuthoredTargetInstance ResolveAuthoredTargetForActiveSelection()
    {
        return ResolveAuthoredTargetById(GetActiveTargetIdForSave());
    }

    private AuthoredTargetInstance ResolveAuthoredTargetById(string targetId)
    {
        if (string.IsNullOrWhiteSpace(targetId) || targetSelectionManager == null)
            return null;

        int index = targetSelectionManager.FindTargetIndexById(targetId.Trim());
        if (index < 0)
            return null;

        GameObject go = targetSelectionManager.GetTargetAt(index);
        return go != null ? go.GetComponent<AuthoredTargetInstance>() : null;
    }

    private void EnsureTargetReferenceDraftHydratedFromAuthored()
    {
        string targetId = GetActiveTargetIdForSave();
        if (string.IsNullOrWhiteSpace(targetId))
            return;

        if (GetActiveTargetReferenceDraft() != null)
            return;

        AuthoredTargetInstance authored = ResolveAuthoredTargetById(targetId);
        if (authored == null || authored.TargetReferenceBytes == null || authored.TargetReferenceBytes.Length == 0)
            return;

        SetOrReplaceTargetReferenceDraft(targetId, authored.TargetReferenceBytes, authored.TargetReferenceOriginalFileName);
        if (targetReferencesByTargetId.TryGetValue(targetId, out TargetReferenceDraft draft) && draft != null)
            draft.isUnsaved = authored.TargetReferenceRemoteDirty;
    }

    private void RequestWorkspaceSnapshotSave()
    {
        WorkspaceAutoSaveService autoSave = FindFirstObjectByType<WorkspaceAutoSaveService>();
        if (autoSave != null)
            autoSave.NotifyWorkspaceChanged();
    }

private void SpawnLocalContentFromFileSelection(FrostweepGames.Plugins.WebGLFileBrowser.File selectedFile)
{
    if (selectedFile == null || selectedFile.fileInfo == null || selectedFile.data == null || selectedFile.data.Length == 0)
    {
        if (filePathInput != null) filePathInput.value = "Invalid local file";
        return;
    }

    string baseName = !string.IsNullOrWhiteSpace(selectedFile.fileInfo.name) ? selectedFile.fileInfo.name : "local-file";
    string extension = selectedFile.fileInfo.extension ?? "";
    
    // --- THE FIX: FOOLPROOF EXTENSION GLUE ---
    string displayName = baseName;
    if (!string.IsNullOrWhiteSpace(extension) && !baseName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
    {
        displayName += extension.StartsWith(".") ? extension : "." + extension;
    }
    // -----------------------------------------

    string lowerName = displayName.ToLowerInvariant();

    SpawnContentType type;
    if (lowerName.EndsWith(".glb"))
    {
        type = SpawnContentType.Model;
    }
    else if (lowerName.EndsWith(".mp4") || lowerName.EndsWith(".mov") || lowerName.EndsWith(".webm"))
    {
        type = SpawnContentType.Video;
    }
    else
    {
        type = SpawnContentType.Image;
    }

    string mime = GuessMimeTypeFromExtension(extension);
    SpawnContentResult outcome = ReplaceActiveTargetContent(new SpawnRequest
    {
        contentType = type,
        originalFileName = displayName,
        localFileBytes = selectedFile.data,
        localMimeType = mime,
        isLocalDraft = true,
        targetId = GetActiveTargetIdForSave()
    });

    if (!outcome.success || outcome.spawnedObject == null)
    {
        if (filePathInput != null)
            filePathInput.value = "Spawn failed: " + displayName;
        return;
    }

    string label = type == SpawnContentType.Model ? "Model"
        : type == SpawnContentType.Video ? "Video"
        : "Image";

    if (outcome.draggableObject != null)
    {
        RegisterLocalDraft(outcome.draggableObject, type, selectedFile, displayName);
        SetActiveAuthoringObject(outcome.draggableObject, "", label);
    }
    else if (outcome.spawnedObject != null)
    {
        RegisterLocalDraftForTransform(outcome.spawnedObject.transform, type, selectedFile, displayName);
        SetActiveAuthoringTransform(outcome.spawnedObject.transform, "", label);
    }

    if (filePathInput != null)
        filePathInput.value = "Content: " + displayName;
    if (youtubeUrlInput != null)
        youtubeUrlInput.value = "";
}

    private void OnTargetImageUploadCompleted(ApiResult<UploadFileResponseDto> result, FrostweepGames.Plugins.WebGLFileBrowser.File selectedFile)
    {
        if (result == null || !result.success || result.payload == null || string.IsNullOrWhiteSpace(result.payload.url))
        {
            pendingTargetImageUrl = "";
            if (createTargetImageUrlInput != null)
                createTargetImageUrlInput.value = "Target image upload failed";

            string code = result != null ? result.errorCode : ApiErrorCodes.Unknown;
            string message = result != null ? result.message : "No result";
            Debug.LogError($"Target image upload failed via IApiClient: [{code}] {message}");
            return;
        }

        pendingTargetImageUrl = result.payload.url.Trim();
        string displayName = selectedFile?.fileInfo != null && !string.IsNullOrWhiteSpace(selectedFile.fileInfo.name)
            ? selectedFile.fileInfo.name
            : "target-image";
        if (createTargetImageUrlInput != null)
            createTargetImageUrlInput.value = "Ready: " + displayName;

        targetSelectionManager = ResolveTargetSelectionManager();
        GameObject activeTarget = targetSelectionManager != null ? targetSelectionManager.GetActiveTarget() : null;
        ApplyPendingTargetImageToTarget(activeTarget);

        NotifyWorkspacePersistenceChanged();
        Debug.Log("Target image upload complete via IApiClient! URL: " + pendingTargetImageUrl);
    }

    private void SetActiveAuthoringObject(DraggableObject targetObj, string mediaValue, string contentType)
    {
        activeDraggedObject = targetObj;
        authoringSpatialTarget = targetObj.transform;
        activeContentDraft = ResolveDraftForSelection(authoringSpatialTarget, activeDraggedObject);

        suppressSpatialUiCallbacks = true;
        try
        {
            SyncTransformToInspector(targetObj.transform);
        }
        finally
        {
            suppressSpatialUiCallbacks = false;
        }

        ApplyInspectorModeContent();

        if (contentType != null && contentType.StartsWith("Text", System.StringComparison.Ordinal))
        {
            if (youtubeUrlInput != null) youtubeUrlInput.value = "";
            if (filePathInput != null)
                filePathInput.value = string.IsNullOrWhiteSpace(mediaValue) ? "No file..." : mediaValue.Trim();
        }
        else
            ApplyUrlToMediaFields(mediaValue);

        if (contentTypeInput != null)
            contentTypeInput.value = contentType;
        Debug.Log("Now authoring " + targetObj.gameObject.name);
    }

    private void SetActiveAuthoringTransform(Transform targetTransform, string mediaValue, string contentType)
    {
        if (targetTransform == null)
            return;

        activeDraggedObject = targetTransform.GetComponent<DraggableObject>();
        authoringSpatialTarget = targetTransform;
        activeContentDraft = ResolveDraftForSelection(authoringSpatialTarget, activeDraggedObject);

        suppressSpatialUiCallbacks = true;
        try
        {
            SyncTransformToInspector(targetTransform);
        }
        finally
        {
            suppressSpatialUiCallbacks = false;
        }

        ApplyInspectorModeContent();

        if (contentType != null && contentType.StartsWith("Text", System.StringComparison.Ordinal))
        {
            if (youtubeUrlInput != null) youtubeUrlInput.value = "";
            if (filePathInput != null)
                filePathInput.value = string.IsNullOrWhiteSpace(mediaValue) ? "No file..." : mediaValue.Trim();
        }
        else
        {
            ApplyUrlToMediaFields(mediaValue);
        }

        if (contentTypeInput != null)
            contentTypeInput.value = contentType;
        Debug.Log("Now authoring " + targetTransform.gameObject.name);
    }

    private ContentDraftState ResolveDraftForSelection(Transform selectedTransform, DraggableObject selectedDraggable)
    {
        if (selectedDraggable != null && contentDraftsByDraggable.TryGetValue(selectedDraggable, out ContentDraftState draggableDraft))
            return draggableDraft;

        if (selectedTransform != null && contentDraftsByTransform.TryGetValue(selectedTransform, out ContentDraftState transformDraft))
            return transformDraft;

        return null;
    }

    private void RegisterTextDraft(DraggableObject draggableObject, string textPayload)
    {
        if (draggableObject == null)
            return;

        ContentDraftState existing = ResolveDraftForSelection(draggableObject.transform, draggableObject);
        ContentDraftState draft = existing ?? new ContentDraftState
        {
            draftId = Guid.NewGuid().ToString("N"),
            contentType = SpawnContentType.Text,
            draggableObject = draggableObject,
            contentTransform = draggableObject.transform
        };

        draft.targetId = GetActiveTargetIdForSave();
        draft.textPayload = textPayload ?? "";
        draft.mediaUrl = "";
        draft.isUnsaved = true;
        draft.uploadPending = false;
        draft.persistPending = true;
        draft.lastError = "";

        contentDraftsByDraggable[draggableObject] = draft;
        contentDraftsByTransform[draggableObject.transform] = draft;
    }

    private void RegisterTextDraftForTransform(Transform contentTransform, string textPayload)
    {
        if (contentTransform == null)
            return;

        DraggableObject draggableObject = contentTransform.GetComponent<DraggableObject>();
        if (draggableObject != null)
        {
            RegisterTextDraft(draggableObject, textPayload);
            return;
        }

        ContentDraftState existing = ResolveDraftForSelection(contentTransform, null);
        ContentDraftState draft = existing ?? new ContentDraftState
        {
            draftId = Guid.NewGuid().ToString("N"),
            contentType = SpawnContentType.Text,
            draggableObject = null,
            contentTransform = contentTransform
        };

        draft.targetId = GetActiveTargetIdForSave();
        draft.textPayload = textPayload ?? "";
        draft.mediaUrl = "";
        draft.isUnsaved = true;
        draft.uploadPending = false;
        draft.persistPending = true;
        draft.lastError = "";

        contentDraftsByTransform[contentTransform] = draft;
    }

    private void RegisterRemoteBackedDraft(DraggableObject draggableObject, SpawnContentType contentType, string mediaUrl, string localFileName)
    {
        if (draggableObject == null)
            return;

        ContentDraftState existing = ResolveDraftForSelection(draggableObject.transform, draggableObject);
        ContentDraftState draft = existing ?? new ContentDraftState
        {
            draftId = Guid.NewGuid().ToString("N"),
            draggableObject = draggableObject,
            contentTransform = draggableObject.transform
        };

        draft.contentType = contentType;
        draft.targetId = GetActiveTargetIdForSave();
        draft.localFileName = localFileName ?? "";
        draft.mediaUrl = mediaUrl ?? "";
        draft.isUnsaved = true;
        draft.uploadPending = false;
        draft.persistPending = true;
        draft.lastError = "";

        contentDraftsByDraggable[draggableObject] = draft;
        contentDraftsByTransform[draggableObject.transform] = draft;
    }

    private void RegisterRemoteBackedDraftForTransform(Transform contentTransform, SpawnContentType contentType, string mediaUrl, string localFileName)
    {
        if (contentTransform == null)
            return;

        DraggableObject draggableObject = contentTransform.GetComponent<DraggableObject>();
        if (draggableObject != null)
        {
            RegisterRemoteBackedDraft(draggableObject, contentType, mediaUrl, localFileName);
            return;
        }

        ContentDraftState existing = ResolveDraftForSelection(contentTransform, null);
        ContentDraftState draft = existing ?? new ContentDraftState
        {
            draftId = Guid.NewGuid().ToString("N"),
            draggableObject = null,
            contentTransform = contentTransform
        };

        draft.contentType = contentType;
        draft.targetId = GetActiveTargetIdForSave();
        draft.localFileName = localFileName ?? "";
        draft.mediaUrl = mediaUrl ?? "";
        draft.isUnsaved = true;
        draft.uploadPending = false;
        draft.persistPending = true;
        draft.lastError = "";

        contentDraftsByTransform[contentTransform] = draft;
    }

    private void RegisterLocalDraft(DraggableObject draggableObject, SpawnContentType contentType, FrostweepGames.Plugins.WebGLFileBrowser.File selectedFile, string localFileName)
    {
        if (draggableObject == null)
            return;

        ContentDraftState existing = ResolveDraftForSelection(draggableObject.transform, draggableObject);
        ContentDraftState draft = existing ?? new ContentDraftState
        {
            draftId = Guid.NewGuid().ToString("N"),
            draggableObject = draggableObject,
            contentTransform = draggableObject.transform
        };

        string ext = selectedFile?.fileInfo != null ? (selectedFile.fileInfo.extension ?? "") : "";
        draft.contentType = contentType;
        draft.targetId = GetActiveTargetIdForSave();
        draft.localFileName = localFileName ?? "";
        draft.localFileBytes = selectedFile?.data;
        draft.localMimeType = GuessMimeTypeFromExtension(ext);
        draft.mediaUrl = "";
        draft.isUnsaved = true;
        draft.uploadPending = true;
        draft.persistPending = true;
        draft.lastError = "";

        contentDraftsByDraggable[draggableObject] = draft;
        contentDraftsByTransform[draggableObject.transform] = draft;
    }

    private void RegisterLocalDraftForTransform(Transform contentTransform, SpawnContentType contentType, FrostweepGames.Plugins.WebGLFileBrowser.File selectedFile, string localFileName)
    {
        if (contentTransform == null)
            return;

        DraggableObject draggableObject = contentTransform.GetComponent<DraggableObject>();
        if (draggableObject != null)
        {
            RegisterLocalDraft(draggableObject, contentType, selectedFile, localFileName);
            return;
        }

        ContentDraftState existing = ResolveDraftForSelection(contentTransform, null);
        ContentDraftState draft = existing ?? new ContentDraftState
        {
            draftId = Guid.NewGuid().ToString("N"),
            draggableObject = null,
            contentTransform = contentTransform
        };

        string ext = selectedFile?.fileInfo != null ? (selectedFile.fileInfo.extension ?? "") : "";
        draft.contentType = contentType;
        draft.targetId = GetActiveTargetIdForSave();
        draft.localFileName = localFileName ?? "";
        draft.localFileBytes = selectedFile?.data;
        draft.localMimeType = GuessMimeTypeFromExtension(ext);
        draft.mediaUrl = "";
        draft.isUnsaved = true;
        draft.uploadPending = true;
        draft.persistPending = true;
        draft.lastError = "";

        contentDraftsByTransform[contentTransform] = draft;
    }

    private static string GuessMimeTypeFromExtension(string extension) =>
        UploadWorkflowService.GuessMimeTypeFromExtension(extension);

    private void MarkActiveDraftDirty()
    {
        if (activeContentDraft == null)
            activeContentDraft = ResolveDraftForSelection(authoringSpatialTarget, activeDraggedObject);
        if (activeContentDraft != null)
        {
            activeContentDraft.isUnsaved = true;
            activeContentDraft.persistPending = true;
            activeContentDraft.targetId = GetActiveTargetIdForSave();
        }

        if (authoringSpatialTarget != null)
            WorkspaceAuthoredAttach.MarkContentRemoteDirty(authoringSpatialTarget);
    }

    private static bool LooksLikeYouTubeUrl(string u)
    {
        if (string.IsNullOrWhiteSpace(u))
            return false;
        string lower = u.ToLowerInvariant();
        return lower.Contains("youtube.com/") || lower.Contains("youtu.be/");
    }

    private void ApplyUrlToMediaFields(string url)
    {
        if (youtubeUrlInput != null) youtubeUrlInput.value = "";
        if (filePathInput != null) filePathInput.value = "No file...";

        if (string.IsNullOrWhiteSpace(url))
            return;

        string t = url.Trim();
        if (LooksLikeYouTubeUrl(t))
        {
            if (youtubeUrlInput != null) youtubeUrlInput.value = t;
            return;
        }

        if (filePathInput != null)
            filePathInput.value = t;
    }

    private const string SaveFabDefaultTooltip = "Save to server";

    void OnSaveButtonClicked()
    {
        if (!IsWorkspaceReadyForAuthoring(showBlockedMessage: true))
            return;

        if (isSaveInProgress)
            return;

        spawnerManager ??= BuildSpawnerManager();
        apiClient = ResolveApiClient();
        if (apiClient == null || spawnerManager == null)
        {
            ShowSaveFabFeedback("Save failed", "API client or spawner is not available.", isError: true);
            Debug.LogWarning("Save skipped: API client or spawner manager is not available.");
            return;
        }

        StartCoroutine(SaveAllDraftsRoutine());
    }

    private void ResetSaveFabFeedback()
    {
        if (saveButton == null)
            return;
        saveButton.tooltip = SaveFabDefaultTooltip;
        saveButton.EnableInClassList("authoring-save-fab--busy", false);
    }

    private void SetSaveFabBusy(bool busy)
    {
        if (saveButton == null)
            return;
        saveButton.EnableInClassList("authoring-save-fab--busy", busy);
        if (busy)
            saveButton.tooltip = "Saving…";
        else
            saveButton.tooltip = SaveFabDefaultTooltip;
    }

    private void ShowSaveFabFeedback(string title, string message, bool isError = false)
    {
        if (saveButton != null)
            saveButton.tooltip = $"{title}: {message}";

        if (_syncStatusToast == null || _syncStatusTitle == null || _syncStatusMessage == null)
            return;

        CancelSyncToastHideRoutine();
        ApplyRemoteSyncToastStyle(isError ? WorkspaceRemoteSyncToastKind.Failed : WorkspaceRemoteSyncToastKind.Synced);
        _syncStatusTitle.style.display = DisplayStyle.Flex;
        _syncStatusTitle.text = title;
        _syncStatusMessage.text = message;
        _syncStatusMessage.style.fontSize = 11;
        _syncStatusMessage.style.unityFontStyleAndWeight = FontStyle.Normal;
        _syncStatusToast.RemoveFromClassList("sync-toast--hidden");
        _syncStatusToast.style.display = DisplayStyle.Flex;
        _syncToastHideRoutine = StartCoroutine(HideSyncStatusToastAfterDelay(isError ? 8f : 4f));
    }

    private bool backToSwitcherInProgress;

    void OnBackToSwitcherButtonClicked()
    {
        if (SceneTransitionService.IsTransitioning || backToSwitcherInProgress)
            return;

        StartCoroutine(BackToSwitcherRoutine());
    }

    private IEnumerator BackToSwitcherRoutine()
    {
        backToSwitcherInProgress = true;
        if (backToSwitcherButton != null)
            backToSwitcherButton.SetEnabled(false);

        try
        {
            if (!AppFlowController.TryGetWorkspaceSession(out WorkspaceSessionContext session)
                || session == null
                || string.IsNullOrWhiteSpace(session.workspaceId))
            {
                Debug.Log("[WorkspacePersistence] BackToSwitcher: no session — clearing and loading switcher.");
                AppFlowController.ClearWorkspaceSession();
                AppFlowController.TransitionToWorkspaceSwitcher();
                yield break;
            }

            string workspaceId = session.workspaceId.Trim();
            string workspaceName = string.IsNullOrWhiteSpace(session.workspaceName) ? workspaceId : session.workspaceName.Trim();
            Debug.Log($"[WorkspacePersistence] BackToSwitcher: syncing workspace '{workspaceId}' before navigation.");

            WorkspaceRemoteSyncService remoteSync = ResolveRemoteSyncService();
            if (remoteSync != null)
                yield return remoteSync.SyncWorkspaceAndWait(workspaceId, workspaceName);
            else
                Debug.LogWarning("[WorkspacePersistence] BackToSwitcher: WorkspaceRemoteSyncService not found — skipping cloud sync.");

            AppFlowController.ClearWorkspaceSession();
            Debug.Log("[WorkspacePersistence] BackToSwitcher: ClearWorkspaceSession done → loading switcher.");
            AppFlowController.TransitionToWorkspaceSwitcher();
        }
        finally
        {
            backToSwitcherInProgress = false;
            if (backToSwitcherButton != null)
                backToSwitcherButton.SetEnabled(true);
        }
    }

    private IEnumerator SaveAllDraftsRoutine()
    {
        isSaveInProgress = true;
        SetSaveFabBusy(true);
        if (saveButton != null)
            saveButton.SetEnabled(false);

        List<ContentDraftState> drafts = CollectPendingDrafts();
        if (drafts.Count == 0)
        {
            ShowSaveFabFeedback("Nothing to save", "No pending content drafts to upload.");
            isSaveInProgress = false;
            if (saveButton != null)
                saveButton.SetEnabled(true);
            ResetSaveFabFeedback();
            ResolveRemoteSyncService()?.SyncNow();
            yield break;
        }

        int successCount = 0;
        int failedCount = 0;

        for (int i = 0; i < drafts.Count; i++)
        {
            ContentDraftState draft = drafts[i];
            if (draft == null || draft.contentTransform == null)
            {
                failedCount++;
                continue;
            }

            draft.targetId = ResolveDraftTargetId(draft);
            bool uploaded = false;
            if (RequiresMediaUpload(draft))
            {
                yield return UploadDraftMediaRoutine(draft, uploadOk => uploaded = uploadOk);
                if (!uploaded)
                {
                    failedCount++;
                    continue;
                }
            }

            bool synced = false;
            yield return SyncDraftRoutine(draft, syncOk => synced = syncOk);
            if (synced)
            {
                successCount++;
                draft.isUnsaved = false;
                draft.uploadPending = false;
                draft.persistPending = false;
                draft.lastError = "";
            }
            else
            {
                failedCount++;
                draft.isUnsaved = true;
                draft.persistPending = true;
            }
        }

        if (failedCount == 0)
        {
            ShowSaveFabFeedback("Saved", $"Persisted {successCount} item(s) to the server.");
            Debug.Log($"Save complete: persisted {successCount} draft(s).");
        }
        else
        {
            ShowSaveFabFeedback(
                "Save partial",
                $"Saved {successCount} of {drafts.Count}; {failedCount} failed.",
                isError: true);
            Debug.LogWarning($"Save finished with failures: success={successCount}, failed={failedCount}.");
        }

        isSaveInProgress = false;
        if (saveButton != null)
            saveButton.SetEnabled(true);
        ResetSaveFabFeedback();
    }

    private List<ContentDraftState> CollectPendingDrafts()
    {
        var drafts = new List<ContentDraftState>();
        var seenIds = new HashSet<string>();

        void AddIfPending(ContentDraftState draft)
        {
            if (draft == null || string.IsNullOrWhiteSpace(draft.draftId))
                return;
            if (!draft.isUnsaved && !draft.persistPending)
                return;
            if (!seenIds.Add(draft.draftId))
                return;
            drafts.Add(draft);
        }

        foreach (ContentDraftState draft in contentDraftsByDraggable.Values)
            AddIfPending(draft);

        foreach (ContentDraftState draft in contentDraftsByTransform.Values)
            AddIfPending(draft);

        return drafts;
    }

    private static bool RequiresMediaUpload(ContentDraftState draft)
    {
        if (draft == null)
            return false;
        if (draft.contentType == SpawnContentType.Text)
            return false;
        if (!string.IsNullOrWhiteSpace(draft.mediaUrl))
            return false;
        return draft.localFileBytes != null && draft.localFileBytes.Length > 0;
    }

    private IEnumerator UploadDraftMediaRoutine(ContentDraftState draft, Action<bool> onCompleted)
    {
        if (draft == null)
        {
            onCompleted?.Invoke(false);
            yield break;
        }

        draft.uploadPending = true;
        draft.lastError = "";

        EnsureStableServerContentIdForDraft(draft);
        string contentIdForUpload = ResolveServerContentIdForDraft(draft);

        // Fully qualified to prevent System.IO conflicts
        var file = new FrostweepGames.Plugins.WebGLFileBrowser.File
        {
            data = draft.localFileBytes,
            fileInfo = new FrostweepGames.Plugins.WebGLFileBrowser.FileInfo
            {
                name = draft.localFileName,
                extension = System.IO.Path.GetExtension(draft.localFileName ?? "")
            }
        };

        bool done = false;
        bool success = false;
        uploadWorkflowService.UploadSelectedFile(
            file,
            apiClient,
            result =>
            {
                success = result != null && result.success && result.payload != null && !string.IsNullOrWhiteSpace(result.payload.url);
                if (success)
                {
                    draft.mediaUrl = result.payload.url.Trim();
                    draft.uploadPending = false;
                    draft.localFileBytes = null;
                    // --- ADDED THIS TO START PLAYBACK AFTER UPLOAD ---
                    if (draft.contentType == SpawnContentType.Video && draft.contentTransform != null)
                    {
                        var vPlayer = draft.contentTransform.GetComponent<UnityEngine.Video.VideoPlayer>();
                        if (vPlayer != null)
                        {
                            vPlayer.source = UnityEngine.Video.VideoSource.Url;
                            vPlayer.url = draft.mediaUrl;
                            vPlayer.Play();
                        }
                    }
                    // -------------------------------------------------
                }
                else
                {
                    string code = result != null ? result.errorCode : ApiErrorCodes.Unknown;
                    string message = result != null ? result.message : "No result";
                    draft.lastError = $"Upload failed [{code}] {message}";
                    draft.uploadPending = true;
                    Debug.LogWarning($"Draft upload failed ({draft.draftId}): {draft.lastError}");
                    ShowError($"Upload Failed! [{code}] {message}"); // Task 6 NewAdd
                }
                done = true;
            },
            uploadTimeoutSeconds,
            contentIdForUpload);

        while (!done)
            yield return null;

        onCompleted?.Invoke(success);
    }

    private static string ResolveServerContentIdForDraft(ContentDraftState draft)
    {
        if (draft?.contentTransform == null)
            return null;

        AuthoredContentInstance ac = draft.contentTransform.GetComponent<AuthoredContentInstance>()
            ?? draft.contentTransform.GetComponentInParent<AuthoredContentInstance>()
            ?? draft.contentTransform.GetComponentInChildren<AuthoredContentInstance>(true);
        if (ac == null || string.IsNullOrWhiteSpace(ac.ServerContentId))
            return null;

        return ac.ServerContentId.Trim();
    }

    private IEnumerator SyncDraftRoutine(ContentDraftState draft, Action<bool> onCompleted)
    {
        if (draft == null || draft.contentTransform == null)
        {
            onCompleted?.Invoke(false);
            yield break;
        }

        if (draft.contentType != SpawnContentType.Text && string.IsNullOrWhiteSpace(draft.mediaUrl))
        {
            draft.lastError = "Persist skipped: mediaUrl is unresolved. Upload must complete before CreateContent.";
            onCompleted?.Invoke(false);
            yield break;
        }

        EnsureStableServerContentIdForDraft(draft);
        SpawnRequest syncRequest = BuildSyncRequestFromDraft(draft);
        bool done = false;
        bool success = false;
        spawnerManager.BeginSyncCreateContent(
            apiClient,
            syncRequest,
            draft.contentTransform,
            result =>
            {
                success = result != null && result.success;
                if (success)
                    MarkContentSyncedAfterManualSave(draft);
                if (!success)
                {
                    string code = result != null ? result.errorCode : ApiErrorCodes.Unknown;
                    string message = result != null ? result.message : "No result";
                    draft.lastError = $"Persist failed [{code}] {message}";
                    Debug.LogWarning($"Draft persist failed ({draft.draftId}): {draft.lastError}");
                    ShowError($"Save Failed! [{code}] {message}"); // Task6NewAdd
                }
                done = true;
            },
            createContentTimeoutSeconds);

        while (!done)
            yield return null;

        onCompleted?.Invoke(success);
    }

    private static void MarkContentSyncedAfterManualSave(ContentDraftState draft)
    {
        if (draft?.contentTransform == null)
            return;

        AuthoredContentInstance ac = draft.contentTransform.GetComponent<AuthoredContentInstance>()
            ?? draft.contentTransform.GetComponentInParent<AuthoredContentInstance>()
            ?? draft.contentTransform.GetComponentInChildren<AuthoredContentInstance>(true);
        if (ac == null)
            return;

        ac.RemoteDirty = false;
        ac.LastRemoteSyncedAtUtc = DateTime.UtcNow.ToString("o");
        if (!string.IsNullOrWhiteSpace(draft.mediaUrl))
            ac.MediaUrl = draft.mediaUrl.Trim();
    }

    /// <summary>
    /// Ensures <see cref="AuthoredContentInstance.ServerContentId"/> is set so repeated Save / Layer 3 POST use the same content id (backend upsert).
    /// </summary>
    private static void EnsureStableServerContentIdForDraft(ContentDraftState draft)
    {
        if (draft?.contentTransform == null)
            return;

        AuthoredContentInstance ac = draft.contentTransform.GetComponent<AuthoredContentInstance>()
            ?? draft.contentTransform.GetComponentInParent<AuthoredContentInstance>()
            ?? draft.contentTransform.GetComponentInChildren<AuthoredContentInstance>(true);
        if (ac == null)
            return;

        if (string.IsNullOrWhiteSpace(ac.LocalContentId))
            ac.LocalContentId = Guid.NewGuid().ToString("N");
        if (string.IsNullOrWhiteSpace(ac.ServerContentId))
            ac.ServerContentId = ac.LocalContentId;
    }

    private SpawnRequest BuildSyncRequestFromDraft(ContentDraftState draft)
    {
        Vector3 localScale = draft.contentTransform != null ? draft.contentTransform.localScale : Vector3.one;
        var req = new SpawnRequest
        {
            contentType = draft.contentType,
            targetId = draft.targetId ?? "",
            mediaUrl = draft.contentType == SpawnContentType.Text ? "" : (draft.mediaUrl ?? ""),
            textPayload = draft.contentType == SpawnContentType.Text ? (draft.textPayload ?? "") : "",
            hasTransformOverride = true,
            transformOverride = new SpawnTransformData
            {
                localPosition = draft.contentTransform != null ? draft.contentTransform.localPosition : Vector3.zero,
                localEuler = draft.contentTransform != null ? draft.contentTransform.localEulerAngles : Vector3.zero,
                localScale = localScale
            }
        };

        if (draft.contentTransform != null)
        {
            AuthoredContentInstance ac = draft.contentTransform.GetComponent<AuthoredContentInstance>()
                ?? draft.contentTransform.GetComponentInParent<AuthoredContentInstance>()
                ?? draft.contentTransform.GetComponentInChildren<AuthoredContentInstance>(true);
            if (ac != null && !string.IsNullOrWhiteSpace(ac.ServerContentId))
                req.contentIdOverride = ac.ServerContentId.Trim();
        }

        return req;
    }

    private string ResolveDraftTargetId(ContentDraftState draft)
    {
        if (draft == null)
            return "";
        if (!string.IsNullOrWhiteSpace(draft.targetId))
            return draft.targetId;
        return GetActiveTargetIdForSave();
    }


    private void OnSpawnYoutubeClicked()
    {
        if (!IsWorkspaceReadyForAuthoring(showBlockedMessage: true))
            return;

        string url = youtubeUrlInput?.value;

        if (string.IsNullOrWhiteSpace(url))
        {
            Debug.LogWarning("YouTube URL is empty.");
            return;
        }

        string currentTargetId = GetActiveTargetIdForSave();
        SpawnContentResult localResult = ReplaceActiveTargetContent(new SpawnRequest
        {
            contentType = SpawnContentType.Video,
            mediaUrl = url,
            targetId = currentTargetId
        });
        if (!localResult.success || localResult.spawnedObject == null)
            return;

        if (localResult.draggableObject != null)
        {
            RegisterRemoteBackedDraft(
                localResult.draggableObject,
                SpawnContentType.Video,
                url,
                localFileName: "youtube-link");
            SetActiveAuthoringObject(localResult.draggableObject, url, "Video");
        }
        else if (localResult.spawnedObject != null)
        {
            RegisterRemoteBackedDraftForTransform(
                localResult.spawnedObject.transform,
                SpawnContentType.Video,
                url,
                localFileName: "youtube-link");
            SetActiveAuthoringTransform(localResult.spawnedObject.transform, url, "Video");
        }

        Debug.Log("Successfully spawned YouTube stream to AR wall.");
    }

    // This MUST be public so the DraggableObject can see it!
    public void UpdateCoordinatesFromDrag(Vector3 newPosition)
    {
        if (authoringSpatialTarget == null)
            return;

        SyncTransformToInspector(authoringSpatialTarget);
        MarkActiveDraftDirty();
        NotifyWorkspacePersistenceChanged();
    }

    private SemanticAxisMapping.PlacementPosture ResolvePlacementPosture()
    {
        if (_placementBoundsService != null)
            return _placementBoundsService.ActivePosture;

        AuthoringWorkspaceEntry entry = FindFirstObjectByType<ARGallery.AppFlow.AuthoringWorkspaceEntry>();
        if (entry != null)
            return SemanticAxisMapping.FromWorkspacePosture((int)entry.AppliedPosture);

        return SemanticAxisMapping.PlacementPosture.Wall;
    }

    private void ApplyPlacementOffsetRowLabels(SemanticAxisMapping.PlacementPosture posture)
    {
        SemanticAxisMapping.RowLabels labels = SemanticAxisMapping.GetRowLabels(posture);
        SetLabelText(posLeftRightRowLabel, labels.leftRight);
        SetLabelText(posUpDownRowLabel, labels.middle);
        SetLabelText(posCloserFurtherRowLabel, labels.standoff);
    }

    private void BindManipulatorBottomPanel(VisualElement root)
    {
        if (root == null)
            return;

        TargetLocalTransformService localSvc = FindFirstObjectByType<TargetLocalTransformService>();
        ContentTransformManipulator manipulator = ResolveContentManipulator();

        _manipulatorPanel.Bind(
            root,
            manipulator,
            _placementBoundsService,
            localSvc,
            () => authoringSpatialTarget,
            () => inspectorMode == InspectorMode.Content,
            () => transformGizmoController != null ? transformGizmoController.CurrentMode : TransformGizmoController.GizmoMode.Translate,
            ResolvePlacementPosture,
            OnManipulatorPanelTransformEdited);

        _manipulatorPanel.RegisterModePill(modeMovePill, TransformGizmoController.GizmoMode.Translate, SetManipulatorMode);
        _manipulatorPanel.RegisterModePill(modeRotatePill, TransformGizmoController.GizmoMode.Rotate, SetManipulatorMode);
        _manipulatorPanel.RegisterModePill(modeScalePill, TransformGizmoController.GizmoMode.Scale, SetManipulatorMode);
    }

    private void SetManipulatorMode(TransformGizmoController.GizmoMode mode)
    {
        if (transformGizmoController == null)
            transformGizmoController = FindFirstObjectByType<TransformGizmoController>();
        if (transformGizmoController == null)
            return;

        transformGizmoController.SetMode(mode);
        _lastKnownGizmoMode = mode;
        if (modeIndicatorLabel != null)
            modeIndicatorLabel.text = "Mode: " + GetModeDisplayName(mode);
        SetModePillActive(TransformGizmoController.GizmoMode.Translate, mode == TransformGizmoController.GizmoMode.Translate);
        SetModePillActive(TransformGizmoController.GizmoMode.Rotate, mode == TransformGizmoController.GizmoMode.Rotate);
        SetModePillActive(TransformGizmoController.GizmoMode.Scale, mode == TransformGizmoController.GizmoMode.Scale);
        SetModePillActive(TransformGizmoController.GizmoMode.Universal, mode == TransformGizmoController.GizmoMode.Universal);
        _manipulatorPanel.RefreshVisibilityAndValues();
    }

    private void OnManipulatorPanelTransformEdited()
    {
        if (authoringSpatialTarget != null)
            SyncTransformToInspector(authoringSpatialTarget);

        MarkActiveDraftDirty();
        if (authoringSpatialTarget != null)
            WorkspaceAuthoredAttach.MarkContentRemoteDirty(authoringSpatialTarget);
    }

    private ContentTransformManipulator ResolveContentManipulator()
    {
        if (authoringTransformCoordinator != null && authoringTransformCoordinator.ContentManipulator != null)
            return authoringTransformCoordinator.ContentManipulator;

        return FindFirstObjectByType<ContentTransformManipulator>();
    }
}