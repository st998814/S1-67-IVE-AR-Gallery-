using System;
using System.Collections;
using ARGallery.Workspace.Persistence;
using UnityEngine;
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
        [SerializeField] private bool createMissingTarget = true;
        [SerializeField] private string defaultWorkspaceId = WorkspaceDomain.MockWorkspaceProvider.DefaultWorkspaceId;
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
                    SceneTransitionService.TransitionToScene(AppFlowController.TargetInstantiationSceneName);
                yield break;
            }

            string workspaceId = ResolveWorkspaceId(session);
            var snapshotRepo = new WorkspaceSnapshotRepository();
            if (snapshotRepo.TryLoadSnapshot(workspaceId, out WorkspaceSnapshot snapshotForRebuild))
            {
                WorkspaceSceneReconstructor reconstructor = FindFirstObjectByType<WorkspaceSceneReconstructor>();
                if (reconstructor != null)
                {
                    bool completed = false;
                    bool rebuildOk = false;
                    reconstructor.BeginRebuildFromDisk(workspaceId, ok =>
                    {
                        rebuildOk = ok;
                        completed = true;
                    });

                    while (!completed)
                        yield return null;

                    if (rebuildOk)
                    {
                        WorkspaceDomain.WorkspaceDraftState draftAfterRebuild = LoadWorkspaceDraft(workspaceId);
                        // Draft may be mock-provider fallback (wrong targetId). Snapshot + session carry the real ids.
                        string resolvedTargetId = ResolveAuthoringTargetId(draftAfterRebuild, session, workspaceId, snapshotForRebuild);
                        if (string.IsNullOrWhiteSpace(resolvedTargetId))
                        {
                            Debug.LogWarning($"AuthoringWorkspaceEntry: Snapshot restored but could not resolve target id for workspace '{workspaceId}'.");
                            yield break;
                        }

                        ApplyWorkspaceContextAfterSnapshotRebuild(draftAfterRebuild, session, workspaceId, snapshotForRebuild, resolvedTargetId);
                        yield break;
                    }

                    Debug.LogWarning($"AuthoringWorkspaceEntry: Snapshot rebuild reported failure for '{workspaceId}'. Falling back to draft-only entry.");
                }
                else
                {
                    Debug.LogWarning("AuthoringWorkspaceEntry: snapshot.json exists but WorkspaceSceneReconstructor is missing; using draft-only entry.");
                }
            }

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
        /// Resolves AR target id: session (switcher) → snapshot.json → draft.
        /// Draft alone is unreliable for UUID workspaces because <see cref="Workspace.MockWorkspaceProvider"/> falls back to the default wall workspace ids.
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

            if (!string.IsNullOrWhiteSpace(resolvedWorkspaceId))
            {
                var repo = new WorkspaceSnapshotRepository();
                if (repo.TryLoadSnapshot(resolvedWorkspaceId.Trim(), out WorkspaceSnapshot snap))
                {
                    string fromSnap = ResolvePrimaryTargetIdFromSnapshot(snap);
                    if (!string.IsNullOrWhiteSpace(fromSnap))
                        return fromSnap.Trim();
                }
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
                if (session != null && string.IsNullOrWhiteSpace(session.targetId))
                    AppFlowController.MarkWorkspaceReady(targetId);
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
            if (session != null && string.IsNullOrWhiteSpace(session.targetId))
                AppFlowController.MarkWorkspaceReady(targetId);

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
            if (!string.IsNullOrWhiteSpace(session.targetImageRelativePath))
                auth.TargetImageLocalPath = session.targetImageRelativePath.Trim();
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

        /// <summary>
        /// After <see cref="WorkspaceSceneReconstructor"/> rebuilds from disk, targets/content already exist — only bind selection, posture, and visuals.
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

            string displayName = workspace?.target != null && !string.IsNullOrWhiteSpace(workspace.target.displayLabel)
                ? workspace.target.displayLabel.Trim()
                : workspace?.target != null && !string.IsNullOrWhiteSpace(workspace.target.targetName)
                    ? workspace.target.targetName.Trim()
                    : (!string.IsNullOrWhiteSpace(snapshot?.workspaceName) ? snapshot.workspaceName.Trim() : targetId);

            float physicalWidthM = ResolvePhysicalWidthMeters(targetId, snapshot, workspace);

            manager.SetActiveTarget(index);
            EnsureAuthoredTargetForPersistence(manager.GetActiveTarget(), targetId, displayName, session, physicalWidthM);
            WorkspaceDomain.WorkspacePosture posture = workspace?.target != null
                ? workspace.target.posture
                : WorkspaceDomain.WorkspacePosture.Wall;
            ApplyWorkspacePreset(manager.GetActiveTarget(), posture);
            string imageUrl = workspace?.target != null ? workspace.target.targetImageUrl : "";
            ApplyWorkspaceTargetVisual(manager.GetActiveTarget(), imageUrl ?? "", session);
            if (session != null && string.IsNullOrWhiteSpace(session.targetId))
                AppFlowController.MarkWorkspaceReady(targetId);

            Debug.Log($"AuthoringWorkspaceEntry: Activated workspace target '{targetId}' from snapshot (index={index}).");
        }

        private void ApplyWorkspacePreset(GameObject targetRootObject, WorkspaceDomain.WorkspacePosture posture)
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
            targetRoot.localPosition = ResolveTargetLocalPositionForPosture(posture);
            targetRoot.localRotation = Quaternion.Euler(preset.target.targetLocalEuler);

            PlacementBoundsService placementBounds = FindFirstObjectByType<PlacementBoundsService>();
            if (placementBounds != null)
            {
                placementBounds.SetTargetContext(targetRoot, targetRoot.Find("ContentRoot"));
                placementBounds.SetPosture(posture);
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

            Debug.Log($"AuthoringWorkspaceEntry: Applied workspace preset posture='{posture}' target='{targetRootObject.name}'.");
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
                    return;
            }
            targetWorkflowService.ApplyTargetImageFromUrl(this, targetObject, targetImageUrl);
        }
    }
}
