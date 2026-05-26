using System.Collections.Generic;
using ARGallery.AppFlow;
using ARGallery.Workspace;
using ARGallery.Workspace.Persistence;
using ARGallery.Workspace.Presets;
using RTG;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Wires sandbox transform stack (<see cref="ObjectSelectionManager"/>, <see cref="TransformGizmoController"/>)
/// into the authoring scene: target switching, UI sync, and selection affordances.
/// Content transforms are changed only via <see cref="ContentTransformManipulator"/> (gizmo, inspector sliders).
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

    private readonly List<Transform> _contentObjects = new List<Transform>();
    private readonly List<int> _lastContentInstanceIds = new List<int>();
    private int _selectedListIndex = -1;
    private int _authoringSyncedTargetIndex = int.MinValue;
    private bool _suppressAuthoringSyncFromSelection;
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
        {
            placementBoundsService.SetTargetContext(targetRootTransform, contentRoot);
            placementBoundsService.SetPosture(ResolveActiveWorkspacePosture());
        }

        SpatialMappingCoordinator spatialMapping = FindFirstObjectByType<SpatialMappingCoordinator>();
        if (spatialMapping != null)
            spatialMapping.RefreshPlacementVolume();

        if (targetRootTransform != null)
            WorkspaceOrientationHelper.Apply(targetRootTransform, false, 0.35f, 0.01f);

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
        ContentSelectionChanged?.Invoke(selected);

        if (_suppressAuthoringSyncFromSelection)
        {
            _suppressAuthoringSyncFromSelection = false;
            return;
        }

        if (authoringUI != null)
            authoringUI.OnContentSelectedInScene(selected);
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

        if (DraggableObject.IsDraggingObjectInteractionActive)
            return;

        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            SelectNextContent();
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
        {
            _contentObjects.Add(child);
            DraggableObject.ConfigureForContentShell(child.GetComponent<DraggableObject>());
        }

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

    private static WorkspacePosture ResolveActiveWorkspacePosture()
    {
        AuthoringWorkspaceEntry entry = FindFirstObjectByType<AuthoringWorkspaceEntry>();
        if (entry != null)
            return entry.AppliedPosture;

        return WorkspacePosture.Wall;
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

    private static void NotifyWorkspaceAutosave()
    {
        WorkspaceAutoSaveService autoSave = UnityEngine.Object.FindFirstObjectByType<WorkspaceAutoSaveService>();
        if (autoSave != null)
            autoSave.NotifyWorkspaceChanged();
    }
}
