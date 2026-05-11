using System;
using System.Collections.Generic;
using UnityEngine;

namespace ARGallery.Workspace
{
    public enum WorkspacePosture
    {
        Wall = 0,
        Floor = 1,
        Ceiling = 2
    }

    [Serializable]
    public class WorkspaceDraftState
    {
        public string workspaceId = "";
        public string workspaceName = "";
        public TargetDraftState target = new TargetDraftState();
        public List<ContentDraftState> content = new List<ContentDraftState>();
        public string schemaVersion = "v1";
        public bool isDirty;
        public string localModifiedAtUtc = "";

        public WorkspaceDraftState Clone()
        {
            var copy = new WorkspaceDraftState
            {
                workspaceId = workspaceId,
                workspaceName = workspaceName,
                target = target != null ? target.Clone() : new TargetDraftState(),
                schemaVersion = schemaVersion,
                isDirty = isDirty,
                localModifiedAtUtc = localModifiedAtUtc,
                content = new List<ContentDraftState>()
            };

            if (content != null)
            {
                for (int i = 0; i < content.Count; i++)
                {
                    ContentDraftState item = content[i];
                    if (item != null)
                        copy.content.Add(item.Clone());
                }
            }

            return copy;
        }
    }

    [Serializable]
    public class TargetDraftState
    {
        public string targetId = "";
        public string targetName = "";
        public string displayLabel = "";
        public string targetImageUrl = "";
        public float physicalWidth = 0.2f;
        public WorkspacePosture posture = WorkspacePosture.Wall;
        public string vuforiaTargetName = "";

        public TargetDraftState Clone()
        {
            return new TargetDraftState
            {
                targetId = targetId,
                targetName = targetName,
                displayLabel = displayLabel,
                targetImageUrl = targetImageUrl,
                physicalWidth = physicalWidth,
                posture = posture,
                vuforiaTargetName = vuforiaTargetName
            };
        }
    }

    [Serializable]
    public class ContentDraftState
    {
        public string contentId = "";
        public string contentType = "";
        public string mediaUrl = "";
        public Vector3 localPosition = Vector3.zero;
        public Vector3 localEuler = Vector3.zero;
        public Vector3 localScale = Vector3.one;

        public ContentDraftState Clone()
        {
            return new ContentDraftState
            {
                contentId = contentId,
                contentType = contentType,
                mediaUrl = mediaUrl,
                localPosition = localPosition,
                localEuler = localEuler,
                localScale = localScale
            };
        }
    }
}
