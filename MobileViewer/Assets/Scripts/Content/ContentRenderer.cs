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
    /// <summary>
    /// Renders content for detected AR targets, including image, video, model, and mock preview fallback.
    /// Handles runtime placement, content loading, rendering failure feedback, and scene object creation.
    /// </summary>
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
        [SerializeField] private bool useTargetPhysicalWidthScaling = true;

        [Header("Authored placement (all content types)")]
        [Tooltip("Fallback in AuthoringTool ContentRoot space when API localPosition is missing. Negative Z = in front of target.")]
        [SerializeField] private Vector3 authoredFallbackLocalPosition = new(0f, 0f, -0.05f);
        [Tooltip("Matches AuthoringTool FrontSideConstraint minimum standoff for in-front content.")]
        [SerializeField] private float minimumFrontStandoffMeters = 0.05f;
        [SerializeField] private bool clampAuthoredOffset;
        [SerializeField] private Vector3 authoredOffsetClampPerAxis = new(2f, 2f, 2f);

        [Header("Posture Rotation Correction")]
        [SerializeField] private bool applyPostureRotationCorrection = true;
        [SerializeField] private Vector3 wallRotationCorrectionEuler = new(90f, 0f, 0f);
        [Tooltip("Matches wall quad correction: authored surface content uses the same plane basis on floor targets.")]
        [SerializeField] private Vector3 floorRotationCorrectionEuler = new(90f, 0f, 0f);
        [SerializeField] private Vector3 ceilingRotationCorrectionEuler = new(-90f, 0f, 0f);

        [Header("Video Content")]
        [SerializeField] private bool showVideoContent = true;
        [SerializeField] private float videoPrepareTimeoutSeconds = 12f;
        [SerializeField] private float videoDimensionTimeoutSeconds = 3f;

        [Header("Model Content")]
        [SerializeField] private bool showModelContent = true;
        [SerializeField] private float modelDefaultLocalScale = 0.05f;
        [Tooltip("Treat legacy model payload scale (1,1,1) as missing so old saved GLBs do not render at raw asset size.")]
        [SerializeField] private bool treatUnitModelScaleAsDefault = true;

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

        /// <summary>
        /// Render the given content data at the tracked target transform.
        /// Chooses the renderer path based on contentType and manages failure fallbacks.
        /// </summary>
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
            ConfigureVideoPlayerRenderTarget();
            videoPlayer.waitForFirstFrame = true;
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
                videoPlayer.prepareCompleted -= OnVideoPrepared;
                videoPlayer.prepareCompleted += OnVideoPrepared;
                videoPlayer.Stop();
                videoPlayer.source = VideoSource.Url;
                videoPlayer.url = playbackUrl;
                videoPlayer.isLooping = true;
                videoPlayer.playOnAwake = false;
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
            Debug.LogError($"[ContentRenderer] VideoPlayer error: {message} url={activeVideoUrl}");
            StopVideoPlayback();
            RenderFailureFallback(
                currentContentData,
                currentTargetTransform,
                ContentRenderFailureReason.MediaPlaybackFailed,
                string.IsNullOrWhiteSpace(message) ? "VideoPlayer reported an error." : message);
        }

        private void OnVideoPrepared(VideoPlayer source)
        {
            if (source != videoPlayer || videoObject == null)
            {
                return;
            }

            if (TryBindVideoPlaybackSurface(source, out var width, out var height))
            {
                Debug.Log($"[ContentRenderer] Video prepared {width}x{height} url={activeVideoUrl}");
                return;
            }

            Debug.LogWarning(
                $"[ContentRenderer] Video prepared but dimensions not ready yet ({source.width}x{source.height}).");
        }

        private IEnumerator PrepareAndPlayVideoRoutine(string playbackUrl, ContentData contentData, Transform targetTransform)
        {
            if (videoPlayer == null)
            {
                videoPrepareCoroutine = null;
                yield break;
            }

            videoPlayer.waitForFirstFrame = true;
            videoPlayer.Prepare();
            Debug.Log($"[ContentRenderer] Preparing video url={playbackUrl}");

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

            var dimensionTimeout = Mathf.Max(0.5f, videoDimensionTimeoutSeconds);
            var dimensionElapsed = 0f;
            while (!TryBindVideoPlaybackSurface(videoPlayer, out _, out _)
                && dimensionElapsed < dimensionTimeout)
            {
                if (!string.Equals(activeVideoUrl, playbackUrl, StringComparison.Ordinal))
                {
                    videoPrepareCoroutine = null;
                    yield break;
                }

                dimensionElapsed += Time.deltaTime;
                yield return null;
            }

            if (!TryBindVideoPlaybackSurface(videoPlayer, out var width, out var height))
            {
                Debug.LogWarning(
                    $"[ContentRenderer] Video dimensions unavailable after prepare; continuing with material override ({videoPlayer.width}x{videoPlayer.height}).");
                ConfigureVideoPlayerRenderTarget();
            }
            else
            {
                Debug.Log($"[ContentRenderer] Video surface bound {width}x{height}");
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
                surfaceTransform.localPosition = ResolveAuthoredLocalPosition(contentData);
                surfaceTransform.localRotation = ResolveRuntimeLocalRotation(contentData);
                surfaceTransform.localScale = ResolveImageLocalScale(contentData);

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
                contentTransform.localPosition = ResolveAuthoredLocalPosition(contentData);
                contentTransform.localRotation = ResolveRuntimeLocalRotation(contentData);
                contentTransform.localScale = ResolveModelLocalScale(contentData);
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
            contentTransform.localScale = Vector3.one * Mathf.Max(0.001f, modelDefaultLocalScale);
            return true;
        }

        private Vector3 ResolveModelLocalScale(ContentData contentData)
        {
            var scale = contentData != null ? contentData.localScale : Vector3.zero;
            if (scale == Vector3.zero || (treatUnitModelScaleAsDefault && IsApproximatelyUnitScale(scale)))
            {
                return Vector3.one * Mathf.Max(0.001f, modelDefaultLocalScale);
            }

            return new Vector3(
                Mathf.Max(0.001f, scale.x),
                Mathf.Max(0.001f, scale.y),
                Mathf.Max(0.001f, scale.z));
        }

        private static bool IsApproximatelyUnitScale(Vector3 scale)
        {
            const float epsilon = 0.0001f;
            return Mathf.Abs(scale.x - 1f) <= epsilon
                && Mathf.Abs(scale.y - 1f) <= epsilon
                && Mathf.Abs(scale.z - 1f) <= epsilon;
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
                videoPlayer.prepareCompleted -= OnVideoPrepared;
                videoPlayer.Stop();
                videoPlayer.url = string.Empty;
                videoPlayer.targetTexture = null;
                videoPlayer.targetMaterialRenderer = null;
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
                videoRenderer.material = CreateVideoSurfaceMaterial();
            }

            videoPlayer = videoObject.AddComponent<VideoPlayer>();
            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = true;
            videoPlayer.skipOnDrop = true;
            videoPlayer.waitForFirstFrame = true;

            videoAudioSource = videoObject.AddComponent<AudioSource>();
            videoAudioSource.playOnAwake = false;
            videoAudioSource.mute = true;

            ConfigureVideoPlayerRenderTarget();
        }

        private static Material CreateVideoSurfaceMaterial()
        {
            var template = Resources.Load<Material>("VideoRuntimeUnlit");
            Material material;
            if (template != null)
            {
                material = new Material(template);
            }
            else
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit")
                    ?? Shader.Find("Unlit/Texture")
                    ?? Shader.Find("Standard");
                if (shader == null)
                {
                    return null;
                }

                material = new Material(shader);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", Color.white);
            }

            EnableDoubleSidedVideoMaterial(material);
            ResetVideoTextureTransform(material);
            return material;
        }

        private static void EnableDoubleSidedVideoMaterial(Material material)
        {
            if (material != null && material.HasProperty("_Cull"))
            {
                material.SetFloat("_Cull", 0f);
            }
        }

        private static void ResetVideoTextureTransform(Material material)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_BaseMap"))
            {
                material.SetTextureScale("_BaseMap", Vector2.one);
                material.SetTextureOffset("_BaseMap", Vector2.zero);
            }

            if (material.HasProperty("_MainTex"))
            {
                material.SetTextureScale("_MainTex", Vector2.one);
                material.SetTextureOffset("_MainTex", Vector2.zero);
            }
        }

        private static string ResolveVideoMaterialPropertyName(Material material)
        {
            if (material != null && material.HasProperty("_BaseMap"))
            {
                return "_BaseMap";
            }

            return "_MainTex";
        }

        private bool TryBindVideoPlaybackSurface(VideoPlayer source, out int width, out int height)
        {
            width = 0;
            height = 0;

            if (source == null || videoObject == null)
            {
                return false;
            }

            width = Mathf.Max(0, (int)source.width);
            height = Mathf.Max(0, (int)source.height);
            if (width <= 0 || height <= 0)
            {
                return false;
            }

            ConfigureVideoPlayerRenderTarget();
            ApplyVideoAspectToQuad(width, height);
            return true;
        }

        private void ConfigureVideoPlayerRenderTarget()
        {
            if (videoPlayer == null || videoRenderer == null)
            {
                return;
            }

            var material = videoRenderer.material;
            if (material == null)
            {
                return;
            }

            EnableDoubleSidedVideoMaterial(material);
            ResetVideoTextureTransform(material);

            videoPlayer.renderMode = VideoRenderMode.MaterialOverride;
            videoPlayer.targetTexture = null;
            videoPlayer.targetMaterialRenderer = videoRenderer;
            videoPlayer.targetMaterialProperty = ResolveVideoMaterialPropertyName(material);
        }

        private void ApplyVideoAspectToQuad(int videoWidth, int videoHeight)
        {
            if (videoObject == null || videoWidth <= 0 || videoHeight <= 0)
            {
                return;
            }

            var aspect = videoWidth / (float)videoHeight;
            var baseScale = videoObject.transform.localScale;
            var width = baseScale.x * Mathf.Clamp(aspect, 0.5f, 2.0f);
            videoObject.transform.localScale = new Vector3(width, baseScale.y, baseScale.z);
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
                previewObject.transform.localPosition = ResolveAuthoredLocalPosition(contentData);
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

        private static void ApplyImageTextureToRenderer(Renderer renderer, Texture texture)
        {
            if (renderer == null || texture == null)
            {
                return;
            }

            var material = renderer.material;
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
                material.SetTextureScale("_BaseMap", Vector2.one);
                material.SetTextureOffset("_BaseMap", Vector2.zero);
            }

            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", texture);
                material.SetTextureScale("_MainTex", Vector2.one);
                material.SetTextureOffset("_MainTex", Vector2.zero);
            }
            else
            {
                material.mainTexture = texture;
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

            ApplyImageTextureToRenderer(imageRenderer, texture);
            ApplyImageTextureToRenderer(imageBackRenderer, texture);
            if (texture.height > 0)
            {
                var aspect = (float)texture.width / texture.height;
                var baseScale = imageObject.transform.localScale;
                var width = baseScale.x * Mathf.Clamp(aspect, 0.5f, 2.0f);
                imageObject.transform.localScale = new Vector3(width, baseScale.y, baseScale.z);
            }

            imageLoadCoroutine = null;
        }

        private static bool IsAuthoredPositionMissing(Vector3 position)
        {
            return position.sqrMagnitude < 1e-8f;
        }

        private static string NormalizeTargetPosture(string posture)
        {
            return string.IsNullOrWhiteSpace(posture) ? "wall" : posture.Trim().ToLowerInvariant();
        }

        /// <summary>
        /// Maps AuthoringTool ContentRoot-local metres into Vuforia target-local space.
        /// Authoring semantics: X = left/right, Y = up/down on target, negative Z = in front of target.
        /// </summary>
        private Vector3 ResolveAuthoredLocalPosition(ContentData contentData)
        {
            var authored = contentData != null ? contentData.localPosition : Vector3.zero;
            if (IsAuthoredPositionMissing(authored))
            {
                authored = authoredFallbackLocalPosition;
            }

            var posture = NormalizeTargetPosture(contentData?.targetPosture);
            var runtime = MapAuthoredToRuntimePosition(posture, authored);
            ApplyMinimumFrontStandoff(posture, authored, ref runtime);

            if (clampAuthoredOffset)
            {
                runtime.x = Mathf.Clamp(runtime.x, -authoredOffsetClampPerAxis.x, authoredOffsetClampPerAxis.x);
                runtime.y = Mathf.Clamp(runtime.y, -authoredOffsetClampPerAxis.y, authoredOffsetClampPerAxis.y);
                runtime.z = Mathf.Clamp(runtime.z, -authoredOffsetClampPerAxis.z, authoredOffsetClampPerAxis.z);
            }

            return runtime;
        }

        private static Vector3 MapAuthoredToRuntimePosition(string posture, Vector3 authored)
        {
            // Wall + 90° X content correction: quad lies in the XZ plane, so depth is local Y and height is local Z.
            if (posture == "wall")
            {
                return new Vector3(authored.x, -authored.z, authored.y);
            }

            // Floor/ceiling: depth remains along Vuforia local +Z.
            return new Vector3(authored.x, authored.y, -authored.z);
        }

        private void ApplyMinimumFrontStandoff(string posture, Vector3 authored, ref Vector3 runtime)
        {
            if (authored.z > 0f || minimumFrontStandoffMeters <= 0f)
            {
                return;
            }

            if (posture == "wall")
            {
                if (runtime.y >= 0f && runtime.y < minimumFrontStandoffMeters)
                {
                    runtime.y = minimumFrontStandoffMeters;
                }

                return;
            }

            if (runtime.z >= 0f && runtime.z < minimumFrontStandoffMeters)
            {
                runtime.z = minimumFrontStandoffMeters;
            }
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
