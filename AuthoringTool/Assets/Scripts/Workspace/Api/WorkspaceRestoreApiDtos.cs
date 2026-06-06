using System;

namespace ARGallery.Workspace.Api
{
    /// <summary>
    /// JsonUtility DTOs for GET /api/workspaces/{id} restore payloads.
    /// Shared by <see cref="ARGallery.AppFlow.AuthoringWorkspaceEntry"/> and
    /// <see cref="ARGallery.AppFlow.WorkspaceSwitcherController"/>.
    /// </summary>
    [Serializable]
    public sealed class WorkspaceRestoreEnvelope
    {
        public WorkspaceDetailDto workspace;
        public WorkspaceTargetDto[] targets;
        public WorkspaceContentDto[] contents;
    }

    [Serializable]
    public sealed class WorkspaceDetailDto
    {
        public string workspaceId;
        public string workspaceName;
        public string state;
        public int schemaVersion;
        public string createdAtUtc;
        public string updatedAtUtc;
    }

    [Serializable]
    public sealed class WorkspaceTargetDto
    {
        public string targetId;
        public string workspaceId;
        public string targetName;
        public string displayLabel;
        public string targetImageUrl;
        public string targetReferenceImageUrl;
        public float physicalWidthM;
        public WorkspaceApiVector3Dto localPosition;
        public WorkspaceApiVector3Dto localEuler;
        public WorkspaceApiVector3Dto localScale;
        public string vuforiaTargetId;
        public string vuforiaStatus;
        public string status;
        public string createdAtUtc;
        public string updatedAtUtc;
    }

    [Serializable]
    public sealed class WorkspaceContentDto
    {
        public string contentId;
        public string targetId;
        public string workspaceId;
        public string contentType;
        public string mediaUrl;
        public WorkspaceApiVector3Dto localPosition;
        public WorkspaceApiVector3Dto localEuler;
        public WorkspaceApiVector3Dto localScale;
        public string renderKind;
        public string assetFormat;
        public string status;
        public string createdAtUtc;
        public string updatedAtUtc;
    }

    [Serializable]
    public sealed class WorkspaceApiVector3Dto
    {
        public float x;
        public float y;
        public float z;
    }
}
