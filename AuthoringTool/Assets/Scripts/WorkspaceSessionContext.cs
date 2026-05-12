using System;
using UnityEngine;

namespace ARGallery.AppFlow
{
    public enum WorkspaceSetupState
    {
        PendingTargetSetup = 0,
        Ready = 1
    }

    /// <summary>
    /// Runtime payload passed from workspace switcher into authoring scene.
    /// A workspace is defined as one target context in this phase.
    /// </summary>
    [Serializable]
    public class WorkspaceSessionContext
    {
        public string workspaceId = "";
        public string workspaceName = "";
        public string targetId = "";
        public byte[] targetImageBytes;
        public string targetImageFileName = "";
        /// <summary>Workspace-relative path under persistentDataPath, e.g. assets/targets/&lt;guid&gt;.png</summary>
        public string targetImageRelativePath = "";
        /// <summary>Vuforia cloud target id returned after successful registration.</summary>
        public string vuforiaTargetId = "";
        public bool isNewWorkspace;
        public string thumbnailKey = "";
        public WorkspaceSetupState setupState = WorkspaceSetupState.PendingTargetSetup;

        public WorkspaceSessionContext Clone()
        {
            return new WorkspaceSessionContext
            {
                workspaceId = workspaceId,
                workspaceName = workspaceName,
                targetId = targetId,
                targetImageBytes = targetImageBytes != null ? (byte[])targetImageBytes.Clone() : null,
                targetImageFileName = targetImageFileName,
                targetImageRelativePath = targetImageRelativePath ?? "",
                vuforiaTargetId = vuforiaTargetId ?? "",
                isNewWorkspace = isNewWorkspace,
                thumbnailKey = thumbnailKey,
                setupState = setupState
            };
        }

        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(workspaceId);
        }

        public bool IsReadyForAuthoring()
        {
            return setupState == WorkspaceSetupState.Ready;
        }
    }
}
