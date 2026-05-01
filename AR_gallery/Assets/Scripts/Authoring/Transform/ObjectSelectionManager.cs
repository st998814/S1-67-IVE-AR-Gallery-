using System;
using RTG;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using UnityEngine.EventSystems;

/// <summary>
/// Raycast-based single selection manager for content objects.
/// Intended to be reused across sandbox and AuthoringToolScene.
/// </summary>
public sealed class ObjectSelectionManager : MonoBehaviour
{
    [SerializeField] private Camera raycastCamera;
    [SerializeField] private Transform contentRoot;
    [SerializeField] private LayerMask selectionMask = ~0;
    [SerializeField] private float maxRayDistance = 500f;
    [SerializeField] private bool clearSelectionOnEmptyClick = true;
    [SerializeField] private bool blockWhenPointerOverUi = true;

    private Transform _selected;

    public event Action<Transform> SelectionChanged;
    public Transform Selected => _selected;

    private void Start()
    {
        if (raycastCamera == null)
            raycastCamera = Camera.main;
    }

    private void Update()
    {
#if !ENABLE_INPUT_SYSTEM
        return;
#else
        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
            return;

        if (blockWhenPointerOverUi && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        if (RTGizmosEngine.Get != null && RTGizmosEngine.Get.HoveredGizmo != null)
            return;

        if (raycastCamera == null)
            raycastCamera = Camera.main;
        if (raycastCamera == null)
            return;

        Ray ray = raycastCamera.ScreenPointToRay(mouse.position.ReadValue());
        if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, selectionMask, QueryTriggerInteraction.Ignore))
        {
            Transform selectable = ResolveSelectableTransform(hit.transform);
            if (selectable != null)
            {
                SetSelected(selectable);
                return;
            }
        }

        if (clearSelectionOnEmptyClick)
            SetSelected(null);
#endif
    }

    public void SetSelected(Transform selected)
    {
        if (_selected == selected)
            return;

        _selected = selected;
        SelectionChanged?.Invoke(_selected);
    }

    private Transform ResolveSelectableTransform(Transform hitTransform)
    {
        if (hitTransform == null)
            return null;

        if (contentRoot == null)
            return hitTransform;

        Transform current = hitTransform;
        while (current != null)
        {
            if (current.parent == contentRoot)
                return current;
            if (current == contentRoot)
                return null;
            current = current.parent;
        }

        return null;
    }
}
