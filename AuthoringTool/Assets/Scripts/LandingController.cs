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

            VisualElement screenRoot = root.Q<VisualElement>("LandingRoot") ?? root;
            AppFlowWallpaper.Apply(screenRoot);

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
            AppFlowWallpaper.Apply(root);

            root.AddToClassList("landing-root");
            root.AddToClassList("app-flow-wallpaper");

            var titleLabel = new Label("AR Authoring Tool") { name = "TitleLabel" };
            titleLabel.AddToClassList("landing-title");
            root.Add(titleLabel);

            var subtitle = new Label("Immersive AR Authoring Experience");
            subtitle.AddToClassList("landing-subtitle");
            root.Add(subtitle);

            var start = new Button { name = StartButtonName, tooltip = "Get started" };
            start.AddToClassList("landing-start-btn");
            var icon = new VisualElement();
            icon.AddToClassList("landing-start-btn__icon");
            start.Add(icon);
            root.Add(start);
        }
    }
}
