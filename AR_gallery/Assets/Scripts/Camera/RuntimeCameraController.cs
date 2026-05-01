using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using RTG;
using UnityEngine.UIElements;

namespace ARGallery.CameraControl
{
    public readonly struct RuntimeCameraPose
    {
        public readonly Vector3 position;
        public readonly Quaternion rotation;

        public RuntimeCameraPose(Vector3 position, Quaternion rotation)
        {
            this.position = position;
            this.rotation = rotation;
        }
    }

    public sealed class RuntimeCameraController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float boostMultiplier = 2.25f;

        [Header("Mouse Look (Right Mouse Button)")]
        [SerializeField] private float lookSensitivity = 0.12f;
        [SerializeField] private bool invertY;
        [SerializeField] private float minPitch = -85f;
        [SerializeField] private float maxPitch = 85f;

        [Header("Scroll Zoom")]
        [SerializeField] private float zoomSpeed = 4f;
        [SerializeField] private float minHeight = 0.25f;
        [SerializeField] private float maxHeight = 25f;

        [Header("Input Gating")]
        [Tooltip("If empty, found at runtime via FindFirstObjectByType.")]
        [SerializeField] private AuthoringUIController authoringUI;
        [SerializeField] private float interactionRayDistance = 1000f;
        [SerializeField] private bool clampHeightOnPoseApply = true;

        private float _yaw;
        private float _pitch;
        private bool _isLooking;
        private RuntimeCameraPose _startupPose;
        private RuntimeCameraPose _lastAppliedPose;
        private bool _hasLastAppliedPose;

        private void Awake()
        {
            _startupPose = new RuntimeCameraPose(transform.position, transform.rotation);
            Vector3 euler = transform.rotation.eulerAngles;
            _yaw = euler.y;
            _pitch = NormalizePitch(euler.x);
        }

        private void Start()
        {
            if (authoringUI == null)
                authoringUI = FindFirstObjectByType<AuthoringUIController>();
        }

        private void OnDisable()
        {
            if (_isLooking)
                EndLook();
            DraggableObject.ReleaseCameraInteraction();
        }

        private void Update()
        {
#if !ENABLE_INPUT_SYSTEM
            return;
#else
            if (Keyboard.current == null || Mouse.current == null)
                return;

            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;

            Vector2 mousePos = mouse.position.ReadValue();
            if (IsBlockedByUi(mousePos) || IsBlockedBySceneInteraction(mousePos))
            {
                if (_isLooking)
                    EndLook();
                DraggableObject.ReleaseCameraInteraction();
                return;
            }

            bool wantsLook = mouse.rightButton.isPressed;
            if (wantsLook && !_isLooking)
            {
                if (!DraggableObject.TryAcquireCameraInteraction())
                    return;
                BeginLook();
            }
            else if (!wantsLook && _isLooking)
                EndLook();

            if (_isLooking)
                ApplyMouseLook(mouse);

            ApplyKeyboardMove(keyboard);
            ApplyScrollZoom(mouse);
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private void ApplyKeyboardMove(Keyboard keyboard)
        {
            Vector2 input = Vector2.zero;

            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;

            if (input.sqrMagnitude < 0.0001f)
                return;

            float boost = (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed) ? boostMultiplier : 1f;
            float speed = moveSpeed * boost * Time.deltaTime;

            Vector3 forward = transform.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;

            Vector3 right = transform.right;
            right.y = 0f;
            right = right.sqrMagnitude > 0.0001f ? right.normalized : Vector3.right;

            Vector3 delta = (right * input.x + forward * input.y) * speed;
            transform.position += delta;

            ClampHeight();
        }

        private void ApplyMouseLook(Mouse mouse)
        {
            Vector2 delta = mouse.delta.ReadValue();

            _yaw += delta.x * lookSensitivity;
            float ySign = invertY ? 1f : -1f;
            _pitch += delta.y * lookSensitivity * ySign;
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);

            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        private void ApplyScrollZoom(Mouse mouse)
        {
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) < 0.01f)
                return;

            float step = (scroll / 120f) * zoomSpeed;
            transform.position += transform.forward * step;

            ClampHeight();
        }

        private bool IsBlockedByUi(Vector2 mouseScreenPos)
        {
            return authoringUI != null && authoringUI.IsPointerOverAuthoringUi(mouseScreenPos);
        }

        private static bool IsUiToolkitTextOrNumericFieldFocused()
        {
            UIDocument[] docs = Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
            foreach (UIDocument doc in docs)
            {
                if (doc == null || !doc.enabled || doc.rootVisualElement == null)
                    continue;
                IPanel panel = doc.rootVisualElement.panel;
                FocusController fc = panel?.focusController;
                if (fc?.focusedElement is not VisualElement focused)
                    continue;

                for (VisualElement p = focused; p != null; p = p.parent)
                {
                    if (p is TextField || p is FloatField || p is IntegerField || p is LongField || p is DoubleField)
                        return true;
                }
            }

            return false;
        }

        private bool IsBlockedBySceneInteraction(Vector2 mouseScreenPos)
        {
            if (TargetMovementController.IsTargetDragActive)
                return true;

            if (DraggableObject.IsDraggingObjectInteractionActive)
                return true;

            if (RTGizmosEngine.Get != null)
            {
                if (RTGizmosEngine.Get.DraggedGizmo != null)
                    return true;
                if (RTGizmosEngine.Get.HoveredGizmo != null)
                    return true;
            }

            // If user is holding left button over a draggable object, don't move/zoom/rotate camera.
            if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            {
                Camera cam = UnityEngine.Camera.main;
                if (cam == null)
                    return false;

                Ray ray = cam.ScreenPointToRay(mouseScreenPos);
                if (Physics.Raycast(ray, out RaycastHit hit, interactionRayDistance, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider != null && hit.collider.GetComponentInParent<DraggableObject>() != null)
                        return true;
                }
            }

            return false;
        }
#endif

        private void BeginLook()
        {
            _isLooking = true;
            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
            UnityEngine.Cursor.visible = false;
        }

        private void EndLook()
        {
            _isLooking = false;
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
            DraggableObject.ReleaseCameraInteraction();
        }

        private void ClampHeight()
        {
            Vector3 p = transform.position;
            p.y = Mathf.Clamp(p.y, minHeight, maxHeight);
            transform.position = p;
        }

        private static float NormalizePitch(float pitchEuler)
        {
            float p = pitchEuler;
            while (p > 180f) p -= 360f;
            while (p < -180f) p += 360f;
            return p;
        }

        /// <summary>
        /// Applies an external camera pose and synchronizes runtime yaw/pitch
        /// so the next right-mouse look continues smoothly.
        /// </summary>
        public void ApplyPose(RuntimeCameraPose pose, bool rememberAsResetPose = true)
        {
            if (_isLooking)
                EndLook();

            transform.SetPositionAndRotation(pose.position, pose.rotation);
            if (clampHeightOnPoseApply)
                ClampHeight();

            SyncAnglesFromTransform();
            if (rememberAsResetPose)
            {
                _lastAppliedPose = new RuntimeCameraPose(transform.position, transform.rotation);
                _hasLastAppliedPose = true;
            }
        }

        public void ApplyPose(Vector3 position, Quaternion rotation, bool rememberAsResetPose = true)
        {
            ApplyPose(new RuntimeCameraPose(position, rotation), rememberAsResetPose);
        }

        public bool TryResetToLastAppliedPose()
        {
            if (!_hasLastAppliedPose)
                return false;

            ApplyPose(_lastAppliedPose, rememberAsResetPose: false);
            return true;
        }

        public void ResetToStartupPose()
        {
            ApplyPose(_startupPose, rememberAsResetPose: false);
        }

        private void SyncAnglesFromTransform()
        {
            Vector3 euler = transform.rotation.eulerAngles;
            _yaw = euler.y;
            _pitch = NormalizePitch(euler.x);
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }
    }
}

