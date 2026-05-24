using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityEngine.Video;
using MobileViewer.UI;

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

        [Header("Video Content")]
        [SerializeField] private bool showVideoContent = true;
        [SerializeField] private float videoPrepareTimeoutSeconds = 12f;

        [Header("Model Content")]
        [SerializeField] private bool showModelContent = true;

        [Header("Failure Feedback")]
        [SerializeField] private MobileViewerStatusUI statusUI;
        [SerializeField] private Color failureMediaTint = new(0.95f, 0.45f, 0.1f, 1f);
        [SerializeField] private Color failureUnsupportedTint = new(0.55f, 0.55f, 0.55f, 1f);

        private GameObject previewObject;
        private Renderer previewRenderer;
        private GameObject imageObject;
        private Renderer imageRenderer;
        private GameObject imageBackObject;
        private Renderer imageBackRenderer;
        private Coroutine imageLoadCoroutine;
        private string activeImageUrl;

        private GameObject videoObject;
        private Renderer videoRenderer;
        private VideoPlayer videoPlayer;
        private AudioSource videoAudioSource;
        private Coroutine videoPrepareCoroutine;
        private string activeVideoUrl;

        private GameObject modelRoot;
        private Transform modelAttachTransform;
        private string activeModelUrl;
        private int modelLoadGeneration;

        private ContentData currentContentData;
        private Transform currentTargetTransform;

        private void Awake()
        {
            if (statusUI == null)
            {
                statusUI = GetComponent<MobileViewerStatusUI>();
            }
        }

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

            StopVideoPlayback();
            StopModelLoad();
        }

        public void Render(ContentData contentData, Transform targetTransform)
        {
            if (contentData == null)
            {
                Debug.LogWarning("ContentRenderer.Render called with null ContentData.");
                return;
            }

            currentContentData = contentData;
            currentTargetTransform = targetTransform;

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

            if (normalizedType == "video")
            {
                RenderVideoObject(contentData, targetTransform);
                return;
            }

            if (normalizedType == "model")
            {
                RenderModelObject(contentData, targetTransform);
                return;
            }

            StopModelLoad();
            StopVideoPlayback();

            if (imageObject != null)
            {
                imageObject.SetActive(false);
            }

            if (videoObject != null)
            {
                videoObject.SetActive(false);
            }

            if (IsLegacyMockContentType(normalizedType))
            {
                RenderMockObject(contentData, targetTransform);
                return;
            }

            RenderFailureFallback(
                contentData,
                targetTransform,
                ContentRenderFailureReason.UnsupportedContentType,
                $"Unsupported contentType '{contentData.contentType}'.");
        }

        private void RenderImageObject(ContentData contentData, Transform targetTransform)
        {
            StopModelLoad();
            StopVideoPlayback();

            if (!showImageContent)
            {
                RenderFailureFallback(
                    contentData,
                    targetTransform,
                    ContentRenderFailureReason.UnsupportedContentType,
                    "Image rendering is disabled in ContentRenderer.");
                return;
            }

            if (!TryResolveHttpMediaUrl(contentData.mediaUrl, out _, out var urlReason, out var urlDetail))
            {
                RenderFailureFallback(contentData, targetTransform, urlReason, urlDetail);
                return;
            }

            EnsureImageObject();
            if (imageObject == null)
            {
                RenderFailureFallback(
                    contentData,
                    targetTransform,
                    ContentRenderFailureReason.MediaLoadFailed,
                    "Failed to create image plane.");
                return;
            }

            if (previewObject != null)
            {
                previewObject.SetActive(false);
            }

            if (!TryApplySurfaceTransform(imageObject.transform, contentData, targetTransform))
            {
                RenderFailureFallback(
                    contentData,
                    targetTransform,
                    ContentRenderFailureReason.MediaLoadFailed,
                    "No target transform or camera for image placement.");
                return;
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

        private void RenderVideoObject(ContentData contentData, Transform targetTransform)
        {
            StopModelLoad();

            if (imageObject != null)
            {
                imageObject.SetActive(false);
            }

            if (imageBackObject != null)
            {
                imageBackObject.SetActive(false);
            }

            if (!showVideoContent)
            {
                RenderFailureFallback(
                    contentData,
                    targetTransform,
                    ContentRenderFailureReason.UnsupportedContentType,
                    "Video rendering is disabled in ContentRenderer.");
                return;
            }

            if (!TryResolveHttpMediaUrl(contentData.mediaUrl, out var playbackUrl, out var urlReason, out var urlDetail))
            {
                StopVideoPlayback();
                RenderFailureFallback(contentData, targetTransform, urlReason, urlDetail);
                return;
            }

            EnsureVideoObject();
            if (videoObject == null || videoPlayer == null || videoRenderer == null)
            {
                RenderFailureFallback(
                    contentData,
                    targetTransform,
                    ContentRenderFailureReason.MediaPlaybackFailed,
                    "Failed to create video surface.");
                return;
            }

            if (previewObject != null)
            {
                previewObject.SetActive(false);
            }

            if (!TryApplySurfaceTransform(videoObject.transform, contentData, targetTransform))
            {
                StopVideoPlayback();
                RenderFailureFallback(
                    contentData,
                    targetTransform,
                    ContentRenderFailureReason.MediaPlaybackFailed,
                    "No target transform or camera for video placement.");
                return;
            }

            videoObject.SetActive(true);
            videoPlayer.targetTexture = null;
            videoPlayer.renderMode = VideoRenderMode.MaterialOverride;
            videoPlayer.targetMaterialRenderer = videoRenderer;
            videoPlayer.targetMaterialProperty = "_MainTex";
            videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
            videoPlayer.SetTargetAudioSource(0, videoAudioSource);
            if (videoAudioSource != null)
            {
                videoAudioSource.mute = true;
            }

            if (!string.Equals(activeVideoUrl, playbackUrl, StringComparison.Ordinal))
            {
                if (videoPrepareCoroutine != null)
                {
                    StopCoroutine(videoPrepareCoroutine);
                    videoPrepareCoroutine = null;
                }

                videoPlayer.errorReceived -= OnVideoPlayerError;
                videoPlayer.errorReceived += OnVideoPlayerError;
                videoPlayer.Stop();
                videoPlayer.source = VideoSource.Url;
                videoPlayer.url = playbackUrl;
                videoPlayer.isLooping = true;
                videoPlayer.playOnAwake = true;
                activeVideoUrl = playbackUrl;
                videoPrepareCoroutine = StartCoroutine(PrepareAndPlayVideoRoutine(playbackUrl, contentData, targetTransform));
            }
            else if (!videoPlayer.isPlaying && videoPlayer.isPrepared)
            {
                videoPlayer.Play();
            }
        }

        private void OnVideoPlayerError(VideoPlayer source, string message)
        {
            StopVideoPlayback();
            RenderFailureFallback(
                currentContentData,
                currentTargetTransform,
                ContentRenderFailureReason.MediaPlaybackFailed,
                string.IsNullOrWhiteSpace(message) ? "VideoPlayer reported an error." : message);
        }

        private IEnumerator PrepareAndPlayVideoRoutine(string playbackUrl, ContentData contentData, Transform targetTransform)
        {
            if (videoPlayer == null)
            {
                videoPrepareCoroutine = null;
                yield break;
            }

            videoPlayer.Prepare();
            var timeout = Mathf.Max(1f, videoPrepareTimeoutSeconds);
            var elapsed = 0f;
            while (!videoPlayer.isPrepared && elapsed < timeout)
            {
                if (!string.Equals(activeVideoUrl, playbackUrl, StringComparison.Ordinal))
                {
                    videoPrepareCoroutine = null;
                    yield break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (!videoPlayer.isPrepared)
            {
                StopVideoPlayback();
                RenderFailureFallback(
                    contentData,
                    targetTransform,
                    ContentRenderFailureReason.MediaPlaybackFailed,
                    $"Video prepare timed out ({videoPrepareTimeoutSeconds:0}s).");
                videoPrepareCoroutine = null;
                yield break;
            }

            if (!videoPlayer.isPlaying)
            {
                videoPlayer.Play();
            }

            videoPrepareCoroutine = null;
        }

        private bool TryApplySurfaceTransform(Transform surfaceTransform, ContentData contentData, Transform targetTransform)
        {
            if (surfaceTransform == null)
            {
                return false;
            }

            if (targetTransform != null)
            {
                surfaceTransform.SetParent(targetTransform, false);
                surfaceTransform.localPosition = ResolveImageLocalPosition(contentData);
                surfaceTransform.localRotation = ResolveRuntimeLocalRotation(contentData);
                surfaceTransform.localScale = ResolveImageLocalScale(contentData);

                if (imageForwardOffset > 0f)
                {
                    surfaceTransform.localPosition += Vector3.forward * imageForwardOffset;
                }

                return true;
            }

            if (previewCamera == null)
            {
                previewCamera = Camera.main;
            }

            if (previewCamera == null)
            {
                return false;
            }

            surfaceTransform.SetParent(null);
            var forward = previewCamera.transform.forward;
            surfaceTransform.position = previewCamera.transform.position + forward * previewDistance;
            surfaceTransform.rotation = Quaternion.LookRotation(-forward, Vector3.up);
            surfaceTransform.localScale = ResolveImageLocalScale(contentData);
            return true;
        }

        private static bool TryResolveHttpMediaUrl(
            string mediaUrl,
            out string resolvedUrl,
            out ContentRenderFailureReason failureReason,
            out string failureDetail)
        {
            resolvedUrl = null;
            failureReason = ContentRenderFailureReason.None;
            failureDetail = null;

            if (string.IsNullOrWhiteSpace(mediaUrl))
            {
                failureReason = ContentRenderFailureReason.MissingMediaUrl;
                failureDetail = "mediaUrl is missing.";
                return false;
            }

            var trimmed = mediaUrl.Trim();
            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                failureReason = ContentRenderFailureReason.InvalidMediaUrl;
                failureDetail = "mediaUrl must be an absolute http(s) URL.";
                return false;
            }

            var host = uri.Host.ToLowerInvariant();
            if (host.Contains("youtube.com") || host == "youtu.be")
            {
                failureReason = ContentRenderFailureReason.UnsupportedStreamingUrl;
                failureDetail = "YouTube streaming URLs are not supported in mobile runtime.";
                return false;
            }

            resolvedUrl = trimmed;
            return true;
        }

        private void RenderFailureFallback(
            ContentData contentData,
            Transform targetTransform,
            ContentRenderFailureReason reason,
            string detail)
        {
            var targetName = contentData?.targetName ?? "unknown";
            var contentType = contentData?.contentType ?? string.Empty;
            var url = contentData?.mediaUrl ?? string.Empty;
            Debug.LogWarning(
                $"[ContentRenderer] {reason}: {detail} (target={targetName}, type={contentType}, url={url})");

            statusUI?.ShowContentRenderFailed(ContentRenderFailureMessages.ToastFor(reason));
            AppendFailureSuffixToPanel(contentData, reason);

            if (imageObject != null)
            {
                imageObject.SetActive(false);
            }

            if (imageBackObject != null)
            {
                imageBackObject.SetActive(false);
            }

            StopVideoPlayback();
            StopModelLoad();
            RenderMockObject(contentData, targetTransform, ResolveFailureTint(reason));
        }

        private void AppendFailureSuffixToPanel(ContentData contentData, ContentRenderFailureReason reason)
        {
            if (!showContentPanel || contentData == null)
            {
                return;
            }

            var suffix = ContentRenderFailureMessages.PanelSuffixFor(reason);
            var baseDescription = contentData.description ?? string.Empty;
            if (baseDescription.Contains(suffix, StringComparison.Ordinal))
            {
                return;
            }

            var combined = string.IsNullOrWhiteSpace(baseDescription) ? suffix : $"{baseDescription} {suffix}";
            if (descriptionTmpText != null)
            {
                descriptionTmpText.text = combined;
            }

            if (descriptionText != null)
            {
                descriptionText.text = combined;
            }
        }

        private Color ResolveFailureTint(ContentRenderFailureReason reason)
        {
            return reason == ContentRenderFailureReason.UnsupportedContentType
                ? failureUnsupportedTint
                : failureMediaTint;
        }

        private static bool IsLegacyMockContentType(string normalizedType)
        {
            if (string.IsNullOrWhiteSpace(normalizedType))
            {
                return false;
            }

            return normalizedType.Contains("cube")
                || normalizedType.Contains("sphere")
                || normalizedType.Contains("capsule");
        }

        private void RenderModelObject(ContentData contentData, Transform targetTransform)
        {
            StopVideoPlayback();

            if (imageObject != null)
            {
                imageObject.SetActive(false);
            }

            if (imageBackObject != null)
            {
                imageBackObject.SetActive(false);
            }

            if (!showModelContent)
            {
                RenderFailureFallback(
                    contentData,
                    targetTransform,
                    ContentRenderFailureReason.UnsupportedContentType,
                    "Model rendering is disabled in ContentRenderer.");
                return;
            }

            if (!TryResolveHttpMediaUrl(contentData.mediaUrl, out var modelUrl, out var urlReason, out var urlDetail))
            {
                StopModelLoad();
                RenderFailureFallback(contentData, targetTransform, urlReason, urlDetail);
                return;
            }

            if (!modelUrl.EndsWith(".glb", StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning(
                    $"ContentRenderer: Model mediaUrl does not end with .glb ('{modelUrl}'); attempting load anyway.");
            }

            EnsureModelRoot();
            if (modelRoot == null || modelAttachTransform == null)
            {
                RenderFailureFallback(
                    contentData,
                    targetTransform,
                    ContentRenderFailureReason.ModelImportFailed,
                    "Failed to create model root.");
                return;
            }

            if (previewObject != null)
            {
                previewObject.SetActive(false);
            }

            if (!TryApplyVolumetricTransform(modelRoot.transform, contentData, targetTransform))
            {
                StopModelLoad();
                RenderFailureFallback(
                    contentData,
                    targetTransform,
                    ContentRenderFailureReason.ModelImportFailed,
                    "No target transform or camera for model placement.");
                return;
            }

            modelRoot.SetActive(true);

            if (string.Equals(activeModelUrl, modelUrl, StringComparison.Ordinal)
                && modelAttachTransform.childCount > 0)
            {
                return;
            }

            ClearModelChildren();
            int generation = ++modelLoadGeneration;
            activeModelUrl = modelUrl;

            MobileGlbLoadService.BeginLoadGlb(
                this,
                modelUrl,
                modelAttachTransform,
                outcome => OnModelLoadCompleted(outcome, generation, contentData, targetTransform));
        }

        private void OnModelLoadCompleted(
            MobileGlbLoadService.LoadOutcome outcome,
            int generation,
            ContentData contentData,
            Transform targetTransform)
        {
            if (generation != modelLoadGeneration)
            {
                return;
            }

            if (!outcome.success)
            {
                StopModelLoad();
                RenderFailureFallback(
                    contentData,
                    targetTransform,
                    ContentRenderFailureReason.ModelImportFailed,
                    outcome.message);
                return;
            }

            if (previewObject != null)
            {
                previewObject.SetActive(false);
            }

            if (modelRoot != null)
            {
                modelRoot.SetActive(true);
            }
        }

        private void EnsureModelRoot()
        {
            if (modelRoot != null)
            {
                return;
            }

            modelRoot = new GameObject("RuntimeModelRoot");
            var attachObject = new GameObject("ModelAttach");
            attachObject.transform.SetParent(modelRoot.transform, false);
            modelAttachTransform = attachObject.transform;
        }

        private void ClearModelChildren()
        {
            if (modelAttachTransform == null)
            {
                return;
            }

            for (var i = modelAttachTransform.childCount - 1; i >= 0; i--)
            {
                Destroy(modelAttachTransform.GetChild(i).gameObject);
            }
        }

        private void StopModelLoad()
        {
            modelLoadGeneration++;
            ClearModelChildren();

            if (modelRoot != null)
            {
                modelRoot.SetActive(false);
            }

            activeModelUrl = null;
        }

        private bool TryApplyVolumetricTransform(Transform contentTransform, ContentData contentData, Transform targetTransform)
        {
            if (contentTransform == null)
            {
                return false;
            }

            if (targetTransform != null)
            {
                contentTransform.SetParent(targetTransform, false);
                contentTransform.localPosition = contentData.localPosition;
                contentTransform.localRotation = ResolveRuntimeLocalRotation(contentData);
                var resolvedScale = contentData.localScale;
                if (resolvedScale == Vector3.zero)
                {
                    resolvedScale = Vector3.one * previewScale;
                }

                contentTransform.localScale = resolvedScale;
                return true;
            }

            if (previewCamera == null)
            {
                previewCamera = Camera.main;
            }

            if (previewCamera == null)
            {
                return false;
            }

            contentTransform.SetParent(null);
            var forward = previewCamera.transform.forward;
            var position = previewCamera.transform.position + forward * previewDistance;
            position.y += previewVerticalOffset;
            contentTransform.position = position;
            contentTransform.rotation = Quaternion.LookRotation(-forward, Vector3.up);
            contentTransform.localScale = Vector3.one * previewScale;
            return true;
        }

        private void StopVideoPlayback()
        {
            if (videoPrepareCoroutine != null)
            {
                StopCoroutine(videoPrepareCoroutine);
                videoPrepareCoroutine = null;
            }

            if (videoPlayer != null)
            {
                videoPlayer.errorReceived -= OnVideoPlayerError;
                videoPlayer.Stop();
                videoPlayer.url = string.Empty;
            }

            if (videoObject != null)
            {
                videoObject.SetActive(false);
            }

            activeVideoUrl = null;
        }

        private void EnsureVideoObject()
        {
            if (videoObject != null)
            {
                return;
            }

            videoObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            videoObject.name = "RuntimeVideoPlane";

            var collider = videoObject.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            videoRenderer = videoObject.GetComponent<Renderer>();
            if (videoRenderer != null)
            {
                var shader = Shader.Find("Unlit/Texture") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard");
                if (shader != null)
                {
                    videoRenderer.material = new Material(shader);
                }
            }

            videoPlayer = videoObject.AddComponent<VideoPlayer>();
            videoAudioSource = videoObject.AddComponent<AudioSource>();
            videoAudioSource.playOnAwake = false;
            videoAudioSource.mute = true;
        }

        private void RenderMockObject(ContentData contentData, Transform targetTransform, Color? tintOverride = null)
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
                previewRenderer.material.color = tintOverride ?? contentData.mockColor;
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
                if (string.Equals(activeImageUrl, imageUrl, StringComparison.Ordinal))
                {
                    RenderFailureFallback(
                        currentContentData,
                        currentTargetTransform,
                        ContentRenderFailureReason.NetworkError,
                        string.IsNullOrWhiteSpace(request.error) ? "Image download failed." : request.error);
                }

                imageLoadCoroutine = null;
                yield break;
            }

            var texture = DownloadHandlerTexture.GetContent(request);
            if (texture == null || imageRenderer == null)
            {
                if (string.Equals(activeImageUrl, imageUrl, StringComparison.Ordinal))
                {
                    RenderFailureFallback(
                        currentContentData,
                        currentTargetTransform,
                        ContentRenderFailureReason.MediaLoadFailed,
                        "Image texture is empty or renderer is missing.");
                }

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
