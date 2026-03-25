using UnityEngine;

public class DraggableObject : MonoBehaviour
{
    private float zCoord;
    private Vector3 offset;
    private AuthoringUIController uiController;

    void Start()
    {
        // Automatically find your UI Controller when the game starts
        uiController = FindFirstObjectByType<AuthoringUIController>();
    }

    void OnMouseDown()
    {
        zCoord = Camera.main.WorldToScreenPoint(gameObject.transform.position).z;
        offset = gameObject.transform.position - GetMouseAsWorldPoint();
    }

    void OnMouseDrag()
    {
        transform.position = GetMouseAsWorldPoint() + offset;
    }

    void OnMouseUp()
    {
        // When you let go, send the local coordinates to the UI
        if (uiController != null)
        {
            uiController.UpdateCoordinatesFromDrag(transform.localPosition);
        }
    }

    private Vector3 GetMouseAsWorldPoint()
    {
        Vector3 mousePoint = Input.mousePosition;
        mousePoint.z = zCoord;
        return Camera.main.ScreenToWorldPoint(mousePoint);
    }
}