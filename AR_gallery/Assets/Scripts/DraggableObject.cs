using UnityEngine;
using UnityEngine.InputSystem;

public class DraggableObject : MonoBehaviour
{
    [Header("Drag Constraints")]
    [SerializeField] private bool lockLocalZ;
    [SerializeField] private bool moveParentOnDrag;

    [Header("Scale While Dragging")]
    [SerializeField] private bool allowScrollScale;
    [SerializeField] private float scrollScaleStep = 0.05f;
    [SerializeField] private float minUniformScale = 0.05f;
    [SerializeField] private float maxUniformScale = 8f;

    private float zCoord;
    private Vector3 offset;
    private float lockedLocalZ;
    private AuthoringUIController uiController;
    private bool isDragging;
    private Camera cam;
    private Transform dragTransform;

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
                hit.collider.GetComponentInParent<DraggableObject>() == this)
            {
                isDragging = true;
                dragTransform = ResolveDragTransform();
                lockedLocalZ = dragTransform.localPosition.z;
                zCoord = cam.WorldToScreenPoint(dragTransform.position).z;
                offset = dragTransform.position - GetMouseAsWorldPoint(mouse);
            }
        }

        if (isDragging && mouse.leftButton.isPressed)
        {
            if (dragTransform == null)
                dragTransform = ResolveDragTransform();

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
            isDragging = false;
            if (uiController != null)
                uiController.UpdateCoordinatesFromDrag((dragTransform != null ? dragTransform : transform).localPosition);
        }
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

        Transform t = dragTransform != null ? dragTransform : ResolveDragTransform();
        float current = t.localScale.x;
        float next = Mathf.Clamp(current + scroll * scrollScaleStep, minUniformScale, maxUniformScale);
        t.localScale = Vector3.one * next;
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

    private Transform ResolveDragTransform()
    {
        if (moveParentOnDrag && transform.parent != null)
            return transform.parent;
        return transform;
    }
}
