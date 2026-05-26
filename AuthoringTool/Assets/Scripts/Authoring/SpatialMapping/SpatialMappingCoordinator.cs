using ARGallery.AppFlow;
using ARGallery.Workspace;
using UnityEngine;

/// <summary>
/// Owns authoring-only placement volume and spatial mapping indicators; separate from content rendering.
/// </summary>
[DefaultExecutionOrder(-35)]
public sealed class SpatialMappingCoordinator : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private TargetSelectionManager targetSelectionManager;
    [SerializeField] private ObjectSelectionManager objectSelectionManager;
    [SerializeField] private AuthoringTransformCoordinator authoringTransformCoordinator;
    [SerializeField] private ContentTransformManipulator contentTransformManipulator;
    [SerializeField] private PlacementBoundsService placementBoundsService;
    [SerializeField] private FrontSideConstraint frontSideConstraint;
    [SerializeField] private Camera mainCamera;

    [Header("Placement Boundary")]
    [SerializeField] private Color boundaryCornerColor = new Color(0.95f, 0.32f, 0.32f, 0.22f);
    [SerializeField] private float boundaryEdgeWidth = 0.003f;
    [SerializeField] private float boundaryCornerLegLength = 0.035f;

    [Header("Holographic Projection")]
    [SerializeField] private Color hologramProjectionColor = new Color(0.52f, 0.78f, 0.9f, 0.32f);
    [SerializeField] private float hologramEdgeWidth = 0.006f;

    private PlacementSpaceVisualizer _placementVolume;
    private HolographicProjectionIndicator _holographicProjection;
    private Transform _activeContentRoot;
    private Transform _activeVolumeParent;
    private int _syncedTargetIndex = int.MinValue;
    private Transform _trackedSelectedContent;

    private void Awake()
    {
        if (targetSelectionManager == null)
            targetSelectionManager = FindFirstObjectByType<TargetSelectionManager>();
        if (objectSelectionManager == null)
            objectSelectionManager = FindFirstObjectByType<ObjectSelectionManager>();
        if (authoringTransformCoordinator == null)
            authoringTransformCoordinator = FindFirstObjectByType<AuthoringTransformCoordinator>();
        if (contentTransformManipulator == null)
            contentTransformManipulator = FindFirstObjectByType<ContentTransformManipulator>();
        if (mainCamera == null)
            mainCamera = Camera.main;
        if (frontSideConstraint == null)
            frontSideConstraint = FindFirstObjectByType<FrontSideConstraint>();
        if (placementBoundsService == null)
            placementBoundsService = FindFirstObjectByType<PlacementBoundsService>();
        if (placementBoundsService == null && frontSideConstraint != null)
        {
            placementBoundsService = frontSideConstraint.gameObject.AddComponent<PlacementBoundsService>();
            placementBoundsService.Configure(frontSideConstraint);
        }

        _placementVolume = new PlacementSpaceVisualizer(
            boundaryCornerColor,
            boundaryEdgeWidth,
            boundaryCornerLegLength);
        _placementVolume.SetCamera(mainCamera);

        _holographicProjection = new HolographicProjectionIndicator(
            hologramProjectionColor,
            hologramEdgeWidth);
        _holographicProjection.SetCamera(mainCamera);
    }

    private void OnEnable()
    {
        if (targetSelectionManager != null)
            targetSelectionManager.ActiveTargetChanged += OnActiveTargetChanged;
        if (authoringTransformCoordinator != null)
            authoringTransformCoordinator.ContentSelectionChanged += OnContentSelectionChanged;
        if (contentTransformManipulator != null)
            contentTransformManipulator.ContentTransformChanged += OnContentTransformChanged;
    }

    private void OnDisable()
    {
        if (targetSelectionManager != null)
            targetSelectionManager.ActiveTargetChanged -= OnActiveTargetChanged;
        if (authoringTransformCoordinator != null)
            authoringTransformCoordinator.ContentSelectionChanged -= OnContentSelectionChanged;
        if (contentTransformManipulator != null)
            contentTransformManipulator.ContentTransformChanged -= OnContentTransformChanged;
        _placementVolume?.Hide();
        _holographicProjection?.Hide();
    }

    private void OnDestroy()
    {
        _placementVolume?.Dispose();
        _holographicProjection?.Dispose();
    }

    private void Start()
    {
        if (targetSelectionManager != null)
            _syncedTargetIndex = targetSelectionManager.ActiveTargetIndex;
        RefreshPlacementVolume();
        RefreshMappingIndicators();
    }

    private void LateUpdate()
    {
        if (_placementVolume != null && _placementVolume.IsAttached && _activeContentRoot != null && placementBoundsService != null)
        {
            if (_placementVolume.TryRefreshFromTargetVisualLayout())
                RefreshPlacementVolume();
            else if (placementBoundsService.TryGetPlacementVolumeVisualBounds(
                _activeContentRoot,
                out PlacementBoundsCalculator.Snapshot bounds,
                out _))
            {
                _placementVolume.SetCamera(mainCamera != null ? mainCamera : Camera.main);
                _placementVolume.Refresh(bounds);
            }
        }

        if (_holographicProjection != null && _trackedSelectedContent != null && _holographicProjection.IsAttached)
            _holographicProjection.Refresh();
    }

    private void OnActiveTargetChanged(int newTargetIndex)
    {
        if (newTargetIndex == _syncedTargetIndex)
            return;

        _syncedTargetIndex = newTargetIndex;
        RefreshPlacementVolume();
        RefreshMappingIndicators();
    }

    private void OnContentSelectionChanged(Transform selectedContent)
    {
        _trackedSelectedContent = selectedContent;
        RefreshMappingIndicators();
    }

    private void OnContentTransformChanged(Transform content)
    {
        if (content == null || _trackedSelectedContent == null)
            return;

        if (content != _trackedSelectedContent)
            return;

        RefreshMappingIndicators();
    }

    /// <summary>Rebuilds placement volume bounds for the active target (e.g. after posture preset changes).</summary>
    public void RefreshPlacementVolume()
    {
        if (_placementVolume == null)
            return;

        Transform contentRoot = ResolveActiveContentRoot();
        if (contentRoot == null || placementBoundsService == null)
        {
            _activeContentRoot = null;
            _placementVolume.Hide();
            return;
        }

        ApplyPlacementBoundsTargetContext(contentRoot);
        placementBoundsService.SetPosture(ResolveActiveWorkspacePosture());

        Transform volumeParent = contentRoot.parent != null ? contentRoot.parent : contentRoot;
        if (!placementBoundsService.TryGetPlacementVolumeVisualBounds(
            contentRoot,
            out PlacementBoundsCalculator.Snapshot bounds,
            out Transform resolvedParent))
        {
            _placementVolume.Hide();
            _activeContentRoot = null;
            _activeVolumeParent = null;
            return;
        }

        if (resolvedParent != null)
            volumeParent = resolvedParent;

        if (_activeContentRoot != contentRoot || _activeVolumeParent != volumeParent)
        {
            _activeContentRoot = contentRoot;
            _activeVolumeParent = volumeParent;
            _placementVolume.InvalidateTargetVisualLayoutCache();
            _placementVolume.AttachTo(volumeParent, contentRoot);
        }

        _placementVolume.SetCamera(mainCamera != null ? mainCamera : Camera.main);
        _placementVolume.Refresh(bounds);
        RefreshMappingIndicators();
    }

    private void RefreshMappingIndicators()
    {
        Camera camera = mainCamera != null ? mainCamera : Camera.main;
        Transform contentRoot = ResolveActiveContentRoot();
        Transform selected = ResolveSelectedContent();
        _trackedSelectedContent = selected;

        if (contentRoot == null || selected == null || !IsContentUnderRoot(selected, contentRoot))
        {
            _holographicProjection?.Hide();
            return;
        }

        Transform targetRoot = contentRoot.parent;
        Transform targetVisual = targetRoot != null ? targetRoot.Find("TargetVisual") : null;
        Transform anchorRoot = targetRoot != null ? targetRoot : contentRoot;

        if (_holographicProjection != null)
        {
            _holographicProjection.SetCamera(camera);
            _holographicProjection.AttachTo(anchorRoot, targetRoot, targetVisual, contentRoot);
            _holographicProjection.SetSelectedContent(selected);
        }
    }

    public void RefreshForCurrentSelection()
    {
        RefreshMappingIndicators();
    }

    private Transform ResolveSelectedContent()
    {
        if (authoringTransformCoordinator != null)
            return authoringTransformCoordinator.GetSelectedContentTransform();

        return objectSelectionManager != null ? objectSelectionManager.Selected : null;
    }

    private static bool IsContentUnderRoot(Transform content, Transform contentRoot)
    {
        if (content == null || contentRoot == null)
            return false;

        return content == contentRoot || content.IsChildOf(contentRoot);
    }

    private void ApplyPlacementBoundsTargetContext(Transform contentRoot)
    {
        if (placementBoundsService == null || contentRoot == null)
            return;

        Transform targetRoot = contentRoot.parent;
        placementBoundsService.SetTargetContext(targetRoot, contentRoot);
    }

    private Transform ResolveActiveContentRoot()
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
}
