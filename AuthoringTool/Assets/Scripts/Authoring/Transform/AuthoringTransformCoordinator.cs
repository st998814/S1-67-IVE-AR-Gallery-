using System.Collections.Generic;
using ARGallery.Workspace.Persistence;
using RTG;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Wires sandbox transform stack (<see cref="ObjectSelectionManager"/>, <see cref="TransformGizmoController"/>)
/// into the authoring scene: target switching, UI sync, and legacy keyboard / selection affordances.
/// </summary>
[DefaultExecutionOrder(-40)]
public sealed class AuthoringTransformCoordinator : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private TargetSelectionManager targetSelectionManager;
    [SerializeField] private ObjectSelectionManager objectSelectionManager;
    [SerializeField] private TransformGizmoController gizmoController;
    [SerializeField] private FrontSideConstraint frontSideConstraint;
    [SerializeField] private PlacementBoundsService placementBoundsService;
    [SerializeField] private ContentTransformManipulator contentTransformManipulator;
    [SerializeField] private AuthoringUIController authoringUI;
    [SerializeField] private Camera mainCamera;

    [Header("Keyboard nudges (when gizmo is not active)")]
    [SerializeField] private bool enableKeyboardNudges = true;
    [SerializeField] private float moveStep = 0.1f;
    [SerializeField] private float rotateStep = 10f;
    [SerializeField] private float scaleStep = 0.1f;

    [Header("Selection Highlight")]
    [SerializeField] private Color selectionBoundsColor = new Color(0.23f, 0.51f, 0.96f, 0.9f);
    [SerializeField] private float selectionBoundsPadding = 0.03f;
    [SerializeField] private float selectionPulseSpeed = 2.2f;
    [SerializeField] private float selectionPulseAlphaRange = 0.25f;
    [SerializeField] private float selectionEdgeWidth = 0.0075f;

    private readonly List<Transform> _contentObjects = new List<Transform>();
    private readonly List<int> _lastContentInstanceIds = new List<int>();
    private int _selectedListIndex = -1;
    private int _authoringSyncedTargetIndex = int.MinValue;
    private bool _suppressAuthoringSyncFromSelection;
    private Transform _highlightTarget;
    private GameObject _selectionBoundsVisual;
    private readonly List<LineRenderer> _selectionEdgeRenderers = new List<LineRenderer>(12);
    private Material _selectionBoundsMaterial;
    private Color _baseSelectionBoundsColor;
    private float _pulseSeed;
    public event System.Action ContentListChanged;
    public event System.Action<Transform> ContentSelectionChanged;

    public IReadOnlyList<Transform> GetActiveContentEntries()
    {
        return _contentObjects;
    }

    public Transform GetSelectedContentTransform()
    {
        return objectSelectionManager != null ? objectSelectionManager.Selected : null;
    }

    private void Awake()
    {
        RTGRuntimeBootstrap.EnsureRTGModules();

        if (targetSelectionManager == null)
            targetSelectionManager = FindFirstObjectByType<TargetSelectionManager>();
        if (objectSelectionManager == null)
            objectSelectionManager = FindFirstObjectByType<ObjectSelectionManager>();
        if (gizmoController == null)
            gizmoController = FindFirstObjectByType<TransformGizmoController>();
        if (frontSideConstraint == null)
            frontSideConstraint = FindFirstObjectByType<FrontSideConstraint>();
        if (placementBoundsService == null)
            placementBoundsService = FindFirstObjectByType<PlacementBoundsService>();
        if (placementBoundsService == null && frontSideConstraint != null)
        {
            placementBoundsService = frontSideConstraint.gameObject.AddComponent<PlacementBoundsService>();
            placementBoundsService.Configure(frontSideConstraint);
        }
        if (contentTransformManipulator == null)
            contentTransformManipulator = FindFirstObjectByType<ContentTransformManipulator>();
        if (contentTransformManipulator == null && frontSideConstraint != null)
        {
            var localSvc = FindFirstObjectByType<TargetLocalTransformService>();
            contentTransformManipulator = frontSideConstraint.gameObject.AddComponent<ContentTransformManipulator>();
            contentTransformManipulator.Configure(localSvc, placementBoundsService, frontSideConstraint);
        }
        if (authoringUI == null)
            authoringUI = FindFirstObjectByType<AuthoringUIController>();
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (gizmoController != null && objectSelectionManager != null && contentTransformManipulator != null)
            gizmoController.ConfigureDependencies(objectSelectionManager, contentTransformManipulator);
    }

    public ContentTransformManipulator ContentManipulator => contentTransformManipulator;

    private void OnEnable()
    {
        if (targetSelectionManager != null)
            targetSelectionManager.ActiveTargetChanged += OnActiveAuthoringTargetChanged;
        if (gizmoController != null)
            gizmoController.ContentTransformChanged += OnGizmoContentTransformChanged;
        if (objectSelectionManager != null)
            objectSelectionManager.SelectionChanged += OnObjectSelectionChanged;
    }

    private void OnDisable()
    {
        if (targetSelectionManager != null)
            targetSelectionManager.ActiveTargetChanged -= OnActiveAuthoringTargetChanged;
        if (gizmoController != null)
            gizmoController.ContentTransformChanged -= OnGizmoContentTransformChanged;
        if (objectSelectionManager != null)
            objectSelectionManager.SelectionChanged -= OnObjectSelectionChanged;
        HideSelectionBoundsVisual();
    }

    private void Start()
    {
        if (targetSelectionManager != null)
            _authoringSyncedTargetIndex = targetSelectionManager.ActiveTargetIndex;
        ApplyTargetContextAndReselect();
    }

    private void OnActiveAuthoringTargetChanged(int newTargetIndex)
    {
        if (newTargetIndex == _authoringSyncedTargetIndex)
            return;
        _authoringSyncedTargetIndex = newTargetIndex;
        _selectedListIndex = 0;
        ApplyTargetContextAndReselect();
    }

    private void ApplyTargetContextAndReselect()
    {
        Transform contentRoot = GetActiveContentRoot();
        if (objectSelectionManager != null)
            objectSelectionManager.Configure(mainCamera, contentRoot);

        GameObject activeTarget = targetSelectionManager != null ? targetSelectionManager.GetActiveTarget() : null;
        Transform targetRootTransform = activeTarget != null ? activeTarget.transform : null;
        if (frontSideConstraint != null)
            frontSideConstraint.SetTargetContext(targetRootTransform, contentRoot);
        if (placementBoundsService != null)
            placementBoundsService.SetTargetContext(targetRootTransform, contentRoot);

        RefreshContentList();

        if (_contentObjects.Count > 0)
        {
            if (_selectedListIndex < 0 || _selectedListIndex >= _contentObjects.Count)
                _selectedListIndex = 0;

            Transform pick = _contentObjects[_selectedListIndex];
            _suppressAuthoringSyncFromSelection = false;
            objectSelectionManager?.SetSelected(pick);
        }
        else
        {
            _selectedListIndex = -1;
            objectSelectionManager?.SetSelected(null);
        }
    }

    /// <summary>Same contract as legacy <see cref="ContentTransformController.SelectContentTransform"/>.</summary>
    public void SelectContentTransform(Transform contentChild, bool syncAuthoringUi = true)
    {
        if (contentChild == null || objectSelectionManager == null)
            return;

        RefreshContentList();

        for (int i = 0; i < _contentObjects.Count; i++)
        {
            if (_contentObjects[i] != contentChild)
                continue;

            _selectedListIndex = i;
            _suppressAuthoringSyncFromSelection = !syncAuthoringUi;
            objectSelectionManager.SetSelected(contentChild);
            return;
        }
    }

    /// <summary>Clears current content selection (used when switching inspector to Target mode).</summary>
    public void ClearContentSelection(bool syncAuthoringUi = true)
    {
        if (objectSelectionManager == null)
            return;

        _selectedListIndex = -1;
        _suppressAuthoringSyncFromSelection = !syncAuthoringUi;
        objectSelectionManager.SetSelected(null);
    }

    private void OnObjectSelectionChanged(Transform selected)
    {
        if (selected == null)
        {
            _selectedListIndex = -1;
            UpdateSelectionVisual();
            ContentSelectionChanged?.Invoke(null);
            if (_suppressAuthoringSyncFromSelection)
            {
                _suppressAuthoringSyncFromSelection = false;
                return;
            }

            authoringUI?.ClearAuthoringSpatialSelection();
            return;
        }

        RefreshContentList();
        _selectedListIndex = _contentObjects.IndexOf(selected);
        UpdateSelectionVisual();

        if (_suppressAuthoringSyncFromSelection)
        {
            _suppressAuthoringSyncFromSelection = false;
            return;
        }

        if (authoringUI != null)
            authoringUI.OnContentSelectedInScene(selected);

        ContentSelectionChanged?.Invoke(selected);
    }

    private void OnGizmoContentTransformChanged(Transform contentTransform)
    {
        if (contentTransform != null && authoringUI != null)
        {
            authoringUI.SyncTransformToInspector(contentTransform);
            WorkspaceAuthoredAttach.MarkContentRemoteDirty(contentTransform);
            NotifyWorkspaceAutosave();
        }
    }

    private void Update()
    {
        RefreshContentList();
        SyncHighlightTargetByInspectorMode();
        UpdateSelectionBoundsVisual();

        if (DraggableObject.IsDraggingObjectInteractionActive)
            return;

        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            SelectNextContent();

        Transform selected = objectSelectionManager != null ? objectSelectionManager.Selected : null;

        if (!enableKeyboardNudges || selected == null)
            return;

        if (gizmoController != null && gizmoController.IsManipulating)
            return;

        if (RTGizmosEngine.Get != null && RTGizmosEngine.Get.DraggedGizmo != null)
            return;

        HandlePositionInput(selected);
        HandleRotationInput(selected);
        HandleScaleInput(selected);
    }

    private void SelectNextContent()
    {
        RefreshContentList();
        if (_contentObjects.Count == 0)
        {
            _selectedListIndex = -1;
            objectSelectionManager?.SetSelected(null);
            return;
        }

        _selectedListIndex++;
        if (_selectedListIndex >= _contentObjects.Count)
            _selectedListIndex = 0;

        Transform sel = _contentObjects[_selectedListIndex];
        objectSelectionManager?.SetSelected(sel);
    }

    private void RefreshContentList()
    {
        _contentObjects.Clear();

        Transform contentRoot = GetActiveContentRoot();
        if (contentRoot == null)
        {
            _selectedListIndex = -1;
            bool wasNotEmpty = _lastContentInstanceIds.Count > 0;
            _lastContentInstanceIds.Clear();
            if (wasNotEmpty)
                ContentListChanged?.Invoke();
            return;
        }

        foreach (Transform child in contentRoot)
            _contentObjects.Add(child);

        Transform sel = objectSelectionManager != null ? objectSelectionManager.Selected : null;
        if (sel == null)
        {
            _selectedListIndex = -1;
        }
        else
        {
            int idx = _contentObjects.IndexOf(sel);
            _selectedListIndex = idx;
            if (idx < 0 && _contentObjects.Count > 0)
            {
                _selectedListIndex = 0;
                objectSelectionManager.SetSelected(_contentObjects[0]);
            }
        }

        bool listChanged = HasContentListChanged();
        if (listChanged)
            ContentListChanged?.Invoke();
    }

    private bool HasContentListChanged()
    {
        if (_lastContentInstanceIds.Count != _contentObjects.Count)
        {
            CacheCurrentContentInstanceIds();
            return true;
        }

        for (int i = 0; i < _contentObjects.Count; i++)
        {
            int id = _contentObjects[i] != null ? _contentObjects[i].GetInstanceID() : 0;
            if (_lastContentInstanceIds[i] != id)
            {
                CacheCurrentContentInstanceIds();
                return true;
            }
        }

        return false;
    }

    private void CacheCurrentContentInstanceIds()
    {
        _lastContentInstanceIds.Clear();
        for (int i = 0; i < _contentObjects.Count; i++)
            _lastContentInstanceIds.Add(_contentObjects[i] != null ? _contentObjects[i].GetInstanceID() : 0);
    }

    private Transform GetActiveContentRoot()
    {
        if (targetSelectionManager == null)
            return null;

        GameObject activeTarget = targetSelectionManager.GetActiveTarget();
        if (activeTarget == null)
            return null;

        return activeTarget.transform.Find("ContentRoot");
    }

    private void UpdateSelectionVisual()
    {
        for (int i = 0; i < _contentObjects.Count; i++)
        {
            Renderer renderer = _contentObjects[i].GetComponent<Renderer>();
            if (renderer == null)
                continue;

            if (RendererHasAssignedTexture(renderer))
                continue;

            if (i == _selectedListIndex)
                renderer.material.color = Color.yellow;
            else
                renderer.material.color = Color.red;
        }
    }

    private void SyncHighlightTargetByInspectorMode()
    {
        Transform desiredHighlight = null;

        bool targetInspectorActive = authoringUI != null && authoringUI.IsTargetInspectorActive();
        if (targetInspectorActive)
        {
            GameObject activeTarget = targetSelectionManager != null ? targetSelectionManager.GetActiveTarget() : null;
            if (activeTarget != null)
            {
                Transform targetVisual = activeTarget.transform.Find("TargetVisual");
                desiredHighlight = targetVisual != null ? targetVisual : activeTarget.transform;
            }
        }
        else
        {
            desiredHighlight = objectSelectionManager != null ? objectSelectionManager.Selected : null;
        }

        if (desiredHighlight == _highlightTarget)
            return;

        _highlightTarget = desiredHighlight;
        RebuildSelectionBoundsVisual();
    }

    private void UpdateSelectionBoundsVisual()
    {
        if (_highlightTarget == null || _selectionBoundsVisual == null || _selectionEdgeRenderers.Count == 0 || _selectionBoundsMaterial == null)
            return;

        if (!TryComputeRenderBounds(_highlightTarget, out Bounds bounds))
        {
            HideSelectionBoundsVisual();
            return;
        }

        UpdateSelectionEdgeGeometry(bounds);

        float pulse = 0.5f + 0.5f * Mathf.Sin((Time.unscaledTime + _pulseSeed) * selectionPulseSpeed);
        float alpha = Mathf.Clamp01(_baseSelectionBoundsColor.a - selectionPulseAlphaRange * 0.5f + pulse * selectionPulseAlphaRange);
        Color c = _baseSelectionBoundsColor;
        c.a = alpha;
        ApplyColorToSelectionBoundsMaterial(c);
    }

    private void RebuildSelectionBoundsVisual()
    {
        HideSelectionBoundsVisual();
        if (_highlightTarget == null)
            return;
        if (!TryComputeRenderBounds(_highlightTarget, out Bounds bounds))
            return;

        _selectionBoundsVisual = new GameObject("__SelectionBoundsVisual");
        _selectionBoundsMaterial = BuildSelectionBoundsMaterial();
        BuildSelectionEdgeRenderers();
        UpdateSelectionEdgeGeometry(bounds);
        _baseSelectionBoundsColor = selectionBoundsColor;
        _pulseSeed = Random.Range(0f, 10f);
    }

    private Material BuildSelectionBoundsMaterial()
    {
        Shader shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Standard");

        var mat = new Material(shader);
        mat.name = "AuthoringSelectionBoundsMaterial (Runtime)";
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        if (mat.HasProperty("_Surface"))
            mat.SetFloat("_Surface", 1f);
        if (mat.HasProperty("_ZWrite"))
            mat.SetFloat("_ZWrite", 0f);
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", selectionBoundsColor);
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", selectionBoundsColor);
        return mat;
    }

    private void ApplyColorToSelectionBoundsMaterial(Color color)
    {
        if (_selectionBoundsMaterial == null)
            return;

        if (_selectionBoundsMaterial.HasProperty("_Color"))
            _selectionBoundsMaterial.SetColor("_Color", color);
        if (_selectionBoundsMaterial.HasProperty("_BaseColor"))
            _selectionBoundsMaterial.SetColor("_BaseColor", color);
    }

    private void BuildSelectionEdgeRenderers()
    {
        if (_selectionBoundsVisual == null || _selectionBoundsMaterial == null)
            return;

        const int edgeCount = 12;
        for (int i = 0; i < edgeCount; i++)
        {
            var edge = new GameObject($"Edge_{i:00}");
            edge.transform.SetParent(_selectionBoundsVisual.transform, false);
            var line = edge.AddComponent<LineRenderer>();
            line.sharedMaterial = _selectionBoundsMaterial;
            line.useWorldSpace = true;
            line.loop = false;
            line.positionCount = 2;
            line.startWidth = selectionEdgeWidth;
            line.endWidth = selectionEdgeWidth;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            line.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            _selectionEdgeRenderers.Add(line);
        }
    }

    private void UpdateSelectionEdgeGeometry(Bounds bounds)
    {
        if (_selectionEdgeRenderers.Count == 0)
            return;

        Vector3 ext = bounds.extents + Vector3.one * (selectionBoundsPadding * 0.5f);
        Vector3 c = bounds.center;
        Vector3[] p = new Vector3[8];
        p[0] = c + new Vector3(-ext.x, -ext.y, -ext.z);
        p[1] = c + new Vector3( ext.x, -ext.y, -ext.z);
        p[2] = c + new Vector3( ext.x,  ext.y, -ext.z);
        p[3] = c + new Vector3(-ext.x,  ext.y, -ext.z);
        p[4] = c + new Vector3(-ext.x, -ext.y,  ext.z);
        p[5] = c + new Vector3( ext.x, -ext.y,  ext.z);
        p[6] = c + new Vector3( ext.x,  ext.y,  ext.z);
        p[7] = c + new Vector3(-ext.x,  ext.y,  ext.z);

        SetEdge(0, p[0], p[1]); SetEdge(1, p[1], p[2]); SetEdge(2, p[2], p[3]); SetEdge(3, p[3], p[0]);
        SetEdge(4, p[4], p[5]); SetEdge(5, p[5], p[6]); SetEdge(6, p[6], p[7]); SetEdge(7, p[7], p[4]);
        SetEdge(8, p[0], p[4]); SetEdge(9, p[1], p[5]); SetEdge(10, p[2], p[6]); SetEdge(11, p[3], p[7]);
    }

    private void SetEdge(int edgeIndex, Vector3 from, Vector3 to)
    {
        if (edgeIndex < 0 || edgeIndex >= _selectionEdgeRenderers.Count)
            return;

        LineRenderer lr = _selectionEdgeRenderers[edgeIndex];
        if (lr == null)
            return;
        lr.SetPosition(0, from);
        lr.SetPosition(1, to);
    }

    private void HideSelectionBoundsVisual()
    {
        if (_selectionBoundsVisual != null)
            Destroy(_selectionBoundsVisual);
        if (_selectionBoundsMaterial != null)
            Destroy(_selectionBoundsMaterial);

        _selectionBoundsVisual = null;
        _selectionEdgeRenderers.Clear();
        _selectionBoundsMaterial = null;
    }

    private static bool TryComputeRenderBounds(Transform target, out Bounds bounds)
    {
        bounds = default;
        if (target == null)
            return false;

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(includeInactive: false);
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null || !r.enabled)
                continue;

            if (!hasBounds)
            {
                bounds = r.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }

        return hasBounds;
    }

    private static bool RendererHasAssignedTexture(Renderer renderer)
    {
        Material m = renderer.sharedMaterial;
        if (m == null)
            return false;
        if (m.mainTexture != null)
            return true;
        if (m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null)
            return true;
        if (m.HasProperty("_MainTex") && m.GetTexture("_MainTex") != null)
            return true;
        return false;
    }

    private void HandlePositionInput(Transform target)
    {
        if (Keyboard.current == null)
            return;

        Vector3 pos = target.localPosition;
        bool anyKey = false;

        if (Keyboard.current.aKey.isPressed) { pos.x -= moveStep * Time.deltaTime * 10f; anyKey = true; }
        if (Keyboard.current.dKey.isPressed) { pos.x += moveStep * Time.deltaTime * 10f; anyKey = true; }
        if (Keyboard.current.wKey.isPressed) { pos.y += moveStep * Time.deltaTime * 10f; anyKey = true; }
        if (Keyboard.current.sKey.isPressed) { pos.y -= moveStep * Time.deltaTime * 10f; anyKey = true; }
        if (Keyboard.current.qKey.isPressed) { pos.z -= moveStep * Time.deltaTime * 10f; anyKey = true; }
        if (Keyboard.current.eKey.isPressed) { pos.z += moveStep * Time.deltaTime * 10f; anyKey = true; }

        if (!anyKey)
            return;

        target.localPosition = pos;
        authoringUI?.SyncTransformToInspector(target);
        WorkspaceAuthoredAttach.MarkContentRemoteDirty(target);
        NotifyWorkspaceAutosave();
    }

    private void HandleRotationInput(Transform target)
    {
        if (Keyboard.current == null)
            return;

        Vector3 rot = target.localEulerAngles;
        bool anyKey = false;

        if (Keyboard.current.zKey.isPressed) { rot.y -= rotateStep * Time.deltaTime * 10f; anyKey = true; }
        if (Keyboard.current.xKey.isPressed) { rot.y += rotateStep * Time.deltaTime * 10f; anyKey = true; }

        if (!anyKey)
            return;

        target.localEulerAngles = rot;
        authoringUI?.SyncTransformToInspector(target);
        WorkspaceAuthoredAttach.MarkContentRemoteDirty(target);
        NotifyWorkspaceAutosave();
    }

    private void HandleScaleInput(Transform target)
    {
        if (Keyboard.current == null)
            return;

        Vector3 scale = target.localScale;
        bool anyKey = false;

        if (Keyboard.current.cKey.isPressed) { scale += Vector3.one * scaleStep * Time.deltaTime * 10f; anyKey = true; }
        if (Keyboard.current.vKey.isPressed) { scale -= Vector3.one * scaleStep * Time.deltaTime * 10f; anyKey = true; }

        if (!anyKey)
            return;

        scale.x = Mathf.Max(0.1f, scale.x);
        scale.y = Mathf.Max(0.1f, scale.y);
        scale.z = Mathf.Max(0.1f, scale.z);

        target.localScale = scale;
        authoringUI?.SyncTransformToInspector(target);
        WorkspaceAuthoredAttach.MarkContentRemoteDirty(target);
        NotifyWorkspaceAutosave();
    }

    private static void NotifyWorkspaceAutosave()
    {
        WorkspaceAutoSaveService autoSave = UnityEngine.Object.FindFirstObjectByType<WorkspaceAutoSaveService>();
        if (autoSave != null)
            autoSave.NotifyWorkspaceChanged();
    }
}
