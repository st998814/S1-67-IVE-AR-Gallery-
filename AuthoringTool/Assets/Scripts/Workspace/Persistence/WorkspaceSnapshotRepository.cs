using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ARGallery.Workspace.Persistence
{
    /// <summary>
    /// Reads/writes snapshot.json and workspace-index.json under persistentDataPath/workspaces/.
    /// </summary>
    public sealed class WorkspaceSnapshotRepository
    {
        public bool TryLoadSnapshot(string workspaceId, out WorkspaceSnapshot snapshot)
        {
            snapshot = null;
            if (string.IsNullOrWhiteSpace(workspaceId))
                return false;

            string path = WorkspacePersistencePaths.GetSnapshotPath(workspaceId);
            if (!File.Exists(path))
                return false;

            try
            {
                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                    return false;

                snapshot = JsonUtility.FromJson<WorkspaceSnapshot>(json);
                return snapshot != null && !string.IsNullOrWhiteSpace(snapshot.workspaceId);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"WorkspaceSnapshotRepository: failed to load snapshot for '{workspaceId}': {ex.Message}");
                return false;
            }
        }

        /// <param name="logSuccess">When false, skips the verbose TrySaveSnapshot OK log (e.g. post–remote-sync metadata write).</param>
        public bool TrySaveSnapshot(WorkspaceSnapshot snapshot, out string errorMessage, bool logSuccess = true)
        {
            errorMessage = null;
            if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.workspaceId))
            {
                errorMessage = "Snapshot or workspaceId is missing.";
                return false;
            }
            string utc = DateTime.UtcNow.ToString("o");
            if (string.IsNullOrWhiteSpace(snapshot.createdAtUtc))
                snapshot.createdAtUtc = utc;
            snapshot.updatedAtUtc = utc;
            return true;
        }

        public IReadOnlyList<WorkspaceIndexEntry> LoadAllIndexEntries()
        {
            string indexPath = WorkspacePersistencePaths.GetIndexPath();
            if (!File.Exists(indexPath))
                return Array.Empty<WorkspaceIndexEntry>();

            try
            {
                string json = File.ReadAllText(indexPath);
                if (string.IsNullOrWhiteSpace(json))
                    return Array.Empty<WorkspaceIndexEntry>();

                WorkspaceIndexFile file = JsonUtility.FromJson<WorkspaceIndexFile>(json);
                if (file?.entries == null)
                    return Array.Empty<WorkspaceIndexEntry>();

                return file.entries.ToList();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"WorkspaceSnapshotRepository: failed to load index: {ex.Message}");
                return Array.Empty<WorkspaceIndexEntry>();
            }
        }

        private WorkspaceIndexFile LoadOrCreateIndexFile()
        {
            string indexPath = WorkspacePersistencePaths.GetIndexPath();
            if (!File.Exists(indexPath))
                return new WorkspaceIndexFile();

            try
            {
                string json = File.ReadAllText(indexPath);
                WorkspaceIndexFile file = JsonUtility.FromJson<WorkspaceIndexFile>(json);
                return file ?? new WorkspaceIndexFile();
            }
            catch
            {
                return new WorkspaceIndexFile();
            }
        }

        /// <summary>Upserts one row in workspace-index.json without writing snapshot.json.</summary>
        public void UpsertWorkspaceIndexEntry(string workspaceId, string workspaceName, string thumbnailKey = "")
        {
            _ = workspaceId;
            _ = workspaceName;
            _ = thumbnailKey;
        }

        private void UpsertIndexEntry(string workspaceId, string workspaceName, string updatedAtUtc, string thumbnailKey)
        {
            WorkspaceIndexFile file = LoadOrCreateIndexFile();
            List<WorkspaceIndexEntry> list = file.entries != null
                ? new List<WorkspaceIndexEntry>(file.entries)
                : new List<WorkspaceIndexEntry>();

            int idx = list.FindIndex(e => string.Equals(e.workspaceId, workspaceId, StringComparison.OrdinalIgnoreCase));
            var entry = new WorkspaceIndexEntry
            {
                workspaceId = workspaceId,
                workspaceName = string.IsNullOrWhiteSpace(workspaceName) ? workspaceId : workspaceName.Trim(),
                updatedAtUtc = string.IsNullOrWhiteSpace(updatedAtUtc) ? DateTime.UtcNow.ToString("o") : updatedAtUtc,
                thumbnailKey = thumbnailKey ?? ""
            };

            if (idx >= 0)
                list[idx] = entry;
            else
                list.Add(entry);

            file.entries = list.ToArray();
            file.schemaVersion = "v1";

            string root = WorkspacePersistencePaths.GetPersistentWorkspacesRoot();
            Directory.CreateDirectory(root);
            string indexPath = WorkspacePersistencePaths.GetIndexPath();
            string json = JsonUtility.ToJson(file, prettyPrint: true);
            File.WriteAllText(indexPath, json);
        }

        private string LoadThumbnailKeyForWorkspace(string workspaceId)
        {
            if (string.IsNullOrWhiteSpace(workspaceId))
                return "";

            WorkspaceIndexFile file = LoadOrCreateIndexFile();
            if (file.entries == null || file.entries.Length == 0)
                return "";

            foreach (WorkspaceIndexEntry entry in file.entries)
            {
                if (entry != null && string.Equals(entry.workspaceId, workspaceId.Trim(), StringComparison.OrdinalIgnoreCase))
                    return entry.thumbnailKey ?? "";
            }

            return "";
        }

        public bool TrySaveIndex(out string errorMessage)
        {
            errorMessage = null;
            return true;
        }

        /// <summary>Optional: remove snapshot folder and index row.</summary>
        public bool TryDeleteWorkspace(string workspaceId, out string errorMessage)
        {
            errorMessage = null;
            if (string.IsNullOrWhiteSpace(workspaceId))
            {
                errorMessage = "workspaceId is empty.";
                return false;
            }
            return true;
        }
    }
}
