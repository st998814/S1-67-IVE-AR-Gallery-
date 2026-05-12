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
            relativePathOut = null;
            string workspaceRoot = WorkspacePersistencePaths.GetWorkspaceRoot(workspaceId);
            string targetsDir = WorkspacePersistencePaths.GetTargetsAssetsDirectory(workspaceId);

            byte[] data = ResolveBytes(bytes, sourcePath, out errorMessage);
            if (data == null || data.Length == 0)
            {
                if (string.IsNullOrEmpty(errorMessage))
                    errorMessage = "Target image data is empty.";
                return false;
            }

            try
            {
                Directory.CreateDirectory(workspaceRoot);
                Directory.CreateDirectory(targetsDir);

                string ext = GetExtension(originalFileName, sourcePath);
                string fileName = $"{Guid.NewGuid():N}{ext}";
                string absoluteFile = Path.Combine(targetsDir, fileName);
                File.WriteAllBytes(absoluteFile, data);

                relativePathOut = NormalizeRelativePath(
                    $"{WorkspacePersistencePaths.AssetsFolderName}/{WorkspacePersistencePaths.TargetsAssetsFolderName}/{fileName}");
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
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
            out string errorMessage)
        {
            relativePathOut = null;
            string workspaceRoot = WorkspacePersistencePaths.GetWorkspaceRoot(workspaceId);
            string contentsDir = WorkspacePersistencePaths.GetContentsAssetsDirectory(workspaceId);

            byte[] data = ResolveBytes(bytes, sourcePath, out errorMessage);
            if (data == null || data.Length == 0)
            {
                if (string.IsNullOrEmpty(errorMessage))
                    errorMessage = "Content asset data is empty.";
                return false;
            }

            try
            {
                Directory.CreateDirectory(workspaceRoot);
                Directory.CreateDirectory(contentsDir);

                string ext = GetExtension(originalFileName, sourcePath);
                string fileName = $"{Guid.NewGuid():N}{ext}";
                string absoluteFile = Path.Combine(contentsDir, fileName);
                File.WriteAllBytes(absoluteFile, data);

                relativePathOut = NormalizeRelativePath(
                    $"{WorkspacePersistencePaths.AssetsFolderName}/{WorkspacePersistencePaths.ContentsAssetsFolderName}/{fileName}");

                _ = contentTypeHint;
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        public string ResolveFullPath(string workspaceId, string relativePathWithForwardSlashes)
        {
            return WorkspacePersistencePaths.ResolveRelativeToWorkspaceRoot(workspaceId, relativePathWithForwardSlashes);
        }

        public bool Exists(string workspaceId, string relativePathWithForwardSlashes)
        {
            return WorkspacePersistencePaths.ExistsRelative(workspaceId, relativePathWithForwardSlashes);
        }
    }
}
