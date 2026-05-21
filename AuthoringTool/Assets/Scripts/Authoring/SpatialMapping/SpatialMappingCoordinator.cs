using ARGallery.AppFlow;
using ARGallery.Workspace;
using UnityEngine;

/// <summary>
/// Owns authoring-only placement volume visualization; separate from content rendering and selection highlights.
/// </summary>
[DefaultExecutionOrder(-35)]
public sealed class SpatialMappingCoordinator : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private TargetSelectionManager targetSelectionManager;
    [SerializeField] private PlacementBoundsService placementBoundsService;
    [SerializeField] private FrontSideConstraint frontSideConstraint;

    [Header("Placement Volume")]
    [SerializeField] private Color volumeColor = new Color(0.45f, 0.78f, 0.92f, 0.42f);
    [SerializeField] private float edgeWidth = 0.006f;
    [SerializeField] private bool showFrontPlaneGrid = true;
    [SerializeField] private int gridDivisions = 4;
    [SerializeField] private float cornerTickLength = 0.04f;

    private PlacementSpaceVisualizer _placementVolume;
    private Transform _activeContentRoot;
    private int _syncedTargetIndex = int.MinValue;

    private void Awake()
    {
        if (targetSelectionManager == null)
            targetSelectionManager = FindFirstObjectByType<TargetSelectionManager>();
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
            cornerTickLength);
    }

    private void OnEnable()
    {
        if (targetSelectionManager != null)
            targetSelectionManager.ActiveTargetChanged += OnActiveTargetChanged;
    }

    private void OnDisable()
    {
        if (targetSelectionManager != null)
            targetSelectionManager.ActiveTargetChanged -= OnActiveTargetChanged;
        _placementVolume?.Hide();
    }

    private void OnDestroy()
    {
        _placementVolume?.Dispose();
    }

    private void Start()
    {
        if (targetSelectionManager != null)
            _syncedTargetIndex = targetSelectionManager.ActiveTargetIndex;
        RefreshPlacementVolume();
    }

    private void LateUpdate()
    {
        if (_placementVolume == null || !_placementVolume.IsAttached)
            return;

        if (_placementVolume.TryRefreshFromTargetVisualScale())
            RefreshPlacementVolume();
    }

    private void OnActiveTargetChanged(int newTargetIndex)
    {
        if (newTargetIndex == _syncedTargetIndex)
            return;

        _syncedTargetIndex = newTargetIndex;
        RefreshPlacementVolume();
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

        _placementVolume.Refresh(bounds);
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
