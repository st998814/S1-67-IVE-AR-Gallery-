using ARGallery.Workspace;
using ARGallery.Workspace.Presets;
using UnityEngine;

/// <summary>
/// Resolves placement boundary limits for content on an AR target and clamps local positions.
/// XY extents come from <see cref="TargetVisual"/> scale and active <see cref="PlacementBoundaryPreset"/>;
/// Z from preset depth and <see cref="FrontSideConstraint"/>.
/// </summary>
public sealed class PlacementBoundsService : MonoBehaviour
{
    [SerializeField] private FrontSideConstraint frontSideConstraint;
    [Tooltip("Fallback inset when no posture preset is applied (metres).")]
    [SerializeField] private float edgeMargin = 0.02f;
    [Tooltip("Fallback max depth when no posture preset is applied (metres).")]
    [SerializeField] private float maxDepthFromTarget = 2f;
    [Tooltip("Fallback half-extent when TargetVisual is missing (meters).")]
    [SerializeField] private float fallbackHalfExtent = 0.1f;

    [SerializeField] private Transform targetRoot;
    [SerializeField] private Transform contentRoot;

    private PlacementBoundaryPreset _activeBoundaryPreset = PlacementBoundaryPreset.WallDefault;
    private bool _hasActiveBoundaryPreset;

    public PlacementBoundaryPreset ActiveBoundaryPreset => _activeBoundaryPreset;
    public float EdgeMargin => _activeBoundaryPreset.edgeMargin;
    public float MaxDepthFromTarget => _activeBoundaryPreset.depthMeters;

    public void Configure(FrontSideConstraint frontConstraintRef)
    {
        if (frontConstraintRef != null)
            frontSideConstraint = frontConstraintRef;
    }

    /// <summary>
    /// Repoints bounds resolution when the active authoring target changes.
    /// </summary>
    public void SetTargetContext(Transform newTargetRoot, Transform newContentRoot)
    {
        targetRoot = newTargetRoot;
        contentRoot = newContentRoot;
    }

    /// <summary>
    /// Applies placement boundary parameters from the workspace posture preset table.
    /// </summary>
    public void SetPosture(WorkspacePosture posture)
    {
        WorkspacePreset workspacePreset = WorkspacePresetLibrary.GetPreset(posture);
        SetPlacementBoundaryPreset(workspacePreset.placementBoundary.boundary);
    }

    /// <summary>
    /// Applies an explicit placement boundary preset (e.g. from <see cref="WorkspacePresetLibrary"/>).
    /// </summary>
    public void SetPlacementBoundaryPreset(PlacementBoundaryPreset preset)
    {
        _activeBoundaryPreset = preset;
        _hasActiveBoundaryPreset = true;
    }

    /// <summary>
    /// Clears posture preset; bounds fall back to serialized fallback fields on this component.
    /// </summary>
    public void ClearPlacementBoundaryPreset()
    {
        _hasActiveBoundaryPreset = false;
        _activeBoundaryPreset = BuildFallbackBoundaryPreset();
    }

    /// <summary>
    /// Resolves the editable placement volume for the active target (ContentRoot-local), without requiring content.
    /// </summary>
    public bool TryGetPlacementVolumeBounds(out PlacementBoundsCalculator.Snapshot bounds)
    {
        return TryGetPlacementVolumeBounds(contentRoot, out bounds);
    }

    public bool TryGetPlacementVolumeBounds(Transform contentRootTransform, out PlacementBoundsCalculator.Snapshot bounds)
    {
        bounds = default;
        Transform root = contentRootTransform != null ? contentRootTransform.parent : targetRoot;
        if (root == null)
            return false;

        Transform targetVisual = root.Find("TargetVisual");
        Vector3 targetVisualLocalScale = targetVisual != null && targetVisual.localScale.sqrMagnitude > 1e-8f
            ? targetVisual.localScale
            : Vector3.one * (fallbackHalfExtent * 2f);

        if (!TryGetFrontZParameters(out float effectiveMinZ, out bool negativeFrontZ))
        {
            effectiveMinZ = 0.5f;
            negativeFrontZ = true;
        }

        PlacementBoundaryPreset preset = ResolveBoundaryPreset();
        bounds = PlacementBoundsCalculator.Compute(
            targetVisualLocalScale,
            preset,
            effectiveMinZ,
            negativeFrontZ);

        return true;
    }

    public bool TryGetBoundsForContent(Transform content, out PlacementBoundsCalculator.Snapshot bounds)
    {
        bounds = default;
        if (content == null)
            return false;

        if (!TryResolveTargetVisualScale(content, out Vector3 targetVisualLocalScale))
            targetVisualLocalScale = Vector3.one * (fallbackHalfExtent * 2f);

        if (!TryGetFrontZParameters(out float effectiveMinZ, out bool negativeFrontZ))
        {
            effectiveMinZ = 0.5f;
            negativeFrontZ = true;
        }

        PlacementBoundaryPreset preset = ResolveBoundaryPreset();
        bounds = PlacementBoundsCalculator.Compute(
            targetVisualLocalScale,
            preset,
            effectiveMinZ,
            negativeFrontZ);

        return true;
    }

    private PlacementBoundaryPreset ResolveBoundaryPreset()
    {
        if (_hasActiveBoundaryPreset)
            return _activeBoundaryPreset;

        return BuildFallbackBoundaryPreset();
    }

    private PlacementBoundaryPreset BuildFallbackBoundaryPreset()
    {
        return new PlacementBoundaryPreset(1f, 1f, maxDepthFromTarget, edgeMargin);
    }

    public PlacementBoundsCalculator.AxisRange GetAxisRange(Transform content, PlacementBoundsCalculator.SemanticAxis axis)
    {
        if (!TryGetBoundsForContent(content, out PlacementBoundsCalculator.Snapshot bounds))
            return new PlacementBoundsCalculator.AxisRange(0f, 0f);

        return bounds.GetRange(axis);
    }

    public Vector3 ClampLocalPosition(Transform content, Vector3 localPosition)
    {
        if (content == null)
            return localPosition;

        if (!TryGetBoundsForContent(content, out PlacementBoundsCalculator.Snapshot bounds))
            return localPosition;

        return bounds.Clamp(localPosition);
    }

    public Vector3 SetSemanticAxis(Transform content, PlacementBoundsCalculator.SemanticAxis axis, float value)
    {
        if (content == null)
            return Vector3.zero;

        Vector3 localPosition = content.localPosition;
        localPosition = PlacementBoundsCalculator.SetAxisComponent(localPosition, axis, value);
        localPosition = ClampLocalPosition(content, localPosition);
        return localPosition;
    }

    private bool TryResolveTargetVisualScale(Transform content, out Vector3 targetVisualLocalScale)
    {
        targetVisualLocalScale = default;
        Transform root = ResolveTargetRoot(content);
        if (root == null)
            root = targetRoot;
        if (root == null)
            return false;

        Transform targetVisual = root.Find("TargetVisual");
        if (targetVisual == null)
            return false;

        targetVisualLocalScale = targetVisual.localScale;
        return targetVisualLocalScale.sqrMagnitude > 1e-8f;
    }

    private bool TryGetFrontZParameters(out float effectiveMinimumLocalZ, out bool negativeFrontLocalZ)
    {
        effectiveMinimumLocalZ = 0.5f;
        negativeFrontLocalZ = true;

        if (frontSideConstraint == null)
            frontSideConstraint = FindFirstObjectByType<FrontSideConstraint>();
        if (frontSideConstraint == null)
            return false;

        effectiveMinimumLocalZ = frontSideConstraint.EffectiveMinimumLocalZ;
        negativeFrontLocalZ = frontSideConstraint.FrontDirectionSign < 0f;
        return true;
    }

    private static Transform ResolveTargetRoot(Transform content)
    {
        if (content == null)
            return null;

        Transform current = content;
        while (current != null)
        {
            if (string.Equals(current.name, "ContentRoot", System.StringComparison.Ordinal))
                return current.parent;
            current = current.parent;
        }

        return content.parent != null ? content.parent.parent : null;
    }

    private void Awake()
    {
        if (frontSideConstraint == null)
            frontSideConstraint = FindFirstObjectByType<FrontSideConstraint>();

        if (targetRoot == null)
        {
            GameObject go = GameObject.Find("TargetRoot");
            if (go != null)
                targetRoot = go.transform;
        }

        if (contentRoot == null && targetRoot != null)
            contentRoot = targetRoot.Find("ContentRoot");
    }
}
