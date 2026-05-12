using System;
using ARGallery.AppFlow;
using ARGallery.Spawning;
using UnityEngine;

namespace ARGallery.Workspace.Persistence
{
    /// <summary>
    /// Ensures <see cref="AuthoredTargetInstance"/> / <see cref="AuthoredContentInstance"/> exist on spawned objects
    /// so <see cref="WorkspaceStateSerializer"/> can persist them (registry alone is not enough without these components).
    /// </summary>
    public static class WorkspaceAuthoredAttach
    {
        public static AuthoredTargetInstance EnsureTarget(GameObject targetRoot, string targetId, string targetDisplayName = "")
        {
            if (targetRoot == null || string.IsNullOrWhiteSpace(targetId))
                return null;

            string id = targetId.Trim();
            AuthoredTargetInstance auth = targetRoot.GetComponent<AuthoredTargetInstance>() ?? targetRoot.AddComponent<AuthoredTargetInstance>();
            auth.LocalTargetId = id;
            auth.ServerTargetId = id;
            auth.TargetName = string.IsNullOrWhiteSpace(targetDisplayName) ? id : targetDisplayName.Trim();
            AuthoredObjectRegistry.RegisterTarget(auth);
            return auth;
        }

        public static AuthoredContentInstance EnsureContent(
            GameObject spawnedRoot,
            string resolvedTargetId,
            SpawnContentType contentType,
            SpawnRenderKind renderKind,
            SpawnRequest request)
        {
            if (spawnedRoot == null || string.IsNullOrWhiteSpace(resolvedTargetId))
                return null;

            AuthoredContentInstance ac = spawnedRoot.GetComponent<AuthoredContentInstance>() ?? spawnedRoot.AddComponent<AuthoredContentInstance>();
            if (string.IsNullOrWhiteSpace(ac.LocalContentId))
                ac.LocalContentId = Guid.NewGuid().ToString("N");

            ac.TargetId = resolvedTargetId.Trim();
            ac.ContentType = WorkspaceStateSerializer.ToSnapshotContentTypeLabel(contentType);
            ac.RenderKind = WorkspaceStateSerializer.ToSnapshotRenderKindLabel(renderKind);

            if (request != null)
            {
                ac.OriginalFileName = request.originalFileName ?? "";
                ac.MediaUrl = request.mediaUrl ?? "";
                ac.TextBody = request.contentType == SpawnContentType.Text ? (request.textPayload ?? "") : "";
                ac.Title = string.IsNullOrWhiteSpace(request.originalFileName)
                    ? (request.contentType == SpawnContentType.Text ? ac.TextBody : ac.TargetId)
                    : request.originalFileName.Trim();
            }

            TryPersistLocalContentAsset(ac, request);

            AuthoredObjectRegistry.RegisterContent(ac);
            return ac;
        }

        /// <summary>
        /// Copies <see cref="SpawnRequest.localFileBytes"/> under persistentDataPath so snapshot.json can reference a stable <see cref="AuthoredContentInstance.AssetLocalPath"/>.
        /// </summary>
        private static void TryPersistLocalContentAsset(AuthoredContentInstance ac, SpawnRequest request)
        {
            if (ac == null || request == null)
                return;
            if (request.contentType == SpawnContentType.Text)
                return;
            if (request.localFileBytes == null || request.localFileBytes.Length == 0)
                return;
            if (!string.IsNullOrWhiteSpace(ac.AssetLocalPath))
                return;

            if (!AppFlowController.TryGetWorkspaceSession(out WorkspaceSessionContext session) || session == null
                || string.IsNullOrWhiteSpace(session.workspaceId))
            {
                Debug.LogWarning("[WorkspacePersistence] TryPersistLocalContentAsset: no workspace session — content bytes not copied to disk.");
                return;
            }

            string workspaceId = session.workspaceId.Trim();
            string originalName = string.IsNullOrWhiteSpace(request.originalFileName) ? "content.bin" : request.originalFileName.Trim();
            string hint = WorkspaceStateSerializer.ToSnapshotContentTypeLabel(request.contentType);

            var repo = new WorkspaceAssetRepository();
            if (!repo.TryImportContentAsset(workspaceId, originalName, hint, request.localFileBytes, "", out string relativePath, out string error))
            {
                Debug.LogWarning($"[WorkspacePersistence] TryImportContentAsset failed: {error}");
                return;
            }

            ac.AssetLocalPath = relativePath;
        }
    }
}
