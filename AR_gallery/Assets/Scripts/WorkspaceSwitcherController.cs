using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ARGallery.AppFlow
{
    /// <summary>
    /// Workspace switcher controller for mock workspace navigation and entry.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class WorkspaceSwitcherController : MonoBehaviour
    {
        private const string LeftArrowButtonName = "LeftArrowButton";
        private const string RightArrowButtonName = "RightArrowButton";
        private const string WorkspaceCardsRowName = "WorkspaceCardsRow";
        private const string ActiveWorkspaceNameLabelName = "ActiveWorkspaceNameLabel";
        private const string NewButtonName = "NewButton";
        private const string EditButtonName = "EditButton";

        private readonly List<WorkspaceSessionContext> mockWorkspaces = new List<WorkspaceSessionContext>();
        private readonly List<VisualElement> cardElements = new List<VisualElement>();
        private int selectedIndex;

        private Button leftArrowButton;
        private Button rightArrowButton;
        private VisualElement workspaceCardsRow;
        private Label activeWorkspaceNameLabel;
        private Button newButton;
        private Button editButton;

        private void OnEnable()
        {
            UIDocument uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null)
            {
                Debug.LogError("WorkspaceSwitcherController: UIDocument is missing.");
                return;
            }

            VisualElement root = uiDocument.rootVisualElement;
            if (root == null)
            {
                Debug.LogError("WorkspaceSwitcherController: rootVisualElement is null.");
                return;
            }

            EnsureSwitcherFallbackUi(root);
            BindUi(root);
            SeedMockWorkspaces();
            RebuildCards();
            RefreshSelectionUi();
        }

        private void OnDisable()
        {
            if (leftArrowButton != null) leftArrowButton.clicked -= OnLeftArrowClicked;
            if (rightArrowButton != null) rightArrowButton.clicked -= OnRightArrowClicked;
            if (newButton != null) newButton.clicked -= OnNewButtonClicked;
            if (editButton != null) editButton.clicked -= OnEditButtonClicked;
        }

        private void BindUi(VisualElement root)
        {
            leftArrowButton = root.Q<Button>(LeftArrowButtonName);
            rightArrowButton = root.Q<Button>(RightArrowButtonName);
            workspaceCardsRow = root.Q<VisualElement>(WorkspaceCardsRowName);
            activeWorkspaceNameLabel = root.Q<Label>(ActiveWorkspaceNameLabelName);
            newButton = root.Q<Button>(NewButtonName);
            editButton = root.Q<Button>(EditButtonName);

            if (leftArrowButton == null || rightArrowButton == null || workspaceCardsRow == null || activeWorkspaceNameLabel == null || newButton == null || editButton == null)
            {
                Debug.LogError("WorkspaceSwitcherController: required UI elements were not found.");
                return;
            }

            leftArrowButton.clicked += OnLeftArrowClicked;
            rightArrowButton.clicked += OnRightArrowClicked;
            newButton.clicked += OnNewButtonClicked;
            editButton.clicked += OnEditButtonClicked;
        }

        private void SeedMockWorkspaces()
        {
            if (mockWorkspaces.Count > 0)
                return;

            mockWorkspaces.Add(new WorkspaceSessionContext
            {
                workspaceId = "workspace-poster-a",
                workspaceName = "Poster A",
                targetId = "workspace-poster-a"
            });
            mockWorkspaces.Add(new WorkspaceSessionContext
            {
                workspaceId = "workspace-poster-b",
                workspaceName = "Poster B",
                targetId = "workspace-poster-b"
            });
            mockWorkspaces.Add(new WorkspaceSessionContext
            {
                workspaceId = "workspace-event-corner",
                workspaceName = "Event Corner",
                targetId = "workspace-event-corner"
            });
        }

        private void RebuildCards()
        {
            if (workspaceCardsRow == null)
                return;

            workspaceCardsRow.Clear();
            cardElements.Clear();

            for (int i = 0; i < mockWorkspaces.Count; i++)
            {
                WorkspaceSessionContext ws = mockWorkspaces[i];
                var card = new VisualElement { name = "WorkspaceCard" + i };
                card.style.width = 240;
                card.style.height = 150;
                card.style.marginLeft = 8;
                card.style.marginRight = 8;
                card.style.borderTopLeftRadius = 12;
                card.style.borderTopRightRadius = 12;
                card.style.borderBottomLeftRadius = 12;
                card.style.borderBottomRightRadius = 12;
                card.style.borderLeftWidth = 1;
                card.style.borderRightWidth = 1;
                card.style.borderTopWidth = 1;
                card.style.borderBottomWidth = 1;
                card.style.borderLeftColor = new Color(1f, 1f, 1f, 0.20f);
                card.style.borderRightColor = new Color(1f, 1f, 1f, 0.20f);
                card.style.borderTopColor = new Color(1f, 1f, 1f, 0.20f);
                card.style.borderBottomColor = new Color(1f, 1f, 1f, 0.20f);
                card.style.backgroundColor = new Color(0.18f, 0.18f, 0.2f, 1f);
                card.style.justifyContent = Justify.Center;
                card.style.alignItems = Align.Center;

                var label = new Label(ws.workspaceName);
                label.style.color = Color.white;
                label.style.fontSize = 18;
                label.style.unityFontStyleAndWeight = FontStyle.Bold;
                card.Add(label);

                int idx = i;
                card.RegisterCallback<ClickEvent>(_ =>
                {
                    selectedIndex = idx;
                    RefreshSelectionUi();
                });

                workspaceCardsRow.Add(card);
                cardElements.Add(card);
            }
        }

        private void RefreshSelectionUi()
        {
            if (mockWorkspaces.Count == 0)
                return;

            selectedIndex = Mathf.Clamp(selectedIndex, 0, mockWorkspaces.Count - 1);
            WorkspaceSessionContext selected = mockWorkspaces[selectedIndex];
            if (activeWorkspaceNameLabel != null)
                activeWorkspaceNameLabel.text = "Selected: " + selected.workspaceName;

            for (int i = 0; i < cardElements.Count; i++)
            {
                bool isSelected = i == selectedIndex;
                VisualElement card = cardElements[i];
                card.style.scale = isSelected ? new Scale(new Vector3(1f, 1f, 1f)) : new Scale(new Vector3(0.92f, 0.92f, 1f));
                card.style.opacity = isSelected ? 1f : 0.55f;
                card.style.borderLeftColor = isSelected ? new Color(1f, 1f, 1f, 0.75f) : new Color(1f, 1f, 1f, 0.2f);
                card.style.borderRightColor = isSelected ? new Color(1f, 1f, 1f, 0.75f) : new Color(1f, 1f, 1f, 0.2f);
                card.style.borderTopColor = isSelected ? new Color(1f, 1f, 1f, 0.75f) : new Color(1f, 1f, 1f, 0.2f);
                card.style.borderBottomColor = isSelected ? new Color(1f, 1f, 1f, 0.75f) : new Color(1f, 1f, 1f, 0.2f);
            }
        }

        private void OnLeftArrowClicked()
        {
            if (mockWorkspaces.Count == 0)
                return;
            selectedIndex = (selectedIndex - 1 + mockWorkspaces.Count) % mockWorkspaces.Count;
            RefreshSelectionUi();
        }

        private void OnRightArrowClicked()
        {
            if (mockWorkspaces.Count == 0)
                return;
            selectedIndex = (selectedIndex + 1) % mockWorkspaces.Count;
            RefreshSelectionUi();
        }

        private void OnNewButtonClicked()
        {
            if (SceneTransitionService.IsTransitioning)
                return;

            WorkspaceSessionContext newWorkspace = AppFlowController.BuildNewWorkspaceSession("New Workspace");
            AppFlowController.SetWorkspaceSession(newWorkspace);
            SceneTransitionService.TransitionToScene(AppFlowController.AuthoringSceneName);
        }

        private void OnEditButtonClicked()
        {
            if (SceneTransitionService.IsTransitioning || mockWorkspaces.Count == 0)
                return;

            WorkspaceSessionContext selected = mockWorkspaces[Mathf.Clamp(selectedIndex, 0, mockWorkspaces.Count - 1)].Clone();
            selected.isNewWorkspace = false;
            AppFlowController.SetWorkspaceSession(selected);
            SceneTransitionService.TransitionToScene(AppFlowController.AuthoringSceneName);
        }

        private static void EnsureSwitcherFallbackUi(VisualElement root)
        {
            if (root.Q<Button>(LeftArrowButtonName) != null)
                return;

            root.Clear();
            root.style.flexGrow = 1f;
            root.style.backgroundColor = new Color(0.06f, 0.06f, 0.08f, 1f);
            root.style.justifyContent = Justify.Center;
            root.style.alignItems = Align.Center;

            var title = new Label("Workspace Switcher");
            title.style.color = Color.white;
            title.style.fontSize = 26;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 20;
            root.Add(title);

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.justifyContent = Justify.Center;
            row.style.width = new Length(100, LengthUnit.Percent);
            row.style.maxWidth = 980;
            row.style.height = 190;
            row.style.marginBottom = 16;
            root.Add(row);

            var left = new Button { name = LeftArrowButtonName, text = "<" };
            left.style.width = 52;
            left.style.height = 52;
            left.style.marginRight = 12;
            row.Add(left);

            var cardsRow = new VisualElement { name = WorkspaceCardsRowName };
            cardsRow.style.flexDirection = FlexDirection.Row;
            cardsRow.style.justifyContent = Justify.Center;
            cardsRow.style.alignItems = Align.Center;
            cardsRow.style.flexGrow = 1;
            row.Add(cardsRow);

            var right = new Button { name = RightArrowButtonName, text = ">" };
            right.style.width = 52;
            right.style.height = 52;
            right.style.marginLeft = 12;
            row.Add(right);

            var selectedLabel = new Label("Selected: -") { name = ActiveWorkspaceNameLabelName };
            selectedLabel.style.color = Color.white;
            selectedLabel.style.fontSize = 16;
            selectedLabel.style.marginBottom = 18;
            root.Add(selectedLabel);

            var actionRow = new VisualElement();
            actionRow.style.flexDirection = FlexDirection.Row;
            actionRow.style.alignItems = Align.Center;
            actionRow.style.justifyContent = Justify.Center;

            var newBtn = new Button { name = NewButtonName, text = "NEW" };
            newBtn.style.width = 160;
            newBtn.style.height = 46;
            newBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            actionRow.Add(newBtn);

            var editBtn = new Button { name = EditButtonName, text = "EDIT" };
            editBtn.style.width = 160;
            editBtn.style.height = 46;
            editBtn.style.marginLeft = 14;
            editBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            actionRow.Add(editBtn);

            root.Add(actionRow);
        }
    }
}
