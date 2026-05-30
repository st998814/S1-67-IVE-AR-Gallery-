using System;
using ARGallery.AppFlow;
using WorkspaceServices = ARGallery.Workspace;

namespace ARGallery.Workspace.Persistence
{
    /// <summary>
    /// Deletes local persistence: workspace folder (including <c>snapshot.json</c> and assets), index row, and in-memory draft cache.
    /// Call <c>DELETE /api/workspaces/&lt;id&gt;</c> first when using the backend so Postgres rows and upload files stay in sync.
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

            if (WorkspaceServices.WorkspaceDataServices.Provider is global::ARGallery.Workspace.MockWorkspaceProvider mock)
                mock.OnDeletedFromDisk(id);

            if (AppFlowController.TryGetWorkspaceSession(out WorkspaceSessionContext session) && session != null
                && string.Equals(session.workspaceId, id, StringComparison.OrdinalIgnoreCase))
                AppFlowController.ClearWorkspaceSession();

            return true;
        }
    }
}
