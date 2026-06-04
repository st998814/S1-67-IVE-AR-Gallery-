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
            auth.RemoteDirty = true;
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
            string requestedContentId = request != null && !string.IsNullOrWhiteSpace(request.contentIdOverride)
                ? request.contentIdOverride.Trim()
                : "";
            if (!string.IsNullOrWhiteSpace(requestedContentId))
            {
                ac.LocalContentId = requestedContentId;
                ac.ServerContentId = requestedContentId;
            }

            if (string.IsNullOrWhiteSpace(ac.LocalContentId))
                ac.LocalContentId = Guid.NewGuid().ToString("N");
            if (string.IsNullOrWhiteSpace(ac.ServerContentId))
                ac.ServerContentId = ac.LocalContentId;

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
                RetainContentAssetBytes(ac, request);
            }

            ac.RemoteDirty = true;
            AuthoredObjectRegistry.RegisterContent(ac);
            return ac;
        }

        private static void RetainContentAssetBytes(AuthoredContentInstance ac, SpawnRequest request)
        {
            if (ac == null || request == null)
                return;
            if (request.contentType == SpawnContentType.Text)
            {
                ac.AssetBytes = null;
                ac.AssetLocalPath = "";
                return;
            }

            if (request.localFileBytes == null || request.localFileBytes.Length == 0)
            {
                ac.AssetBytes = null;
                ac.AssetLocalPath = "";
                return;
            }

            ac.AssetBytes = PersistenceByteUtility.CloneBytes(request.localFileBytes);
            ac.AssetLocalPath = "";
        }

        /// <summary>
        /// Marks authored content as needing a Layer-3 push (<see cref="WorkspaceRemoteSyncService"/>).
        /// Call after any user-driven change to local position/rotation/scale under a target.
        /// </summary>
        public static void MarkContentRemoteDirty(Transform contentTransform)
        {
            if (contentTransform == null)
                return;

            AuthoredContentInstance ac = contentTransform.GetComponent<AuthoredContentInstance>()
                ?? contentTransform.GetComponentInParent<AuthoredContentInstance>()
                ?? contentTransform.GetComponentInChildren<AuthoredContentInstance>(true);
            if (ac != null)
                ac.RemoteDirty = true;
        }
    }
}
