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

    [Header("Placement Volume")]
    [SerializeField] private Color volumeColor = new Color(0.4f, 0.74f, 0.86f, 0.58f);
    [SerializeField] private float edgeWidth = 0.0045f;
    [SerializeField] private bool showFrontPlaneGrid = false;
    [SerializeField] private int gridDivisions = 3;
    [SerializeField] private float cornerAccentLength = 0.022f;

    [Header("Mapping Indicators")]
    [SerializeField] private Color relationshipLineColor = new Color(0.98f, 0.62f, 0.18f, 0.9f);
    [SerializeField] private Color axisXColor = new Color(1f, 0.38f, 0.38f, 0.75f);
    [SerializeField] private Color axisYColor = new Color(0.38f, 1f, 0.42f, 0.75f);
    [SerializeField] private Color axisZColor = new Color(0.4f, 0.65f, 1f, 0.75f);
    [SerializeField] private float indicatorEdgeWidth = 0.007f;
    [SerializeField] private float indicatorArrowHeadLength = 0.035f;

    private PlacementSpaceVisualizer _placementVolume;
    private SpatialMappingIndicatorRenderer _mappingIndicators;
    private Transform _activeContentRoot;
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
            volumeColor,
            edgeWidth,
            showFrontPlaneGrid,
            gridDivisions,
            cornerAccentLength);
        _placementVolume.SetCamera(mainCamera);

        _mappingIndicators = new SpatialMappingIndicatorRenderer(
            relationshipLineColor,
            axisXColor,
            axisYColor,
            axisZColor,
            indicatorEdgeWidth,
            indicatorArrowHeadLength);
        _mappingIndicators.SetCamera(mainCamera);
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
        _mappingIndicators?.Hide();
    }

    private void OnDestroy()
    {
        _placementVolume?.Dispose();
        _mappingIndicators?.Dispose();
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
        if (_placementVolume != null && _placementVolume.IsAttached)
        {
            if (_placementVolume.TryRefreshFromTargetVisualScale())
            {
                RefreshPlacementVolume();
            }
            else if (_activeContentRoot != null && placementBoundsService != null
                && placementBoundsService.TryGetPlacementVolumeBounds(_activeContentRoot, out PlacementBoundsCalculator.Snapshot bounds))
            {
                _placementVolume.SetCamera(mainCamera != null ? mainCamera : Camera.main);
                _placementVolume.ApplyDynamicSizing(bounds);
            }
        }

        if (_mappingIndicators != null && _trackedSelectedContent != null && _mappingIndicators.IsAttached)
            _mappingIndicators.Refresh();
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

        if (_activeContentRoot != contentRoot)
        {
            _activeContentRoot = contentRoot;
            _placementVolume.InvalidateTargetVisualScaleCache();
            _placementVolume.AttachTo(contentRoot);
        }

        if (!placementBoundsService.TryGetPlacementVolumeBounds(contentRoot, out PlacementBoundsCalculator.Snapshot bounds))
        {
            _placementVolume.Hide();
            return;
        }

        _placementVolume.SetCamera(mainCamera != null ? mainCamera : Camera.main);
        _placementVolume.Refresh(bounds);
        RefreshMappingIndicators();
    }

    private void RefreshMappingIndicators()
    {
        if (_mappingIndicators == null)
            return;

        _mappingIndicators.SetCamera(mainCamera != null ? mainCamera : Camera.main);

        Transform contentRoot = ResolveActiveContentRoot();
        if (contentRoot == null)
        {
            _mappingIndicators.Hide();
            return;
        }

        Transform selected = ResolveSelectedContent();
        _trackedSelectedContent = selected;
        if (selected == null || !IsContentUnderRoot(selected, contentRoot))
        {
            _mappingIndicators.Hide();
            return;
        }

        _mappingIndicators.AttachTo(contentRoot);
        _mappingIndicators.SetSelectedContent(selected);
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
