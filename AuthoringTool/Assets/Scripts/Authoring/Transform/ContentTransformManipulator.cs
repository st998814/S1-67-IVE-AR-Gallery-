using System;
using UnityEngine;

/// <summary>
/// Single write path for content transforms: placement bounds, front-side Z, uniform scale, and change events.
/// </summary>
public sealed class ContentTransformManipulator : MonoBehaviour
{
    [SerializeField] private TargetLocalTransformService targetLocalTransformService;
    [SerializeField] private PlacementBoundsService placementBoundsService;
    [SerializeField] private FrontSideConstraint frontSideConstraint;

    private LocalTransformSnapshot _lastSnapshot;
    private bool _hasSnapshot;

    public event Action<Transform> ContentTransformChanged;
    public event Action<TransformGizmoController.TransformChangeEvent> ContentTransformChangedDetailed;

    public void Configure(
        TargetLocalTransformService localServiceRef,
        PlacementBoundsService placementBoundsRef,
        FrontSideConstraint frontConstraintRef)
    {
        if (localServiceRef != null)
            targetLocalTransformService = localServiceRef;
        if (placementBoundsRef != null)
            placementBoundsService = placementBoundsRef;
        if (frontConstraintRef != null)
            frontSideConstraint = frontConstraintRef;
    }

    /// <summary>
    /// Sets one semantic placement axis, clamps to bounds, enforces front-side Z, and notifies listeners.
    /// </summary>
    public void SetSemanticAxis(Transform content, PlacementBoundsCalculator.SemanticAxis axis, float value)
    {
        if (content == null)
            return;

        Vector3 localPosition = placementBoundsService != null
            ? placementBoundsService.SetSemanticAxis(content, axis, value)
            : PlacementBoundsCalculator.SetAxisComponent(content.localPosition, axis, value);

        ApplyLocalPosition(content, localPosition);
        NotifyIfChanged(content, TransformGizmoController.GizmoMode.Translate);
    }

    public void SetLocalPosition(Transform content, Vector3 localPosition)
    {
        if (content == null)
            return;

        ApplyLocalPosition(content, localPosition);
        NotifyIfChanged(content, TransformGizmoController.GizmoMode.Translate);
    }

    public void SetLocalRotation(Transform content, Quaternion localRotation)
    {
        if (content == null)
            return;

        if (targetLocalTransformService != null)
            targetLocalTransformService.SetLocalRotation(content, localRotation);
        else
            content.localRotation = localRotation;

        NotifyIfChanged(content, TransformGizmoController.GizmoMode.Rotate);
    }

    public void SetUniformScale(Transform content, float scale)
    {
        if (content == null)
            return;

        if (targetLocalTransformService != null)
            targetLocalTransformService.SetUniformLocalScale(content, scale);
        else
            content.localScale = Vector3.one * Mathf.Max(0.01f, scale);

        NotifyIfChanged(content, TransformGizmoController.GizmoMode.Scale);
    }

    /// <summary>
    /// Post-processes RTG gizmo output (after per-mode axis locks applied by <see cref="TransformGizmoController"/>).
    /// </summary>
    public void ApplyGizmoResult(Transform content, TransformGizmoController.GizmoMode mode, bool enforceUniformScale)
    {
        if (content == null)
            return;

        switch (mode)
        {
            case TransformGizmoController.GizmoMode.Translate:
                ApplyLocalPosition(content, content.localPosition);
                break;

            case TransformGizmoController.GizmoMode.Rotate:
                if (targetLocalTransformService != null)
                    targetLocalTransformService.SetLocalRotation(content, content.localRotation);
                break;

            case TransformGizmoController.GizmoMode.Scale:
                if (targetLocalTransformService != null)
                {
                    targetLocalTransformService.SetLocalPosition(content, content.localPosition);
                    targetLocalTransformService.SetLocalRotation(content, content.localRotation);
                    if (enforceUniformScale)
                        targetLocalTransformService.NormalizeUniformScale(content);
                }
                else if (enforceUniformScale)
                {
                    Vector3 s = content.localScale;
                    float avg = (s.x + s.y + s.z) / 3f;
                    content.localScale = Vector3.one * avg;
                }
                break;

            case TransformGizmoController.GizmoMode.Universal:
            default:
                ApplyLocalPosition(content, content.localPosition);
                if (targetLocalTransformService != null)
                    targetLocalTransformService.SetLocalRotation(content, content.localRotation);
                if (enforceUniformScale)
                {
                    if (targetLocalTransformService != null)
                        targetLocalTransformService.NormalizeUniformScale(content);
                    else
                    {
                        Vector3 s = content.localScale;
                        float avg = (s.x + s.y + s.z) / 3f;
                        content.localScale = Vector3.one * avg;
                    }
                }
                break;
        }

        frontSideConstraint?.Enforce(content);
        NotifyIfChanged(content, mode);
    }

    private void ApplyLocalPosition(Transform content, Vector3 localPosition)
    {
        if (placementBoundsService != null)
            localPosition = placementBoundsService.ClampLocalPosition(content, localPosition);

        if (targetLocalTransformService != null)
            targetLocalTransformService.SetLocalPosition(content, localPosition);
        else
            content.localPosition = localPosition;

        frontSideConstraint?.Enforce(content);
    }

    private void NotifyIfChanged(Transform content, TransformGizmoController.GizmoMode mode)
    {
        LocalTransformSnapshot snapshot = LocalTransformSnapshot.From(content);
        if (_hasSnapshot && _lastSnapshot.Equals(snapshot))
            return;

        _lastSnapshot = snapshot;
        _hasSnapshot = true;
        ContentTransformChanged?.Invoke(content);
        ContentTransformChangedDetailed?.Invoke(BuildTransformChangeEvent(content, mode));
    }

    public void ResetChangeTracking()
    {
        _hasSnapshot = false;
    }

    private static TransformGizmoController.TransformChangeEvent BuildTransformChangeEvent(
        Transform content,
        TransformGizmoController.GizmoMode mode)
    {
        Transform targetRoot = ResolveTargetRoot(content);
        string targetId = ResolveTargetId(targetRoot);
        string contentId = content != null ? content.name : "";
        return new TransformGizmoController.TransformChangeEvent(
            targetId,
            contentId,
            content != null ? content.localPosition : Vector3.zero,
            content != null ? content.localEulerAngles : Vector3.zero,
            content != null ? content.localScale : Vector3.one,
            mode);
    }

    private static Transform ResolveTargetRoot(Transform content)
    {
        if (content == null)
            return null;

        Transform current = content;
        while (current != null)
        {
            if (string.Equals(current.name, "ContentRoot", StringComparison.Ordinal))
                return current.parent;
            current = current.parent;
        }

        return content.parent;
    }

    private static string ResolveTargetId(Transform targetRoot)
    {
        if (targetRoot == null)
            return "";

        ArImageTarget arImageTarget = targetRoot.GetComponent<ArImageTarget>();
        if (arImageTarget != null && !string.IsNullOrWhiteSpace(arImageTarget.TargetId))
            return arImageTarget.TargetId.Trim();

        ImageTargetPlaceholder placeholder = targetRoot.GetComponentInChildren<ImageTargetPlaceholder>();
        if (placeholder != null && !string.IsNullOrWhiteSpace(placeholder.TargetId))
            return placeholder.TargetId.Trim();

        return targetRoot.name;
    }

    private void Awake()
    {
        if (targetLocalTransformService == null)
            targetLocalTransformService = FindFirstObjectByType<TargetLocalTransformService>();
        if (placementBoundsService == null)
            placementBoundsService = FindFirstObjectByType<PlacementBoundsService>();
        if (frontSideConstraint == null)
            frontSideConstraint = FindFirstObjectByType<FrontSideConstraint>();
    }

    private readonly struct LocalTransformSnapshot : IEquatable<LocalTransformSnapshot>
    {
        public readonly Vector3 position;
        public readonly Quaternion rotation;
        public readonly Vector3 scale;

        private LocalTransformSnapshot(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            this.position = position;
            this.rotation = rotation;
            this.scale = scale;
        }

        public static LocalTransformSnapshot From(Transform transform)
        {
            return new LocalTransformSnapshot(transform.localPosition, transform.localRotation, transform.localScale);
        }

        public bool Equals(LocalTransformSnapshot other)
        {
            return position == other.position && rotation == other.rotation && scale == other.scale;
        }
    }
}
