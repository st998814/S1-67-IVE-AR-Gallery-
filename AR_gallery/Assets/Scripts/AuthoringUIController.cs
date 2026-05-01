using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;
using System.Collections.Generic;
using ARGallery.Content;
using FrostweepGames.Plugins.WebGLFileBrowser;
using ARGallery.Spawning;
using ARGallery.AppFlow;
using System;
using UnityEngine.InputSystem;

public class AuthoringUIController : MonoBehaviour
{
    public DatabaseManager dbManager;
    public GameObject videoPrefab;

    [SerializeField] private TargetSelectionManager targetSelectionManager;

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

    private Button browseButton, saveButton;
    private Button backToSwitcherButton;

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

    private enum UploadPurpose
    {
        Content,
        TargetImage
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
        if (spawnTextButton != null) spawnTextButton.SetEnabled(ready);
        if (createTargetButton != null) createTargetButton.SetEnabled(ready);
        if (browseTargetImageButton != null) browseTargetImageButton.SetEnabled(ready);
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
        
        browseButton = root.Q<Button>("BrowseButton");
        saveButton = root.Q<Button>("SaveButton");
        backToSwitcherButton = root.Q<Button>("BackToSwitcherButton");

        // --- TASK 6: 获取并初始化 Loading 和 Error 元素 ---
        _loadingOverlay = root.Q<VisualElement>("loading-overlay");
        _errorToast = root.Q<VisualElement>("error-toast");
        _errorLabel = root.Q<Label>("error-label");

        HideLoading();
        HideErrorToast();
        // ------------------------------------------------

        // Event Listeners
        browseButton.clicked += OnBrowseButtonClicked;
        if (browseTargetImageButton != null) browseTargetImageButton.clicked += OnBrowseTargetImageButtonClicked;
        saveButton.clicked += OnSaveButtonClicked;
        if (backToSwitcherButton != null) backToSwitcherButton.clicked += OnBackToSwitcherButtonClicked;
        if (createTargetButton != null) createTargetButton.clicked += OnCreateTargetButtonClicked;

        if (createTargetImageUrlInput != null && string.IsNullOrWhiteSpace(createTargetImageUrlInput.value))
            createTargetImageUrlInput.value = "No target image selected";
        
        // NEW: Event Listener for spawning text
        spawnTextButton.clicked += OnSpawnTextButtonClicked;

        // NEW: Listen for when the user selects a file in the browser
        WebGLFileBrowser.FilesWereOpenedEvent += OnFilesOpened;

        RegisterSpatialFieldCallbacks();

        targetSelectionManager = ResolveTargetSelectionManager();
        apiClient = ResolveApiClient();
        spawnerManager = BuildSpawnerManager();

        RefreshImageTargetDropdownChoices();
        if (imageTargetDropdown != null)
            imageTargetDropdown.RegisterValueChangedCallback(OnImageTargetDropdownChanged);
        if (targetSelectionManager != null)
            targetSelectionManager.ActiveTargetChanged += OnManagerActiveTargetChanged;
        RefreshWorkspaceGuardUiState();
    }

    void OnDisable()
    {
        UnregisterSpatialFieldCallbacks();
        WebGLFileBrowser.FilesWereOpenedEvent -= OnFilesOpened;

        if (browseButton != null) browseButton.clicked -= OnBrowseButtonClicked;
        if (browseTargetImageButton != null) browseTargetImageButton.clicked -= OnBrowseTargetImageButtonClicked;
        if (saveButton != null) saveButton.clicked -= OnSaveButtonClicked;
        if (backToSwitcherButton != null) backToSwitcherButton.clicked -= OnBackToSwitcherButtonClicked;
        if (spawnTextButton != null) spawnTextButton.clicked -= OnSpawnTextButtonClicked;
        if (createTargetButton != null) createTargetButton.clicked -= OnCreateTargetButtonClicked;

        if (imageTargetDropdown != null)
            imageTargetDropdown.UnregisterValueChangedCallback(OnImageTargetDropdownChanged);
        if (targetSelectionManager != null)
            targetSelectionManager.ActiveTargetChanged -= OnManagerActiveTargetChanged;
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
        if (suppressSpatialUiCallbacks || authoringSpatialTarget == null)
            return;

        Vector3 lp = authoringSpatialTarget.localPosition;
        lp.x = posXInput.value;
        lp.y = posYInput.value;
        lp.z = posZInput.value;
        authoringSpatialTarget.localPosition = lp;
        MarkActiveDraftDirty();
    }

    private void OnScaleFloatFieldChanged(ChangeEvent<float> _)
    {
        if (suppressSpatialUiCallbacks || authoringSpatialTarget == null)
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
            scaleInput.value = (float)System.Math.Round(target.localScale.x, 2);
        }
        finally
        {
            suppressSpatialUiCallbacks = false;
        }
    }

    /// <summary>场景里选中 ContentRoot 下的物体时调用，与 Gizmo / 保存逻辑对齐。</summary>
    public void OnContentSelectedInScene(Transform contentTransform)
    {
        if (contentTransform == null)
            return;

        authoringSpatialTarget = contentTransform;
        activeDraggedObject = contentTransform.GetComponent<DraggableObject>();

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
        suppressSpatialUiCallbacks = true;
        try
        {
            if (posXInput != null) posXInput.value = 0;
            if (posYInput != null) posYInput.value = 0;
            if (posZInput != null) posZInput.value = 0;
            if (scaleInput != null) scaleInput.value = 1;
        }
        finally
        {
            suppressSpatialUiCallbacks = false;
        }
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

        FindFirstObjectByType<ContentTransformController>()?.SelectContentTransform(localResult.spawnedObject.transform, syncAuthoringUi: false);
    }

    void OnBrowseButtonClicked()
    {
        if (!IsWorkspaceReadyForAuthoring(showBlockedMessage: true))
            return;

        pendingUploadPurpose = UploadPurpose.Content;
        WebGLFileBrowser.OpenFilePanelWithFilters(".png,.jpg,.jpeg,.glb,.mp4,.mov", false);
    }



    void OnBrowseTargetImageButtonClicked()
    {
        if (!IsWorkspaceReadyForAuthoring(showBlockedMessage: true))
            return;

        pendingUploadPurpose = UploadPurpose.TargetImage;
        if (createTargetImageUrlInput != null)
            createTargetImageUrlInput.value = "Uploading target image...";
        WebGLFileBrowser.OpenFilePanelWithFilters(".png,.jpg", false);
    }

    // This runs automatically when an image is selected
    private void OnFilesOpened(FrostweepGames.Plugins.WebGLFileBrowser.File[] files)
    {
        if (!IsWorkspaceReadyForAuthoring(showBlockedMessage: true))
            return;

        if (files == null || files.Length == 0)
            return;

        var selectedFile = files[0];
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
        }
        else
            SpawnLocalContentFromFileSelection(selectedFile);
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

    // ... rest of the method remains exactly the same

        SpawnContentResult outcome = spawnerManager.CreateContent(new SpawnRequest
        {
            contentType = type,
            originalFileName = displayName,
            localFileBytes = selectedFile.data,
            localMimeType = GuessMimeTypeFromExtension(extension),
            isLocalDraft = true
        });

        if (!outcome.success || outcome.spawnedObject == null)
        {
            if (filePathInput != null)
                filePathInput.value = "Local spawn failed";
            Debug.LogError("Local content spawn failed: " + outcome.message);
            return;
        }

        if (filePathInput != null)
            filePathInput.value = "Local draft: " + displayName;
        if (youtubeUrlInput != null)
            youtubeUrlInput.value = "";

        if (outcome.draggableObject != null)
        {
            RegisterLocalDraft(outcome.draggableObject, outcome.contentType, selectedFile, displayName);
            
            string label = "Object";
            if (outcome.contentType == SpawnContentType.Model) label = "Model";
            else if (outcome.contentType == SpawnContentType.Video) label = "Video";
            else if (outcome.contentType == SpawnContentType.Image) label = "Image";

            SetActiveAuthoringObject(outcome.draggableObject, "", label);
        }

        FindFirstObjectByType<ContentTransformController>()?.SelectContentTransform(outcome.spawnedObject.transform, syncAuthoringUi: false);
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

        if (contentType != null && contentType.StartsWith("Text", System.StringComparison.Ordinal))
        {
            if (youtubeUrlInput != null) youtubeUrlInput.value = "";
            if (filePathInput != null)
                filePathInput.value = string.IsNullOrWhiteSpace(mediaValue) ? "No file..." : mediaValue.Trim();
        }
        else
            ApplyUrlToMediaFields(mediaValue);

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

        FindFirstObjectByType<ContentTransformController>()?.SelectContentTransform(localResult.spawnedObject.transform, syncAuthoringUi: false);
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
            scaleInput.value = (float)System.Math.Round(authoringSpatialTarget.localScale.x, 2);
        }
        finally
        {
            suppressSpatialUiCallbacks = false;
        }
    }
}