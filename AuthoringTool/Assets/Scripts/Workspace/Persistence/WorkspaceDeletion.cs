using System;
using ARGallery.AppFlow;
using WorkspaceServices = ARGallery.Workspace;

namespace ARGallery.Workspace.Persistence
{
    /// <summary>
    /// Clears in-memory workspace draft cache and app session when a workspace is deleted.
    /// Call <c>DELETE /api/workspaces/&lt;id&gt;</c> first so Postgres rows and upload files stay in sync.
    /// </summary>
    public static class WorkspaceDeletion
    {
        public static bool TryDeleteWorkspaceEverywhere(string workspaceId, out string errorMessage)
        {
            errorMessage = null;
            if (string.IsNullOrWhiteSpace(workspaceId))
            {
                errorMessage = "workspaceId is empty.";
                return false;
            }

            string id = workspaceId.Trim();

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
