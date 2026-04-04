using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Networking;
using TMPro; // NEW: Required for TextMeshPro
using System.Collections;
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

    [SerializeField] private string uploadApiUrl = "http://127.0.0.1:5050/api/upload";

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
        
        // NEW: Text Spawning UI elements
        spawningTextInput = root.Q<TextField>("SpawningTextInput");
        spawnTextButton = root.Q<Button>("SpawnTextButton");
        
        browseButton = root.Q<Button>("BrowseButton");
        saveButton = root.Q<Button>("SaveButton");

        // Event Listeners
        browseButton.clicked += OnBrowseButtonClicked;
        saveButton.clicked += OnSaveButtonClicked;
        
        // NEW: Event Listener for spawning text
        spawnTextButton.clicked += OnSpawnTextButtonClicked;

        // NEW: Listen for when the user selects a file in the browser
        WebGLFileBrowser.FilesWereOpenedEvent += OnFilesOpened;

        RegisterSpatialFieldCallbacks();

        if (targetSelectionManager == null)
            targetSelectionManager = FindFirstObjectByType<TargetSelectionManager>();

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
        if (saveButton != null) saveButton.clicked -= OnSaveButtonClicked;
        if (spawnTextButton != null) spawnTextButton.clicked -= OnSpawnTextButtonClicked;

        if (imageTargetDropdown != null)
            imageTargetDropdown.UnregisterValueChangedCallback(OnImageTargetDropdownChanged);
        if (targetSelectionManager != null)
            targetSelectionManager.ActiveTargetChanged -= OnManagerActiveTargetChanged;
    }

    private void RefreshImageTargetDropdownChoices()
    {
        if (imageTargetDropdown == null)
            return;

        if (targetSelectionManager == null)
            targetSelectionManager = FindFirstObjectByType<TargetSelectionManager>();

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
        if (targetSelectionManager == null)
            targetSelectionManager = FindFirstObjectByType<TargetSelectionManager>();
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
        if (textPrefab == null) { Debug.LogError("Text Prefab is not assigned in the Inspector!"); return; }

        string textToDisplay = spawningTextInput.value;

        // Instantiation! Spawn the text prefab into the scene origin.
        GameObject spawnedTextObj = Instantiate(textPrefab, Vector3.zero, Quaternion.identity);
        ParentNewContentToActiveTarget(spawnedTextObj, alignToTargetFrame: false);

        // Assign the text value to the TextMeshPro component
        spawnedTextObj.GetComponent<TextMeshPro>().text = textToDisplay;
        
        DraggableObject dragHandler = spawnedTextObj.GetComponent<DraggableObject>();
        if (dragHandler != null)
        {
            spawnedMediaUrls[dragHandler] = textToDisplay;
            SetActiveAuthoringObject(dragHandler, textToDisplay, "Text");
        }

        FindFirstObjectByType<ContentTransformController>()?.SelectContentTransform(spawnedTextObj.transform, syncAuthoringUi: false);
    }

    void OnBrowseButtonClicked()
    {
        WebGLFileBrowser.OpenFilePanelWithFilters(".png,.jpg", false);
    }

    // This runs automatically when an image is selected
    private void OnFilesOpened(File[] files)
    {
        if (files == null || files.Length == 0)
            return;

        var selectedFile = files[0];
        if (filePathInput != null)
            filePathInput.value = "Uploading: " + selectedFile.fileInfo.name;

        StartCoroutine(UploadFileCoroutine(selectedFile));
    }

    /// <summary>
    /// Frostweep 在 Editor 里用 Path.GetExtension，扩展名已带点（.png）。不能再写 name + "." + extension，否则会 Poster_A..png。
    /// 优先用 fullName（与系统里真实文件名一致）。
    /// </summary>
    private static string GetSanitizedUploadFileName(File file)
    {
        if (file?.fileInfo == null)
            return "upload.bin";

        if (!string.IsNullOrEmpty(file.fileInfo.fullName))
            return System.IO.Path.GetFileName(file.fileInfo.fullName.Trim());

        string baseName = string.IsNullOrEmpty(file.fileInfo.name) ? "image" : file.fileInfo.name.TrimEnd('.');
        string ext = file.fileInfo.extension ?? "";
        if (string.IsNullOrEmpty(ext))
            return baseName;
        return ext.StartsWith(".") ? baseName + ext : baseName + "." + ext;
    }

    private IEnumerator UploadFileCoroutine(File file)
    {
        string uploadName = GetSanitizedUploadFileName(file);
        WWWForm form = new WWWForm();
        form.AddBinaryData("file", file.data, uploadName);

        using (UnityWebRequest request = UnityWebRequest.Post(uploadApiUrl, form))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string jsonResponse = request.downloadHandler.text;
                string uploadedUrl = JsonUtility.FromJson<UploadResponse>(jsonResponse).url;

                if (filePathInput != null)
                    filePathInput.value = uploadedUrl;
                if (youtubeUrlInput != null)
                    youtubeUrlInput.value = "";
                Debug.Log("Upload complete! URL: " + uploadedUrl);
                InstantiatePictureAtOrigin(uploadedUrl, System.IO.Path.GetFileNameWithoutExtension(uploadName));
            }
            else
            {
                if (filePathInput != null)
                    filePathInput.value = "Upload Failed!";

                string body = request.downloadHandler != null ? request.downloadHandler.text : "";
                Debug.LogError(
                    $"Upload failed: {request.error} HTTP {(long)request.responseCode} " +
                    (string.IsNullOrEmpty(body) ? "" : $"Body: {body}"));
            }
        }
    }

    // --- NEW: Spawning Images ---
    private void InstantiatePictureAtOrigin(string url, string filename)
    {
        if (picturePrefab == null) { Debug.LogError("Picture Prefab is not assigned in the Inspector!"); return; }

        // 1. Instantiation! Spawn the picture prefab.
        GameObject spawnedPicObj = Instantiate(picturePrefab, Vector3.zero, Quaternion.identity);
        ParentNewContentToActiveTarget(spawnedPicObj, alignToTargetFrame: true);

        DraggableObject dragHandler = spawnedPicObj.GetComponent<DraggableObject>();
        if (dragHandler != null)
        {
            spawnedMediaUrls[dragHandler] = url;
            StartCoroutine(ApplyTextureToSpawningObject(spawnedPicObj, url));
            SetActiveAuthoringObject(dragHandler, url, "Image (" + filename + ")");
        }

        FindFirstObjectByType<ContentTransformController>()?.SelectContentTransform(spawnedPicObj.transform, syncAuthoringUi: false);
    }

    // Coroutine to download the texture and apply it to the Unlit/Texture shader
    private IEnumerator ApplyTextureToSpawningObject(GameObject objToTex, string url)
    {
        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
        {
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                Texture2D texture = ((DownloadHandlerTexture)request.downloadHandler).texture;
                // Apply the texture directly to the Renderer's material.
                objToTex.GetComponent<Renderer>().material.mainTexture = texture;
            }
            else { Debug.LogError("Error loading image for preview: " + request.error); }
        }
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

    // Helper: Required helper class to parse the JSON response from your Flask server
    [System.Serializable]
    public class UploadResponse { public string url; }

    private static bool IsPlaceholderImagePath(string v)
    {
        if (string.IsNullOrWhiteSpace(v))
            return true;
        return v == "No file..." || v == "Upload Failed!" || v.StartsWith("Uploading:");
    }

    private static bool LooksLikeYouTubeUrl(string u)
    {
        if (string.IsNullOrWhiteSpace(u))
            return false;
        string lower = u.ToLowerInvariant();
        return lower.Contains("youtube.com/") || lower.Contains("youtu.be/");
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

        dbManager.SaveContentToDatabase(type, position, scale, url, GetActiveTargetIdForSave());
        
        saveButton.text = "Saved Successfully! ✓";
        saveButton.schedule.Execute(() => { saveButton.text = "Save to Database"; }).StartingIn(2000);
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
