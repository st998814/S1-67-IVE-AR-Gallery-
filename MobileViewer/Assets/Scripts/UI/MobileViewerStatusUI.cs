using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MobileViewer.UI
{
    /// <summary>
    /// Displays transient status toasts and fallback GUI messages for the mobile viewer.
    /// Handles queueing, animation, and tone-based visual styling for runtime feedback.
    /// </summary>
    public class MobileViewerStatusUI : MonoBehaviour
    {
        private enum AlertTone
        {
            Primary,
            Info,
            Success,
            Warning,
            Danger
        }

        [SerializeField] private TMP_Text statusTmpText;
        [SerializeField] private Text statusText;
        [SerializeField] private Image statusBackground;
        [SerializeField] private RectTransform toastRoot;
        [SerializeField] private CanvasGroup toastCanvasGroup;
        [SerializeField] private bool logToConsole = true;
        [SerializeField] private bool showOnGuiFallback = true;
        [SerializeField] private int fallbackFontSize = 34;
        [SerializeField] private float toastVisibleSeconds = 3f;
        [SerializeField] private float toastAnimationSeconds = 0.25f;
        [SerializeField] private float topPadding = 128f;
        [SerializeField] private float hiddenYOffset = -80f;
        [SerializeField] private Color primaryColor = new(0.05f, 0.33f, 0.85f, 0.9f);
        [SerializeField] private Color infoColor = new(0.05f, 0.45f, 0.75f, 0.9f);
        [SerializeField] private Color successColor = new(0.12f, 0.55f, 0.22f, 0.9f);
        [SerializeField] private Color warningColor = new(0.95f, 0.62f, 0.05f, 0.9f);
        [SerializeField] private Color dangerColor = new(0.75f, 0.18f, 0.2f, 0.9f);

        private string currentMessage = "Initializing...";
        private string lastQueuedMessage;
        private GUIStyle fallbackStyle;
        private readonly Queue<(string Message, AlertTone Tone)> toastQueue = new();
        private Coroutine toastCoroutine;
        private Vector2 shownPosition;
        private Vector2 hiddenPosition;

        /// <summary>
        /// Initialize toast positioning and make sure references are available.
        /// </summary>
        private void Awake()
        {
            EnsureToastReferences();
            ConfigureToastLayout();
            SetToastVisibility(visible: false);
        }

        public void SetStatus(string message)
        {
            var tone = ResolveTone(message);
            EnqueueToast(message, tone);
        }

        public void SetScanning() => EnqueueToast("Scanning...", AlertTone.Info);
        public void SetTargetDetected(string targetName) => EnqueueToast($"Target detected: {targetName}", AlertTone.Primary);
        public void SetLoadingContent() => EnqueueToast("Loading content...", AlertTone.Warning);
        public void SetContentLoaded() => EnqueueToast("Content loaded", AlertTone.Success);

        public void ShowContentRenderFailed(string message) => EnqueueToast(message, AlertTone.Danger);

        private void EnqueueToast(string message, AlertTone tone)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            if (message == lastQueuedMessage)
            {
                return;
            }

            lastQueuedMessage = message;
            toastQueue.Enqueue((message, tone));

            if (toastCoroutine == null)
            {
                toastCoroutine = StartCoroutine(ProcessToastQueue());
            }
        }

        private IEnumerator ProcessToastQueue()
        {
            while (toastQueue.Count > 0)
            {
                var (message, tone) = toastQueue.Dequeue();
                ApplyStatus(message, tone);

                if (toastRoot != null && toastCanvasGroup != null)
                {
                    yield return AnimateToast(show: true);
                    yield return new WaitForSeconds(toastVisibleSeconds);
                    yield return AnimateToast(show: false);
                }
                else
                {
                    yield return new WaitForSeconds(toastVisibleSeconds);
                }
            }

            toastCoroutine = null;
        }

        private void ApplyStatus(string message, AlertTone tone)
        {
            currentMessage = message;
            if (statusTmpText != null)
            {
                statusTmpText.text = message;
            }

            if (statusText != null)
            {
                statusText.text = message;
            }

            ApplyBackgroundTone(tone);

            if (logToConsole)
            {
                Debug.Log($"[MobileViewerStatusUI] {message}");
            }
        }

        private IEnumerator AnimateToast(bool show)
        {
            var duration = Mathf.Max(0.01f, toastAnimationSeconds);
            var startPos = toastRoot.anchoredPosition;
            var endPos = show ? shownPosition : hiddenPosition;
            var startAlpha = toastCanvasGroup.alpha;
            var endAlpha = show ? 1f : 0f;

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = 1f - Mathf.Pow(1f - t, 3f);
                toastRoot.anchoredPosition = Vector2.Lerp(startPos, endPos, eased);
                toastCanvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, eased);
                yield return null;
            }

            toastRoot.anchoredPosition = endPos;
            toastCanvasGroup.alpha = endAlpha;
        }

        private void EnsureToastReferences()
        {
            if (toastRoot == null)
            {
                if (statusBackground != null)
                {
                    toastRoot = statusBackground.rectTransform;
                }
                else if (statusTmpText != null)
                {
                    toastRoot = statusTmpText.rectTransform.parent as RectTransform;
                }
                else if (statusText != null)
                {
                    toastRoot = statusText.rectTransform.parent as RectTransform;
                }
            }

            if (toastRoot != null && toastCanvasGroup == null)
            {
                toastCanvasGroup = toastRoot.GetComponent<CanvasGroup>();
                if (toastCanvasGroup == null)
                {
                    toastCanvasGroup = toastRoot.gameObject.AddComponent<CanvasGroup>();
                }
            }
        }

        private void ConfigureToastLayout()
        {
            if (toastRoot == null)
            {
                return;
            }

            toastRoot.anchorMin = new Vector2(0.5f, 1f);
            toastRoot.anchorMax = new Vector2(0.5f, 1f);
            toastRoot.pivot = new Vector2(0.5f, 1f);

            shownPosition = new Vector2(0f, -topPadding);
            hiddenPosition = new Vector2(0f, -topPadding + hiddenYOffset);
        }

        private void SetToastVisibility(bool visible)
        {
            if (toastRoot == null || toastCanvasGroup == null)
            {
                return;
            }

            toastRoot.anchoredPosition = visible ? shownPosition : hiddenPosition;
            toastCanvasGroup.alpha = visible ? 1f : 0f;
        }

        private void OnGUI()
        {
            if (!showOnGuiFallback || string.IsNullOrWhiteSpace(currentMessage))
            {
                return;
            }

            if (statusTmpText != null || statusText != null)
            {
                return;
            }

            if (fallbackStyle == null)
            {
                fallbackStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = fallbackFontSize,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.white },
                    alignment = TextAnchor.UpperCenter
                };
            }

            GUI.Label(new Rect(20f, 60f, Screen.width - 40f, 120f), currentMessage, fallbackStyle);
        }

        private AlertTone ResolveTone(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return AlertTone.Info;
            }

            var lowered = message.ToLowerInvariant();
            if (lowered.Contains("error") || lowered.Contains("failed"))
            {
                return AlertTone.Danger;
            }

            if (lowered.Contains("loaded"))
            {
                return AlertTone.Success;
            }

            if (lowered.Contains("loading"))
            {
                return AlertTone.Warning;
            }

            if (lowered.Contains("detected"))
            {
                return AlertTone.Primary;
            }

            return AlertTone.Info;
        }

        private void ApplyBackgroundTone(AlertTone tone)
        {
            if (statusBackground == null)
            {
                return;
            }

            statusBackground.color = tone switch
            {
                AlertTone.Primary => primaryColor,
                AlertTone.Info => infoColor,
                AlertTone.Success => successColor,
                AlertTone.Warning => warningColor,
                AlertTone.Danger => dangerColor,
                _ => infoColor
            };
        }
    }
}
