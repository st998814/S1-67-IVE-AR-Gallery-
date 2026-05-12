using System;
using System.Collections.Generic;

namespace ARGallery.Workspace
{
    /// <summary>
    /// In-memory workspace draft cache for authoring scene development.
    /// Keeps local unsaved changes while switching workspaces.
    /// </summary>
    public class LocalWorkspaceStore
    {
        private readonly Dictionary<string, WorkspaceDraftState> cache = new Dictionary<string, WorkspaceDraftState>(StringComparer.OrdinalIgnoreCase);

        public WorkspaceDraftState GetOrLoad(string workspaceId, Func<string, WorkspaceDraftState> loader)
        {
            if (string.IsNullOrWhiteSpace(workspaceId))
                return null;

            string key = workspaceId.Trim();
            if (cache.TryGetValue(key, out WorkspaceDraftState existing) && existing != null)
                return existing.Clone();

            WorkspaceDraftState loaded = loader?.Invoke(key);
            if (loaded == null)
                return null;

            cache[key] = loaded.Clone();
            return loaded.Clone();
        }

        public void UpdateWorkspace(WorkspaceDraftState workspace, bool markDirty = true)
        {
            if (workspace == null || string.IsNullOrWhiteSpace(workspace.workspaceId))
                return;

            WorkspaceDraftState clone = workspace.Clone();
            if (markDirty)
            {
                clone.isDirty = true;
                clone.localModifiedAtUtc = DateTime.UtcNow.ToString("o");
            }

            cache[clone.workspaceId] = clone;
        }

        public WorkspaceDraftState GetWorkspaceSnapshot(string workspaceId)
        {
            if (string.IsNullOrWhiteSpace(workspaceId))
                return null;
            if (!cache.TryGetValue(workspaceId.Trim(), out WorkspaceDraftState workspace) || workspace == null)
                return null;
            return workspace.Clone();
        }

        public IReadOnlyList<WorkspaceDraftState> GetDirtyWorkspaceSnapshots()
        {
            var snapshots = new List<WorkspaceDraftState>();
            foreach (WorkspaceDraftState workspace in cache.Values)
            {
                if (workspace != null && workspace.isDirty)
                    snapshots.Add(workspace.Clone());
            }

            return snapshots;
        }

        public void MarkSaved(string workspaceId)
        {
            if (string.IsNullOrWhiteSpace(workspaceId))
                return;

            string key = workspaceId.Trim();
            if (!cache.TryGetValue(key, out WorkspaceDraftState workspace) || workspace == null)
                return;

            workspace.isDirty = false;
            workspace.localModifiedAtUtc = DateTime.UtcNow.ToString("o");
            cache[key] = workspace;
        }

        /// <summary>Removes cached draft for this id (e.g. after deleting workspace from disk).</summary>
        public bool TryRemoveFromCache(string workspaceId)
        {
            if (string.IsNullOrWhiteSpace(workspaceId))
                return false;
            return cache.Remove(workspaceId.Trim());
        }
    }
}
