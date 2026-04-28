using UnityEngine;
using UnityEngine.SceneManagement;

namespace ARGallery.AppFlow
{
    /// <summary>
    /// Minimal app-level scene flow coordinator for:
    /// Landing -> WorkspaceSwitcher -> AuthoringToolScene.
    /// </summary>
    public static class AppFlowController
    {
        public const string LandingSceneName = "LandingScene";
        public const string WorkspaceSwitcherSceneName = "WorkspaceSwitcherScene";
        public const string TargetInstantiationSceneName = "TargetInstantiationScene";
        public const string AuthoringSceneName = "AuthoringToolScene";

        private static WorkspaceSessionContext currentWorkspace;

        public static WorkspaceSessionContext CurrentWorkspace
        {
            get => currentWorkspace?.Clone();
        }

        public static void ClearWorkspaceSession()
        {
            currentWorkspace = null;
        }

        public static void SetWorkspaceSession(WorkspaceSessionContext context)
        {
            if (context == null)
            {
                currentWorkspace = null;
                return;
            }

            currentWorkspace = context.Clone();
        }

        public static bool TryGetWorkspaceSession(out WorkspaceSessionContext context)
        {
            if (currentWorkspace == null)
            {
                context = null;
                return false;
            }

            context = currentWorkspace.Clone();
            return true;
        }

        public static void GoToLanding()
        {
            SceneManager.LoadScene(LandingSceneName);
        }

        public static void GoToWorkspaceSwitcher()
        {
            SceneManager.LoadScene(WorkspaceSwitcherSceneName);
        }

        public static void EnterAuthoringWithWorkspace(WorkspaceSessionContext context)
        {
            SetWorkspaceSession(context);
            SceneManager.LoadScene(AuthoringSceneName);
        }

        public static WorkspaceSessionContext BuildNewWorkspaceSession(string workspaceName)
        {
            string normalizedName = string.IsNullOrWhiteSpace(workspaceName)
                ? "New Workspace"
                : workspaceName.Trim();
            string generatedId = System.Guid.NewGuid().ToString("N");

            return new WorkspaceSessionContext
            {
                workspaceId = generatedId,
                workspaceName = normalizedName,
                targetId = "",
                isNewWorkspace = true,
                setupState = WorkspaceSetupState.PendingTargetSetup
            };
        }

        public static void MarkWorkspaceReady(string targetId)
        {
            if (currentWorkspace == null)
                return;

            currentWorkspace.targetId = string.IsNullOrWhiteSpace(targetId) ? currentWorkspace.targetId : targetId.Trim();
            currentWorkspace.setupState = WorkspaceSetupState.Ready;
        }
    }
}
