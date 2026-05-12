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
        public string originalFileName = "";
        public Vector3Data position = new Vector3Data(0f, 0f, 0f);
        public Vector3Data rotation = new Vector3Data(0f, 0f, 0f);
        public Vector3Data scale = new Vector3Data(1f, 1f, 1f);
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
