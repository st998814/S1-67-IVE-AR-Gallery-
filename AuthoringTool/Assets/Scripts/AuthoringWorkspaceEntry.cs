using System;
using System.Collections;
using ARGallery.Content;
using ARGallery.Workspace.Persistence;
using UnityEngine;
using UnityEngine.Networking;
using WorkspaceDomain = global::ARGallery.Workspace;
using WorkspacePresets = global::ARGallery.Workspace.Presets;
using CameraControl = global::ARGallery.CameraControl;

namespace ARGallery.AppFlow
{
    /// <summary>
    /// Authoring scene entry adapter that consumes app-level workspace context
    /// and aligns runtime target selection to a 1:1 workspace-target mapping.
    /// </summary>
    public class AuthoringWorkspaceEntry : MonoBehaviour
    {
        [Serializable]
        private class WorkspaceRestoreEnvelope
        {
            public WorkspaceDetailDto workspace;
            public WorkspaceTargetDto[] targets;
            public WorkspaceContentDto[] contents;
        }

        [Serializable]
        private class WorkspaceDetailDto
        {
            public string workspaceId;
            public string workspaceName;
            public string state;
            public int schemaVersion;
            public string createdAtUtc;
            public string updatedAtUtc;
        }

        [Serializable]
        private class WorkspaceTargetDto
        {
            public string targetId;
            public string workspaceId;
            public string targetName;
            public string displayLabel;
            public string targetImageUrl;
            public string targetReferenceImageUrl;
            public float physicalWidthM;
            public SerializableVector3Dto localPosition;
            public SerializableVector3Dto localEuler;
            public SerializableVector3Dto localScale;
            public string vuforiaTargetId;
            public string vuforiaStatus;
            public string status;
            public string createdAtUtc;
            public string updatedAtUtc;
        }

        [Serializable]
        private class WorkspaceContentDto
        {
            public string contentId;
            public string targetId;
            public string workspaceId;
            public string contentType;
            public string mediaUrl;
            public SerializableVector3Dto localPosition;
            public SerializableVector3Dto localEuler;
            public SerializableVector3Dto localScale;
            public string renderKind;
            public string assetFormat;
            public string status;
            public string createdAtUtc;
            public string updatedAtUtc;
        }

        [Serializable]
        private class SerializableVector3Dto
        {
            public float x;
            public float y;
            public float z;
        }

        [SerializeField] private bool createMissingTarget = true;
        [SerializeField] private string defaultWorkspaceId = WorkspaceDomain.MockWorkspaceProvider.DefaultWorkspaceId;
        [SerializeField] private string backendApiBaseUrl = "http://127.0.0.1:5050";

        public string BackendApiBaseUrl => backendApiBaseUrl;
        [Header("Orientation Helper")]
        [SerializeField] private bool showOrientationHelper = false;
        [SerializeField] private float orientationHelperAxisLength = 0.35f;
        [SerializeField] private float orientationHelperAxisThickness = 0.01f;
        [SerializeField] private float ceilingTargetHeightOffset = 1.2f;

        private readonly TargetWorkflowService targetWorkflowService = new TargetWorkflowService();
        private WorkspaceDomain.WorkspacePosture _appliedPosture = WorkspaceDomain.WorkspacePosture.Wall;

        /// <summary>Last workspace posture applied in the authoring scene (for placement bounds and coordinator sync).</summary>
        public WorkspaceDomain.WorkspacePosture AppliedPosture => _appliedPosture;

        private void Start()
        {
            StartCoroutine(CoAuthoringEntry());
        }

        private IEnumerator CoAuthoringEntry()
        {
            WorkspaceSessionContext session = null;
            bool hasSession = AppFlowController.TryGetWorkspaceSession(out session) && session != null;

            if (hasSession && !session.IsReadyForAuthoring())
            {
                Debug.Log("AuthoringWorkspaceEntry: Workspace setup is pending. Authoring entry is blocked.");
                if (!SceneTransitionService.IsTransitioning)
                    AppFlowController.TransitionToTargetInstantiation();
                yield break;
            }

            string workspaceId = ResolveWorkspaceId(session);

            bool restoredFromBackend = false;
            yield return TryRebuildFromBackend(workspaceId, session, ok => restoredFromBackend = ok);
            if (restoredFromBackend)
                yield break;

            WorkspaceDomain.WorkspaceDraftState draft = LoadWorkspaceDraft(workspaceId);
            string canonicalTargetId = ResolveAuthoringTargetId(draft, session, workspaceId, null);
            if (draft == null || draft.target == null || string.IsNullOrWhiteSpace(canonicalTargetId))
            {
                Debug.LogWarning($"AuthoringWorkspaceEntry: Workspace draft '{workspaceId}' is missing target context.");
                yield break;
            }

            ApplyWorkspaceContext(draft, session, workspaceId, canonicalTargetId);
        }

        private WorkspaceDomain.WorkspaceDraftState LoadWorkspaceDraft(string workspaceId)
        {
            WorkspaceDomain.LocalWorkspaceStore store = WorkspaceDomain.WorkspaceDataServices.LocalStore;
            WorkspaceDomain.IWorkspaceProvider provider = WorkspaceDomain.WorkspaceDataServices.Provider;
            WorkspaceDomain.WorkspaceDraftState draft = store.GetOrLoad(workspaceId, provider.GetWorkspace);
            if (draft == null)
            {
                Debug.LogWarning($"AuthoringWorkspaceEntry: Workspace '{workspaceId}' not found. Falling back to default.");
                draft = store.GetOrLoad(defaultWorkspaceId, provider.GetWorkspace);
            }

            return draft;
        }

        private string ResolveWorkspaceId(WorkspaceSessionContext session)
        {
            if (session != null && !string.IsNullOrWhiteSpace(session.workspaceId))
                return session.workspaceId.Trim();
            if (!string.IsNullOrWhiteSpace(defaultWorkspaceId))
                return defaultWorkspaceId.Trim();
            return WorkspaceDomain.MockWorkspaceProvider.DefaultWorkspaceId;
        }

        /// <summary>
        /// Resolves AR target id from session or current draft.
        /// </summary>
        private static string ResolveAuthoringTargetId(
            WorkspaceDomain.WorkspaceDraftState workspace,
            WorkspaceSessionContext session,
            string resolvedWorkspaceId,
            WorkspaceSnapshot snapshotOrNull)
        {
            if (session != null && !string.IsNullOrWhiteSpace(session.targetId))
                return session.targetId.Trim();

            if (snapshotOrNull != null)
            {
                string fromSnap = ResolvePrimaryTargetIdFromSnapshot(snapshotOrNull);
                if (!string.IsNullOrWhiteSpace(fromSnap))
                    return fromSnap.Trim();
            }

            if (workspace?.target != null && !string.IsNullOrWhiteSpace(workspace.target.targetId))
                return workspace.target.targetId.Trim();

            return "";
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

        private void ApplyWorkspaceContext(
            WorkspaceDomain.WorkspaceDraftState workspace,
            WorkspaceSessionContext session,
            string resolvedWorkspaceId,
            string canonicalTargetId)
        {
            TargetSelectionManager manager = FindFirstObjectByType<TargetSelectionManager>();
            if (manager == null)
            {
                Debug.LogWarning("AuthoringWorkspaceEntry: TargetSelectionManager not found; cannot apply workspace target context.");
                return;
            }

            string targetId = canonicalTargetId.Trim();
            string targetName = !string.IsNullOrWhiteSpace(workspace.target.displayLabel)
                ? workspace.target.displayLabel.Trim()
                : (!string.IsNullOrWhiteSpace(workspace.target.targetName) ? workspace.target.targetName.Trim() : "WorkspaceTarget");

            float physicalWidthM = ResolvePhysicalWidthMeters(targetId, null, workspace);

            int index = manager.FindTargetIndexById(targetId);
            if (index >= 0)
            {
                manager.SetActiveTarget(index);
                EnsureAuthoredTargetForPersistence(manager.GetActiveTarget(), targetId, targetName, session, physicalWidthM);
                ApplyWorkspacePreset(manager.GetActiveTarget(), workspace.target.posture);
                ApplyWorkspaceTargetVisual(manager.GetActiveTarget(), workspace.target.targetImageUrl, session);
                if (string.IsNullOrWhiteSpace(session?.targetId))
                    AppFlowController.SetWorkspaceTargetId(targetId);
                Debug.Log($"AuthoringWorkspaceEntry: Activated workspace target '{targetId}' (index={index}).");
                return;
            }

            if (!createMissingTarget)
            {
                Debug.LogWarning($"AuthoringWorkspaceEntry: Workspace target '{targetId}' not found and auto-create is disabled.");
                return;
            }

            TargetWorkflowService.LocalCreateResult result = targetWorkflowService.CreateAndRegisterLocal(
                this,
                targetName,
                targetId,
                targetName,
                physicalWidthM);

            if (!result.success)
            {
                if (result.isDuplicate && result.duplicateIndex >= 0)
                {
                    manager.SetActiveTarget(result.duplicateIndex);
                    EnsureAuthoredTargetForPersistence(manager.GetActiveTarget(), targetId, targetName, session, physicalWidthM);
                    ApplyWorkspacePreset(manager.GetActiveTarget(), workspace.target.posture);
                    ApplyWorkspaceTargetVisual(manager.GetActiveTarget(), workspace.target.targetImageUrl, session);
                    Debug.Log($"AuthoringWorkspaceEntry: Duplicate target resolved by activating index={result.duplicateIndex}.");
                    return;
                }

                Debug.LogWarning($"AuthoringWorkspaceEntry: Failed to create workspace target '{targetId}': {result.message}");
                return;
            }

            int createdIndex = manager.FindTargetIndexById(targetId);
            if (createdIndex >= 0)
            {
                manager.SetActiveTarget(createdIndex);
                EnsureAuthoredTargetForPersistence(manager.GetActiveTarget(), targetId, targetName, session, physicalWidthM);
                ApplyWorkspacePreset(manager.GetActiveTarget(), workspace.target.posture);
                ApplyWorkspaceTargetVisual(manager.GetActiveTarget(), workspace.target.targetImageUrl, session);
            }

            // Keep app-flow context aligned with provider-loaded target in mock-first mode.
            if (string.IsNullOrWhiteSpace(session?.targetId))
                AppFlowController.SetWorkspaceTargetId(targetId);

            Debug.Log($"AuthoringWorkspaceEntry: Created and activated workspace target '{targetId}'.");
        }

        private static void EnsureAuthoredTargetForPersistence(
            GameObject targetGo,
            string targetId,
            string targetDisplayName,
            WorkspaceSessionContext session,
            float physicalWidthMeters)
        {
            if (targetGo == null || string.IsNullOrWhiteSpace(targetId))
                return;

            AuthoredTargetInstance auth = WorkspaceAuthoredAttach.EnsureTarget(targetGo, targetId, targetDisplayName);
            if (auth == null)
                return;

            if (physicalWidthMeters > 1e-5f)
                auth.PhysicalWidthM = physicalWidthMeters;

            if (session == null)
                return;

            if (!string.IsNullOrWhiteSpace(session.vuforiaTargetId))
                auth.VuforiaTargetId = session.vuforiaTargetId.Trim();
        }

        private static float ResolvePhysicalWidthMeters(
            string targetId,
            WorkspaceSnapshot snapshotOrNull,
            WorkspaceDomain.WorkspaceDraftState workspace)
        {
            if (!string.IsNullOrWhiteSpace(targetId) && snapshotOrNull?.targets != null)
            {
                string id = targetId.Trim();
                for (int i = 0; i < snapshotOrNull.targets.Length; i++)
                {
                    TargetSnapshot ts = snapshotOrNull.targets[i];
                    if (ts == null)
                        continue;
                    string rowId = !string.IsNullOrWhiteSpace(ts.serverTargetId) ? ts.serverTargetId.Trim() : ts.localTargetId != null ? ts.localTargetId.Trim() : "";
                    if (string.Equals(rowId, id, StringComparison.Ordinal) && ts.physicalWidthM > 1e-5f)
                        return ts.physicalWidthM;
                }
            }

            if (workspace?.target != null && workspace.target.physicalWidth > 1e-5f)
                return workspace.target.physicalWidth;

            return 0.2f;
        }

        private IEnumerator TryRebuildFromBackend(string workspaceId, WorkspaceSessionContext session, Action<bool> onDone)
        {
            onDone?.Invoke(false);
            if (string.IsNullOrWhiteSpace(workspaceId) || string.IsNullOrWhiteSpace(backendApiBaseUrl))
                yield break;

            WorkspaceSceneReconstructor reconstructor = FindFirstObjectByType<WorkspaceSceneReconstructor>();
            if (reconstructor == null)
                yield break;

            string url = $"{backendApiBaseUrl.TrimEnd('/')}/api/workspaces/{Uri.EscapeDataString(workspaceId.Trim())}";
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = 20;
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                    yield break;

                string body = request.downloadHandler != null ? request.downloadHandler.text : "";
                WorkspaceRestoreEnvelope payload = JsonUtility.FromJson<WorkspaceRestoreEnvelope>(body);
                WorkspaceSnapshot snapshot = BuildSnapshotFromBackendPayload(payload, workspaceId, backendApiBaseUrl);
                if (snapshot == null || snapshot.targets == null || snapshot.targets.Length == 0)
                    yield break;

                bool completed = false;
                bool rebuildOk = false;
                reconstructor.BeginRebuild(snapshot, ok =>
                {
                    rebuildOk = ok;
                    completed = true;
                });

                while (!completed)
                    yield return null;

                if (!rebuildOk)
                    yield break;

                WorkspaceDomain.WorkspaceDraftState draftAfterRebuild = LoadWorkspaceDraft(workspaceId);
                string resolvedTargetId = ResolveAuthoringTargetId(draftAfterRebuild, session, workspaceId, snapshot);
                if (string.IsNullOrWhiteSpace(resolvedTargetId))
                    yield break;

                if (string.IsNullOrWhiteSpace(session?.targetId))
                    AppFlowController.SetWorkspaceTargetId(resolvedTargetId);
                if (snapshot.targets != null && snapshot.targets.Length > 0
                    && string.IsNullOrWhiteSpace(session?.targetImageUrl))
                {
                    AppFlowController.SetWorkspaceTargetImageUrl(snapshot.targets[0].targetImageUrl ?? "");
                }

                AppFlowController.TryGetWorkspaceSession(out session);
                ApplyWorkspaceContextAfterSnapshotRebuild(draftAfterRebuild, session, workspaceId, snapshot, resolvedTargetId);
                onDone?.Invoke(true);
            }
        }

        private static WorkspaceSnapshot BuildSnapshotFromBackendPayload(
            WorkspaceRestoreEnvelope payload,
            string fallbackWorkspaceId,
            string apiBaseUrl)
        {
            if (payload == null || payload.workspace == null)
                return null;

            string workspaceId = string.IsNullOrWhiteSpace(payload.workspace.workspaceId)
                ? fallbackWorkspaceId
                : payload.workspace.workspaceId.Trim();
            if (string.IsNullOrWhiteSpace(workspaceId))
                return null;

            var snapshot = new WorkspaceSnapshot
            {
                schemaVersion = "v1",
                workspaceId = workspaceId,
                workspaceName = string.IsNullOrWhiteSpace(payload.workspace.workspaceName) ? workspaceId : payload.workspace.workspaceName.Trim(),
                createdAtUtc = payload.workspace.createdAtUtc ?? DateTime.UtcNow.ToString("o"),
                updatedAtUtc = payload.workspace.updatedAtUtc ?? DateTime.UtcNow.ToString("o"),
                remoteDirty = false,
                lastRemoteSyncedAtUtc = payload.workspace.updatedAtUtc ?? "",
                lastRemoteSyncError = "",
                remoteSyncStatus = RemoteSyncStatus.Synced,
            };

            WorkspaceTargetDto[] sourceTargets = payload.targets ?? Array.Empty<WorkspaceTargetDto>();
            var targets = new System.Collections.Generic.List<TargetSnapshot>(sourceTargets.Length);
            for (int i = 0; i < sourceTargets.Length; i++)
            {
                WorkspaceTargetDto t = sourceTargets[i];
                if (t == null || string.IsNullOrWhiteSpace(t.targetId))
                    continue;

                targets.Add(new TargetSnapshot
                {
                    localTargetId = t.targetId.Trim(),
                    serverTargetId = t.targetId.Trim(),
                    vuforiaTargetId = t.vuforiaTargetId ?? "",
                    targetName = string.IsNullOrWhiteSpace(t.targetName) ? t.targetId.Trim() : t.targetName.Trim(),
                    targetImageUrl = t.targetImageUrl ?? "",
                    targetReferenceImageUrl = t.targetReferenceImageUrl ?? "",
                    physicalWidthM = t.physicalWidthM > 1e-5f ? t.physicalWidthM : 0.2f,
                    position = ToVector3Data(t.localPosition),
                    rotation = ToVector3Data(t.localEuler),
                    scale = ToVector3DataOrDefault(t.localScale, Vector3.one),
                    remoteDirty = false,
                    lastRemoteSyncedAtUtc = t.updatedAtUtc ?? "",
                });
            }
            snapshot.targets = targets.ToArray();

            WorkspaceContentDto[] sourceContents = payload.contents ?? Array.Empty<WorkspaceContentDto>();
            var contents = new System.Collections.Generic.List<ContentSnapshot>(sourceContents.Length);
            for (int i = 0; i < sourceContents.Length; i++)
            {
                WorkspaceContentDto c = sourceContents[i];
                if (c == null || string.IsNullOrWhiteSpace(c.contentId))
                    continue;

                string mediaUrl = c.mediaUrl ?? "";
                contents.Add(new ContentSnapshot
                {
                    localContentId = c.contentId.Trim(),
                    serverContentId = c.contentId.Trim(),
                    targetId = c.targetId ?? "",
                    contentType = string.IsNullOrWhiteSpace(c.contentType) ? "image" : c.contentType.Trim(),
                    mediaUrl = mediaUrl,
                    originalFileName = ContentMediaUrlUtility.FileNameFromUrl(
                        ContentMediaUrlUtility.ResolveAbsoluteUrl(
                            mediaUrl,
                            string.IsNullOrWhiteSpace(apiBaseUrl) ? ContentMediaUrlUtility.DefaultBackendBaseUrl : apiBaseUrl),
                        "asset.bin"),
                    position = ToVector3Data(c.localPosition),
                    rotation = ToVector3Data(c.localEuler),
                    scale = ToVector3DataOrDefault(c.localScale, Vector3.one),
                    renderKind = c.renderKind ?? "",
                    assetFormat = c.assetFormat ?? "",
                    isUnsaved = false,
                    uploadPending = false,
                    persistPending = false,
                    remoteDirty = false,
                    lastRemoteSyncedAtUtc = c.updatedAtUtc ?? "",
                });
            }
            snapshot.contents = contents.ToArray();
            return snapshot;
        }

        private static Vector3Data ToVector3Data(SerializableVector3Dto value)
        {
            if (value == null)
                return new Vector3Data(0f, 0f, 0f);
            return new Vector3Data(value.x, value.y, value.z);
        }

        private static Vector3Data ToVector3DataOrDefault(SerializableVector3Dto value, Vector3 fallback)
        {
            if (value == null)
                return new Vector3Data(fallback.x, fallback.y, fallback.z);
            return new Vector3Data(value.x, value.y, value.z);
        }

        /// <summary>
        /// After <see cref="WorkspaceSceneReconstructor"/> rebuilds from a backend snapshot, targets/content already exist.
        /// Binds selection and authoring services without overwriting API-restored transforms.
        /// </summary>
        private void ApplyWorkspaceContextAfterSnapshotRebuild(
            WorkspaceDomain.WorkspaceDraftState workspace,
            WorkspaceSessionContext session,
            string resolvedWorkspaceId,
            WorkspaceSnapshot snapshot,
            string canonicalTargetId)
        {
            TargetSelectionManager manager = FindFirstObjectByType<TargetSelectionManager>();
            if (manager == null)
            {
                Debug.LogWarning("AuthoringWorkspaceEntry: TargetSelectionManager not found; cannot apply workspace target context.");
                return;
            }

            string targetId = canonicalTargetId.Trim();

            int index = manager.FindTargetIndexById(targetId);
            if (index < 0)
            {
                Debug.LogWarning(
                    $"AuthoringWorkspaceEntry: After snapshot rebuild, target id '{targetId}' was not found in TargetSelectionManager. " +
                    $"draft.targetId={workspace?.target?.targetId ?? "(null)"} workspaceId={resolvedWorkspaceId}");
                return;
            }

            string displayName = ResolveTargetDisplayNameForRestore(snapshot, targetId, workspace);
            float physicalWidthM = ResolvePhysicalWidthMeters(targetId, snapshot, workspace);

            manager.SetActiveTarget(index);
            EnsureAuthoredTargetForPersistence(manager.GetActiveTarget(), targetId, displayName, session, physicalWidthM);

            WorkspaceDomain.WorkspacePosture posture = ResolvePostureForRestoredTarget(snapshot, targetId, workspace);
            ApplyWorkspacePreset(manager.GetActiveTarget(), posture, preserveTargetTransform: true);

            bool hasSessionImageBytes = session != null && session.targetImageBytes != null && session.targetImageBytes.Length > 0;
            if (hasSessionImageBytes)
            {
                string imageUrlFromSnapshot = ResolveTargetImageUrlFromSnapshot(snapshot, targetId);
                ApplyWorkspaceTargetVisual(manager.GetActiveTarget(), imageUrlFromSnapshot, session);
            }

            if (string.IsNullOrWhiteSpace(session?.targetId))
                AppFlowController.SetWorkspaceTargetId(targetId);

            Debug.Log(
                $"AuthoringWorkspaceEntry: Activated workspace target '{targetId}' from backend snapshot (index={index}, posture={posture}, transform preserved).");
            TrySelectFirstRestoredContent(manager.GetActiveTarget());
        }

        private static string ResolveTargetDisplayNameForRestore(
            WorkspaceSnapshot snapshot,
            string targetId,
            WorkspaceDomain.WorkspaceDraftState workspace)
        {
            if (TryFindTargetSnapshot(snapshot, targetId, out TargetSnapshot ts) && !string.IsNullOrWhiteSpace(ts.targetName))
                return ts.targetName.Trim();

            if (workspace?.target != null && !string.IsNullOrWhiteSpace(workspace.target.displayLabel))
                return workspace.target.displayLabel.Trim();
            if (workspace?.target != null && !string.IsNullOrWhiteSpace(workspace.target.targetName))
                return workspace.target.targetName.Trim();
            if (!string.IsNullOrWhiteSpace(snapshot?.workspaceName))
                return snapshot.workspaceName.Trim();
            return targetId;
        }

        private static string ResolveTargetImageUrlFromSnapshot(WorkspaceSnapshot snapshot, string targetId)
        {
            if (!TryFindTargetSnapshot(snapshot, targetId, out TargetSnapshot ts))
                return "";
            return ts.targetImageUrl ?? "";
        }

        /// <summary>
        /// Prefer saved target euler from the API snapshot; fall back to mock draft posture for offline demos.
        /// </summary>
        private static WorkspaceDomain.WorkspacePosture ResolvePostureForRestoredTarget(
            WorkspaceSnapshot snapshot,
            string targetId,
            WorkspaceDomain.WorkspaceDraftState workspace)
        {
            if (TryFindTargetSnapshot(snapshot, targetId, out TargetSnapshot ts) && ts.rotation != null)
                return InferPostureFromTargetLocalEuler(ts.rotation.ToVector3());

            if (workspace?.target != null)
                return workspace.target.posture;

            return WorkspaceDomain.WorkspacePosture.Wall;
        }

        /// <summary>
        /// Matches <see cref="WorkspacePresets.WorkspacePresetLibrary"/> target euler conventions (floor ≈ +90° X, ceiling ≈ -90° X).
        /// </summary>
        private static WorkspaceDomain.WorkspacePosture InferPostureFromTargetLocalEuler(Vector3 localEuler)
        {
            float x = localEuler.x;
            if (x >= 45f)
                return WorkspaceDomain.WorkspacePosture.Floor;
            if (x <= -45f)
                return WorkspaceDomain.WorkspacePosture.Ceiling;
            return WorkspaceDomain.WorkspacePosture.Wall;
        }

        private static bool TryFindTargetSnapshot(WorkspaceSnapshot snapshot, string targetId, out TargetSnapshot match)
        {
            match = null;
            if (snapshot?.targets == null || string.IsNullOrWhiteSpace(targetId))
                return false;

            string needle = targetId.Trim();
            for (int i = 0; i < snapshot.targets.Length; i++)
            {
                TargetSnapshot ts = snapshot.targets[i];
                if (ts == null)
                    continue;

                string rowId = !string.IsNullOrWhiteSpace(ts.serverTargetId)
                    ? ts.serverTargetId.Trim()
                    : ts.localTargetId != null ? ts.localTargetId.Trim() : "";
                if (string.Equals(rowId, needle, StringComparison.OrdinalIgnoreCase))
                {
                    match = ts;
                    return true;
                }
            }

            return false;
        }

        private static void TrySelectFirstRestoredContent(GameObject targetRoot)
        {
            if (targetRoot == null)
                return;

            Transform contentRoot = targetRoot.transform.Find("ContentRoot");
            if (contentRoot == null || contentRoot.childCount == 0)
                return;

            Transform firstContent = contentRoot.GetChild(0);
            AuthoringTransformCoordinator coordinator = FindFirstObjectByType<AuthoringTransformCoordinator>();
            coordinator?.SelectContentTransform(firstContent, syncAuthoringUi: true);
            FindFirstObjectByType<SpatialMappingCoordinator>()?.RefreshForCurrentSelection();
        }

        private void ApplyWorkspacePreset(
            GameObject targetRootObject,
            WorkspaceDomain.WorkspacePosture posture,
            bool preserveTargetTransform = false)
        {
            if (targetRootObject == null)
            {
                Debug.LogWarning("AuthoringWorkspaceEntry: Cannot apply preset because target root is null.");
                return;
            }

            EnsureTargetHierarchyCompatibility(targetRootObject.transform);

            _appliedPosture = posture;
            WorkspacePresets.WorkspacePreset preset = WorkspacePresets.WorkspacePresetLibrary.GetPreset(posture);
            Transform targetRoot = targetRootObject.transform;
            if (!preserveTargetTransform)
            {
                targetRoot.localPosition = ResolveTargetLocalPositionForPosture(posture);
                targetRoot.localRotation = Quaternion.Euler(preset.target.targetLocalEuler);
            }

            PlacementBoundsService placementBounds = FindFirstObjectByType<PlacementBoundsService>();
            if (placementBounds != null)
            {
                placementBounds.SetTargetContext(targetRoot, targetRoot.Find("ContentRoot"));
                placementBounds.SetPosture(posture);
                if (!preserveTargetTransform)
                    ReclampContentUnderTarget(targetRoot, placementBounds);
            }

            SpatialMappingCoordinator spatialMapping = FindFirstObjectByType<SpatialMappingCoordinator>();
            if (spatialMapping != null)
                spatialMapping.RefreshPlacementVolume();
            WorkspacePresets.WorkspaceOrientationHelper.Apply(
                targetRoot,
                showOrientationHelper,
                Mathf.Max(0.05f, orientationHelperAxisLength),
                Mathf.Max(0.002f, orientationHelperAxisThickness));

            CameraControl.RuntimeCameraController cameraController = FindFirstObjectByType<CameraControl.RuntimeCameraController>();
            Camera cameraComponent = cameraController != null
                ? cameraController.GetComponent<Camera>()
                : Camera.main;

            if (cameraController == null || cameraComponent == null)
            {
                Debug.LogWarning("AuthoringWorkspaceEntry: RuntimeCameraController/Main Camera not found; skipped camera preset.");
                return;
            }

            Vector3 worldPosition = targetRoot.TransformPoint(preset.camera.localPositionOffset);
            Vector3 worldLookAt = targetRoot.TransformPoint(preset.camera.localLookAtOffset);
            Vector3 lookDirection = worldLookAt - worldPosition;
            if (lookDirection.sqrMagnitude < 0.0001f)
                lookDirection = targetRoot.forward;

            Quaternion lookRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
            Quaternion tiltedRotation = lookRotation * Quaternion.Euler(preset.camera.tiltDegrees, 0f, 0f);
            cameraController.ApplyPose(worldPosition, tiltedRotation, rememberAsResetPose: true);

            string transformNote = preserveTargetTransform ? " (target transform preserved)" : "";
            Debug.Log($"AuthoringWorkspaceEntry: Applied workspace preset posture='{posture}' target='{targetRootObject.name}'{transformNote}.");
        }

        private Vector3 ResolveTargetLocalPositionForPosture(WorkspaceDomain.WorkspacePosture posture)
        {
            if (posture == WorkspaceDomain.WorkspacePosture.Ceiling)
                return new Vector3(0f, Mathf.Max(0f, ceilingTargetHeightOffset), 0f);

            return Vector3.zero;
        }

        private static void ReclampContentUnderTarget(Transform targetRoot, PlacementBoundsService placementBounds)
        {
            if (targetRoot == null || placementBounds == null)
                return;

            Transform contentRoot = targetRoot.Find("ContentRoot");
            if (contentRoot == null)
                return;

            ContentTransformManipulator manipulator = FindFirstObjectByType<ContentTransformManipulator>();
            for (int i = 0; i < contentRoot.childCount; i++)
            {
                Transform child = contentRoot.GetChild(i);
                if (child == null)
                    continue;

                if (manipulator != null)
                    manipulator.SetLocalPosition(child, child.localPosition);
                else
                    child.localPosition = placementBounds.ClampLocalPosition(child, child.localPosition);
            }
        }

        private static void EnsureTargetHierarchyCompatibility(Transform targetRoot)
        {
            if (targetRoot == null)
                return;

            Transform contentRoot = targetRoot.Find("ContentRoot");
            if (contentRoot == null)
            {
                Transform nestedContentRoot = FindDescendantByName(targetRoot, "ContentRoot");
                if (nestedContentRoot != null)
                {
                    nestedContentRoot.SetParent(targetRoot, worldPositionStays: true);
                    nestedContentRoot.name = "ContentRoot";
                    contentRoot = nestedContentRoot;
                    Debug.LogWarning("AuthoringWorkspaceEntry: Re-parented nested ContentRoot to target root for compatibility.");
                }
            }

            if (contentRoot == null)
            {
                GameObject createdContentRoot = new GameObject("ContentRoot");
                createdContentRoot.transform.SetParent(targetRoot, false);
                createdContentRoot.transform.localPosition = Vector3.zero;
                createdContentRoot.transform.localRotation = Quaternion.identity;
                createdContentRoot.transform.localScale = Vector3.one;
                Debug.LogWarning("AuthoringWorkspaceEntry: Created missing ContentRoot for compatibility.");
            }

            Transform targetVisual = targetRoot.Find("TargetVisual");
            if (targetVisual == null)
            {
                Transform targetPlane = FindDescendantByName(targetRoot, "TargetPlane");
                if (targetPlane != null)
                {
                    targetPlane.SetParent(targetRoot, worldPositionStays: true);
                    targetPlane.name = "TargetVisual";
                    targetVisual = targetPlane;
                    Debug.LogWarning("AuthoringWorkspaceEntry: Promoted TargetPlane to TargetVisual compatibility node.");
                }
            }

            if (targetVisual == null)
            {
                GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Quad);
                visual.name = "TargetVisual";
                visual.transform.SetParent(targetRoot, false);
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;
                Debug.LogWarning("AuthoringWorkspaceEntry: Created missing TargetVisual for compatibility.");
            }
        }

        private static Transform FindDescendantByName(Transform root, string name)
        {
            if (root == null || string.IsNullOrWhiteSpace(name))
                return null;

            Transform[] descendants = root.GetComponentsInChildren<Transform>(includeInactive: true);
            for (int i = 0; i < descendants.Length; i++)
            {
                Transform current = descendants[i];
                if (current == null || current == root)
                    continue;
                if (string.Equals(current.name, name, System.StringComparison.Ordinal))
                    return current;
            }

            return null;
        }

        private void ApplyWorkspaceTargetVisual(GameObject targetObject, string targetImageUrl, WorkspaceSessionContext session)
        {
            AuthoredTargetInstance authored = targetObject != null ? targetObject.GetComponent<AuthoredTargetInstance>() : null;
            if (authored != null)
                authored.TargetImageUrl = string.IsNullOrWhiteSpace(targetImageUrl) ? "" : targetImageUrl.Trim();

            if (session != null && session.targetImageBytes != null && session.targetImageBytes.Length > 0)
            {
                if (targetWorkflowService.ApplyTargetImageBytes(targetObject, session.targetImageBytes))
                {
                    authored ??= targetObject.GetComponent<AuthoredTargetInstance>();
                    if (authored != null)
                        authored.TargetImageBytes = PersistenceByteUtility.CloneBytes(session.targetImageBytes);
                    return;
                }
            }
            targetWorkflowService.ApplyTargetImageFromUrl(this, targetObject, targetImageUrl);
        }
    }
}
