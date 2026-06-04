using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ARGallery.AppFlow
{
    /// <summary>
    /// Reusable runtime scene transition service for fade-to-black transitions.
    /// Use <see cref="TransitionToScene(string)"/> from any scene controller.
    /// </summary>
    public class SceneTransitionService : MonoBehaviour
    {
        private const float DefaultFadeOutSeconds = 0.25f;
        private const float DefaultFadeInSeconds = 0.25f;
        private const int OverlaySortOrder = 10_000;

        private static SceneTransitionService instance;

        private CanvasGroup canvasGroup;
        private bool isTransitioning;

        public static bool IsTransitioning => instance != null && instance.isTransitioning;

        public static SceneTransitionService Instance
        {
            get
            {
                if (instance != null)
                    return instance;

                var go = new GameObject(nameof(SceneTransitionService));
                instance = go.AddComponent<SceneTransitionService>();
                return instance;
            }
        }

        /// <summary>
        /// Starts a transition: fade out to black, load scene, fade in from black.
        /// Returns false when rejected (invalid scene name or transition already in progress).
        /// </summary>
        public static bool TransitionToScene(string sceneName, float fadeOutSeconds = DefaultFadeOutSeconds, float fadeInSeconds = DefaultFadeInSeconds)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogWarning("SceneTransitionService: scene name is empty.");
                return false;
            }

            if (Instance.isTransitioning)
                return false;

            Instance.StartCoroutine(Instance.TransitionRoutine(sceneName, Mathf.Max(0.01f, fadeOutSeconds), Mathf.Max(0.01f, fadeInSeconds)));
            return true;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureOverlay();
            SetOverlayAlpha(0f, blockInput: false);
        }

        private IEnumerator TransitionRoutine(string sceneName, float fadeOutSeconds, float fadeInSeconds)
        {
            isTransitioning = true;

            yield return FadeTo(1f, fadeOutSeconds, blockInput: true);

            AsyncOperation loadOp = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (loadOp == null)
            {
                Debug.LogError($"SceneTransitionService: failed to start loading scene '{sceneName}'. Is it in Build Settings?");
                yield return RecoverOverlayAfterFailedLoad(fadeInSeconds);
                yield break;
            }

            while (!loadOp.isDone)
                yield return null;

            // Let the new scene initialize one frame before fade-in.
            yield return null;

            yield return FadeTo(0f, fadeInSeconds, blockInput: true);
            SetOverlayAlpha(0f, blockInput: false);

            isTransitioning = false;
        }

        private IEnumerator RecoverOverlayAfterFailedLoad(float fadeInSeconds)
        {
            yield return FadeTo(0f, fadeInSeconds, blockInput: true);
            SetOverlayAlpha(0f, blockInput: false);
            isTransitioning = false;
        }

        private IEnumerator FadeTo(float targetAlpha, float duration, bool blockInput)
        {
            EnsureOverlay();
            if (canvasGroup == null)
                yield break;

            canvasGroup.blocksRaycasts = blockInput;
            canvasGroup.interactable = blockInput;

            float startAlpha = canvasGroup.alpha;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                yield return null;
            }

            canvasGroup.alpha = targetAlpha;
        }

        private void SetOverlayAlpha(float alpha, bool blockInput)
        {
            EnsureOverlay();
            if (canvasGroup == null)
                return;

            canvasGroup.alpha = Mathf.Clamp01(alpha);
            canvasGroup.blocksRaycasts = blockInput;
            canvasGroup.interactable = blockInput;
        }

        private void EnsureOverlay()
        {
            if (canvasGroup != null)
                return;

            Canvas canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null)
                canvas = gameObject.AddComponent<Canvas>();

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = OverlaySortOrder;
            canvas.pixelPerfect = false;

            if (gameObject.GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            if (gameObject.GetComponent<CanvasScaler>() == null)
            {
                CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }

            canvasGroup = gameObject.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            Transform existing = transform.Find("BlackOverlay");
            GameObject overlayObject;
            if (existing != null)
            {
                overlayObject = existing.gameObject;
            }
            else
            {
                overlayObject = new GameObject("BlackOverlay");
                overlayObject.transform.SetParent(transform, false);
            }

            RectTransform rect = overlayObject.GetComponent<RectTransform>();
            if (rect == null)
                rect = overlayObject.AddComponent<RectTransform>();

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image image = overlayObject.GetComponent<Image>();
            if (image == null)
                image = overlayObject.AddComponent<Image>();

            image.color = Color.black;
            image.raycastTarget = true;
        }
    }
}
