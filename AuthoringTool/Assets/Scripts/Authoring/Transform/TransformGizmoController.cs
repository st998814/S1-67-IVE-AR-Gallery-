using System;
using System.Collections;
using RTG;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Binds RTG gizmos to the current selection. Rotate mode uses a rotation gizmo;
/// Move and Scale modes use inspector sliders only (no translate/scale gizmo handles).
/// </summary>
[DefaultExecutionOrder(1000)]
public sealed class TransformGizmoController : MonoBehaviour
{
    [SerializeField] private ObjectSelectionManager selectionManager;
    [SerializeField] private ContentTransformManipulator contentTransformManipulator;
    [SerializeField] private bool useRuntimeTransformGizmo = true;
    [Tooltip("For a wall target, Local often avoids fighting the front-Z clamp when using a thick 3D proxy mesh.")]
    [SerializeField] private GizmoSpace gizmoTransformSpace = GizmoSpace.Global;
    [SerializeField] private bool enforceUniformScale = true;
    [SerializeField] private bool allowKeyboardModeShortcuts = true;

    private ObjectTransformGizmo _moveGizmo;
    private ObjectTransformGizmo _rotateGizmo;
    private ObjectTransformGizmo _scaleGizmo;
    private ObjectTransformGizmo _universalGizmo;
    private ObjectTransformGizmo _workGizmo;
    private Transform _selected;
    private bool _wasDragging;
    private GizmoMode _mode = GizmoMode.Translate;
    private LocalTransformSnapshot _dragStartSnapshot;
    private bool _hasDragStartSnapshot;
    private bool _gizmosInitialized;
    private int _lastSyncedTargetInstanceId = int.MinValue;
    private GizmoSpace _lastSyncedGizmoSpace;

    public enum GizmoMode
    {
        Translate,
        Rotate,
        Scale,
        Universal
    }

    public event Action<Transform> ContentTransformChanged;
    public event Action<TransformChangeEvent> ContentTransformChangedDetailed;

    public readonly struct TransformChangeEvent
    {
        public readonly string targetId;
        public readonly string contentId;
        public readonly Vector3 localPosition;
        public readonly Vector3 localEuler;
        public readonly Vector3 localScale;
        public readonly GizmoMode mode;

        public TransformChangeEvent(
            string targetId,
            string contentId,
            Vector3 localPosition,
            Vector3 localEuler,
            Vector3 localScale,
            GizmoMode mode)
        {
            this.targetId = string.IsNullOrWhiteSpace(targetId) ? "" : targetId.Trim();
            this.contentId = string.IsNullOrWhiteSpace(contentId) ? "" : contentId.Trim();
            this.localPosition = localPosition;
            this.localEuler = localEuler;
            this.localScale = localScale;
            this.mode = mode;
        }
    }

    public bool IsManipulating
    {
        get
        {
            if (!_gizmosInitialized || _workGizmo == null || _workGizmo.Gizmo == null || RTGizmosEngine.Get == null)
                return false;
            return RTGizmosEngine.Get.DraggedGizmo == _workGizmo.Gizmo;
        }
    }

    public GizmoMode CurrentMode => _mode;

    /// <summary>True when the active mode shows an enabled RTG gizmo (currently Rotate and Universal only).</summary>
    public bool HasActiveSceneGizmo => _gizmosInitialized && _workGizmo != null && _workGizmo.Gizmo != null;

    public void ConfigureDependencies(
        ObjectSelectionManager selectionRef,
        ContentTransformManipulator manipulatorRef)
    {
        if (selectionRef != null)
            selectionManager = selectionRef;
        if (manipulatorRef != null)
            BindManipulator(manipulatorRef);
    }

    /// <summary>Legacy overload — resolves or creates <see cref="ContentTransformManipulator"/> from transform services.</summary>
    public void ConfigureDependencies(
        ObjectSelectionManager selectionRef,
        TargetLocalTransformService localServiceRef,
        FrontSideConstraint frontConstraintRef)
    {
        if (selectionRef != null)
            selectionManager = selectionRef;

        if (contentTransformManipulator == null)
            contentTransformManipulator = FindFirstObjectByType<ContentTransformManipulator>();
        if (contentTransformManipulator == null && frontConstraintRef != null)
        {
            contentTransformManipulator = frontConstraintRef.gameObject.AddComponent<ContentTransformManipulator>();
            contentTransformManipulator.Configure(
                localServiceRef,
                FindFirstObjectByType<PlacementBoundsService>(),
                frontConstraintRef);
        }
        else if (contentTransformManipulator != null)
        {
            contentTransformManipulator.Configure(
                localServiceRef,
                FindFirstObjectByType<PlacementBoundsService>(),
                frontConstraintRef);
        }

        BindManipulator(contentTransformManipulator);
    }

    private void BindManipulator(ContentTransformManipulator manipulator)
    {
        if (contentTransformManipulator != null)
        {
            contentTransformManipulator.ContentTransformChanged -= ForwardContentTransformChanged;
            contentTransformManipulator.ContentTransformChangedDetailed -= ForwardContentTransformChangedDetailed;
        }

        contentTransformManipulator = manipulator;
        if (contentTransformManipulator == null)
            return;

        contentTransformManipulator.ContentTransformChanged += ForwardContentTransformChanged;
        contentTransformManipulator.ContentTransformChangedDetailed += ForwardContentTransformChangedDetailed;
    }

    private void Awake()
    {
        if (selectionManager == null)
            selectionManager = FindFirstObjectByType<ObjectSelectionManager>();
        if (contentTransformManipulator == null)
            contentTransformManipulator = FindFirstObjectByType<ContentTransformManipulator>();
    }

    private void OnEnable()
    {
        if (selectionManager != null)
            selectionManager.SelectionChanged += OnSelectionChanged;
        if (contentTransformManipulator != null)
            BindManipulator(contentTransformManipulator);
    }

    private void OnDisable()
    {
        if (selectionManager != null)
            selectionManager.SelectionChanged -= OnSelectionChanged;
        if (contentTransformManipulator != null)
        {
            contentTransformManipulator.ContentTransformChanged -= ForwardContentTransformChanged;
            contentTransformManipulator.ContentTransformChangedDetailed -= ForwardContentTransformChangedDetailed;
        }
    }

    private void ForwardContentTransformChanged(Transform content) => ContentTransformChanged?.Invoke(content);

    private void ForwardContentTransformChangedDetailed(TransformChangeEvent e) => ContentTransformChangedDetailed?.Invoke(e);

    private void Start()
    {
        StartCoroutine(InitializeGizmoRoutine());
    }

    private IEnumerator InitializeGizmoRoutine()
    {
        if (!useRuntimeTransformGizmo)
            yield break;

        const int maxWaitFrames = 180;
        for (int i = 0; i < maxWaitFrames && RTGizmosEngine.Get == null; i++)
        {
            RTGRuntimeBootstrap.EnsureRTGModules();
            yield return null;
        }

        if (RTGizmosEngine.Get == null)
        {
            Debug.LogWarning("TransformGizmoController: RTGizmosEngine is not ready after bootstrap retry.");
            yield break;
        }

        _moveGizmo = RTGizmosEngine.Get.CreateObjectMoveGizmo();
        _rotateGizmo = RTGizmosEngine.Get.CreateObjectRotationGizmo();
        _scaleGizmo = RTGizmosEngine.Get.CreateObjectScaleGizmo();
        _universalGizmo = RTGizmosEngine.Get.CreateObjectUniversalGizmo();

        if (_moveGizmo == null || _rotateGizmo == null || _scaleGizmo == null || _universalGizmo == null)
        {
            Debug.LogWarning("TransformGizmoController: Failed to create one or more RTG gizmos.");
            yield break;
        }

        _moveGizmo.SetTransformSpace(gizmoTransformSpace);
        _rotateGizmo.SetTransformSpace(gizmoTransformSpace);
        _scaleGizmo.SetTransformSpace(gizmoTransformSpace);
        _universalGizmo.SetTransformSpace(gizmoTransformSpace);

        SafeSetGizmoEnabled(_moveGizmo, false);
        SafeSetGizmoEnabled(_rotateGizmo, false);
        SafeSetGizmoEnabled(_scaleGizmo, false);
        SafeSetGizmoEnabled(_universalGizmo, false);

        _gizmosInitialized = true;
        _workGizmo = _moveGizmo;
        if (_selected != null)
            PushCurrentSelectionToAllGizmos();
        ApplyGizmoModeVisualState();
    }

    private void PushCurrentSelectionToAllGizmos()
    {
        if (!_gizmosInitialized || _selected == null)
            return;

        GameObject targetGo = _selected.gameObject;
        _moveGizmo.SetTransformSpace(gizmoTransformSpace);
        _rotateGizmo.SetTransformSpace(gizmoTransformSpace);
        _scaleGizmo.SetTransformSpace(gizmoTransformSpace);
        _universalGizmo.SetTransformSpace(gizmoTransformSpace);
        _moveGizmo.SetTargetObject(targetGo);
        _rotateGizmo.SetTargetObject(targetGo);
        _scaleGizmo.SetTargetObject(targetGo);
        _universalGizmo.SetTargetObject(targetGo);
        _lastSyncedGizmoSpace = gizmoTransformSpace;
        _lastSyncedTargetInstanceId = targetGo.GetInstanceID();
    }

    private void Update()
    {
        if (!_gizmosInitialized)
            return;

#if ENABLE_INPUT_SYSTEM
        if (allowKeyboardModeShortcuts && Keyboard.current != null)
        {
            if (Keyboard.current.digit1Key.wasPressedThisFrame) SetMode(GizmoMode.Translate);
            if (Keyboard.current.digit2Key.wasPressedThisFrame) SetMode(GizmoMode.Rotate);
            if (Keyboard.current.digit3Key.wasPressedThisFrame) SetMode(GizmoMode.Scale);
            if (Keyboard.current.digit4Key.wasPressedThisFrame) SetMode(GizmoMode.Universal);
        }
#endif

        if (_selected == null)
        {
            DisableAllGizmos();
            _lastSyncedTargetInstanceId = int.MinValue;
            return;
        }

        // Do not call ApplyGizmoModeVisualState every frame: it disables all gizmos first,
        // which cancels RTG drags and makes handles appear to do nothing.
        // Only refresh target/space when needed, and never during an active gizmo drag.
        bool rtgDragging = RTGizmosEngine.Get != null && RTGizmosEngine.Get.DraggedGizmo != null;
        if (!rtgDragging)
            SyncAllGizmosTargetAndSpaceIfDirty();
    }

    private void SyncAllGizmosTargetAndSpaceIfDirty()
    {
        if (_selected == null)
            return;

        int id = _selected.gameObject.GetInstanceID();
        bool spaceChanged = gizmoTransformSpace != _lastSyncedGizmoSpace;
        bool targetChanged = id != _lastSyncedTargetInstanceId;
        if (!spaceChanged && !targetChanged)
            return;

        _lastSyncedGizmoSpace = gizmoTransformSpace;
        _lastSyncedTargetInstanceId = id;

        _moveGizmo.SetTransformSpace(gizmoTransformSpace);
        _rotateGizmo.SetTransformSpace(gizmoTransformSpace);
        _scaleGizmo.SetTransformSpace(gizmoTransformSpace);
        _universalGizmo.SetTransformSpace(gizmoTransformSpace);

        GameObject targetGo = _selected.gameObject;
        _moveGizmo.SetTargetObject(targetGo);
        _rotateGizmo.SetTargetObject(targetGo);
        _scaleGizmo.SetTargetObject(targetGo);
        _universalGizmo.SetTargetObject(targetGo);
    }

    private void LateUpdate()
    {
        if (!_gizmosInitialized || _selected == null || _workGizmo == null || _workGizmo.Gizmo == null || RTGizmosEngine.Get == null)
            return;

        Gizmo dragged = RTGizmosEngine.Get.DraggedGizmo;
        bool draggingNow = dragged != null && dragged == _workGizmo.Gizmo;
        if (draggingNow)
        {
            if (!_wasDragging)
            {
                _dragStartSnapshot = LocalTransformSnapshot.From(_selected);
                _hasDragStartSnapshot = true;
            }

            ApplyModeConstraintRules(_selected);
            ApplyPostTransformRules(_selected);
            _wasDragging = true;
            return;
        }

        if (RTGizmosEngine.Get.JustReleasedDrag && _wasDragging)
        {
            ApplyModeConstraintRules(_selected);
            ApplyPostTransformRules(_selected);
            _wasDragging = false;
            _hasDragStartSnapshot = false;
        }
    }

    private void OnSelectionChanged(Transform selected)
    {
        _selected = selected;
        _hasDragStartSnapshot = false;
        contentTransformManipulator?.ResetChangeTracking();

        if (!_gizmosInitialized)
            return;

        if (_selected == null)
        {
            DisableAllGizmos();
            _lastSyncedTargetInstanceId = int.MinValue;
            return;
        }

        PushCurrentSelectionToAllGizmos();
        ApplyGizmoModeVisualState();
        ApplyPostTransformRules(_selected);
    }

    public void SetMode(GizmoMode mode)
    {
        _mode = mode;
        ApplyGizmoModeVisualState();
    }

    private void ApplyGizmoModeVisualState()
    {
        if (!_gizmosInitialized)
            return;

        DisableAllGizmos();

        _workGizmo = ResolveGizmoForMode(_mode);
        if (_workGizmo == null || _workGizmo.Gizmo == null)
            return;

        if (_selected != null)
            SafeSetGizmoEnabled(_workGizmo, true);
    }

    private void DisableAllGizmos()
    {
        SafeSetGizmoEnabled(_moveGizmo, false);
        SafeSetGizmoEnabled(_rotateGizmo, false);
        SafeSetGizmoEnabled(_scaleGizmo, false);
        SafeSetGizmoEnabled(_universalGizmo, false);
    }

    private static void SafeSetGizmoEnabled(ObjectTransformGizmo gizmo, bool enabled)
    {
        if (gizmo?.Gizmo == null)
            return;
        gizmo.Gizmo.SetEnabled(enabled);
    }

    private ObjectTransformGizmo ResolveGizmoForMode(GizmoMode mode)
    {
        switch (mode)
        {
            case GizmoMode.Translate:
            case GizmoMode.Scale:
                // Position / uniform scale are driven by inspector sliders via ContentTransformManipulator.
                return null;

            case GizmoMode.Rotate:
                return _rotateGizmo;

            case GizmoMode.Universal:
                return _universalGizmo;

            default:
                return null;
        }
    }

    private void ApplyPostTransformRules(Transform content)
    {
        if (content == null)
            return;

        if (contentTransformManipulator != null)
        {
            bool normalizeScale = enforceUniformScale || _mode == GizmoMode.Scale;
            contentTransformManipulator.ApplyGizmoResult(content, _mode, normalizeScale);
            return;
        }

        Debug.LogWarning("TransformGizmoController: ContentTransformManipulator is missing; gizmo transform was not committed.");
    }

    private void ApplyModeConstraintRules(Transform content)
    {
        if (content == null || !_hasDragStartSnapshot)
            return;

        switch (_mode)
        {
            case GizmoMode.Translate:
                // Move only: keep start local rotation + local scale.
                content.localRotation = _dragStartSnapshot.rotation;
                content.localScale = _dragStartSnapshot.scale;
                break;

            case GizmoMode.Rotate:
                // Rotate only: keep start local position + local scale.
                content.localPosition = _dragStartSnapshot.position;
                content.localScale = _dragStartSnapshot.scale;
                break;

            case GizmoMode.Scale:
                // Uniform scale only: keep start local position + local rotation.
                content.localPosition = _dragStartSnapshot.position;
                content.localRotation = _dragStartSnapshot.rotation;
                break;

            case GizmoMode.Universal:
            default:
                break;
        }
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
