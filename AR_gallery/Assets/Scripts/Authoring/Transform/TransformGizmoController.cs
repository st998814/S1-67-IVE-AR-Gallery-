using System;
using System.Collections;
using RTG;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Binds a runtime gizmo to the current selection and enforces target-local transform constraints.
/// </summary>
public sealed class TransformGizmoController : MonoBehaviour
{
    [SerializeField] private ObjectSelectionManager selectionManager;
    [SerializeField] private TargetLocalTransformService targetLocalTransformService;
    [SerializeField] private FrontSideConstraint frontSideConstraint;
    [SerializeField] private bool useRuntimeTransformGizmo = true;
    [SerializeField] private GizmoSpace gizmoTransformSpace = GizmoSpace.Global;
    [SerializeField] private bool enforceUniformScale = true;
    [SerializeField] private bool allowKeyboardModeShortcuts = true;

    private ObjectTransformGizmo _universalGizmo;
    private Transform _selected;
    private bool _wasDragging;
    private GizmoMode _mode = GizmoMode.Translate;
    private LocalTransformSnapshot _dragStartSnapshot;
    private bool _hasDragStartSnapshot;
    private LocalTransformSnapshot _lastSnapshot;
    private bool _hasSnapshot;

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
            if (_universalGizmo == null || RTGizmosEngine.Get == null)
                return false;
            return RTGizmosEngine.Get.DraggedGizmo == _universalGizmo.Gizmo;
        }
    }

    public GizmoMode CurrentMode => _mode;

    private void Awake()
    {
        if (selectionManager == null)
            selectionManager = FindFirstObjectByType<ObjectSelectionManager>();
        if (targetLocalTransformService == null)
            targetLocalTransformService = FindFirstObjectByType<TargetLocalTransformService>();
        if (frontSideConstraint == null)
            frontSideConstraint = FindFirstObjectByType<FrontSideConstraint>();
    }

    private void OnEnable()
    {
        if (selectionManager != null)
            selectionManager.SelectionChanged += OnSelectionChanged;
    }

    private void OnDisable()
    {
        if (selectionManager != null)
            selectionManager.SelectionChanged -= OnSelectionChanged;
    }

    private void Start()
    {
        StartCoroutine(InitializeGizmoRoutine());
    }

    private IEnumerator InitializeGizmoRoutine()
    {
        if (!useRuntimeTransformGizmo)
            yield break;

        // Wait a frame so RTG bootstrap has time to create modules.
        yield return null;

        if (RTGizmosEngine.Get == null)
        {
            Debug.LogWarning("TransformGizmoController: RTGizmosEngine is not ready.");
            yield break;
        }

        _universalGizmo = RTGizmosEngine.Get.CreateObjectUniversalGizmo();
        _universalGizmo.SetTransformSpace(gizmoTransformSpace);
        _universalGizmo.Gizmo.SetEnabled(false);
        ApplyGizmoModeVisualState();
    }

    private void Update()
    {
        if (_universalGizmo == null)
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
            _universalGizmo.Gizmo.SetEnabled(false);
            return;
        }

        _universalGizmo.SetTransformSpace(gizmoTransformSpace);
        _universalGizmo.SetTargetObject(_selected.gameObject);
        _universalGizmo.Gizmo.SetEnabled(true);
    }

    private void LateUpdate()
    {
        if (_selected == null || _universalGizmo == null || RTGizmosEngine.Get == null)
            return;

        bool draggingNow = RTGizmosEngine.Get.DraggedGizmo == _universalGizmo.Gizmo;
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
        _hasSnapshot = false;
        _hasDragStartSnapshot = false;

        if (_universalGizmo == null)
            return;

        if (_selected == null)
        {
            _universalGizmo.Gizmo.SetEnabled(false);
            return;
        }

        _universalGizmo.SetTargetObject(_selected.gameObject);
        _universalGizmo.Gizmo.SetEnabled(true);
        ApplyPostTransformRules(_selected);
    }

    public void SetMode(GizmoMode mode)
    {
        _mode = mode;
        ApplyGizmoModeVisualState();
    }

    private void ApplyGizmoModeVisualState()
    {
        if (_universalGizmo == null)
            return;

        // Keep one RTG gizmo instance for stability and portability.
        // Mode still maps to explicit transform intent for future scene UI wiring.
        bool isTranslate = _mode == GizmoMode.Translate;
        bool isRotate = _mode == GizmoMode.Rotate;
        bool isScale = _mode == GizmoMode.Scale;
        bool isUniversal = _mode == GizmoMode.Universal;

        _universalGizmo.Gizmo.SetEnabled(isTranslate || isRotate || isScale || isUniversal);
    }

    private void ApplyPostTransformRules(Transform content)
    {
        if (content == null)
            return;

        // Always write local-space values through the local service to keep
        // transform persistence model target-local and explicit.
        if (targetLocalTransformService != null)
        {
            targetLocalTransformService.SetLocalPosition(content, content.localPosition);
            targetLocalTransformService.SetLocalRotation(content, content.localRotation);
            if (enforceUniformScale || _mode == GizmoMode.Scale)
                targetLocalTransformService.NormalizeUniformScale(content);
        }

        frontSideConstraint?.Enforce(content);
        RaiseChangedIfNeeded(content);
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

    private void RaiseChangedIfNeeded(Transform content)
    {
        LocalTransformSnapshot snapshot = LocalTransformSnapshot.From(content);
        if (_hasSnapshot && _lastSnapshot.Equals(snapshot))
            return;

        _lastSnapshot = snapshot;
        _hasSnapshot = true;
        ContentTransformChanged?.Invoke(content);
        ContentTransformChangedDetailed?.Invoke(BuildTransformChangeEvent(content));
    }

    private TransformChangeEvent BuildTransformChangeEvent(Transform content)
    {
        Transform targetRoot = ResolveTargetRoot(content);
        string targetId = ResolveTargetId(targetRoot);
        string contentId = content != null ? content.name : "";
        return new TransformChangeEvent(
            targetId,
            contentId,
            content != null ? content.localPosition : Vector3.zero,
            content != null ? content.localEulerAngles : Vector3.zero,
            content != null ? content.localScale : Vector3.one,
            _mode);
    }

    private static Transform ResolveTargetRoot(Transform content)
    {
        if (content == null)
            return null;

        Transform current = content;
        while (current != null)
        {
            if (string.Equals(current.name, "ContentRoot", StringComparison.Ordinal))
                return current.parent;
            current = current.parent;
        }

        return content.parent;
    }

    private static string ResolveTargetId(Transform targetRoot)
    {
        if (targetRoot == null)
            return "";

        ArImageTarget arImageTarget = targetRoot.GetComponent<ArImageTarget>();
        if (arImageTarget != null && !string.IsNullOrWhiteSpace(arImageTarget.TargetId))
            return arImageTarget.TargetId.Trim();

        ImageTargetPlaceholder placeholder = targetRoot.GetComponentInChildren<ImageTargetPlaceholder>();
        if (placeholder != null && !string.IsNullOrWhiteSpace(placeholder.TargetId))
            return placeholder.TargetId.Trim();

        return targetRoot.name;
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
