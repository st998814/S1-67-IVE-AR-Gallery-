using UnityEngine;
using UnityEngine.InputSystem;

public class DraggableObject : MonoBehaviour
{
    public enum InteractionOwner
    {
        None,
        Camera,
        DraggingObject
    }

    [Header("Drag Constraints")]
    [SerializeField] private bool lockLocalZ;
    [SerializeField] private bool moveParentOnDrag;

    [Header("Scale While Dragging")]
    [SerializeField] private bool allowScrollScale;
    [SerializeField] private bool scaleParentOnScroll;
    [SerializeField] private float scrollScaleStep = 0.05f;
    [SerializeField] private float minUniformScale = 0.05f;
    [SerializeField] private float maxUniformScale = 8f;

    private static InteractionOwner currentInteractionOwner = InteractionOwner.None;
    private static int dragOwnerInstanceId = -1;

    private float zCoord;
    private Vector3 offset;
    private float lockedLocalZ;
    private AuthoringUIController uiController;
    private bool isDragging;
    private Camera cam;
    private Transform dragTransform;
    private Transform scaleTransform;

    public static InteractionOwner CurrentInteractionOwner => currentInteractionOwner;
    public static bool IsDraggingObjectInteractionActive => currentInteractionOwner == InteractionOwner.DraggingObject;
    public static bool IsCameraInteractionActive => currentInteractionOwner == InteractionOwner.Camera;

    void Start()
    {
        uiController = FindFirstObjectByType<AuthoringUIController>();
        cam = Camera.main;
    }

    void Update()
    {
        if (cam == null)
            cam = Camera.main;
        Mouse mouse = Mouse.current;
        if (mouse == null || cam == null)
            return;

        if (mouse.leftButton.wasPressedThisFrame && !isDragging)
        {
            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f) &&
                hit.collider != null &&
                hit.collider.GetComponentInParent<DraggableObject>() == this &&
                TryAcquireDragInteraction())
            {
                isDragging = true;
                dragTransform = ResolveDragTransform();
                scaleTransform = ResolveScaleTransform();
                lockedLocalZ = dragTransform.localPosition.z;
                zCoord = cam.WorldToScreenPoint(dragTransform.position).z;
                offset = dragTransform.position - GetMouseAsWorldPoint(mouse);
            }
        }

        if (isDragging && mouse.leftButton.isPressed)
        {
            if (dragTransform == null)
                dragTransform = ResolveDragTransform();
            if (scaleTransform == null)
                scaleTransform = ResolveScaleTransform();

            dragTransform.position = GetMouseAsWorldPoint(mouse) + offset;
            if (lockLocalZ)
            {
                Vector3 localPos = dragTransform.localPosition;
                localPos.z = lockedLocalZ;
                dragTransform.localPosition = localPos;
            }

            if (allowScrollScale)
                ApplyScrollScale(mouse);
        }

        if (isDragging && mouse.leftButton.wasReleasedThisFrame)
        {
            EndDrag(notifyUi: true);
        }
    }

    private void OnDisable()
    {
        if (isDragging)
            EndDrag(notifyUi: false);
    }

    private Vector3 GetMouseAsWorldPoint(Mouse mouse)
    {
        Vector3 mousePoint = mouse.position.ReadValue();
        mousePoint.z = zCoord;
        return cam.ScreenToWorldPoint(mousePoint);
    }

    private void ApplyScrollScale(Mouse mouse)
    {
        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) < 0.01f)
            return;

        Transform t = scaleTransform != null ? scaleTransform : ResolveScaleTransform();
        float current = t.localScale.x;
        float next = Mathf.Clamp(current + scroll * scrollScaleStep, minUniformScale, maxUniformScale);
        t.localScale = Vector3.one * next;
    }

    private void EndDrag(bool notifyUi)
    {
        isDragging = false;
        ReleaseDragInteraction();
        if (notifyUi && uiController != null)
            uiController.UpdateCoordinatesFromDrag((dragTransform != null ? dragTransform : transform).localPosition);
    }

    public void ConfigureConstraints(bool shouldLockLocalZ, bool shouldAllowScrollScale)
    {
        lockLocalZ = shouldLockLocalZ;
        allowScrollScale = shouldAllowScrollScale;
    }

    public void ConfigureDragBinding(bool shouldMoveParentOnDrag)
    {
        moveParentOnDrag = shouldMoveParentOnDrag;
    }

    public void ConfigureScaleBinding(bool shouldScaleParentOnScroll)
    {
        scaleParentOnScroll = shouldScaleParentOnScroll;
    }

    private Transform ResolveDragTransform()
    {
        if (moveParentOnDrag && transform.parent != null)
            return transform.parent;
        return transform;
    }

    private Transform ResolveScaleTransform()
    {
        if (scaleParentOnScroll && transform.parent != null)
            return transform.parent;
        return transform;
    }

    public static bool TryAcquireCameraInteraction()
    {
        if (currentInteractionOwner == InteractionOwner.None || currentInteractionOwner == InteractionOwner.Camera)
        {
            currentInteractionOwner = InteractionOwner.Camera;
            return true;
        }

        return false;
    }

    public static void ReleaseCameraInteraction()
    {
        if (currentInteractionOwner == InteractionOwner.Camera)
            currentInteractionOwner = InteractionOwner.None;
    }

    private bool TryAcquireDragInteraction()
    {
        if (currentInteractionOwner == InteractionOwner.None ||
            (currentInteractionOwner == InteractionOwner.DraggingObject && dragOwnerInstanceId == GetInstanceID()))
        {
            currentInteractionOwner = InteractionOwner.DraggingObject;
            dragOwnerInstanceId = GetInstanceID();
            return true;
        }

        return false;
    }

    private void ReleaseDragInteraction()
    {
        if (currentInteractionOwner == InteractionOwner.DraggingObject && dragOwnerInstanceId == GetInstanceID())
        {
            currentInteractionOwner = InteractionOwner.None;
            dragOwnerInstanceId = -1;
        }
    }
}
