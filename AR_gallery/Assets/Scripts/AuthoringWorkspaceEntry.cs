using UnityEngine;
using ARGallery.Workspace;

namespace ARGallery.AppFlow
{
    /// <summary>
    /// Authoring scene entry adapter that consumes app-level workspace context
    /// and aligns runtime target selection to a 1:1 workspace-target mapping.
    /// </summary>
    public class AuthoringWorkspaceEntry : MonoBehaviour
    {
        [SerializeField] private bool createMissingTarget = true;
        [SerializeField] private string defaultWorkspaceId = MockWorkspaceProvider.DefaultWorkspaceId;

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
            WorkspaceDraftState draft = LoadWorkspaceDraft(workspaceId);
            if (draft == null || draft.target == null || string.IsNullOrWhiteSpace(draft.target.targetId))
            {
                Debug.LogWarning($"AuthoringWorkspaceEntry: Workspace draft '{workspaceId}' is missing target context.");
                return;
            }

            ApplyWorkspaceContext(draft, session);
        }

        private WorkspaceDraftState LoadWorkspaceDraft(string workspaceId)
        {
            LocalWorkspaceStore store = WorkspaceDataServices.LocalStore;
            IWorkspaceProvider provider = WorkspaceDataServices.Provider;
            WorkspaceDraftState draft = store.GetOrLoad(workspaceId, provider.GetWorkspace);
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
            return MockWorkspaceProvider.DefaultWorkspaceId;
        }

        private void ApplyWorkspaceContext(WorkspaceDraftState workspace, WorkspaceSessionContext session)
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
                    Debug.Log($"AuthoringWorkspaceEntry: Duplicate target resolved by activating index={result.duplicateIndex}.");
                    return;
                }

                Debug.LogWarning($"AuthoringWorkspaceEntry: Failed to create workspace target '{targetId}': {result.message}");
                return;
            }

            int createdIndex = manager.FindTargetIndexById(targetId);
            if (createdIndex >= 0)
                manager.SetActiveTarget(createdIndex);

            // Keep app-flow context aligned with provider-loaded target in mock-first mode.
            if (session != null && string.IsNullOrWhiteSpace(session.targetId))
                AppFlowController.MarkWorkspaceReady(targetId);

            Debug.Log($"AuthoringWorkspaceEntry: Created and activated workspace target '{targetId}'.");
        }
    }
}
