using RTG;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Drag-based movement for TargetRoot constrained to the posture plane.
/// </summary>
public sealed class TargetMovementController : MonoBehaviour
{
    [SerializeField] private Transform targetRoot;
    [SerializeField] private Camera raycastCamera;
    [SerializeField] private TransformGizmoController gizmoController;
    [SerializeField] private LayerMask targetMask = ~0;
    [SerializeField] private float maxRayDistance = 1000f;
    [SerializeField] private bool useTargetForwardAsPlaneNormal = true;
    [SerializeField] private Vector3 fallbackPlaneNormal = Vector3.forward;

    private bool _isDraggingTarget;
    private Plane _dragPlane;
    private Vector3 _dragOffsetWorld;

    public static bool IsTargetDragActive { get; private set; }

    private void Start()
    {
        if (raycastCamera == null)
            raycastCamera = Camera.main;
        if (gizmoController == null)
            gizmoController = FindFirstObjectByType<TransformGizmoController>();
        if (targetRoot == null)
            targetRoot = transform;
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
        if (gizmoController != null && gizmoController.IsManipulating)
            return;
        if (RTGizmosEngine.Get != null && RTGizmosEngine.Get.HoveredGizmo != null)
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

        Vector3 planeNormal = useTargetForwardAsPlaneNormal
            ? targetRoot.forward
            : (fallbackPlaneNormal.sqrMagnitude < 0.0001f ? Vector3.forward : fallbackPlaneNormal.normalized);
        _dragPlane = new Plane(planeNormal, targetRoot.position);

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
