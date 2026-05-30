using System;
using System.Collections.Generic;
using System.Linq;
using ARGallery.Spawning;
using UnityEngine;

namespace ARGallery.Workspace.Persistence
{
    /// <summary>
    /// Builds <see cref="WorkspaceSnapshot"/> from session metadata and <see cref="AuthoredObjectRegistry"/> instances.
    /// Uses local transforms on each instance's <see cref="Transform"/> (target-centric content under ContentRoot).
    /// </summary>
    public static class WorkspaceStateSerializer
    {
        /// <param name="existingForTimestamps">When saving repeatedly, pass previous snapshot to preserve <see cref="WorkspaceSnapshot.createdAtUtc"/>.</param>
        public static WorkspaceSnapshot BuildSnapshot(
            string workspaceId,
            string workspaceName,
            AuthoredObjectRegistry registry,
            WorkspaceSnapshot existingForTimestamps = null)
        {
            string utcNow = DateTime.UtcNow.ToString("o");
            var snapshot = new WorkspaceSnapshot
            {
                schemaVersion = "v1",
                workspaceId = workspaceId ?? "",
                workspaceName = workspaceName ?? "",
                updatedAtUtc = utcNow,
                createdAtUtc = existingForTimestamps != null && !string.IsNullOrWhiteSpace(existingForTimestamps.createdAtUtc)
                    ? existingForTimestamps.createdAtUtc
                    : utcNow
            };

            MergeWorkspaceRemoteFieldsFromExisting(snapshot, existingForTimestamps);

            if (registry == null)
            {
                snapshot.targets = Array.Empty<TargetSnapshot>();
                snapshot.contents = Array.Empty<ContentSnapshot>();
                return snapshot;
            }

            snapshot.targets = BuildTargetSnapshots(registry);
            snapshot.contents = BuildContentSnapshots(registry);
            return snapshot;
        }

        /// <summary>
        /// Preserves Layer 3 workspace-level fields across local rebuilds so autosave does not wipe sync metadata.
        /// </summary>
        private static void MergeWorkspaceRemoteFieldsFromExisting(WorkspaceSnapshot snapshot, WorkspaceSnapshot existing)
        {
            if (snapshot == null || existing == null)
                return;

            snapshot.remoteDirty = existing.remoteDirty;
            snapshot.lastRemoteSyncedAtUtc = existing.lastRemoteSyncedAtUtc ?? "";
            snapshot.lastRemoteSyncError = existing.lastRemoteSyncError ?? "";
            snapshot.remoteSyncStatus = string.IsNullOrWhiteSpace(existing.remoteSyncStatus)
                ? RemoteSyncStatus.LocalOnly
                : existing.remoteSyncStatus;
        }

        private static TargetSnapshot[] BuildTargetSnapshots(AuthoredObjectRegistry registry)
        {
            IReadOnlyList<AuthoredTargetInstance> list = registry.GetTargetsOrdered();
            var arr = new TargetSnapshot[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                AuthoredTargetInstance t = list[i];
                arr[i] = ToTargetSnapshot(t);
            }

            return arr;
        }

        private static ContentSnapshot[] BuildContentSnapshots(AuthoredObjectRegistry registry)
        {
            IReadOnlyList<AuthoredContentInstance> list = registry.GetContentsOrdered();
            var byTarget = new Dictionary<string, ContentSnapshot>(StringComparer.OrdinalIgnoreCase);
            var byContentId = new Dictionary<string, ContentSnapshot>(StringComparer.OrdinalIgnoreCase);
            var unkeyed = new List<ContentSnapshot>();
            for (int i = 0; i < list.Count; i++)
            {
                AuthoredContentInstance c = list[i];
                if (!IsPersistableContent(c))
                    continue;

                ContentSnapshot snapshot = ToContentSnapshot(c);
                string targetKey = NormalizeKey(snapshot.targetId);
                string contentKey = NormalizeKey(!string.IsNullOrWhiteSpace(snapshot.serverContentId)
                    ? snapshot.serverContentId
                    : snapshot.localContentId);

                if (!string.IsNullOrWhiteSpace(targetKey))
                {
                    byTarget[targetKey] = snapshot;
                    if (!string.IsNullOrWhiteSpace(contentKey))
                        byContentId[contentKey] = snapshot;
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(contentKey))
                {
                    byContentId[contentKey] = snapshot;
                    continue;
                }

                unkeyed.Add(snapshot);
            }

            var merged = new List<ContentSnapshot>(byTarget.Values);
            foreach (ContentSnapshot snapshot in byContentId.Values)
            {
                if (snapshot == null)
                    continue;
                string targetKey = NormalizeKey(snapshot.targetId);
                if (!string.IsNullOrWhiteSpace(targetKey) && byTarget.ContainsKey(targetKey))
                    continue;
                merged.Add(snapshot);
            }

            merged.AddRange(unkeyed);
            return merged
                .OrderBy(c => c.targetId ?? "", StringComparer.OrdinalIgnoreCase)
                .ThenBy(c => c.serverContentId ?? "", StringComparer.OrdinalIgnoreCase)
                .ThenBy(c => c.localContentId ?? "", StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static bool IsPersistableContent(AuthoredContentInstance content)
        {
            return content != null
                && content.gameObject != null
                && content.gameObject.activeInHierarchy;
        }

        private static string NormalizeKey(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "" : value.Trim();
        }

        public static TargetSnapshot ToTargetSnapshot(AuthoredTargetInstance t)
        {
            if (t == null)
                return new TargetSnapshot();

            Transform tr = t.transform;
            return new TargetSnapshot
            {
                localTargetId = t.LocalTargetId ?? "",
                serverTargetId = t.ServerTargetId ?? "",
                vuforiaTargetId = t.VuforiaTargetId ?? "",
                targetName = t.TargetName ?? "",
                targetImageLocalPath = t.TargetImageLocalPath ?? "",
                originalFileName = t.OriginalFileName ?? "",
                physicalWidthM = t.PhysicalWidthM > 1e-5f ? t.PhysicalWidthM : 0.2f,
                position = new Vector3Data(tr.localPosition),
                rotation = new Vector3Data(tr.localEulerAngles),
                scale = new Vector3Data(tr.localScale),
                remoteDirty = t.RemoteDirty,
                lastRemoteSyncedAtUtc = t.LastRemoteSyncedAtUtc ?? ""
            };
        }

        public static ContentSnapshot ToContentSnapshot(AuthoredContentInstance c)
        {
            if (c == null)
                return new ContentSnapshot();

            Transform tr = c.transform;
            return new ContentSnapshot
            {
                localContentId = c.LocalContentId ?? "",
                serverContentId = c.ServerContentId ?? "",
                targetId = c.TargetId ?? "",
                contentType = string.IsNullOrWhiteSpace(c.ContentType) ? "unknown" : c.ContentType.Trim(),
                renderKind = c.RenderKind ?? "",
                title = c.Title ?? "",
                description = c.Description ?? "",
                textBody = c.TextBody ?? "",
                assetLocalPath = c.AssetLocalPath ?? "",
                originalFileName = c.OriginalFileName ?? "",
                mediaUrl = c.MediaUrl ?? "",
                assetFormat = c.AssetFormat ?? "",
                position = new Vector3Data(tr.localPosition),
                rotation = new Vector3Data(tr.localEulerAngles),
                scale = new Vector3Data(tr.localScale),
                isUnsaved = c.IsUnsaved,
                uploadPending = c.UploadPending,
                persistPending = c.PersistPending,
                remoteDirty = c.RemoteDirty,
                lastRemoteSyncedAtUtc = c.LastRemoteSyncedAtUtc ?? ""
            };
        }

        /// <summary>Maps spawn pipeline enum to snapshot vocabulary (image | text | video | model).</summary>
        public static string ToSnapshotContentTypeLabel(SpawnContentType type)
        {
            switch (type)
            {
                case SpawnContentType.Image:
                    return "image";
                case SpawnContentType.Text:
                    return "text";
                case SpawnContentType.Model:
                    return "model";
                case SpawnContentType.Video:
                    return "video";
                default:
                    return "unknown";
            }
        }

        public static string ToSnapshotRenderKindLabel(SpawnRenderKind kind)
        {
            switch (kind)
            {
                case SpawnRenderKind.Surface:
                    return "surface";
                case SpawnRenderKind.Volumetric:
                    return "volumetric";
                default:
                    return "";
            }
        }
    }
}
