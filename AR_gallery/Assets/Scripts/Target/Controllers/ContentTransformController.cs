using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using RTG;

public class ContentTransformController : MonoBehaviour
{
    [SerializeField] private TargetSelectionManager targetSelectionManager;
    [SerializeField] private AuthoringUIController authoringUI;
    [SerializeField] private Camera raycastCamera;

    [SerializeField] private float moveStep = 0.1f;
    [SerializeField] private float rotateStep = 10f;
    [SerializeField] private float scaleStep = 0.1f;

    [Tooltip("使用 Runtime Transform Gizmos 的 3D 操作轴（鼠标拖拽）。仍可用 R 键切换选中内容。")]
    [SerializeField] private bool useRuntimeTransformGizmo = true;

    [Tooltip("Global：与世界 XYZ 对齐，墙前深度更直观；Local：沿物体自身轴。")]
    [SerializeField] private GizmoSpace gizmoTransformSpace = GizmoSpace.Global;

    [SerializeField] private float clickSelectRayDistance = 200f;
    [SerializeField] private LayerMask clickSelectLayerMask = ~0;

    private readonly List<Transform> contentObjects = new List<Transform>();
    private int selectedIndex = -1;
    private int _authoringSyncedTargetIndex = int.MinValue;
    private ObjectTransformGizmo _universalGizmo;
    private bool _wasDraggingUniversalGizmo;

    private void Start()
    {
        if (authoringUI == null)
            authoringUI = FindFirstObjectByType<AuthoringUIController>();
        if (raycastCamera == null)
            raycastCamera = Camera.main;

        if (targetSelectionManager != null)
        {
            targetSelectionManager.ActiveTargetChanged += OnActiveAuthoringTargetChanged;
            _authoringSyncedTargetIndex = targetSelectionManager.ActiveTargetIndex;
        }

        RefreshContentList();
        if (contentObjects.Count > 0)
        {
            selectedIndex = Mathf.Clamp(selectedIndex, 0, contentObjects.Count - 1);
            UpdateSelectionVisual();
            Transform sel = GetSelectedContent();
            if (authoringUI != null && sel != null)
                authoringUI.OnContentSelectedInScene(sel);
        }
        else if (authoringUI != null)
        {
            authoringUI.ClearAuthoringSpatialSelection();
        }

        StartCoroutine(InitRuntimeGizmo());
    }

    private void OnDisable()
    {
        if (targetSelectionManager != null)
            targetSelectionManager.ActiveTargetChanged -= OnActiveAuthoringTargetChanged;
    }

    /// <summary>下拉或键盘切换 AR Target 后，把侧栏坐标绑定到「当前可见 Target」下第一个内容，否则会一直改上一个（已隐藏）Target 上的物体。</summary>
    private void OnActiveAuthoringTargetChanged(int newTargetIndex)
    {
        if (newTargetIndex == _authoringSyncedTargetIndex)
            return;
        _authoringSyncedTargetIndex = newTargetIndex;

        RefreshContentList();
        if (contentObjects.Count > 0)
        {
            selectedIndex = 0;
            UpdateSelectionVisual();
            Transform sel = GetSelectedContent();
            if (authoringUI != null && sel != null)
                authoringUI.OnContentSelectedInScene(sel);
        }
        else
        {
            selectedIndex = -1;
            authoringUI?.ClearAuthoringSpatialSelection();
        }
    }

    private IEnumerator InitRuntimeGizmo()
    {
        if (!useRuntimeTransformGizmo)
            yield break;

        yield return null;

        if (RTGizmosEngine.Get == null)
        {
            Debug.LogWarning("ContentTransformController: RTGizmosEngine 未就绪，操作轴不可用。请确认场景存在 Main Camera。");
            yield break;
        }

        _universalGizmo = RTGizmosEngine.Get.CreateObjectUniversalGizmo();
        _universalGizmo.SetTransformSpace(gizmoTransformSpace);
        _universalGizmo.Gizmo.SetEnabled(false);
    }

    /// <summary>刷新列表并选中指定 ContentRoot 子物体（用于上传/生成后与 Gizmo 同步）。</summary>
    public void SelectContentTransform(Transform contentChild, bool syncAuthoringUi = true)
    {
        if (contentChild == null)
            return;

        RefreshContentList();

        for (int i = 0; i < contentObjects.Count; i++)
        {
            if (contentObjects[i] != contentChild)
                continue;

            selectedIndex = i;
            UpdateSelectionVisual();
            if (syncAuthoringUi && authoringUI != null)
                authoringUI.OnContentSelectedInScene(contentChild);
            return;
        }
    }

    private void LateUpdate()
    {
        Transform sel = GetSelectedContent();
        if (sel == null || authoringUI == null || _universalGizmo == null || RTGizmosEngine.Get == null)
            return;

        Gizmo dragged = RTGizmosEngine.Get.DraggedGizmo;
        if (dragged == _universalGizmo.Gizmo)
        {
            authoringUI.SyncTransformToInspector(sel);
            _wasDraggingUniversalGizmo = true;
        }
        else if (RTGizmosEngine.Get.JustReleasedDrag && _wasDraggingUniversalGizmo)
        {
            authoringUI.SyncTransformToInspector(sel);
            _wasDraggingUniversalGizmo = false;
        }
    }

    private void Update()
    {
        RefreshContentList();

        TryClickSelectContent();

        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            SelectNextContent();
        }

        Transform selected = GetSelectedContent();
        SyncRuntimeGizmo(selected);
        if (selected == null)
            return;

        if (IsRuntimeGizmoDragging())
            return;

        HandlePositionInput(selected);
        HandleRotationInput(selected);
        HandleScaleInput(selected);
    }

    private void TryClickSelectContent()
    {
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
            return;

        if (raycastCamera == null)
            return;

        Vector2 screenPos = Mouse.current.position.ReadValue();
        if (authoringUI != null && authoringUI.IsPointerOverAuthoringUi(screenPos))
            return;

        if (RTGizmosEngine.Get != null && RTGizmosEngine.Get.HoveredGizmo != null)
            return;

        Ray ray = raycastCamera.ScreenPointToRay(screenPos);
        if (!Physics.Raycast(ray, out RaycastHit hit, clickSelectRayDistance, clickSelectLayerMask,
                QueryTriggerInteraction.Ignore))
            return;

        Transform hitTr = hit.transform;
        Transform contentChild = FindContentRootChildFromHit(hitTr);
        if (contentChild == null)
            return;

        SelectContentTransform(contentChild, syncAuthoringUi: true);
    }

    /// <summary>从碰撞体向上查找属于当前激活 Target 的 ContentRoot 直系子物体。</summary>
    private Transform FindContentRootChildFromHit(Transform hitTransform)
    {
        if (targetSelectionManager == null || hitTransform == null)
            return null;

        GameObject activeTarget = targetSelectionManager.GetActiveTarget();
        if (activeTarget == null)
            return null;

        Transform contentRoot = activeTarget.transform.Find("ContentRoot");
        if (contentRoot == null)
            return null;

        Transform t = hitTransform;
        while (t != null)
        {
            if (t.parent == contentRoot)
                return t;
            t = t.parent;
        }

        return null;
    }

    private bool IsRuntimeGizmoDragging()
    {
        return _universalGizmo != null
            && RTGizmosEngine.Get != null
            && RTGizmosEngine.Get.DraggedGizmo != null;
    }

    private void SyncRuntimeGizmo(Transform selected)
    {
        if (_universalGizmo == null || !useRuntimeTransformGizmo)
            return;

        if (selected == null)
        {
            _universalGizmo.Gizmo.SetEnabled(false);
            return;
        }

        _universalGizmo.SetTransformSpace(gizmoTransformSpace);
        _universalGizmo.Gizmo.SetEnabled(true);
        _universalGizmo.SetTargetObject(selected.gameObject);
    }

    public void RefreshContentList()
    {
        contentObjects.Clear();

        if (targetSelectionManager == null)
        {
            Debug.LogWarning("ContentTransformController: targetSelectionManager is null.");
            return;
        }

        GameObject activeTarget = targetSelectionManager.GetActiveTarget();
        if (activeTarget == null)
        {
            Debug.LogWarning("ContentTransformController: activeTarget is null.");
            return;
        }

        Transform contentRoot = activeTarget.transform.Find("ContentRoot");
        if (contentRoot == null)
        {
            Debug.LogWarning($"ContentTransformController: ContentRoot not found under {activeTarget.name}");
            return;
        }

        foreach (Transform child in contentRoot)
        {
            contentObjects.Add(child);
        }

        if (contentObjects.Count == 0)
        {
            selectedIndex = -1;
        }
        else if (selectedIndex < 0 || selectedIndex >= contentObjects.Count)
        {
            selectedIndex = 0;
        }
    }

    private void SelectNextContent()
    {
        if (contentObjects.Count == 0)
        {
            selectedIndex = -1;
            Debug.Log("No content objects available.");
            return;
        }

        selectedIndex++;
        if (selectedIndex >= contentObjects.Count)
        {
            selectedIndex = 0;
        }
        UpdateSelectionVisual();

        Transform sel = GetSelectedContent();
        if (authoringUI != null && sel != null)
            authoringUI.OnContentSelectedInScene(sel);

        Debug.Log($"Selected: {contentObjects[selectedIndex].name}");
    }

    private Transform GetSelectedContent()
    {
        if (selectedIndex < 0 || selectedIndex >= contentObjects.Count)
            return null;

        return contentObjects[selectedIndex];
    }

    private void UpdateSelectionVisual()
    {
        for (int i = 0; i < contentObjects.Count; i++)
        {
            Renderer renderer = contentObjects[i].GetComponent<Renderer>();
            if (renderer == null)
                continue;

            // 已加载贴图的海报不要用 material.color 染色，否则会整张贴图变色
            if (RendererHasAssignedTexture(renderer))
                continue;

            if (i == selectedIndex)
            {
                renderer.material.color = Color.yellow;
            }
            else
            {
                renderer.material.color = Color.red;
            }
        }
    }

    private static bool RendererHasAssignedTexture(Renderer renderer)
    {
        Material m = renderer.sharedMaterial;
        if (m == null)
            return false;
        if (m.mainTexture != null)
            return true;
        if (m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null)
            return true;
        if (m.HasProperty("_MainTex") && m.GetTexture("_MainTex") != null)
            return true;
        return false;
    }

    private void HandlePositionInput(Transform target)
    {
        Vector3 pos = target.localPosition;

        if (Keyboard.current.aKey.isPressed) pos.x -= moveStep * Time.deltaTime * 10f;
        if (Keyboard.current.dKey.isPressed) pos.x += moveStep * Time.deltaTime * 10f;
        if (Keyboard.current.wKey.isPressed) pos.y += moveStep * Time.deltaTime * 10f;
        if (Keyboard.current.sKey.isPressed) pos.y -= moveStep * Time.deltaTime * 10f;
        if (Keyboard.current.qKey.isPressed) pos.z -= moveStep * Time.deltaTime * 10f;
        if (Keyboard.current.eKey.isPressed) pos.z += moveStep * Time.deltaTime * 10f;

        target.localPosition = pos;
    }

    private void HandleRotationInput(Transform target)
    {
        Vector3 rot = target.localEulerAngles;

        if (Keyboard.current.zKey.isPressed) rot.y -= rotateStep * Time.deltaTime * 10f;
        if (Keyboard.current.xKey.isPressed) rot.y += rotateStep * Time.deltaTime * 10f;

        target.localEulerAngles = rot;
    }

    private void HandleScaleInput(Transform target)
    {
        Vector3 scale = target.localScale;

        if (Keyboard.current.cKey.isPressed) scale += Vector3.one * scaleStep * Time.deltaTime * 10f;
        if (Keyboard.current.vKey.isPressed) scale -= Vector3.one * scaleStep * Time.deltaTime * 10f;

        scale.x = Mathf.Max(0.1f, scale.x);
        scale.y = Mathf.Max(0.1f, scale.y);
        scale.z = Mathf.Max(0.1f, scale.z);

        target.localScale = scale;
    }
}
