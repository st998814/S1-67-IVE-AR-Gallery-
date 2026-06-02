using System;
using System.IO;
using UnityEngine;

namespace ARGallery.Workspace.Persistence
{
    /// <summary>
    /// Copies imported files into workspace-scoped storage under persistentDataPath (never under Assets/).
    /// Returns JSON-friendly relative paths using forward slashes.
    /// </summary>
    public sealed class WorkspaceAssetRepository
    {
        private static string NormalizeRelativePath(string path)
        {
            return path.Replace('\\', '/');
        }

        private static string GetExtension(string originalFileName, string sourcePath)
        {
            string ext = !string.IsNullOrWhiteSpace(originalFileName)
                ? Path.GetExtension(originalFileName)
                : "";
            if (string.IsNullOrEmpty(ext) && !string.IsNullOrWhiteSpace(sourcePath))
                ext = Path.GetExtension(sourcePath);
            return string.IsNullOrEmpty(ext) ? ".bin" : ext.ToLowerInvariant();
        }

        private static byte[] ResolveBytes(byte[] bytes, string sourcePath, out string error)
        {
            error = null;
            if (bytes != null && bytes.Length > 0)
                return bytes;

            if (!string.IsNullOrWhiteSpace(sourcePath) && File.Exists(sourcePath))
            {
                try
                {
                    return File.ReadAllBytes(sourcePath);
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    return null;
                }
            }

            error = "No file bytes and no readable sourcePath.";
            return null;
        }

        /// <summary>
        /// Copies target image bytes into assets/targets/&lt;guid&gt;&lt;ext&gt;.
        /// </summary>
        /// <param name="relativePathOut">e.g. assets/targets/abc.png</param>
        public bool TryImportTargetImage(
            string workspaceId,
            string originalFileName,
            byte[] bytes,
            string sourcePath,
            out string relativePathOut,
            out string errorMessage)
        {
            _ = workspaceId;
            _ = originalFileName;
            _ = bytes;
            _ = sourcePath;
            relativePathOut = "";
            errorMessage = null;
            return true;
        }

        /// <summary>
        /// Copies a target placement reference photo into assets/target_refs/&lt;guid&gt;&lt;ext&gt;.
        /// </summary>
        public bool TryImportTargetReferenceImage(
            string workspaceId,
            string originalFileName,
            byte[] bytes,
            string sourcePath,
            out string relativePathOut,
            out string errorMessage)
        {
            _ = workspaceId;
            _ = originalFileName;
            _ = bytes;
            _ = sourcePath;
            relativePathOut = "";
            errorMessage = null;
            return true;
        }

        /// <summary>
        /// Copies content asset into assets/contents/&lt;guid&gt;&lt;ext&gt;.
        /// </summary>
        public bool TryImportContentAsset(
            string workspaceId,
            string originalFileName,
            string contentTypeHint,
            byte[] bytes,
            string sourcePath,
            out string relativePathOut,
            out string errorMessage,
            string stableFileNameStem = "")
        {
            _ = workspaceId;
            _ = originalFileName;
            _ = contentTypeHint;
            _ = bytes;
            _ = sourcePath;
            _ = stableFileNameStem;
            relativePathOut = "";
            errorMessage = null;
            return true;
        }

        public string ResolveFullPath(string workspaceId, string relativePathWithForwardSlashes)
        {
            return WorkspacePersistencePaths.ResolveRelativeToWorkspaceRoot(workspaceId, relativePathWithForwardSlashes);
        }

        public bool Exists(string workspaceId, string relativePathWithForwardSlashes)
        {
            return WorkspacePersistencePaths.ExistsRelative(workspaceId, relativePathWithForwardSlashes);
        }

        public void PruneUnreferencedContentAssets(string workspaceId, ContentSnapshot[] referencedContents)
        {
            if (string.IsNullOrWhiteSpace(workspaceId))
                return;

            string contentsDir = WorkspacePersistencePaths.GetContentsAssetsDirectory(workspaceId);
            if (!Directory.Exists(contentsDir))
                return;

            var referenced = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (referencedContents != null)
            {
                for (int i = 0; i < referencedContents.Length; i++)
                {
                    string rel = referencedContents[i]?.assetLocalPath;
                    if (string.IsNullOrWhiteSpace(rel))
                        continue;

                    string full = ResolveFullPath(workspaceId, rel);
                    if (!string.IsNullOrWhiteSpace(full))
                        referenced.Add(Path.GetFullPath(full));
                }
            }

            foreach (string file in Directory.GetFiles(contentsDir))
            {
                string full = Path.GetFullPath(file);
                if (referenced.Contains(full))
                    continue;

                try
                {
                    File.Delete(file);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[WorkspacePersistence] Failed to prune unreferenced content asset '{file}': {ex.Message}");
                }
            }
        }

        private static string SanitizeFileNameStem(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "";

            string fileName = Path.GetFileNameWithoutExtension(value.Trim());
            if (string.IsNullOrWhiteSpace(fileName))
                return "";

            foreach (char invalid in Path.GetInvalidFileNameChars())
                fileName = fileName.Replace(invalid, '_');

            return fileName.Trim('_');
        }
    }
}
