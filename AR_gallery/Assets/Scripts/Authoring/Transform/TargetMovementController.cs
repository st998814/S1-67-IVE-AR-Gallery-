using RTG;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Drag-based movement for TargetRoot constrained to the posture plane.
/// Runs early so <see cref="IsTargetDragActive"/> is set before camera input is evaluated this frame.
/// </summary>
[DefaultExecutionOrder(-100)]
public sealed class TargetMovementController : MonoBehaviour
{
    [SerializeField] private Transform targetRoot;
    [SerializeField] private Transform contentRoot;
    [Tooltip("Optional. When set, the drag plane passes through this transform’s position and uses its forward as the wall/image normal when Use Target Forward As Plane Normal is enabled. Use e.g. the quad mesh transform when pivot and visual plane differ.")]
    [SerializeField] private Transform planeAnchor;
    [SerializeField] private Camera raycastCamera;
    [SerializeField] private TransformGizmoController gizmoController;
    [SerializeField] private LayerMask targetMask = ~0;
    [SerializeField] private float maxRayDistance = 1000f;
    [SerializeField] private bool useTargetForwardAsPlaneNormal = true;
    [SerializeField] private Vector3 fallbackPlaneNormal = Vector3.forward;

    [Tooltip("If true, reparents ContentRoot under TargetRoot at start so moving the target moves all content. Content-only moves never affect the target.")]
    [SerializeField] private bool reparentContentRootUnderTargetIfNeeded = true;

    private bool _isDraggingTarget;
    private Plane _dragPlane;
    private Vector3 _dragOffsetWorld;

    public static bool IsTargetDragActive { get; private set; }

    public void ConfigureDependencies(Transform targetRootRef, Transform contentRootRef, Camera cameraRef, TransformGizmoController gizmoRef, Transform planeAnchorRef = null)
    {
        if (targetRootRef != null)
            targetRoot = targetRootRef;
        if (contentRootRef != null)
            contentRoot = contentRootRef;
        if (cameraRef != null)
            raycastCamera = cameraRef;
        if (gizmoRef != null)
            gizmoController = gizmoRef;
        if (planeAnchorRef != null)
            planeAnchor = planeAnchorRef;
    }

    private void Start()
    {
        if (raycastCamera == null)
            raycastCamera = Camera.main;
        if (gizmoController == null)
            gizmoController = FindFirstObjectByType<TransformGizmoController>();
        if (targetRoot == null)
            targetRoot = transform;
        if (contentRoot == null && targetRoot != null)
        {
            contentRoot = targetRoot.Find("ContentRoot");
            if (contentRoot == null)
            {
                GameObject found = GameObject.Find("ContentRoot");
                if (found != null)
                    contentRoot = found.transform;
            }
        }

        if (reparentContentRootUnderTargetIfNeeded)
            EnsureContentRootIsChildOfTarget();
    }

    /// <summary>
    /// Content must live under TargetRoot so translating the target carries all content.
    /// Gizmo moves only the selected object; it does not move TargetRoot.
    /// </summary>
    private void EnsureContentRootIsChildOfTarget()
    {
        if (targetRoot == null || contentRoot == null)
            return;
        if (contentRoot.parent == targetRoot)
            return;
        if (contentRoot.IsChildOf(targetRoot))
            return;

        contentRoot.SetParent(targetRoot, worldPositionStays: true);
    }

    private void Update()
    {
#if !ENABLE_INPUT_SYSTEM
        return;
#else
        Mouse mouse = Mouse.current;
        if (mouse == null)
            return;

        if (mouse.leftButton.wasPressedThisFrame)
            TryBeginTargetDrag(mouse.position.ReadValue());

        if (_isDraggingTarget && mouse.leftButton.isPressed)
            ContinueTargetDrag(mouse.position.ReadValue());

        if (_isDraggingTarget && mouse.leftButton.wasReleasedThisFrame)
            EndTargetDrag();
#endif
    }

#if ENABLE_INPUT_SYSTEM
    private void TryBeginTargetDrag(Vector2 screenPosition)
    {
        if (targetRoot == null)
            return;
        // Precedence: any RTG drag / hover or app gizmo manipulation wins over target-plane drag.
        if (RTGizmosEngine.Get != null && RTGizmosEngine.Get.DraggedGizmo != null)
            return;
        if (RTGizmosEngine.Get != null && RTGizmosEngine.Get.HoveredGizmo != null)
            return;
        if (gizmoController != null && gizmoController.IsManipulating)
            return;

        if (raycastCamera == null)
            raycastCamera = Camera.main;
        if (raycastCamera == null)
            return;

        Ray ray = raycastCamera.ScreenPointToRay(screenPosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, targetMask, QueryTriggerInteraction.Ignore))
            return;

        if (!IsTargetHit(hit.transform))
            return;
        if (IsContentHit(hit.transform))
            return;

        Transform anchor = planeAnchor != null ? planeAnchor : targetRoot;
        Vector3 planeNormal = useTargetForwardAsPlaneNormal
            ? anchor.forward.normalized
            : (fallbackPlaneNormal.sqrMagnitude < 0.0001f ? Vector3.forward : fallbackPlaneNormal.normalized);
        _dragPlane = new Plane(planeNormal, anchor.position);

        if (!_dragPlane.Raycast(ray, out float enter))
            return;

        Vector3 hitPoint = ray.GetPoint(enter);
        _dragOffsetWorld = targetRoot.position - hitPoint;
        _isDraggingTarget = true;
        IsTargetDragActive = true;
    }

    private void ContinueTargetDrag(Vector2 screenPosition)
    {
        if (targetRoot == null || raycastCamera == null)
            return;

        Ray ray = raycastCamera.ScreenPointToRay(screenPosition);
        if (!_dragPlane.Raycast(ray, out float enter))
            return;

        Vector3 hitPoint = ray.GetPoint(enter);
        Vector3 desired = hitPoint + _dragOffsetWorld;

        // Keep movement inside the posture plane.
        Vector3 planeNormal = _dragPlane.normal;
        Vector3 toDesired = desired - targetRoot.position;
        Vector3 planarDelta = Vector3.ProjectOnPlane(toDesired, planeNormal);
        targetRoot.position += planarDelta;
    }
#endif

    private bool IsTargetHit(Transform hitTransform)
    {
        if (hitTransform == null || targetRoot == null)
            return false;

        return hitTransform == targetRoot || hitTransform.IsChildOf(targetRoot);
    }

    private bool IsContentHit(Transform hitTransform)
    {
        if (hitTransform == null || contentRoot == null)
            return false;
        return hitTransform == contentRoot || hitTransform.IsChildOf(contentRoot);
    }

    private void EndTargetDrag()
    {
        _isDraggingTarget = false;
        IsTargetDragActive = false;
    }

    private void OnDisable()
    {
        EndTargetDrag();
    }
}
