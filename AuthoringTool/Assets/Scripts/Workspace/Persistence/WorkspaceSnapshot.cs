using System;
using UnityEngine;

namespace ARGallery.Workspace.Persistence
{
    /// <summary>
    /// Root snapshot persisted as workspaces/&lt;workspaceId&gt;/snapshot.json (JSON only; binaries live under assets/).
    /// Uses arrays because Unity JsonUtility does not serialize List&lt;T&gt; at the root or as object fields reliably.
    /// </summary>
    [Serializable]
    public class WorkspaceSnapshot
    {
        public string schemaVersion = "v1";
        public string workspaceId = "";
        public string workspaceName = "";
        public string createdAtUtc = "";
        public string updatedAtUtc = "";
        /// <summary>Layer 3: true when local changes need a backend flush.</summary>
        public bool remoteDirty;
        /// <summary>ISO-8601 UTC of last successful remote sync for this workspace snapshot.</summary>
        public string lastRemoteSyncedAtUtc = "";
        /// <summary>Last Layer 3 failure message; empty when clean.</summary>
        public string lastRemoteSyncError = "";
        /// <summary>One of <see cref="RemoteSyncStatus"/> constants.</summary>
        public string remoteSyncStatus = RemoteSyncStatus.LocalOnly;
        public TargetSnapshot[] targets = Array.Empty<TargetSnapshot>();
        public ContentSnapshot[] contents = Array.Empty<ContentSnapshot>();
    }

    [Serializable]
    public class TargetSnapshot
    {
        public string localTargetId = "";
        public string serverTargetId = "";
        public string vuforiaTargetId = "";
        public string targetName = "";
        /// <summary>Relative path under workspace folder, e.g. assets/targets/&lt;id&gt;.png</summary>
        public string targetImageLocalPath = "";
        /// <summary>Relative path for optional real-world placement reference photo.</summary>
        public string targetReferenceLocalPath = "";
        /// <summary>Last synced public URL for reference image (Layer 3).</summary>
        public string targetReferenceImageUrl = "";
        public string originalFileName = "";
        public string targetReferenceOriginalFileName = "";
        /// <summary>Physical width of the printed target in meters; used to size TargetVisual in authoring.</summary>
        public float physicalWidthM = 0.2f;
        public Vector3Data position = new Vector3Data(0f, 0f, 0f);
        public Vector3Data rotation = new Vector3Data(0f, 0f, 0f);
        public Vector3Data scale = new Vector3Data(1f, 1f, 1f);
        /// <summary>Layer 3: target row needs or awaits backend upsert.</summary>
        public bool remoteDirty;
        /// <summary>ISO-8601 UTC of last successful remote sync for this target snapshot row.</summary>
        public string lastRemoteSyncedAtUtc = "";
    }

    /// <summary>Serialized content kinds; align with spawn pipeline naming.</summary>
    [Serializable]
    public class ContentSnapshot
    {
        public string localContentId = "";
        public string serverContentId = "";
        public string targetId = "";
        public string contentType = "unknown";
        public string renderKind = "";
        public string title = "";
        public string description = "";
        public string textBody = "";
        /// <summary>Relative path under workspace folder for imported binary media.</summary>
        public string assetLocalPath = "";
        public string originalFileName = "";
        public string mediaUrl = "";
        public string assetFormat = "";
        public Vector3Data position = new Vector3Data(0f, 0f, 0f);
        public Vector3Data rotation = new Vector3Data(0f, 0f, 0f);
        public Vector3Data scale = new Vector3Data(1f, 1f, 1f);
        public bool isUnsaved;
        public bool uploadPending;
        public bool persistPending;
        /// <summary>Layer 3: content row needs or awaits backend upsert.</summary>
        public bool remoteDirty;
        /// <summary>ISO-8601 UTC of last successful remote sync for this content snapshot row.</summary>
        public string lastRemoteSyncedAtUtc = "";
    }

    /// <summary>Wrapper for workspace-index.json (JsonUtility requires named root object).</summary>
    [Serializable]
    public class WorkspaceIndexFile
    {
        public string schemaVersion = "v1";
        public WorkspaceIndexEntry[] entries = Array.Empty<WorkspaceIndexEntry>();
    }

    [Serializable]
    public class WorkspaceIndexEntry
    {
        public string workspaceId = "";
        public string workspaceName = "";
        public string updatedAtUtc = "";
        public string thumbnailKey = "";
    }
}
