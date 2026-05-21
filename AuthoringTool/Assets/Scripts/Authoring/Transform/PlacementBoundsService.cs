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
        if (!TryComputePlacementVolumeBoundsInContentRoot(contentRootTransform, out bounds))
            return false;

        return true;
    }

    /// <summary>
    /// Placement volume wireframe in target-root local space (aligned with TargetVisual in the scene).
    /// Clamping still uses <see cref="TryGetPlacementVolumeBounds"/> in ContentRoot-local space.
    /// </summary>
    public bool TryGetPlacementVolumeVisualBounds(
        Transform contentRootTransform,
        out PlacementBoundsCalculator.Snapshot bounds,
        out Transform visualParent)
    {
        bounds = default;
        visualParent = null;
        if (!TryComputePlacementVolumeBoundsInContentRoot(contentRootTransform, out PlacementBoundsCalculator.Snapshot contentRootBounds))
            return false;

        visualParent = contentRootTransform != null ? contentRootTransform.parent : targetRoot;
        if (visualParent == null)
            visualParent = contentRootTransform;

        bounds = PlacementBoundsCalculator.ConvertSnapshotLocalSpace(
            contentRootTransform,
            visualParent,
            contentRootBounds);

        return true;
    }

    private bool TryComputePlacementVolumeBoundsInContentRoot(
        Transform contentRootTransform,
        out PlacementBoundsCalculator.Snapshot bounds)
    {
        bounds = default;
        Transform root = contentRootTransform != null ? contentRootTransform.parent : targetRoot;
        if (root == null)
            return false;

        Transform targetVisual = ResolveTargetVisual(contentRootTransform);
        Vector3 targetVisualLocalScale = targetVisual != null && targetVisual.localScale.sqrMagnitude > 1e-8f
            ? targetVisual.localScale
            : Vector3.one * (fallbackHalfExtent * 2f);

        if (!TryGetFrontZParameters(out float effectiveMinZ, out bool negativeFrontZ))
        {
            effectiveMinZ = 0.5f;
            negativeFrontZ = true;
        }

        PlacementBoundaryPreset preset = ResolveBoundaryPreset();
        Vector3 boundsCenterLocal = ResolveTargetVisualCenterInContentRoot(contentRootTransform);
        bounds = PlacementBoundsCalculator.Compute(
            targetVisualLocalScale,
            preset,
            effectiveMinZ,
            negativeFrontZ,
            boundsCenterLocal);

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
        Transform contentRootTransform = ResolveContentRoot(content);
        Vector3 boundsCenterLocal = ResolveTargetVisualCenterInContentRoot(contentRootTransform);
        bounds = PlacementBoundsCalculator.Compute(
            targetVisualLocalScale,
            preset,
            effectiveMinZ,
            negativeFrontZ,
            boundsCenterLocal);

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

    private Vector3 ResolveTargetVisualCenterInContentRoot(Transform contentRootTransform)
    {
        if (contentRootTransform == null)
            return Vector3.zero;

        Transform targetVisual = ResolveTargetVisual(contentRootTransform);
        if (targetVisual == null)
            return Vector3.zero;

        if (targetVisual.parent == contentRootTransform.parent)
            return targetVisual.localPosition - contentRootTransform.localPosition;

        return contentRootTransform.InverseTransformPoint(targetVisual.position);
    }

    private static Transform ResolveTargetVisual(Transform contentRootTransform)
    {
        if (contentRootTransform == null)
            return null;

        Transform current = contentRootTransform;
        while (current != null)
        {
            if (string.Equals(current.name, "TargetVisual", System.StringComparison.Ordinal))
                return current;

            Transform onParent = current.parent != null
                ? current.parent.Find("TargetVisual")
                : null;
            if (onParent != null)
                return onParent;

            current = current.parent;
        }

        return null;
    }

    private static Transform ResolveContentRoot(Transform content)
    {
        if (content == null)
            return null;

        if (string.Equals(content.name, "ContentRoot", System.StringComparison.Ordinal))
            return content;

        Transform current = content;
        while (current != null)
        {
            if (string.Equals(current.name, "ContentRoot", System.StringComparison.Ordinal))
                return current;
            current = current.parent;
        }

        return null;
    }

    private bool TryResolveTargetVisualScale(Transform content, out Vector3 targetVisualLocalScale)
    {
        targetVisualLocalScale = default;
        Transform contentRootTransform = ResolveContentRoot(content);
        Transform targetVisual = ResolveTargetVisual(contentRootTransform);
        if (targetVisual == null)
        {
            Transform root = ResolveTargetRoot(content) ?? targetRoot;
            targetVisual = root != null ? root.Find("TargetVisual") : null;
        }

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
