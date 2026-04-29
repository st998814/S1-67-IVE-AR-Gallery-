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
        [SerializeField] private bool showOrientationHelper = true;
        [SerializeField] private float orientationHelperAxisLength = 0.35f;
        [SerializeField] private float orientationHelperAxisThickness = 0.01f;

        private readonly TargetWorkflowService targetWorkflowService = new TargetWorkflowService();

        private void Start()
        {
            WorkspaceSessionContext session = null;
            bool hasSession = AppFlowController.TryGetWorkspaceSession(out session) && session != null;

            if (hasSession && !session.IsReadyForAuthoring())
            {
                Debug.Log("AuthoringWorkspaceEntry: Workspace setup is pending. Authoring entry is blocked.");
                if (!SceneTransitionService.IsTransitioning)
                    SceneTransitionService.TransitionToScene(AppFlowController.TargetInstantiationSceneName);
                return;
            }

            string workspaceId = ResolveWorkspaceId(session);
            WorkspaceDomain.WorkspaceDraftState draft = LoadWorkspaceDraft(workspaceId);
            if (draft == null || draft.target == null || string.IsNullOrWhiteSpace(draft.target.targetId))
            {
                Debug.LogWarning($"AuthoringWorkspaceEntry: Workspace draft '{workspaceId}' is missing target context.");
                return;
            }

            ApplyWorkspaceContext(draft, session);
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

        private void ApplyWorkspaceContext(WorkspaceDomain.WorkspaceDraftState workspace, WorkspaceSessionContext session)
        {
            TargetSelectionManager manager = FindFirstObjectByType<TargetSelectionManager>();
            if (manager == null)
            {
                Debug.LogWarning("AuthoringWorkspaceEntry: TargetSelectionManager not found; cannot apply workspace target context.");
                return;
            }

            string targetId = workspace.target.targetId.Trim();
            string targetName = !string.IsNullOrWhiteSpace(workspace.target.displayLabel)
                ? workspace.target.displayLabel.Trim()
                : (!string.IsNullOrWhiteSpace(workspace.target.targetName) ? workspace.target.targetName.Trim() : "WorkspaceTarget");

            int index = manager.FindTargetIndexById(targetId);
            if (index >= 0)
            {
                manager.SetActiveTarget(index);
                ApplyWorkspacePreset(manager.GetActiveTarget(), workspace.target.posture);
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
                targetName);

            if (!result.success)
            {
                if (result.isDuplicate && result.duplicateIndex >= 0)
                {
                    manager.SetActiveTarget(result.duplicateIndex);
                    ApplyWorkspacePreset(manager.GetActiveTarget(), workspace.target.posture);
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
                ApplyWorkspacePreset(manager.GetActiveTarget(), workspace.target.posture);
            }

            // Keep app-flow context aligned with provider-loaded target in mock-first mode.
            if (session != null && string.IsNullOrWhiteSpace(session.targetId))
                AppFlowController.MarkWorkspaceReady(targetId);

            Debug.Log($"AuthoringWorkspaceEntry: Created and activated workspace target '{targetId}'.");
        }

        private void ApplyWorkspacePreset(GameObject targetRootObject, WorkspaceDomain.WorkspacePosture posture)
        {
            if (targetRootObject == null)
            {
                Debug.LogWarning("AuthoringWorkspaceEntry: Cannot apply preset because target root is null.");
                return;
            }

            EnsureTargetHierarchyCompatibility(targetRootObject.transform);

            WorkspacePresets.WorkspacePreset preset = WorkspacePresets.WorkspacePresetLibrary.GetPreset(posture);
            Transform targetRoot = targetRootObject.transform;
            targetRoot.localRotation = Quaternion.Euler(preset.target.targetLocalEuler);
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
    }
}
