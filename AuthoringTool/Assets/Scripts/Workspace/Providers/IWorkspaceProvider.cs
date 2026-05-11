using System.Collections.Generic;

namespace ARGallery.Workspace
{
    public interface IWorkspaceProvider
    {
        WorkspaceDraftState GetWorkspace(string workspaceId);
        WorkspaceDraftState SaveWorkspace(WorkspaceDraftState workspace);
        IReadOnlyList<WorkspaceDraftState> GetAvailableWorkspaces();
    }
}
