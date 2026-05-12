using TMPro;
using UnityEngine;
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

        private GameObject previewObject;
        private Renderer previewRenderer;

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

            RenderMockObject(contentData, targetTransform);
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
                previewObject.transform.localPosition = new Vector3(0f, 0.05f, 0f);
                previewObject.transform.localRotation = Quaternion.identity;
                previewObject.transform.localScale = Vector3.one * previewScale;
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
