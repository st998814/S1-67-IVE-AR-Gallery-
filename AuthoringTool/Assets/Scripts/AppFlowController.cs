using System;
using UnityEngine;

namespace ARGallery.AppFlow
{
    /// <summary>
    /// App-level scene flow and workspace session (in-memory, cloned on read).
    /// Scene changes go through <see cref="SceneTransitionService"/> for fade + debounce guards.
    /// </summary>
    public static class AppFlowController
    {
        public const string LandingSceneName = "LandingScene";
        public const string WorkspaceSwitcherSceneName = "WorkspaceSwitcherScene";
        public const string TargetInstantiationSceneName = "TargetInstantiationScene";
        public const string AuthoringSceneName = "AuthoringToolScene";

        private static WorkspaceSessionContext currentWorkspace;

        public static bool HasWorkspaceSession => currentWorkspace != null;

        /// <summary>Clone of the live session; safe to inspect — do not mutate (changes are ignored).</summary>
        public static WorkspaceSessionContext CurrentWorkspace => currentWorkspace?.Clone();

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

        /// <summary>Returns a clone of the live session. Mutating the out value does not update app state.</summary>
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

        /// <summary>Mutates the live session only. Returns false when no session is active.</summary>
        public static bool TryUpdateWorkspaceSession(Action<WorkspaceSessionContext> mutator)
        {
            if (currentWorkspace == null || mutator == null)
                return false;

            mutator(currentWorkspace);
            return true;
        }

        public static bool TransitionToLanding()
        {
            return SceneTransitionService.TransitionToScene(LandingSceneName);
        }

        public static bool TransitionToWorkspaceSwitcher()
        {
            return SceneTransitionService.TransitionToScene(WorkspaceSwitcherSceneName);
        }

        public static bool TransitionToTargetInstantiation()
        {
            return SceneTransitionService.TransitionToScene(TargetInstantiationSceneName);
        }

        public static bool TransitionToAuthoring()
        {
            return SceneTransitionService.TransitionToScene(AuthoringSceneName);
        }

        /// <summary>Sets session then transitions to authoring (fade). Returns false if transition was rejected.</summary>
        public static bool EnterAuthoringWithWorkspace(WorkspaceSessionContext context)
        {
            SetWorkspaceSession(context);
            return TransitionToAuthoring();
        }

        [Obsolete("Use TransitionToLanding() for fade + transition guard.")]
        public static bool GoToLanding() => TransitionToLanding();

        [Obsolete("Use TransitionToWorkspaceSwitcher() for fade + transition guard.")]
        public static bool GoToWorkspaceSwitcher() => TransitionToWorkspaceSwitcher();

        public static WorkspaceSessionContext BuildNewWorkspaceSession(string workspaceName)
        {
            string normalizedName = string.IsNullOrWhiteSpace(workspaceName)
                ? "New Workspace"
                : workspaceName.Trim();
            string generatedId = Guid.NewGuid().ToString("N");

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

            if (!string.IsNullOrWhiteSpace(targetId))
                currentWorkspace.targetId = targetId.Trim();
            currentWorkspace.setupState = WorkspaceSetupState.Ready;
            currentWorkspace.isNewWorkspace = false;
        }

        public static void SetWorkspaceTargetId(string targetId)
        {
            if (currentWorkspace == null || string.IsNullOrWhiteSpace(targetId))
                return;
            currentWorkspace.targetId = targetId.Trim();
        }

        public static void SetWorkspaceTargetImageUrl(string targetImageUrl)
        {
            if (currentWorkspace == null)
                return;
            currentWorkspace.targetImageUrl = string.IsNullOrWhiteSpace(targetImageUrl) ? "" : targetImageUrl.Trim();
        }

        public static void SetWorkspaceTargetImage(byte[] imageBytes, string fileName = "")
        {
            if (currentWorkspace == null)
                return;
            currentWorkspace.targetImageBytes = imageBytes != null ? (byte[])imageBytes.Clone() : null;
            currentWorkspace.targetImageFileName = fileName ?? "";
        }

        /// <summary>Legacy Layer 2 — local disk paths are no longer used (WebGL / L3-only).</summary>
        [Obsolete("Local target image paths are disabled. Use SetWorkspaceTargetImage(bytes) or target URLs after upload.")]
        public static void SetWorkspaceTargetImageLocalPath(string relativePathWithForwardSlashes)
        {
            if (currentWorkspace == null)
                return;
            currentWorkspace.targetImageRelativePath = "";
        }

        public static void SetWorkspaceVuforiaTargetId(string vuforiaCloudTargetId)
        {
            if (currentWorkspace == null)
                return;
            currentWorkspace.vuforiaTargetId = vuforiaCloudTargetId ?? "";
        }

        public static void SetWorkspaceName(string workspaceName)
        {
            if (currentWorkspace == null)
                return;
            currentWorkspace.workspaceName = string.IsNullOrWhiteSpace(workspaceName)
                ? currentWorkspace.workspaceName
                : workspaceName.Trim();
        }
    }
}
