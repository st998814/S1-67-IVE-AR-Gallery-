using UnityEngine;
using UnityEngine.UIElements;

namespace ARGallery.AppFlow
{
    /// <summary>
    /// Landing scene controller: handles Start button -> Workspace Switcher transition.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class LandingController : MonoBehaviour
    {
        private Button startButton;
        private const string StartButtonName = "StartButton";

        private void OnEnable()
        {
            UIDocument uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null)
            {
                Debug.LogError("LandingController: UIDocument is missing.");
                return;
            }

            VisualElement root = uiDocument.rootVisualElement;
            if (root == null)
            {
                Debug.LogError("LandingController: rootVisualElement is null.");
                return;
            }

            // Fallback: if UXML is not bound/imported correctly, build a minimal landing UI at runtime.
            EnsureLandingFallbackUi(root);

            startButton = root.Q<Button>(StartButtonName);
            if (startButton == null)
            {
                Debug.LogError("LandingController: StartButton was not found in Landing UI.");
                return;
            }

            startButton.clicked += OnStartButtonClicked;
        }

        private void OnDisable()
        {
            if (startButton != null)
                startButton.clicked -= OnStartButtonClicked;
        }

        private void OnStartButtonClicked()
        {
            if (SceneTransitionService.IsTransitioning)
                return;

            AppFlowController.ClearWorkspaceSession();
            SceneTransitionService.TransitionToScene(AppFlowController.WorkspaceSwitcherSceneName);
        }

        private static void EnsureLandingFallbackUi(VisualElement root)
        {
            if (root.Q<Button>(StartButtonName) != null)
                return;

            root.Clear();
            root.style.flexGrow = 1f;
            root.style.justifyContent = Justify.Center;
            root.style.alignItems = Align.Center;
            root.style.backgroundColor = Color.black;

            var titleLabel = new Label("AR Authoring Tool Beta");
            titleLabel.style.color = Color.white;
            titleLabel.style.fontSize = 36;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.marginBottom = 28;
            root.Add(titleLabel);

            var start = new Button { name = StartButtonName, text = "Start" };
            start.style.width = 180;
            start.style.height = 52;
            start.style.color = Color.white;
            start.style.fontSize = 18;
            start.style.unityFontStyleAndWeight = FontStyle.Bold;
            start.style.backgroundColor = new Color(1f, 1f, 1f, 0.12f);
            start.style.borderTopLeftRadius = 10;
            start.style.borderTopRightRadius = 10;
            start.style.borderBottomLeftRadius = 10;
            start.style.borderBottomRightRadius = 10;
            root.Add(start);
        }
    }
}
