using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using FrostweepGames.Plugins.WebGLFileBrowser; // NEW: Access the plugin

public class AuthoringUIController : MonoBehaviour
{
    public DatabaseManager dbManager;

    [SerializeField] private TargetSelectionManager targetSelectionManager;

    // --- NEW: Prefab Templates (Drag these in the Inspector) ---
    public GameObject picturePrefab;
    public GameObject textPrefab;
    
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

    // Track the object that is currently "active" in the UI (being dragged)
    private DraggableObject activeDraggedObject;
    /// <summary>当前与面板坐标/缩放绑定的 Transform（含无 DraggableObject 的 Cube 等）。</summary>
    private Transform authoringSpatialTarget;
    private Dictionary<DraggableObject, string> spawnedMediaUrls = new Dictionary<DraggableObject, string>();
    private UIDocument uiDocument;

    /// <summary>为 true 时忽略 FloatField 回调，避免从脚本写 UI 时反向改 Transform。</summary>
    private bool suppressSpatialUiCallbacks;

    [SerializeField] private MonoBehaviour apiClientBehaviour;
    [SerializeField] private float createTargetTimeoutSeconds = 20f;
    [SerializeField] private float uploadTimeoutSeconds = 20f;
    [SerializeField] private float createContentTimeoutSeconds = 20f;
    private IApiClient apiClient;
    private readonly TargetWorkflowService targetWorkflowService = new TargetWorkflowService();
    private readonly UploadWorkflowService uploadWorkflowService = new UploadWorkflowService();
    private readonly ContentWorkflowService contentWorkflowService = new ContentWorkflowService();
    private string pendingTargetImageUrl = "";
    private UploadPurpose pendingUploadPurpose = UploadPurpose.Content;

    private enum UploadPurpose
    {
        Content,
        TargetImage
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

        // Event Listeners
        browseButton.clicked += OnBrowseButtonClicked;
        if (browseTargetImageButton != null) browseTargetImageButton.clicked += OnBrowseTargetImageButtonClicked;
        saveButton.clicked += OnSaveButtonClicked;
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

        RefreshImageTargetDropdownChoices();
        if (imageTargetDropdown != null)
            imageTargetDropdown.RegisterValueChangedCallback(OnImageTargetDropdownChanged);
        if (targetSelectionManager != null)
            targetSelectionManager.ActiveTargetChanged += OnManagerActiveTargetChanged;
    }

    void OnDisable()
    {
        UnregisterSpatialFieldCallbacks();
        WebGLFileBrowser.FilesWereOpenedEvent -= OnFilesOpened;

        if (browseButton != null) browseButton.clicked -= OnBrowseButtonClicked;
        if (browseTargetImageButton != null) browseTargetImageButton.clicked -= OnBrowseTargetImageButtonClicked;
        if (saveButton != null) saveButton.clicked -= OnSaveButtonClicked;
        if (spawnTextButton != null) spawnTextButton.clicked -= OnSpawnTextButtonClicked;
        if (createTargetButton != null) createTargetButton.clicked -= OnCreateTargetButtonClicked;

        if (imageTargetDropdown != null)
            imageTargetDropdown.UnregisterValueChangedCallback(OnImageTargetDropdownChanged);
        if (targetSelectionManager != null)
            targetSelectionManager.ActiveTargetChanged -= OnManagerActiveTargetChanged;
    }
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
        targetSelectionManager = ResolveTargetSelectionManager();
        apiClient = ResolveApiClient();

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

        var localResult = targetWorkflowService.CreateAndRegisterLocal(
            this,
            normalizedName,
            normalizedTargetId,
            displayLabel);

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
        targetWorkflowService.SyncCreateTarget(
            apiClient,
            localResult.targetObject,
            normalizedTargetId,
            normalizedName,
            displayLabel,
            targetImageUrl,
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
    // 
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
    /// <param name="targetIdInput">The target id input.</param>
    /// <param name="fallbackName">The fallback name.</param>
    /// <returns>The normalized target id.</returns>
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
    /// <param name="message">The message to show.</param>
    /// <param name="isError">True if the message is an error, false otherwise.</param>
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
    }

    private void OnScaleFloatFieldChanged(ChangeEvent<float> _)
    {
        if (suppressSpatialUiCallbacks || authoringSpatialTarget == null)
            return;

        float s = Mathf.Max(0.01f, scaleInput.value);
        authoringSpatialTarget.localScale = Vector3.one * s;
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

        if (activeDraggedObject != null && spawnedMediaUrls.TryGetValue(activeDraggedObject, out string url))
            ApplyUrlToMediaFields(url);
    }

    /// <summary>切换 Target 后若无选中内容，清空坐标绑定，避免仍在改「已隐藏目标」上的 Transform。</summary>
    public void ClearAuthoringSpatialSelection()
    {
        authoringSpatialTarget = null;
        activeDraggedObject = null;
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

    // --- NEW: Text Spawning ---
    void OnSpawnTextButtonClicked()
    {
        string textToDisplay = spawningTextInput.value;

        var localResult = contentWorkflowService.SpawnTextLocal(textPrefab, textToDisplay);
        if (!localResult.success || localResult.spawnedObject == null)
        {
            Debug.LogError("Text content spawn failed: " + localResult.message);
            return;
        }

        ParentNewContentToActiveTarget(localResult.spawnedObject, alignToTargetFrame: false);
        if (localResult.draggableObject != null)
        {
            spawnedMediaUrls[localResult.draggableObject] = textToDisplay;
            SetActiveAuthoringObject(localResult.draggableObject, textToDisplay, localResult.contentType);
        }

        FindFirstObjectByType<ContentTransformController>()?.SelectContentTransform(localResult.spawnedObject.transform, syncAuthoringUi: false);
    }

    void OnBrowseButtonClicked()
    {
        pendingUploadPurpose = UploadPurpose.Content;
        WebGLFileBrowser.OpenFilePanelWithFilters(".png,.jpg", false);
    }

    void OnBrowseTargetImageButtonClicked()
    {
        pendingUploadPurpose = UploadPurpose.TargetImage;
        if (createTargetImageUrlInput != null)
            createTargetImageUrlInput.value = "Uploading target image...";
        WebGLFileBrowser.OpenFilePanelWithFilters(".png,.jpg", false);
    }

    // This runs automatically when an image is selected
    private void OnFilesOpened(File[] files)
    {
        if (files == null || files.Length == 0)
            return;

        var selectedFile = files[0];
        bool isTargetImageUpload = pendingUploadPurpose == UploadPurpose.TargetImage;
        if (isTargetImageUpload)
        {
            if (createTargetImageUrlInput != null)
                createTargetImageUrlInput.value = "Uploading target image...";
        }
        else if (filePathInput != null)
            filePathInput.value = "Uploading: " + selectedFile.fileInfo.name;

        apiClient = ResolveApiClient();
        uploadWorkflowService.UploadSelectedFile(
            selectedFile,
            apiClient,
            result =>
            {
                if (isTargetImageUpload)
                    OnTargetImageUploadCompleted(result, selectedFile);
                else
                    OnUploadCompleted(result, selectedFile);
            },
            uploadTimeoutSeconds);
    }

    private void OnTargetImageUploadCompleted(ApiResult<UploadFileResponseDto> result, File selectedFile)
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

        // If a target is already active, apply the freshly uploaded target texture immediately.
        targetSelectionManager = ResolveTargetSelectionManager();
        GameObject activeTarget = targetSelectionManager != null ? targetSelectionManager.GetActiveTarget() : null;
        ApplyPendingTargetImageToTarget(activeTarget);

        Debug.Log("Target image upload complete via IApiClient! URL: " + pendingTargetImageUrl);
    }

    private void OnUploadCompleted(ApiResult<UploadFileResponseDto> result, File selectedFile)
    {
        if (result == null || !result.success || result.payload == null || string.IsNullOrWhiteSpace(result.payload.url))
        {
            if (filePathInput != null)
                filePathInput.value = "Upload Failed!";

            string code = result != null ? result.errorCode : ApiErrorCodes.Unknown;
            string message = result != null ? result.message : "No result";
            Debug.LogError($"Upload failed via IApiClient: [{code}] {message}");
            return;
        }

        string uploadedUrl = result.payload.url.Trim();
        if (filePathInput != null)
            filePathInput.value = uploadedUrl;
        if (youtubeUrlInput != null)
            youtubeUrlInput.value = "";

        string baseName = selectedFile?.fileInfo != null && !string.IsNullOrWhiteSpace(selectedFile.fileInfo.name)
            ? selectedFile.fileInfo.name
            : "image";
        string fileNameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(baseName);

        Debug.Log("Upload complete via IApiClient! URL: " + uploadedUrl);
        var localResult = contentWorkflowService.SpawnImageLocal(this, picturePrefab, uploadedUrl, fileNameWithoutExt);
        if (!localResult.success || localResult.spawnedObject == null)
        {
            Debug.LogError("Content spawn failed: " + localResult.message);
            return;
        }

        ParentNewContentToActiveTarget(localResult.spawnedObject, alignToTargetFrame: true);
        if (localResult.draggableObject != null)
        {
            spawnedMediaUrls[localResult.draggableObject] = uploadedUrl;
            SetActiveAuthoringObject(localResult.draggableObject, uploadedUrl, localResult.contentType);
        }

        FindFirstObjectByType<ContentTransformController>()?.SelectContentTransform(localResult.spawnedObject.transform, syncAuthoringUi: false);
    }

    // Helper: When an object is spawned or selected, update UI fields
    private void SetActiveAuthoringObject(DraggableObject targetObj, string mediaValue, string contentType)
    {
        activeDraggedObject = targetObj;
        authoringSpatialTarget = targetObj.transform;

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

        // Set Content Type
        contentTypeInput.value = contentType;
        
        Debug.Log("Now authoring " + targetObj.gameObject.name);
    }


    private static bool LooksLikeYouTubeUrl(string u)
    {
        if (string.IsNullOrWhiteSpace(u))
            return false;
        string lower = u.ToLowerInvariant();
        return lower.Contains("youtube.com/") || lower.Contains("youtu.be/");
    }

    private static bool IsPlaceholderImagePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;

        string normalized = value.Trim().ToLowerInvariant();
        return normalized == "no file..."
            || normalized == "upload failed!"
            || normalized.StartsWith("uploading:");
    }

    /// <summary>YouTube 填在独立框；保存时仍写入现有 MediaURL 字段，后端无需改表。</summary>
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

    private string GetMediaUrlForSave()
    {
        if (youtubeUrlInput != null)
        {
            string yt = youtubeUrlInput.value.Trim();
            if (yt.Length > 0)
                return yt;
        }

        if (filePathInput != null)
        {
            string f = filePathInput.value.Trim();
            if (f.Length > 0 && !IsPlaceholderImagePath(f))
                return f;
        }

        return "";
    }

    // Coroutine and SaveButton method from earlier
    void OnSaveButtonClicked()
    {
        string type = contentTypeInput.value;
        Vector3 position = new Vector3(posXInput.value, posYInput.value, posZInput.value);
        float scale = scaleInput.value;
        string url = GetMediaUrlForSave();
        string targetId = GetActiveTargetIdForSave();

        apiClient = ResolveApiClient();
        if (apiClient == null)
        {
            // Temporary fallback while moving from DatabaseManager to API abstraction.
            dbManager.SaveContentToDatabase(type, position, scale, url, targetId);
            saveButton.text = "Saved Successfully! ✓";
            saveButton.schedule.Execute(() => { saveButton.text = "Save to Database"; }).StartingIn(2000);
            return;
        }

        contentWorkflowService.SyncCreateContent(
            apiClient,
            type,
            position,
            Vector3.zero,
            new Vector3(scale, scale, scale),
            url,
            targetId,
            OnCreateContentSyncCompleted,
            createContentTimeoutSeconds);
    }

    private void OnCreateContentSyncCompleted(ApiResult<CreateContentResponseDto> result)
    {
        bool success = result != null && result.success;
        if (success)
        {
            saveButton.text = "Saved Successfully! ✓";
            saveButton.schedule.Execute(() => { saveButton.text = "Save to Database"; }).StartingIn(2000);
            Debug.Log($"CreateContent sync success: {result.payload?.contentId}");
            return;
        }

        string code = result != null ? result.errorCode : ApiErrorCodes.Unknown;
        string message = result != null ? result.message : "No result";
        saveButton.text = "Save Failed!";
        saveButton.schedule.Execute(() => { saveButton.text = "Save to Database"; }).StartingIn(2200);
        Debug.LogWarning($"CreateContent sync failed (local content kept): [{code}] {message}");
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
