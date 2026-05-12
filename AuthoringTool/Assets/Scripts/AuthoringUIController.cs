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
using System;
using UnityEngine.InputSystem;

public class AuthoringUIController : MonoBehaviour
{
    public DatabaseManager dbManager;
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
    private FloatField posXInput, posYInput, posZInput, scaleInput;
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
    private ListView contentHierarchyList;
    private bool isLeftPanelExpanded;
    private bool isRightPanelExpanded;
    private bool suppressHierarchySelectionCallbacks;
    private readonly List<ContentLibraryItem> contentLibraryItems = new List<ContentLibraryItem>();
    private bool hasBootstrappedSceneLibrary;

    // --- TASK 6: 新增 UI 变量 ---
    private VisualElement _loadingOverlay;
    private VisualElement _errorToast;
    private Label _errorLabel;
    private Coroutine _errorToastCoroutine;
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

    private sealed class ContentLibraryItem
    {
        public string libraryId;
        public string displayName;
        public SpawnContentType contentType;
        public byte[] localFileBytes;
        public string localMimeType;
        public string originalFileName;
        public Transform instantiatedTransform;
        public bool isSaved;
    }

    void OnEnable()
    {
        uiDocument = GetComponent<UIDocument>();
        var root = uiDocument.rootVisualElement;

        // Basic Fields
        contentTypeInput = root.Q<TextField>("ContentTypeInput");
        posXInput = root.Q<FloatField>("PosXInput");
        posYInput = root.Q<FloatField>("PosYInput");
        posZInput = root.Q<FloatField>("PosZInput");
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
        addContentButton = root.Q<Button>("AddContentButton");
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
        contentHierarchyList = root.Q<ListView>("ContentLibraryList") ?? root.Q<ListView>("ContentHierarchyList");

        // --- TASK 6: 获取并初始化 Loading 和 Error 元素 ---
        _loadingOverlay = root.Q<VisualElement>("loading-overlay");
        _errorToast = root.Q<VisualElement>("error-toast");
        _errorLabel = root.Q<Label>("error-label");

        HideLoading();
        HideErrorToast();
        // ------------------------------------------------

        // Event Listeners
        if (browseButton != null) browseButton.clicked += OnBrowseButtonClicked;
        if (addContentButton != null) addContentButton.clicked += OnBrowseButtonClicked;
        if (browseTargetImageButton != null) browseTargetImageButton.clicked += OnBrowseTargetImageButtonClicked;
        saveButton.clicked += OnSaveButtonClicked;
        if (backToSwitcherButton != null) backToSwitcherButton.clicked += OnBackToSwitcherButtonClicked;
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
        apiClient = ResolveApiClient();
        spawnerManager = BuildSpawnerManager();
        ConfigureHierarchyListView();

        RefreshImageTargetDropdownChoices();
        if (imageTargetDropdown != null)
            imageTargetDropdown.RegisterValueChangedCallback(OnImageTargetDropdownChanged);
        if (targetSelectionManager != null)
            targetSelectionManager.ActiveTargetChanged += OnManagerActiveTargetChanged;
        if (authoringTransformCoordinator != null)
        {
            authoringTransformCoordinator.ContentListChanged += OnCoordinatorContentListChanged;
            authoringTransformCoordinator.ContentSelectionChanged += OnCoordinatorContentSelectionChanged;
        }
        RefreshWorkspaceGuardUiState();
        RefreshHierarchyListFromCoordinator();
        SyncHierarchySelectionFromCoordinator();

        // Right panel inspector mode:
        // - if a content object is selected -> content inspector
        // - otherwise -> target inspector
        if (authoringSpatialTarget == null)
            ApplyInspectorModeTarget();
        else
            ApplyInspectorModeContent();
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
            authoringTransformCoordinator.ContentListChanged -= OnCoordinatorContentListChanged;
            authoringTransformCoordinator.ContentSelectionChanged -= OnCoordinatorContentSelectionChanged;
        }
        if (contentHierarchyList != null)
            contentHierarchyList.onSelectionChange -= OnHierarchySelectionChanged;

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

    private void ConfigureHierarchyListView()
    {
        if (contentHierarchyList == null)
            return;

        contentHierarchyList.selectionType = SelectionType.Single;
        contentHierarchyList.fixedItemHeight = 26;
        contentHierarchyList.makeItem = () =>
        {
            var row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.SpaceBetween,
                    alignItems = Align.Center
                }
            };
            row.style.paddingLeft = 6;
            row.style.paddingRight = 6;

            var nameLabel = new Label { name = "entry-name" };
            nameLabel.style.fontSize = 11;
            nameLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            nameLabel.style.flexGrow = 1;
            nameLabel.style.color = new Color(0.90f, 0.91f, 0.92f, 1f);

            var statusLabel = new Label { name = "entry-status" };
            statusLabel.style.fontSize = 10;
            statusLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            statusLabel.style.marginLeft = 6;
            statusLabel.style.unityTextAlign = TextAnchor.MiddleRight;

            row.Add(nameLabel);
            row.Add(statusLabel);
            return row;
        };
        contentHierarchyList.bindItem = (element, index) =>
        {
            var nameLabel = element.Q<Label>("entry-name");
            var statusLabel = element.Q<Label>("entry-status");
            if (nameLabel == null || statusLabel == null)
                return;
            if (index < 0 || index >= contentLibraryItems.Count || contentLibraryItems[index] == null)
            {
                nameLabel.text = "Missing";
                statusLabel.text = "unsaved";
                statusLabel.style.color = new Color(0.95f, 0.40f, 0.40f, 1f);
                return;
            }

            ContentLibraryItem item = contentLibraryItems[index];
            nameLabel.text = string.IsNullOrWhiteSpace(item.displayName) ? "Content" : item.displayName;
            bool isUnsaved = !item.isSaved;
            statusLabel.text = isUnsaved ? "unsaved" : "saved";
            statusLabel.style.color = isUnsaved
                ? new Color(0.95f, 0.40f, 0.40f, 1f)
                : new Color(0.45f, 0.88f, 0.56f, 1f);
        };
        contentHierarchyList.itemsSource = contentLibraryItems;
        contentHierarchyList.onSelectionChange += OnHierarchySelectionChanged;
    }

    private bool IsTransformUnsaved(Transform tr)
    {
        if (tr == null)
            return true;
        if (contentDraftsByTransform.TryGetValue(tr, out ContentDraftState draft) && draft != null)
            return draft.isUnsaved || draft.persistPending || draft.uploadPending;
        return false;
    }

    private void OnHierarchySelectionChanged(IEnumerable<object> selectedItems)
    {
        if (suppressHierarchySelectionCallbacks || authoringTransformCoordinator == null)
            return;

        foreach (object item in selectedItems)
        {
            ContentLibraryItem selectedItem = item as ContentLibraryItem;
            if (selectedItem == null)
                continue;
            if (selectedItem.instantiatedTransform != null)
            {
                authoringTransformCoordinator.SelectContentTransform(selectedItem.instantiatedTransform, syncAuthoringUi: true);
            }
            else if (selectedItem.localFileBytes != null && selectedItem.localFileBytes.Length > 0)
            {
                ActivateLibraryItem(selectedItem);
            }
            break;
        }
    }

    private void OnCoordinatorContentListChanged()
    {
        RefreshHierarchyListFromCoordinator();
    }

    private void OnCoordinatorContentSelectionChanged(Transform _)
    {
        SyncHierarchySelectionFromCoordinator();
    }

    private void RefreshHierarchyListFromCoordinator()
    {
        if (contentHierarchyList == null)
            return;

        SyncLibraryWithActiveSceneContent();
        RefreshLibrarySavedFlagsFromDrafts();

        // Remove stale entries that have neither payload nor live transform.
        contentLibraryItems.RemoveAll(item =>
            item == null
            || (item.instantiatedTransform == null && (item.localFileBytes == null || item.localFileBytes.Length == 0)));

        if (authoringTransformCoordinator != null)
            _ = authoringTransformCoordinator.GetActiveContentEntries();
        contentHierarchyList.Rebuild();
        SyncHierarchySelectionFromCoordinator();
    }

    private void SyncHierarchySelectionFromCoordinator()
    {
        if (contentHierarchyList == null)
            return;

        Transform selected = authoringTransformCoordinator != null
            ? authoringTransformCoordinator.GetSelectedContentTransform()
            : null;

        suppressHierarchySelectionCallbacks = true;
        try
        {
            if (selected == null)
            {
                contentHierarchyList.ClearSelection();
                return;
            }

            int idx = -1;
            for (int i = 0; i < contentLibraryItems.Count; i++)
            {
                if (contentLibraryItems[i] != null && contentLibraryItems[i].instantiatedTransform == selected)
                {
                    idx = i;
                    break;
                }
            }
            if (idx >= 0)
                contentHierarchyList.SetSelectionWithoutNotify(new[] { idx });
            else
                contentHierarchyList.ClearSelection();
        }
        finally
        {
            suppressHierarchySelectionCallbacks = false;
        }
    }

    private void SyncLibraryWithActiveSceneContent()
    {
        if (authoringTransformCoordinator == null)
            return;

        // Bootstrap scene content into the library only once. After that, runtime library items
        // are authoritative to avoid duplicate auto-generated "saved" entries on each switch.
        if (hasBootstrappedSceneLibrary)
            return;

        IReadOnlyList<Transform> activeEntries = authoringTransformCoordinator.GetActiveContentEntries();
        if (activeEntries == null)
            return;

        for (int i = 0; i < activeEntries.Count; i++)
        {
            Transform tr = activeEntries[i];
            if (tr == null)
                continue;

            if (FindLibraryItemByTransform(tr) != null)
                continue;

            var item = new ContentLibraryItem
            {
                libraryId = Guid.NewGuid().ToString("N"),
                displayName = tr.name,
                contentType = SpawnContentType.Image,
                instantiatedTransform = tr,
                isSaved = !IsTransformUnsaved(tr)
            };
            contentLibraryItems.Add(item);
        }

        hasBootstrappedSceneLibrary = true;
    }

    private void RefreshLibrarySavedFlagsFromDrafts()
    {
        for (int i = 0; i < contentLibraryItems.Count; i++)
        {
            ContentLibraryItem item = contentLibraryItems[i];
            if (item == null)
                continue;

            if (item.instantiatedTransform == null)
            {
                if (item.localFileBytes != null && item.localFileBytes.Length > 0)
                    item.isSaved = false;
                continue;
            }

            item.isSaved = !IsTransformUnsaved(item.instantiatedTransform);
        }
    }

    private ContentLibraryItem FindLibraryItemByTransform(Transform tr)
    {
        for (int i = 0; i < contentLibraryItems.Count; i++)
        {
            ContentLibraryItem item = contentLibraryItems[i];
            if (item != null && item.instantiatedTransform == tr)
                return item;
        }
        return null;
    }

    private void DestroyCurrentSceneContentAndDetachLibraryRefs()
    {
        if (authoringTransformCoordinator == null)
            return;

        IReadOnlyList<Transform> entries = authoringTransformCoordinator.GetActiveContentEntries();
        if (entries == null)
            return;

        var copy = new List<Transform>(entries.Count);
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] != null)
                copy.Add(entries[i]);
        }

        for (int i = 0; i < copy.Count; i++)
        {
            Transform tr = copy[i];
            ContentLibraryItem linked = FindLibraryItemByTransform(tr);
            if (linked != null)
                linked.instantiatedTransform = null;

            RemoveDraftForTransform(tr);
            if (tr != null && tr.gameObject != null)
                Destroy(tr.gameObject);
        }
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

    private void ActivateLibraryItem(ContentLibraryItem item)
    {
        if (item == null || item.localFileBytes == null || item.localFileBytes.Length == 0)
            return;

        spawnerManager ??= BuildSpawnerManager();
        DestroyCurrentSceneContentAndDetachLibraryRefs();

        SpawnContentResult outcome = spawnerManager.CreateContent(new SpawnRequest
        {
            contentType = item.contentType,
            originalFileName = item.originalFileName,
            localFileBytes = item.localFileBytes,
            localMimeType = item.localMimeType,
            isLocalDraft = true
        });

        if (!outcome.success || outcome.spawnedObject == null)
        {
            if (filePathInput != null)
                filePathInput.value = "Library activation failed";
            Debug.LogError("Library activation spawn failed: " + outcome.message);
            return;
        }

        item.instantiatedTransform = outcome.spawnedObject.transform;
        item.isSaved = false;
        if (outcome.draggableObject != null)
        {
            RegisterLocalDraftFromLibraryItem(item, outcome.draggableObject);

            string label = "Object";
            if (outcome.contentType == SpawnContentType.Model) label = "Model";
            else if (outcome.contentType == SpawnContentType.Video) label = "Video";
            else if (outcome.contentType == SpawnContentType.Image) label = "Image";
            SetActiveAuthoringObject(outcome.draggableObject, "", label);
        }

        authoringTransformCoordinator?.SelectContentTransform(outcome.spawnedObject.transform, syncAuthoringUi: false);
        RefreshHierarchyListFromCoordinator();
    }

    private void RegisterLocalDraftFromLibraryItem(ContentLibraryItem item, DraggableObject draggableObject)
    {
        if (item == null || draggableObject == null)
            return;

        ContentDraftState existing = ResolveDraftForSelection(draggableObject.transform, draggableObject);
        ContentDraftState draft = existing ?? new ContentDraftState
        {
            draftId = Guid.NewGuid().ToString("N"),
            draggableObject = draggableObject,
            contentTransform = draggableObject.transform
        };

        draft.contentType = item.contentType;
        draft.targetId = GetActiveTargetIdForSave();
        draft.localFileName = item.originalFileName ?? "";
        draft.localFileBytes = item.localFileBytes;
        draft.localMimeType = item.localMimeType;
        draft.mediaUrl = "";
        draft.isUnsaved = true;
        draft.uploadPending = true;
        draft.persistPending = true;
        draft.lastError = "";

        contentDraftsByDraggable[draggableObject] = draft;
        contentDraftsByTransform[draggableObject.transform] = draft;
        item.instantiatedTransform = draggableObject.transform;
        item.isSaved = false;
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

    // Keep inspector coordinates synced when moving target/content via 3D interaction.
    SyncSpatialInspectorRealtime();
    SyncModeIndicatorLabel();
}

    private void SyncSpatialInspectorRealtime()
    {
        // Avoid fighting user typing.
        if (IsAnySpatialFieldFocused())
            return;

        // If the panel elements are missing (right panel trimmed), do nothing.
        if (posXInput == null || posYInput == null || posZInput == null)
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

        return focusedElement == posXInput || focusedElement == posYInput || focusedElement == posZInput;
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
            return;
        }

        if (modeIndicatorLabel != null && modeIndicatorLabel.style.display != DisplayStyle.Flex)
            modeIndicatorLabel.style.display = DisplayStyle.Flex;
        if (topBarModeGroup != null && topBarModeGroup.style.display != DisplayStyle.Flex)
            topBarModeGroup.style.display = DisplayStyle.Flex;

        TransformGizmoController.GizmoMode current = transformGizmoController.CurrentMode;
        if (current == _lastKnownGizmoMode)
            return;

        _lastKnownGizmoMode = current;
        if (modeIndicatorLabel != null)
            modeIndicatorLabel.text = "Mode: " + GetModeDisplayName(current);
        SetModePillActive(TransformGizmoController.GizmoMode.Translate, current == TransformGizmoController.GizmoMode.Translate);
        SetModePillActive(TransformGizmoController.GizmoMode.Rotate, current == TransformGizmoController.GizmoMode.Rotate);
        SetModePillActive(TransformGizmoController.GizmoMode.Scale, current == TransformGizmoController.GizmoMode.Scale);
        SetModePillActive(TransformGizmoController.GizmoMode.Universal, current == TransformGizmoController.GizmoMode.Universal);
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
        if (_loadingOverlay != null)
            _loadingOverlay.style.display = DisplayStyle.Flex;
    }

    public void HideLoading()
    {
        if (_loadingOverlay != null)
            _loadingOverlay.style.display = DisplayStyle.None;
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

        var localResult = spawnerManager.CreateTarget(new SpawnTargetRequest
        {
            targetName = normalizedName,
            targetId = normalizedTargetId,
            displayLabel = displayLabel,
            targetImageUrl = GetTargetImageUrlForCreateTarget()
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
        
        // create target  , save to database
        spawnerManager.BeginSyncCreateTarget(
            apiClient,
            new SpawnTargetRequest
            {
                targetName = normalizedName,
                targetId = normalizedTargetId,
                displayLabel = displayLabel,
                targetImageUrl = targetImageUrl
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
        if (result != null && result.success)
        {
            Debug.Log($"CreateTarget sync success: {result.payload?.targetId}");
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
        if (posXInput != null) posXInput.RegisterValueChangedCallback(OnPositionFloatFieldChanged);
        if (posYInput != null) posYInput.RegisterValueChangedCallback(OnPositionFloatFieldChanged);
        if (posZInput != null) posZInput.RegisterValueChangedCallback(OnPositionFloatFieldChanged);
        if (scaleInput != null) scaleInput.RegisterValueChangedCallback(OnScaleFloatFieldChanged);
    }

    private void UnregisterSpatialFieldCallbacks()
    {
        if (posXInput != null) posXInput.UnregisterValueChangedCallback(OnPositionFloatFieldChanged);
        if (posYInput != null) posYInput.UnregisterValueChangedCallback(OnPositionFloatFieldChanged);
        if (posZInput != null) posZInput.UnregisterValueChangedCallback(OnPositionFloatFieldChanged);
        if (scaleInput != null) scaleInput.UnregisterValueChangedCallback(OnScaleFloatFieldChanged);
    }

    private void OnPositionFloatFieldChanged(ChangeEvent<float> _)
    {
        if (suppressSpatialUiCallbacks)
            return;

        // Content inspector: edit selected content local position.
        if (authoringSpatialTarget != null)
        {
            Vector3 lp = authoringSpatialTarget.localPosition;
            lp.x = posXInput.value;
            lp.y = posYInput.value;
            lp.z = posZInput.value;
            authoringSpatialTarget.localPosition = lp;
            MarkActiveDraftDirty();
            return;
        }

        // Target inspector: edit active target local position (info + optional edit).
        if (targetSelectionManager == null)
            targetSelectionManager = ResolveTargetSelectionManager();

        if (targetSelectionManager == null || TargetMovementController.IsTargetDragActive)
            return;

        GameObject activeTarget = targetSelectionManager.GetActiveTarget();
        if (activeTarget == null)
            return;

        Vector3 targetLp = activeTarget.transform.localPosition;
        targetLp.x = posXInput.value;
        targetLp.y = posYInput.value;
        targetLp.z = posZInput.value;
        activeTarget.transform.localPosition = targetLp;
    }

    private void OnScaleFloatFieldChanged(ChangeEvent<float> _)
    {
        if (suppressSpatialUiCallbacks || authoringSpatialTarget == null || scaleInput == null)
            return;

        float s = Mathf.Max(0.01f, scaleInput.value);
        authoringSpatialTarget.localScale = Vector3.one * s;
        MarkActiveDraftDirty();
    }

    /// <summary>用于场景点击选中 / Gizmo 拖拽后，把 Transform 写回面板（位置 + 均匀缩放）。</summary>
    public void SyncTransformToInspector(Transform target)
    {
        if (target == null || posXInput == null)
            return;

        suppressSpatialUiCallbacks = true;
        try
        {
            Vector3 lp = target.localPosition;
            posXInput.value = (float)System.Math.Round(lp.x, 2);
            posYInput.value = (float)System.Math.Round(lp.y, 2);
            posZInput.value = (float)System.Math.Round(lp.z, 2);
            if (scaleInput != null)
                scaleInput.value = (float)System.Math.Round(target.localScale.x, 2);
        }
        finally
        {
            suppressSpatialUiCallbacks = false;
        }
    }

    private void ApplyInspectorModeContent()
    {
        inspectorMode = InspectorMode.Content;
        if (targetReferenceContainer != null)
            targetReferenceContainer.style.display = DisplayStyle.None;
        UpdateInspectorModeTabVisualState();
    }

    private void ApplyInspectorModeTarget()
    {
        inspectorMode = InspectorMode.Target;
        if (targetReferenceContainer != null)
            targetReferenceContainer.style.display = DisplayStyle.Flex;

        SyncTargetToInspector();
        RefreshTargetReferenceUiForActiveTarget();
        UpdateTargetReferenceStatusLabel(showUploadingText: false);
        UpdateInspectorModeTabVisualState();
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
        if (posXInput == null || posYInput == null || posZInput == null)
            return;

        if (targetSelectionManager == null)
            targetSelectionManager = ResolveTargetSelectionManager();

        suppressSpatialUiCallbacks = true;
        try
        {
            Vector3 lp = Vector3.zero;
            GameObject activeTarget = targetSelectionManager != null ? targetSelectionManager.GetActiveTarget() : null;
            if (activeTarget != null)
                lp = activeTarget.transform.localPosition;

            posXInput.value = (float)System.Math.Round(lp.x, 2);
            posYInput.value = (float)System.Math.Round(lp.y, 2);
            posZInput.value = (float)System.Math.Round(lp.z, 2);

            if (scaleInput != null)
                scaleInput.value = 1;
        }
        finally
        {
            suppressSpatialUiCallbacks = false;
        }
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

        TargetReferenceDraft activeDraft = GetActiveTargetReferenceDraft();
        if (activeDraft != null && activeDraft.bytes != null && activeDraft.isUnsaved)
        {
            string suffix = string.IsNullOrWhiteSpace(activeDraft.fileName) ? "" : $" ({activeDraft.fileName})";
            targetReferenceStatusLabel.text = $"Unsaved{suffix}";
            // Tailor to the UX: red for unsaved.
            targetReferenceStatusLabel.style.color = new StyleColor(new Color32(220, 53, 69, 255));
            return;
        }

        targetReferenceStatusLabel.text = "Not uploaded yet";
        targetReferenceStatusLabel.style.color = new StyleColor(new Color32(107, 114, 128, 255));
    }

    private void RefreshTargetReferenceUiForActiveTarget()
    {
        if (targetReferencePreviewImage == null)
            return;

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
        if (inspectorMode == InspectorMode.Target)
            ApplyInspectorModeTarget();
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

    /// <param name="alignToTargetFrame">图片应对齐场景中 TargetVisual 海报框的位置与缩放；文字一般保持 ContentRoot 原点。</param>
    private void ParentNewContentToActiveTarget(GameObject instance, bool alignToTargetFrame)
    {
        Transform contentRoot = TryGetActiveContentRoot();
        if (contentRoot == null)
        {
            Debug.LogWarning("AuthoringUIController: 未找到当前 Target 下的 ContentRoot，物体将挂在场景根下。");
            return;
        }

        instance.transform.SetParent(contentRoot, false);

        Transform targetVisual = contentRoot.parent != null ? contentRoot.parent.Find("TargetVisual") : null;
        if (alignToTargetFrame && targetVisual != null)
        {
            instance.transform.localPosition = targetVisual.localPosition;
            instance.transform.localRotation = targetVisual.localRotation;
            instance.transform.localScale = targetVisual.localScale;
            if (spawnForwardOffsetFromWall > 0f)
                instance.transform.position += instance.transform.forward * spawnForwardOffsetFromWall;
        }
        else
        {
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
        }
    }

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

        spawnerManager ??= BuildSpawnerManager();
        string textToDisplay = spawningTextInput.value;

        SpawnContentResult localResult = spawnerManager.CreateContent(new SpawnRequest
        {
            contentType = SpawnContentType.Text,
            textPayload = textToDisplay
        });
        if (!localResult.success || localResult.spawnedObject == null)
        {
            Debug.LogError("Text content spawn failed: " + localResult.message);
            return;
        }

        if (localResult.draggableObject != null)
        {
            RegisterTextDraft(localResult.draggableObject, textToDisplay);
            SetActiveAuthoringObject(localResult.draggableObject, textToDisplay, "Text");
        }

        authoringTransformCoordinator?.SelectContentTransform(localResult.spawnedObject.transform, syncAuthoringUi: false);
    }

    void OnBrowseButtonClicked()
    {
        if (!IsWorkspaceReadyForAuthoring(showBlockedMessage: true))
            return;

        pendingUploadPurpose = UploadPurpose.Content;
        #if UNITY_WEBGL || UNITY_EDITOR
        WebGLFileBrowser.OpenFilePanelWithFilters(".png,.jpg,.jpeg,.glb,.mp4,.mov", false);
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

        string fileName = selectedFile.fileInfo != null ? selectedFile.fileInfo.name : "";
        SetOrReplaceTargetReferenceDraft(pendingTargetReferenceTargetId, selectedFile.data, fileName);
        pendingTargetReferenceTargetId = null;
        UpdateTargetReferenceStatusLabel(showUploadingText: false);
    }

private void SpawnLocalContentFromFileSelection(FrostweepGames.Plugins.WebGLFileBrowser.File selectedFile)
{
    spawnerManager ??= BuildSpawnerManager();
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
    var libraryItem = new ContentLibraryItem
    {
        libraryId = Guid.NewGuid().ToString("N"),
        displayName = displayName,
        contentType = type,
        localFileBytes = selectedFile.data,
        localMimeType = mime,
        originalFileName = displayName,
        instantiatedTransform = null,
        isSaved = false
    };
    contentLibraryItems.Add(libraryItem);

    bool hasAnySceneContent = authoringTransformCoordinator != null
        && authoringTransformCoordinator.GetActiveContentEntries() != null
        && authoringTransformCoordinator.GetActiveContentEntries().Count > 0;

    if (hasAnySceneContent)
    {
        if (filePathInput != null)
            filePathInput.value = "Added to library: " + displayName;
        RefreshHierarchyListFromCoordinator();
        return;
    }

    ActivateLibraryItem(libraryItem);
    if (filePathInput != null)
        filePathInput.value = "Local draft: " + displayName;
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
        RefreshHierarchyListFromCoordinator();
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
        RefreshHierarchyListFromCoordinator();
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
        RefreshHierarchyListFromCoordinator();
    }

    private static string GuessMimeTypeFromExtension(string extension)
    {
        string lower = string.IsNullOrWhiteSpace(extension) ? "" : extension.Trim().ToLowerInvariant();
        if (lower == ".png" || lower == "png")
            return "image/png";
        if (lower == ".jpg" || lower == "jpg" || lower == ".jpeg" || lower == "jpeg")
            return "image/jpeg";
        if (lower == ".glb" || lower == "glb")
            return "model/gltf-binary";
        return "application/octet-stream";
    }

    private void MarkActiveDraftDirty()
    {
        if (activeContentDraft == null)
            activeContentDraft = ResolveDraftForSelection(authoringSpatialTarget, activeDraggedObject);
        if (activeContentDraft == null)
            return;

        activeContentDraft.isUnsaved = true;
        activeContentDraft.persistPending = true;
        activeContentDraft.targetId = GetActiveTargetIdForSave();
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
            saveButton.text = "Save Failed!";
            saveButton.schedule.Execute(() => { saveButton.text = "Save to Database"; }).StartingIn(2200);
            Debug.LogWarning("Save skipped: API client or spawner manager is not available.");
            return;
        }

        StartCoroutine(SaveAllDraftsRoutine());
    }

    void OnBackToSwitcherButtonClicked()
    {
        if (SceneTransitionService.IsTransitioning)
            return;

        AppFlowController.ClearWorkspaceSession();
        SceneTransitionService.TransitionToScene(AppFlowController.WorkspaceSwitcherSceneName);
    }

    private IEnumerator SaveAllDraftsRoutine()
    {
        isSaveInProgress = true;
        saveButton.text = "Saving...";

        List<ContentDraftState> drafts = CollectPendingDrafts();
        if (drafts.Count == 0)
        {
            saveButton.text = "Nothing to save";
            saveButton.schedule.Execute(() => { saveButton.text = "Save to Database"; }).StartingIn(1600);
            isSaveInProgress = false;
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
                MarkLibraryItemSavedByTransform(draft.contentTransform, saved: true);
            }
            else
            {
                failedCount++;
                draft.isUnsaved = true;
                draft.persistPending = true;
                MarkLibraryItemSavedByTransform(draft.contentTransform, saved: false);
            }
        }

        if (failedCount == 0)
        {
            saveButton.text = "Saved Successfully! ✓";
            saveButton.schedule.Execute(() => { saveButton.text = "Save to Database"; }).StartingIn(2000);
            Debug.Log($"Save complete: persisted {successCount} draft(s).");
        }
        else
        {
            saveButton.text = $"Save Partial ({successCount}/{drafts.Count})";
            saveButton.schedule.Execute(() => { saveButton.text = "Save to Database"; }).StartingIn(2600);
            Debug.LogWarning($"Save finished with failures: success={successCount}, failed={failedCount}.");
        }

        isSaveInProgress = false;
        RefreshHierarchyListFromCoordinator();
    }

    private void MarkLibraryItemSavedByTransform(Transform transform, bool saved)
    {
        if (transform == null)
            return;
        ContentLibraryItem item = FindLibraryItemByTransform(transform);
        if (item != null)
            item.isSaved = saved;
    }

    private List<ContentDraftState> CollectPendingDrafts()
    {
        var drafts = new List<ContentDraftState>();
        var seenIds = new HashSet<string>();

        foreach (ContentDraftState draft in contentDraftsByDraggable.Values)
        {
            if (draft == null || string.IsNullOrWhiteSpace(draft.draftId))
                continue;
            if (!draft.isUnsaved && !draft.persistPending)
                continue;
            if (!seenIds.Add(draft.draftId))
                continue;
            drafts.Add(draft);
        }

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
            uploadTimeoutSeconds);

        while (!done)
            yield return null;

        onCompleted?.Invoke(success);
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

    private SpawnRequest BuildSyncRequestFromDraft(ContentDraftState draft)
    {
        Vector3 localScale = draft.contentTransform != null ? draft.contentTransform.localScale : Vector3.one;
        return new SpawnRequest
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
        string url = youtubeUrlInput?.value;

        if (string.IsNullOrWhiteSpace(url))
        {
            Debug.LogWarning("YouTube URL is empty.");
            return;
        }

        spawnerManager ??= BuildSpawnerManager();
        string currentTargetId = GetActiveTargetIdForSave();
        SpawnContentResult localResult = spawnerManager.CreateContent(new SpawnRequest
        {
            contentType = SpawnContentType.Video,
            mediaUrl = url,
            targetId = currentTargetId
        });
        if (!localResult.success || localResult.spawnedObject == null)
        {
            Debug.LogError("YouTube content spawn failed: " + localResult.message);
            return;
        }

        if (localResult.draggableObject != null)
        {
            RegisterRemoteBackedDraft(
                localResult.draggableObject,
                SpawnContentType.Video,
                url,
                localFileName: "youtube-link");
            SetActiveAuthoringObject(localResult.draggableObject, url, "Video");
        }

        authoringTransformCoordinator?.SelectContentTransform(localResult.spawnedObject.transform, syncAuthoringUi: false);
        Debug.Log("Successfully spawned YouTube stream to AR wall.");
    }

    // This MUST be public so the DraggableObject can see it!
    public void UpdateCoordinatesFromDrag(Vector3 newPosition)
    {
        if (authoringSpatialTarget == null)
            return;

        suppressSpatialUiCallbacks = true;
        try
        {
            posXInput.value = (float)System.Math.Round(newPosition.x, 2);
            posYInput.value = (float)System.Math.Round(newPosition.y, 2);
            posZInput.value = (float)System.Math.Round(newPosition.z, 2);
            if (scaleInput != null)
                scaleInput.value = (float)System.Math.Round(authoringSpatialTarget.localScale.x, 2);
        }
        finally
        {
            suppressSpatialUiCallbacks = false;
        }
    }
}