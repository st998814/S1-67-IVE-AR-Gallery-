using System;
using ARGallery.AppFlow;
using WorkspaceServices = ARGallery.Workspace;

namespace ARGallery.Workspace.Persistence
{
    /// <summary>
    /// Deletes a workspace from both persistence layers: on-disk snapshot/assets/index and in-memory <see cref="Workspace.LocalWorkspaceStore"/> draft cache.
    /// Does not remove mock-provider seed definitions (those are code); deleting a seeded id only clears disk/cache/session when present.
    /// </summary>
    public static class WorkspaceDeletion
    {
        /// <returns>False when disk delete fails; cache/session cleanup still attempted after successful disk delete.</returns>
        public static bool TryDeleteWorkspaceEverywhere(string workspaceId, out string errorMessage)
        {
            errorMessage = null;
            if (string.IsNullOrWhiteSpace(workspaceId))
            {
                errorMessage = "workspaceId is empty.";
                return false;
            }

            string id = workspaceId.Trim();

            var snapshotRepo = new WorkspaceSnapshotRepository();
            if (!snapshotRepo.TryDeleteWorkspace(id, out string diskError))
            {
                errorMessage = diskError;
                return false;
            }

            WorkspaceServices.WorkspaceDataServices.RemoveCachedWorkspaceDraft(id);

            if (AppFlowController.TryGetWorkspaceSession(out WorkspaceSessionContext session) && session != null
                && string.Equals(session.workspaceId, id, StringComparison.OrdinalIgnoreCase))
                AppFlowController.ClearWorkspaceSession();

            return true;
        }
    }
}
