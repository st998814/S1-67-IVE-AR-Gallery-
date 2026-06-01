using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ARGallery.Workspace;
using ARGallery.Workspace.Persistence;
using UnityEngine;
using UnityEngine.Networking;
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
        private const string EditButtonName = "EditButton";
        private const string DeleteWorkspaceButtonName = "DeleteWorkspaceButton";

        private readonly List<WorkspaceSessionContext> mockWorkspaces = new List<WorkspaceSessionContext>();
        private readonly List<VisualElement> cardElements = new List<VisualElement>();
        private readonly List<float> cardScaleCurrent = new List<float>();
        private readonly List<float> cardScaleTarget = new List<float>();
        private readonly List<float> cardOpacityCurrent = new List<float>();
        private readonly List<float> cardOpacityTarget = new List<float>();
        private readonly Dictionary<string, Texture2D> thumbnailTextureCache = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
        private int selectedIndex;
        private float stripOffsetCurrent;
        private float stripOffsetTarget;
        private bool hasAnimationState;

        private const float ActiveCardScale = 1f;
        private const float InactiveCardScale = 0.92f;
        private const float ActiveCardOpacity = 1f;
        private const float InactiveCardOpacity = 0.55f;
        private const float CardStepPixels = 256f;
        private const float FocusAnimationSpeed = 12f;

        private Button leftArrowButton;
        private Button rightArrowButton;
        private VisualElement workspaceCardsRow;
        private Label activeWorkspaceNameLabel;
        private Button editButton;
        private Button deleteWorkspaceButton;

        [SerializeField]
        [Tooltip("Backend base URL for DELETE /api/workspaces/{id} before removing local snapshot. Leave empty to only delete on-disk workspace data.")]
        private string backendApiBaseUrl = "http://127.0.0.1:5050";

        private bool workspaceDeleteBusy;

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
            RefreshSelectionUi(forceImmediate: true);
        }

        private void OnDisable()
        {
            if (leftArrowButton != null) leftArrowButton.clicked -= OnLeftArrowClicked;
            if (rightArrowButton != null) rightArrowButton.clicked -= OnRightArrowClicked;
            if (editButton != null) editButton.clicked -= OnEditButtonClicked;
            if (deleteWorkspaceButton != null) deleteWorkspaceButton.clicked -= OnDeleteWorkspaceClicked;
        }

        private void OnDestroy()
        {
            foreach (Texture2D texture in thumbnailTextureCache.Values)
            {
                if (texture != null)
                    Destroy(texture);
            }
            thumbnailTextureCache.Clear();
        }

        private void Update()
        {
            if (!hasAnimationState || cardElements.Count == 0 || workspaceCardsRow == null)
                return;

            float t = 1f - Mathf.Exp(-FocusAnimationSpeed * Time.unscaledDeltaTime);

            stripOffsetCurrent = Mathf.Lerp(stripOffsetCurrent, stripOffsetTarget, t);
            workspaceCardsRow.style.translate = new Translate(
                new Length(stripOffsetCurrent, LengthUnit.Pixel),
                new Length(0f, LengthUnit.Pixel),
                0f);

            for (int i = 0; i < cardElements.Count; i++)
            {
                cardScaleCurrent[i] = Mathf.Lerp(cardScaleCurrent[i], cardScaleTarget[i], t);
                cardOpacityCurrent[i] = Mathf.Lerp(cardOpacityCurrent[i], cardOpacityTarget[i], t);

                bool isSelected = i == selectedIndex;
                VisualElement card = cardElements[i];
                float s = cardScaleCurrent[i];
                card.style.scale = new Scale(new Vector3(s, s, 1f));
                card.style.opacity = cardOpacityCurrent[i];
                card.EnableInClassList("card--active", isSelected);
                card.EnableInClassList("card--inactive", !isSelected);
            }
        }

        private void BindUi(VisualElement root)
        {
            leftArrowButton = root.Q<Button>(LeftArrowButtonName);
            rightArrowButton = root.Q<Button>(RightArrowButtonName);
            workspaceCardsRow = root.Q<VisualElement>(WorkspaceCardsRowName);
            activeWorkspaceNameLabel = root.Q<Label>(ActiveWorkspaceNameLabelName);
            editButton = root.Q<Button>(EditButtonName);
            deleteWorkspaceButton = root.Q<Button>(DeleteWorkspaceButtonName);

            if (leftArrowButton == null || rightArrowButton == null || workspaceCardsRow == null || activeWorkspaceNameLabel == null || editButton == null)
            {
                Debug.LogError("WorkspaceSwitcherController: required UI elements were not found.");
                return;
            }

            workspaceCardsRow.style.justifyContent = Justify.FlexStart;

            leftArrowButton.clicked += OnLeftArrowClicked;
            rightArrowButton.clicked += OnRightArrowClicked;
            editButton.clicked += OnEditButtonClicked;
            if (deleteWorkspaceButton != null)
                deleteWorkspaceButton.clicked += OnDeleteWorkspaceClicked;
        }

        private void SeedMockWorkspaces()
        {
            mockWorkspaces.Clear();

            var byId = new Dictionary<string, WorkspaceSessionContext>(StringComparer.OrdinalIgnoreCase);
            var insertionOrder = new List<string>();
            var diskOnlyPending = new List<(WorkspaceSessionContext session, string updatedAtUtc)>();

            void RegisterSeed(WorkspaceSessionContext ctx)
            {
                string id = ctx.workspaceId.Trim();
                if (byId.ContainsKey(id))
                    return;
                byId[id] = ctx.Clone();
                insertionOrder.Add(id);
            }

            void MergeDiskMetadata(string workspaceId, WorkspaceIndexEntry entry)
            {
                if (entry == null || string.IsNullOrWhiteSpace(workspaceId))
                    return;
                if (!byId.TryGetValue(workspaceId.Trim(), out WorkspaceSessionContext ctx) || ctx == null)
                    return;
                if (!string.IsNullOrWhiteSpace(entry.workspaceName))
                    ctx.workspaceName = entry.workspaceName.Trim();
                if (!string.IsNullOrWhiteSpace(entry.thumbnailKey))
                    ctx.thumbnailKey = entry.thumbnailKey.Trim();
            }

            var providerWorkspaces = Workspace.WorkspaceDataServices.Provider.GetAvailableWorkspaces();
            if (providerWorkspaces != null)
            {
                for (int i = 0; i < providerWorkspaces.Count; i++)
                {
                    Workspace.WorkspaceDraftState ws = providerWorkspaces[i];
                    if (ws == null || string.IsNullOrWhiteSpace(ws.workspaceId) || ws.target == null || string.IsNullOrWhiteSpace(ws.target.targetId))
                        continue;

                    RegisterSeed(new WorkspaceSessionContext
                    {
                        workspaceId = ws.workspaceId.Trim(),
                        workspaceName = string.IsNullOrWhiteSpace(ws.workspaceName) ? ws.workspaceId.Trim() : ws.workspaceName.Trim(),
                        targetId = ws.target.targetId.Trim(),
                        isNewWorkspace = false,
                        setupState = WorkspaceSetupState.Ready
                    });
                }
            }

            var snapshotRepo = new WorkspaceSnapshotRepository();
            IReadOnlyList<WorkspaceIndexEntry> diskIndex = snapshotRepo.LoadAllIndexEntries();
            for (int i = 0; i < diskIndex.Count; i++)
            {
                WorkspaceIndexEntry entry = diskIndex[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.workspaceId))
                    continue;

                string wid = entry.workspaceId.Trim();
                if (byId.ContainsKey(wid))
                {
                    MergeDiskMetadata(wid, entry);
                    continue;
                }

                if (TryBuildSessionFromDiskWorkspace(wid, entry, out WorkspaceSessionContext diskSession))
                    diskOnlyPending.Add((diskSession, entry.updatedAtUtc ?? ""));
            }

            diskOnlyPending.Sort((a, b) => CompareIndexUpdatedDesc(a.updatedAtUtc, b.updatedAtUtc));
            for (int i = 0; i < diskOnlyPending.Count; i++)
                RegisterSeed(diskOnlyPending[i].session);

            if (insertionOrder.Count == 0)
            {
                var hidden = Workspace.MockWorkspaceProvider.LoadHiddenSeedWorkspaceIds();
                if (!hidden.Contains("ws-wall-001"))
                    RegisterSeed(new WorkspaceSessionContext
                    {
                        workspaceId = "ws-wall-001",
                        workspaceName = "Target on Wall",
                        targetId = "target-wall-001",
                        isNewWorkspace = false,
                        setupState = WorkspaceSetupState.Ready
                    });
                if (!hidden.Contains("ws-floor-001"))
                    RegisterSeed(new WorkspaceSessionContext
                    {
                        workspaceId = "ws-floor-001",
                        workspaceName = "Target on Floor",
                        targetId = "target-floor-001",
                        isNewWorkspace = false,
                        setupState = WorkspaceSetupState.Ready
                    });
                if (!hidden.Contains("ws-ceiling-001"))
                    RegisterSeed(new WorkspaceSessionContext
                    {
                        workspaceId = "ws-ceiling-001",
                        workspaceName = "Target on Ceiling",
                        targetId = "target-ceiling-001",
                        isNewWorkspace = false,
                        setupState = WorkspaceSetupState.Ready
                    });
            }

            for (int i = 0; i < insertionOrder.Count; i++)
                mockWorkspaces.Add(byId[insertionOrder[i]]);
        }

        private static int CompareIndexUpdatedDesc(string a, string b)
        {
            DateTime ta = TryParseIndexUtc(a);
            DateTime tb = TryParseIndexUtc(b);
            return tb.CompareTo(ta);
        }

        private static DateTime TryParseIndexUtc(string iso)
        {
            if (string.IsNullOrWhiteSpace(iso))
                return DateTime.MinValue;
            if (DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime dt))
                return dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();
            return DateTime.MinValue;
        }

        /// <summary>
        /// Disk-only row in workspace-index.json: resolve target id from snapshot.json so EDIT can enter authoring.
        /// </summary>
        private static bool TryBuildSessionFromDiskWorkspace(string workspaceId, WorkspaceIndexEntry indexEntry, out WorkspaceSessionContext session)
        {
            session = null;
            if (string.IsNullOrWhiteSpace(workspaceId))
                return false;

            var repo = new WorkspaceSnapshotRepository();
            if (!repo.TryLoadSnapshot(workspaceId.Trim(), out WorkspaceSnapshot snap) || snap == null)
                return false;

            string targetId = ResolvePrimaryTargetIdFromSnapshot(snap);
            if (string.IsNullOrWhiteSpace(targetId))
            {
                WorkspaceDraftState draft = WorkspaceDataServices.LocalStore.GetWorkspaceSnapshot(workspaceId.Trim());
                if (draft?.target != null && !string.IsNullOrWhiteSpace(draft.target.targetId))
                    targetId = draft.target.targetId.Trim();
            }

            if (string.IsNullOrWhiteSpace(targetId))
                return false;

            string name = indexEntry != null && !string.IsNullOrWhiteSpace(indexEntry.workspaceName)
                ? indexEntry.workspaceName.Trim()
                : (!string.IsNullOrWhiteSpace(snap.workspaceName) ? snap.workspaceName.Trim() : workspaceId.Trim());

            session = new WorkspaceSessionContext
            {
                workspaceId = workspaceId.Trim(),
                workspaceName = name,
                targetId = targetId.Trim(),
                thumbnailKey = indexEntry != null && !string.IsNullOrWhiteSpace(indexEntry.thumbnailKey)
                    ? indexEntry.thumbnailKey.Trim()
                    : "",
                isNewWorkspace = false,
                setupState = WorkspaceSetupState.Ready
            };
            return true;
        }

        private static string ResolvePrimaryTargetIdFromSnapshot(WorkspaceSnapshot snap)
        {
            if (snap?.targets == null || snap.targets.Length == 0)
                return "";

            TargetSnapshot ts = snap.targets[0];
            if (ts == null)
                return "";

            if (!string.IsNullOrWhiteSpace(ts.serverTargetId))
                return ts.serverTargetId.Trim();
            return ts.localTargetId != null ? ts.localTargetId.Trim() : "";
        }

        private string ResolveWorkspaceThumbnailPath(WorkspaceSessionContext session)
        {
            if (session == null || string.IsNullOrWhiteSpace(session.workspaceId))
                return null;

            string filePath = ResolveWorkspaceRelativePath(session.workspaceId, session.thumbnailKey);
            if (!string.IsNullOrWhiteSpace(filePath))
                return filePath;

            filePath = ResolveWorkspaceRelativePath(session.workspaceId, session.targetImageRelativePath);
            if (!string.IsNullOrWhiteSpace(filePath))
                return filePath;

            return null;
        }

        private string ResolveWorkspaceRelativePath(string workspaceId, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath) || string.IsNullOrWhiteSpace(workspaceId))
                return null;

            string trimmed = relativePath.Trim();
            if (File.Exists(trimmed))
                return trimmed;

            string resolved = WorkspacePersistencePaths.ResolveRelativeToWorkspaceRoot(workspaceId.Trim(), trimmed);
            if (!string.IsNullOrWhiteSpace(resolved) && File.Exists(resolved))
                return resolved;

            return null;
        }

        private Texture2D GetWorkspaceThumbnailTexture(WorkspaceSessionContext session)
        {
            string path = ResolveWorkspaceThumbnailPath(session);
            if (string.IsNullOrWhiteSpace(path))
                return null;

            if (thumbnailTextureCache.TryGetValue(path, out Texture2D cached) && cached != null)
                return cached;

            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                if (bytes == null || bytes.Length == 0)
                    return null;

                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!texture.LoadImage(bytes, markNonReadable: false))
                {
                    Destroy(texture);
                    return null;
                }

                thumbnailTextureCache[path] = texture;
                return texture;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"WorkspaceSwitcherController: failed to load thumbnail '{path}' for '{session.workspaceId}': {ex.Message}");
                return null;
            }
        }

        private void RebuildCards()
        {
            if (workspaceCardsRow == null)
                return;

            workspaceCardsRow.Clear();
            cardElements.Clear();
            cardScaleCurrent.Clear();
            cardScaleTarget.Clear();
            cardOpacityCurrent.Clear();
            cardOpacityTarget.Clear();

            for (int i = 0; i < mockWorkspaces.Count; i++)
            {
                WorkspaceSessionContext ws = mockWorkspaces[i];
                var card = new VisualElement { name = "WorkspaceCard" + i };
                card.AddToClassList("card");
                card.AddToClassList("workspace-card");
                card.AddToClassList("card--inactive");

                Texture2D thumbnail = GetWorkspaceThumbnailTexture(ws);
                var thumbnailLayer = new VisualElement { name = "WorkspaceCardThumbnail" };
                thumbnailLayer.AddToClassList("workspace-card__thumbnail");
                if (thumbnail != null)
                {
                    thumbnailLayer.style.backgroundImage = new StyleBackground(thumbnail);
                }
                else
                {
                    thumbnailLayer.AddToClassList("workspace-card__thumbnail--placeholder");
                    var placeholderLabel = new Label("No preview");
                    placeholderLabel.AddToClassList("workspace-card__thumbnail-placeholder-label");
                    thumbnailLayer.Add(placeholderLabel);
                }

                var overlay = new VisualElement { name = "WorkspaceCardScrim" };
                overlay.AddToClassList("workspace-card__scrim");

                var label = new Label(ws.workspaceName) { name = "WorkspaceCardLabel" };
                label.AddToClassList("workspace-card__label");

                card.Add(thumbnailLayer);
                card.Add(overlay);
                card.Add(label);

                int idx = i;
                card.RegisterCallback<ClickEvent>(_ =>
                {
                    selectedIndex = idx;
                    RefreshSelectionUi();
                });

                workspaceCardsRow.Add(card);
                cardElements.Add(card);
                cardScaleCurrent.Add(InactiveCardScale);
                cardScaleTarget.Add(InactiveCardScale);
                cardOpacityCurrent.Add(InactiveCardOpacity);
                cardOpacityTarget.Add(InactiveCardOpacity);
            }

            var addCard = new VisualElement { name = "WorkspaceAddCard" };
            addCard.AddToClassList("card");
            addCard.AddToClassList("card--active");
            addCard.AddToClassList("workspace-add-card");
            addCard.style.flexDirection = FlexDirection.Column;
            addCard.style.justifyContent = Justify.Center;
            addCard.style.alignItems = Align.Center;

            var plusIcon = new Label("+");
            plusIcon.AddToClassList("workspace-add-card__icon");
            plusIcon.style.fontSize = 48;
            plusIcon.style.color = Color.white;
            plusIcon.style.unityFontStyleAndWeight = FontStyle.Bold;
            plusIcon.style.unityTextAlign = TextAnchor.MiddleCenter;
            plusIcon.style.width = new Length(100, LengthUnit.Percent);
            addCard.Add(plusIcon);

            var addLabel = new Label("Add workspace");
            addLabel.AddToClassList("workspace-add-card__label");
            addLabel.style.color = Color.white;
            addLabel.style.fontSize = 18;
            addLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            addLabel.style.marginTop = 8;
            addLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            addLabel.style.width = new Length(100, LengthUnit.Percent);
            addCard.Add(addLabel);

            addCard.RegisterCallback<ClickEvent>(_ => OnNewButtonClicked());
            workspaceCardsRow.Add(addCard);
        }

        private void RefreshSelectionUi(bool forceImmediate = false)
        {
            if (mockWorkspaces.Count == 0)
                return;

            selectedIndex = Mathf.Clamp(selectedIndex, 0, mockWorkspaces.Count - 1);
            WorkspaceSessionContext selected = mockWorkspaces[selectedIndex];
            if (activeWorkspaceNameLabel != null)
                activeWorkspaceNameLabel.text = "Selected: " + selected.workspaceName;

            float centerIndex = (mockWorkspaces.Count - 1) * 0.5f;
            stripOffsetTarget = (centerIndex - selectedIndex) * CardStepPixels;

            for (int i = 0; i < cardElements.Count; i++)
            {
                bool isSelected = i == selectedIndex;
                cardScaleTarget[i] = isSelected ? ActiveCardScale : InactiveCardScale;
                cardOpacityTarget[i] = isSelected ? ActiveCardOpacity : InactiveCardOpacity;
            }

            if (!hasAnimationState || forceImmediate)
            {
                stripOffsetCurrent = stripOffsetTarget;
                workspaceCardsRow.style.translate = new Translate(
                    new Length(stripOffsetCurrent, LengthUnit.Pixel),
                    new Length(0f, LengthUnit.Pixel),
                    0f);

                for (int i = 0; i < cardElements.Count; i++)
                {
                    cardScaleCurrent[i] = cardScaleTarget[i];
                    cardOpacityCurrent[i] = cardOpacityTarget[i];
                }
            }

            hasAnimationState = true;
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
            SceneTransitionService.TransitionToScene(AppFlowController.TargetInstantiationSceneName);
        }

        private void OnEditButtonClicked()
        {
            if (SceneTransitionService.IsTransitioning || mockWorkspaces.Count == 0)
                return;

            WorkspaceSessionContext selected = mockWorkspaces[Mathf.Clamp(selectedIndex, 0, mockWorkspaces.Count - 1)].Clone();
            selected.isNewWorkspace = false;
            selected.setupState = WorkspaceSetupState.Ready;
            AppFlowController.SetWorkspaceSession(selected);
            SceneTransitionService.TransitionToScene(AppFlowController.AuthoringSceneName);
        }

        private void OnDeleteWorkspaceClicked()
        {
            if (SceneTransitionService.IsTransitioning || mockWorkspaces.Count == 0 || workspaceDeleteBusy)
                return;

            WorkspaceSessionContext selected = mockWorkspaces[Mathf.Clamp(selectedIndex, 0, mockWorkspaces.Count - 1)];
            string id = selected.workspaceId?.Trim();
            if (string.IsNullOrWhiteSpace(id))
                return;

            StartCoroutine(DeleteWorkspaceCoroutine(id));
        }

        private IEnumerator DeleteWorkspaceCoroutine(string workspaceId)
        {
            workspaceDeleteBusy = true;
            string id = workspaceId.Trim();

            if (string.Equals(id, "default", StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning("WorkspaceSwitcherController: cannot delete reserved workspace 'default'.");
                workspaceDeleteBusy = false;
                yield break;
            }

            if (!string.IsNullOrWhiteSpace(backendApiBaseUrl))
            {
                string url = $"{backendApiBaseUrl.TrimEnd('/')}/api/workspaces/{Uri.EscapeDataString(id)}";
                using (var uwr = new UnityWebRequest(url, "DELETE"))
                {
                    uwr.downloadHandler = new DownloadHandlerBuffer();
                    uwr.timeout = 25;
                    yield return uwr.SendWebRequest();

                    long code = uwr.responseCode;
                    bool accepted = code == 404 || (code >= 200 && code < 300);
                    if (!accepted)
                    {
                        Debug.LogWarning($"WorkspaceSwitcherController: backend workspace delete failed ({code}): {uwr.error}");
                        workspaceDeleteBusy = false;
                        yield break;
                    }
                }
            }

            if (!WorkspaceDeletion.TryDeleteWorkspaceEverywhere(id, out string err))
            {
                Debug.LogWarning($"WorkspaceSwitcherController: local workspace delete failed: {err}");
                workspaceDeleteBusy = false;
                yield break;
            }

            Debug.Log($"WorkspaceSwitcherController: deleted workspace '{id}' (server data if configured + snapshot folder + index + draft cache).");

            SeedMockWorkspaces();
            RebuildCards();
            RefreshSelectionUi(forceImmediate: true);
            workspaceDeleteBusy = false;
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

            var editBtn = new Button { name = EditButtonName, text = "EDIT" };
            editBtn.style.width = 160;
            editBtn.style.height = 46;
            editBtn.style.marginLeft = 0;
            editBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            actionRow.Add(editBtn);

            var deleteBtn = new Button { name = DeleteWorkspaceButtonName, text = "DELETE" };
            deleteBtn.style.width = 160;
            deleteBtn.style.height = 46;
            deleteBtn.style.marginLeft = 14;
            deleteBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            deleteBtn.style.backgroundColor = new Color(185f / 255f, 28f / 255f, 28f / 255f, 1f);
            deleteBtn.style.color = Color.white;
            deleteBtn.style.borderLeftWidth = deleteBtn.style.borderRightWidth = deleteBtn.style.borderTopWidth = deleteBtn.style.borderBottomWidth = 1;
            var dangerBorder = new Color(248f / 255f, 113f / 255f, 113f / 255f, 1f);
            deleteBtn.style.borderLeftColor = deleteBtn.style.borderRightColor = deleteBtn.style.borderTopColor = deleteBtn.style.borderBottomColor = dangerBorder;
            actionRow.Add(deleteBtn);

            root.Add(actionRow);
        }
    }
}
