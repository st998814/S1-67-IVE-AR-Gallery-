using UnityEngine;

namespace ARGallery.AppFlow
{
    /// <summary>
    /// Authoring scene entry adapter that consumes app-level workspace context
    /// and aligns runtime target selection to a 1:1 workspace-target mapping.
    /// </summary>
    public class AuthoringWorkspaceEntry : MonoBehaviour
    {
        [SerializeField] private bool createMissingTarget = true;

        private readonly TargetWorkflowService targetWorkflowService = new TargetWorkflowService();

        private void Start()
        {
            if (!AppFlowController.TryGetWorkspaceSession(out WorkspaceSessionContext workspace) || workspace == null)
            {
                Debug.Log("AuthoringWorkspaceEntry: No workspace session provided; keeping current authoring target state.");
                return;
            }

            if (!workspace.IsReadyForAuthoring())
            {
                Debug.Log("AuthoringWorkspaceEntry: Workspace setup is pending. Authoring entry is blocked.");
                if (!SceneTransitionService.IsTransitioning)
                    SceneTransitionService.TransitionToScene(AppFlowController.TargetInstantiationSceneName);
                return;
            }

            ApplyWorkspaceContext(workspace);
        }

        private void ApplyWorkspaceContext(WorkspaceSessionContext workspace)
        {
            TargetSelectionManager manager = FindFirstObjectByType<TargetSelectionManager>();
            if (manager == null)
            {
                Debug.LogWarning("AuthoringWorkspaceEntry: TargetSelectionManager not found; cannot apply workspace target context.");
                return;
            }

            string targetId = string.IsNullOrWhiteSpace(workspace.targetId)
                ? workspace.workspaceId
                : workspace.targetId.Trim();

            int index = manager.FindTargetIndexById(targetId);
            if (index >= 0)
            {
                manager.SetActiveTarget(index);
                Debug.Log($"AuthoringWorkspaceEntry: Activated workspace target '{targetId}' (index={index}).");
                return;
            }

            if (!createMissingTarget)
            {
                Debug.LogWarning($"AuthoringWorkspaceEntry: Workspace target '{targetId}' not found and auto-create is disabled.");
                return;
            }

            string targetName = string.IsNullOrWhiteSpace(workspace.workspaceName)
                ? "WorkspaceTarget"
                : workspace.workspaceName.Trim();

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

            Debug.Log($"AuthoringWorkspaceEntry: Created and activated workspace target '{targetId}'.");
        }
    }
}
