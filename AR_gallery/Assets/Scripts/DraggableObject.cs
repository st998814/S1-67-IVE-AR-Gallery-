using UnityEngine;
using UnityEngine.InputSystem;

public class DraggableObject : MonoBehaviour
{
    private float zCoord;
    private Vector3 offset;
    private AuthoringUIController uiController;
    private bool isDragging;
    private Camera cam;

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
                zCoord = cam.WorldToScreenPoint(transform.position).z;
                offset = transform.position - GetMouseAsWorldPoint(mouse);
            }
        }

        if (isDragging && mouse.leftButton.isPressed)
        {
            transform.position = GetMouseAsWorldPoint(mouse) + offset;
        }

        if (isDragging && mouse.leftButton.wasReleasedThisFrame)
        {
            isDragging = false;
            if (uiController != null)
                uiController.UpdateCoordinatesFromDrag(transform.localPosition);
        }
    }

    private Vector3 GetMouseAsWorldPoint(Mouse mouse)
    {
        Vector3 mousePoint = mouse.position.ReadValue();
        mousePoint.z = zCoord;
        return cam.ScreenToWorldPoint(mousePoint);
    }
}
