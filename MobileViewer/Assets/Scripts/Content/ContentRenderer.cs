using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace MobileViewer.Content
{
    public class ContentRenderer : MonoBehaviour
    {
        [Header("Panel References")]
        [SerializeField] private bool showContentPanel = false;
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text titleTmpText;
        [SerializeField] private Text titleText;
        [SerializeField] private TMP_Text descriptionTmpText;
        [SerializeField] private Text descriptionText;
        [SerializeField] private TMP_Text displayLabelTmpText;
        [SerializeField] private Text displayLabelText;
        [SerializeField] private Image backgroundImage;

        [Header("Mock 3D Preview")]
        [SerializeField] private bool showMock3DObject = true;
        [SerializeField] private Camera previewCamera;
        [SerializeField] private float previewDistance = 0.6f;
        [SerializeField] private float previewVerticalOffset = -0.05f;
        [SerializeField] private float previewScale = 0.08f;

        [Header("Image Content")]
        [SerializeField] private bool showImageContent = true;
        [Tooltip("When true, localScale from the API is the quad width/height in target local meters (matches AuthoringTool + physical target). When false, legacy: scale × imagePlaneScale × targetPhysicalWidthM.")]
        [SerializeField] private bool treatImageLocalScaleAsMeters = true;
        [SerializeField] private float imagePlaneScale = 0.3f; // fallback when backend localScale is missing
        [SerializeField] private Vector3 imageLocalOffset = new(0f, 0.08f, 0f); // fallback when backend localPosition is missing
        [SerializeField] private float imageForwardOffset = 0.01f;
        [SerializeField] private float authoredPositionScale = 1f;
        [SerializeField] private Vector3 authoredPositionScalePerAxis = Vector3.one;
        [SerializeField] private bool keepImageOnTargetPlane = true;
        [SerializeField] private bool useTargetPhysicalWidthScaling = true;
        [SerializeField] private bool clampImageOffsetNearTarget = true;
        [SerializeField] private Vector3 imageOffsetClampPerAxis = new(0.25f, 0.25f, 0.25f);

        [Header("Posture Rotation Correction")]
        [SerializeField] private bool applyPostureRotationCorrection = true;
        [SerializeField] private Vector3 wallRotationCorrectionEuler = new(90f, 0f, 0f);
        [SerializeField] private Vector3 floorRotationCorrectionEuler = Vector3.zero;
        [SerializeField] private Vector3 ceilingRotationCorrectionEuler = new(-90f, 0f, 0f);

        private GameObject previewObject;
        private Renderer previewRenderer;
        private GameObject imageObject;
        private Renderer imageRenderer;
        private GameObject imageBackObject;
        private Renderer imageBackRenderer;
        private Coroutine imageLoadCoroutine;
        private string activeImageUrl;

        public void Hide()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }

            if (previewObject != null)
            {
                previewObject.SetActive(false);
            }

            if (imageObject != null)
            {
                imageObject.SetActive(false);
            }

            if (imageBackObject != null)
            {
                imageBackObject.SetActive(false);
            }

            if (imageLoadCoroutine != null)
            {
                StopCoroutine(imageLoadCoroutine);
                imageLoadCoroutine = null;
            }
        }

        public void Render(ContentData contentData, Transform targetTransform)
        {
            if (contentData == null)
            {
                Debug.LogWarning("ContentRenderer.Render called with null ContentData.");
                return;
            }

            if (panelRoot != null)
            {
                panelRoot.SetActive(showContentPanel);
            }

            if (showContentPanel && titleTmpText != null)
            {
                titleTmpText.text = contentData.title;
            }

            if (showContentPanel && titleText != null)
            {
                titleText.text = contentData.title;
            }

            if (showContentPanel && descriptionTmpText != null)
            {
                descriptionTmpText.text = contentData.description;
            }

            if (showContentPanel && descriptionText != null)
            {
                descriptionText.text = contentData.description;
            }

            var resolvedLabel = string.IsNullOrWhiteSpace(contentData.displayLabel)
                ? contentData.targetName
                : contentData.displayLabel;

            if (showContentPanel && displayLabelTmpText != null)
            {
                displayLabelTmpText.text = resolvedLabel;
            }

            if (showContentPanel && displayLabelText != null)
            {
                displayLabelText.text = resolvedLabel;
            }

            if (showContentPanel && backgroundImage != null)
            {
                backgroundImage.color = contentData.mockColor;
            }

            var normalizedType = (contentData.contentType ?? string.Empty).Trim().ToLowerInvariant();
            if (normalizedType == "image")
            {
                RenderImageObject(contentData, targetTransform);
                return;
            }

            if (normalizedType == "video" || normalizedType == "model")
            {
                Debug.Log($"ContentRenderer: '{normalizedType}' runtime renderer is not implemented yet; showing mock primitive fallback.");
            }

            if (imageObject != null)
            {
                imageObject.SetActive(false);
            }

            RenderMockObject(contentData, targetTransform);
        }

        private void RenderImageObject(ContentData contentData, Transform targetTransform)
        {
            if (!showImageContent || string.IsNullOrWhiteSpace(contentData.mediaUrl))
            {
                RenderMockObject(contentData, targetTransform);
                return;
            }

            EnsureImageObject();
            if (imageObject == null)
            {
                RenderMockObject(contentData, targetTransform);
                return;
            }

            if (previewObject != null)
            {
                previewObject.SetActive(false);
            }

            if (targetTransform != null)
            {
                imageObject.transform.SetParent(targetTransform, false);
                imageObject.transform.localPosition = ResolveImageLocalPosition(contentData);
                imageObject.transform.localRotation = ResolveRuntimeLocalRotation(contentData);
                imageObject.transform.localScale = ResolveImageLocalScale(contentData);

                if (imageForwardOffset > 0f)
                {
                    imageObject.transform.localPosition += Vector3.forward * imageForwardOffset;
                }
            }
            else
            {
                if (previewCamera == null)
                {
                    previewCamera = Camera.main;
                }

                if (previewCamera == null)
                {
                    RenderMockObject(contentData, targetTransform);
                    return;
                }

                imageObject.transform.SetParent(null);
                var forward = previewCamera.transform.forward;
                imageObject.transform.position = previewCamera.transform.position + forward * previewDistance;
                imageObject.transform.rotation = Quaternion.LookRotation(-forward, Vector3.up);
                imageObject.transform.localScale = ResolveImageLocalScale(contentData);
            }

            imageObject.SetActive(true);
            if (imageBackObject != null)
            {
                imageBackObject.SetActive(true);
            }

            if (!string.Equals(activeImageUrl, contentData.mediaUrl))
            {
                if (imageLoadCoroutine != null)
                {
                    StopCoroutine(imageLoadCoroutine);
                }

                imageLoadCoroutine = StartCoroutine(LoadImageTexture(contentData.mediaUrl));
            }
        }

        private void RenderMockObject(ContentData contentData, Transform targetTransform)
        {
            if (!showMock3DObject)
            {
                return;
            }

            if (targetTransform == null && previewCamera == null)
            {
                previewCamera = Camera.main;
            }

            if (targetTransform == null && previewCamera == null)
            {
                return;
            }

            EnsurePreviewObject(contentData.contentType);
            if (previewObject == null)
            {
                return;
            }

            if (targetTransform != null)
            {
                previewObject.transform.SetParent(targetTransform, false);
                previewObject.transform.localPosition = contentData.localPosition;
                previewObject.transform.localRotation = ResolveRuntimeLocalRotation(contentData);
                var resolvedScale = contentData.localScale;
                if (resolvedScale == Vector3.zero)
                {
                    resolvedScale = Vector3.one * previewScale;
                }
                previewObject.transform.localScale = resolvedScale;
            }
            else
            {
                previewObject.transform.SetParent(null);

                var forward = previewCamera.transform.forward;
                var position = previewCamera.transform.position + forward * previewDistance;
                position.y += previewVerticalOffset;

                previewObject.transform.position = position;
                previewObject.transform.rotation = Quaternion.LookRotation(-forward, Vector3.up);
                previewObject.transform.localScale = Vector3.one * previewScale;
            }

            previewObject.SetActive(true);

            if (previewRenderer != null)
            {
                previewRenderer.material.color = contentData.mockColor;
            }
        }

        private void EnsurePreviewObject(string contentType)
        {
            var primitiveType = ResolvePrimitiveType(contentType);

            if (previewObject != null && previewObject.name == $"MockPreview_{primitiveType}")
            {
                return;
            }

            if (previewObject != null)
            {
                Destroy(previewObject);
            }

            previewObject = GameObject.CreatePrimitive(primitiveType);
            previewObject.name = $"MockPreview_{primitiveType}";

            var collider = previewObject.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            previewRenderer = previewObject.GetComponent<Renderer>();
            if (previewRenderer != null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (shader != null)
                {
                    previewRenderer.material = new Material(shader);
                }
            }
        }

        private void EnsureImageObject()
        {
            if (imageObject != null)
            {
                return;
            }

            imageObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            imageObject.name = "RuntimeImagePlane";

            var collider = imageObject.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            imageRenderer = imageObject.GetComponent<Renderer>();
            if (imageRenderer != null)
            {
                var shader = Shader.Find("Unlit/Texture") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard");
                if (shader != null)
                {
                    imageRenderer.material = new Material(shader);
                }
            }

            // Back-facing quad so the image remains visible regardless of target orientation/camera side.
            imageBackObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            imageBackObject.name = "RuntimeImagePlane_Back";
            imageBackObject.transform.SetParent(imageObject.transform, false);
            imageBackObject.transform.localPosition = Vector3.zero;
            imageBackObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            imageBackObject.transform.localScale = Vector3.one;

            var backCollider = imageBackObject.GetComponent<Collider>();
            if (backCollider != null)
            {
                Destroy(backCollider);
            }

            imageBackRenderer = imageBackObject.GetComponent<Renderer>();
            if (imageBackRenderer != null && imageRenderer != null)
            {
                imageBackRenderer.material = imageRenderer.material;
            }
        }

        private IEnumerator LoadImageTexture(string imageUrl)
        {
            activeImageUrl = imageUrl;
            using var request = UnityWebRequestTexture.GetTexture(imageUrl);
            yield return request.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
            var failed = request.result != UnityWebRequest.Result.Success;
#else
            var failed = request.isNetworkError || request.isHttpError;
#endif
            if (failed)
            {
                Debug.LogWarning($"ContentRenderer: Failed to download image '{imageUrl}'. {request.error}");
                imageLoadCoroutine = null;
                yield break;
            }

            var texture = DownloadHandlerTexture.GetContent(request);
            if (texture == null || imageRenderer == null)
            {
                imageLoadCoroutine = null;
                yield break;
            }

            imageRenderer.material.mainTexture = texture;
            if (imageBackRenderer != null)
            {
                imageBackRenderer.material.mainTexture = texture;
            }
            if (texture.height > 0)
            {
                var aspect = (float)texture.width / texture.height;
                var baseScale = imageObject.transform.localScale;
                var width = baseScale.x * Mathf.Clamp(aspect, 0.5f, 2.0f);
                imageObject.transform.localScale = new Vector3(width, baseScale.y, baseScale.z);
            }

            imageLoadCoroutine = null;
        }

        private Vector3 ResolveImageLocalPosition(ContentData contentData)
        {
            var position = contentData.localPosition;
            if (position == Vector3.zero)
            {
                position = imageLocalOffset;
                return position;
            }

            // Authoring and runtime use different scene scales; normalize authored offsets for mobile runtime.
            var widthScale = useTargetPhysicalWidthScaling && contentData.targetPhysicalWidthM > 0f
                ? contentData.targetPhysicalWidthM
                : 1f;
            position.x *= authoredPositionScale * authoredPositionScalePerAxis.x * widthScale;
            position.y *= authoredPositionScale * authoredPositionScalePerAxis.y * widthScale;
            position.z *= authoredPositionScale * authoredPositionScalePerAxis.z * widthScale;

            if (keepImageOnTargetPlane)
            {
                position.z = imageLocalOffset.z;
            }

            if (clampImageOffsetNearTarget)
            {
                position.x = Mathf.Clamp(position.x, -imageOffsetClampPerAxis.x, imageOffsetClampPerAxis.x);
                position.y = Mathf.Clamp(position.y, -imageOffsetClampPerAxis.y, imageOffsetClampPerAxis.y);
                position.z = Mathf.Clamp(position.z, -imageOffsetClampPerAxis.z, imageOffsetClampPerAxis.z);
            }

            return position;
        }

        private Vector3 ResolveImageLocalScale(ContentData contentData)
        {
            var scale = contentData.localScale;
            var widthScale = useTargetPhysicalWidthScaling && contentData.targetPhysicalWidthM > 0f
                ? contentData.targetPhysicalWidthM
                : 1f;

            if (scale == Vector3.zero)
            {
                // No authored scale: default plane = imagePlaneScale × one physical-width factor (meters).
                var def = imagePlaneScale * widthScale;
                return new Vector3(Mathf.Max(0.01f, def), Mathf.Max(0.01f, def), 1f);
            }

            if (treatImageLocalScaleAsMeters)
            {
                // Contract: localScale.x / localScale.y are quad width and height in target local meters (z unused for quad thickness).
                return new Vector3(
                    Mathf.Max(0.01f, scale.x),
                    Mathf.Max(0.01f, scale.y),
                    Mathf.Max(0.01f, scale.z <= 0f ? 1f : scale.z));
            }

            // Legacy: treat authored scale as a multiplier over a runtime base size.
            return new Vector3(
                Mathf.Max(0.01f, scale.x * imagePlaneScale * widthScale),
                Mathf.Max(0.01f, scale.y * imagePlaneScale * widthScale),
                Mathf.Max(0.01f, scale.z <= 0f ? 1f : scale.z));
        }

        private Quaternion ResolveRuntimeLocalRotation(ContentData contentData)
        {
            var authored = Quaternion.Euler(contentData.localEuler);
            if (!applyPostureRotationCorrection)
            {
                return authored;
            }

            var posture = (contentData.targetPosture ?? string.Empty).Trim().ToLowerInvariant();
            Vector3 correctionEuler;
            switch (posture)
            {
                case "floor":
                    correctionEuler = floorRotationCorrectionEuler;
                    break;
                case "ceiling":
                    correctionEuler = ceilingRotationCorrectionEuler;
                    break;
                default:
                    correctionEuler = wallRotationCorrectionEuler;
                    break;
            }

            var correction = Quaternion.Euler(correctionEuler);
            return correction * authored;
        }

        private static PrimitiveType ResolvePrimitiveType(string contentType)
        {
            if (string.IsNullOrWhiteSpace(contentType))
            {
                return PrimitiveType.Cube;
            }

            var normalized = contentType.ToLowerInvariant();
            if (normalized.Contains("sphere"))
            {
                return PrimitiveType.Sphere;
            }

            if (normalized.Contains("capsule"))
            {
                return PrimitiveType.Capsule;
            }

            return PrimitiveType.Cube;
        }
    }
}
