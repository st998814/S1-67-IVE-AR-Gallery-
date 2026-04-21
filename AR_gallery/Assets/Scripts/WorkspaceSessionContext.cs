using System;
using UnityEngine;

namespace ARGallery.AppFlow
{
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
        public bool isNewWorkspace;
        public string thumbnailKey = "";

        public WorkspaceSessionContext Clone()
        {
            return new WorkspaceSessionContext
            {
                workspaceId = workspaceId,
                workspaceName = workspaceName,
                targetId = targetId,
                isNewWorkspace = isNewWorkspace,
                thumbnailKey = thumbnailKey
            };
        }

        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(workspaceId);
        }
    }
}
