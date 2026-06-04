using System;
using System.IO;
using UnityEngine;

namespace ARGallery.Workspace.Persistence
{
    /// <summary>
    /// Layout under Application.persistentDataPath:
    /// workspaces/workspace-index.json
    /// workspaces/&lt;workspaceId&gt;/snapshot.json
    /// workspaces/&lt;workspaceId&gt;/assets/targets/
    /// workspaces/&lt;workspaceId&gt;/assets/contents/
    /// </summary>
    public static class WorkspacePersistencePaths
    {
        public const string WorkspacesFolderName = "workspaces";
        public const string SnapshotFileName = "snapshot.json";
        public const string IndexFileName = "workspace-index.json";
        public const string AssetsFolderName = "assets";
        public const string TargetsAssetsFolderName = "targets";
        public const string TargetReferencesAssetsFolderName = "target_refs";
        public const string ContentsAssetsFolderName = "contents";

        public static string GetPersistentWorkspacesRoot()
        {
            return Path.Combine(Application.persistentDataPath, WorkspacesFolderName);
        }

        public static string GetWorkspaceRoot(string workspaceId)
        {
            return Path.Combine(GetPersistentWorkspacesRoot(), SanitizeWorkspaceSegment(workspaceId));
        }

        public static string GetSnapshotPath(string workspaceId)
        {
            return Path.Combine(GetWorkspaceRoot(workspaceId), SnapshotFileName);
        }

        public static string GetIndexPath()
        {
            return Path.Combine(GetPersistentWorkspacesRoot(), IndexFileName);
        }

        public static string GetTargetsAssetsDirectory(string workspaceId)
        {
            return Path.Combine(GetWorkspaceRoot(workspaceId), AssetsFolderName, TargetsAssetsFolderName);
        }

        public static string GetContentsAssetsDirectory(string workspaceId)
        {
            return Path.Combine(GetWorkspaceRoot(workspaceId), AssetsFolderName, ContentsAssetsFolderName);
        }

        public static string GetTargetReferencesAssetsDirectory(string workspaceId)
        {
            return Path.Combine(GetWorkspaceRoot(workspaceId), AssetsFolderName, TargetReferencesAssetsFolderName);
        }

        /// <summary>Combine workspace root with a slash-separated relative path stored in JSON.</summary>
        public static string ResolveRelativeToWorkspaceRoot(string workspaceId, string relativePathWithForwardSlashes)
        {
            if (string.IsNullOrWhiteSpace(relativePathWithForwardSlashes))
                return null;

            try
            {
                string trimmed = relativePathWithForwardSlashes.Trim().Replace('/', Path.DirectorySeparatorChar);
                if (Path.IsPathRooted(trimmed))
                    return null;
                if (trimmed.Contains(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                    || trimmed.StartsWith("..", StringComparison.Ordinal))
                    return null;

                string workspaceRoot = Path.GetFullPath(GetWorkspaceRoot(workspaceId));
                string combined = Path.GetFullPath(Path.Combine(workspaceRoot, trimmed));

                string relative = Path.GetRelativePath(workspaceRoot, combined);
                if (relative.StartsWith("..", StringComparison.Ordinal))
                    return null;

                return combined;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        public static bool ExistsRelative(string workspaceId, string relativePathWithForwardSlashes)
        {
            string full = ResolveRelativeToWorkspaceRoot(workspaceId, relativePathWithForwardSlashes);
            return !string.IsNullOrEmpty(full) && File.Exists(full);
        }

        public static string SanitizeWorkspaceSegment(string workspaceId)
        {
            if (string.IsNullOrWhiteSpace(workspaceId))
                return "_";

            string trimmed = workspaceId.Trim();
            foreach (char c in Path.GetInvalidFileNameChars())
                trimmed = trimmed.Replace(c, '_');

            return string.IsNullOrEmpty(trimmed) ? "_" : trimmed;
        }
    }
}
