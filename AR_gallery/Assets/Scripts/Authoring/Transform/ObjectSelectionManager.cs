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
    [Tooltip("UI Toolkit: when set, blocks scene selection like ContentTransformController did (EventSystem alone is not enough).")]
    [SerializeField] private AuthoringUIController authoringUiOverride;

    private Transform _selected;

    public event Action<Transform> SelectionChanged;
    public Transform Selected => _selected;

    public void Configure(Camera cameraRef, Transform contentRootRef)
    {
        if (cameraRef != null)
            raycastCamera = cameraRef;
        if (contentRootRef != null)
            contentRoot = contentRootRef;
    }

    private void Start()
    {
        if (raycastCamera == null)
            raycastCamera = Camera.main;
        if (contentRoot == null)
            contentRoot = FindContentRootInScene();
    }

    private void LateUpdate()
    {
        // Keep selection state valid if the selected object is destroyed/deactivated.
        if (_selected == null)
            return;

        if (!IsSelectionStillValid(_selected))
            SetSelected(null);
    }

    private void Update()
    {
#if !ENABLE_INPUT_SYSTEM
        return;
#else
        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
            return;

        if (DraggableObject.IsDraggingObjectInteractionActive)
            return;

        if (authoringUiOverride != null)
        {
            Vector2 sp = mouse.position.ReadValue();
            if (authoringUiOverride.IsPointerOverAuthoringUi(sp))
                return;
        }
        else if (blockWhenPointerOverUi && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
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
        if (selected != null && !IsSelectionStillValid(selected))
            selected = null;

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

    private bool IsSelectionStillValid(Transform selected)
    {
        if (selected == null)
            return false;
        if (!selected.gameObject.activeInHierarchy)
            return false;
        if (contentRoot == null)
            return true;
        return selected.parent == contentRoot || selected.IsChildOf(contentRoot);
    }

    private static Transform FindContentRootInScene()
    {
        GameObject go = GameObject.Find("ContentRoot");
        return go != null ? go.transform : null;
    }
}
