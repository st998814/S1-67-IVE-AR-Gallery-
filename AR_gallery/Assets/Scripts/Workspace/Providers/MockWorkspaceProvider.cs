using System;
using System.Collections.Generic;

namespace ARGallery.Workspace
{
    public class MockWorkspaceProvider : IWorkspaceProvider
    {
        public const string DefaultWorkspaceId = "ws-wall-001";

        private readonly Dictionary<string, WorkspaceDraftState> workspaces = new Dictionary<string, WorkspaceDraftState>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> orderedIds = new List<string>();

        public MockWorkspaceProvider()
        {
            AddSeed(CreateWallWorkspace());
            AddSeed(CreateFloorWorkspace());
            AddSeed(CreateCeilingWorkspace());
        }

        public WorkspaceDraftState GetWorkspace(string workspaceId)
        {
            string resolvedId = string.IsNullOrWhiteSpace(workspaceId) ? DefaultWorkspaceId : workspaceId.Trim();
            if (workspaces.TryGetValue(resolvedId, out WorkspaceDraftState state))
                return state.Clone();

            if (workspaces.TryGetValue(DefaultWorkspaceId, out WorkspaceDraftState fallback))
                return fallback.Clone();

            return null;
        }

        public WorkspaceDraftState SaveWorkspace(WorkspaceDraftState workspace)
        {
            if (workspace == null || string.IsNullOrWhiteSpace(workspace.workspaceId))
                return null;

            WorkspaceDraftState cloned = workspace.Clone();
            cloned.isDirty = false;
            cloned.localModifiedAtUtc = DateTime.UtcNow.ToString("o");
            workspaces[cloned.workspaceId] = cloned;
            if (!orderedIds.Contains(cloned.workspaceId))
                orderedIds.Add(cloned.workspaceId);
            return cloned.Clone();
        }

        public IReadOnlyList<WorkspaceDraftState> GetAvailableWorkspaces()
        {
            var list = new List<WorkspaceDraftState>(orderedIds.Count);
            for (int i = 0; i < orderedIds.Count; i++)
            {
                string id = orderedIds[i];
                if (workspaces.TryGetValue(id, out WorkspaceDraftState state))
                    list.Add(state.Clone());
            }

            return list;
        }

        private void AddSeed(WorkspaceDraftState workspace)
        {
            if (workspace == null || string.IsNullOrWhiteSpace(workspace.workspaceId))
                return;

            workspaces[workspace.workspaceId] = workspace.Clone();
            if (!orderedIds.Contains(workspace.workspaceId))
                orderedIds.Add(workspace.workspaceId);
        }

        private static WorkspaceDraftState CreateWallWorkspace()
        {
            return new WorkspaceDraftState
            {
                workspaceId = "ws-wall-001",
                workspaceName = "Mock Wall Workspace",
                schemaVersion = "v1",
                isDirty = false,
                localModifiedAtUtc = "",
                target = new TargetDraftState
                {
                    targetId = "target-wall-001",
                    targetName = "Wall Poster",
                    displayLabel = "Wall Poster",
                    targetImageUrl = "mock://targets/wall-poster.png",
                    physicalWidth = 0.45f,
                    posture = WorkspacePosture.Wall,
                    publishStatus = "draft",
                    vuforiaTargetName = "wall_poster_target",
                    cloudTargetId = ""
                },
                content = new List<ContentDraftState>
                {
                    new ContentDraftState
                    {
                        contentId = "content-wall-image-001",
                        contentType = "image",
                        mediaUrl = "mock://content/wall-image.png",
                        localPosition = new UnityEngine.Vector3(0f, 0f, 0.02f),
                        localEuler = UnityEngine.Vector3.zero,
                        localScale = new UnityEngine.Vector3(0.25f, 0.25f, 0.25f)
                    },
                    new ContentDraftState
                    {
                        contentId = "content-wall-text-001",
                        contentType = "text",
                        mediaUrl = "Welcome to AR Gallery",
                        localPosition = new UnityEngine.Vector3(0f, -0.18f, 0.02f),
                        localEuler = UnityEngine.Vector3.zero,
                        localScale = new UnityEngine.Vector3(0.08f, 0.08f, 0.08f)
                    }
                }
            };
        }

        private static WorkspaceDraftState CreateFloorWorkspace()
        {
            return new WorkspaceDraftState
            {
                workspaceId = "ws-floor-001",
                workspaceName = "Mock Floor Workspace",
                schemaVersion = "v1",
                isDirty = false,
                localModifiedAtUtc = "",
                target = new TargetDraftState
                {
                    targetId = "target-floor-001",
                    targetName = "Floor Marker",
                    displayLabel = "Floor Marker",
                    targetImageUrl = "mock://targets/floor-marker.png",
                    physicalWidth = 0.6f,
                    posture = WorkspacePosture.Floor,
                    publishStatus = "draft",
                    vuforiaTargetName = "floor_marker_target",
                    cloudTargetId = ""
                },
                content = new List<ContentDraftState>()
            };
        }

        private static WorkspaceDraftState CreateCeilingWorkspace()
        {
            return new WorkspaceDraftState
            {
                workspaceId = "ws-ceiling-001",
                workspaceName = "Mock Ceiling Workspace",
                schemaVersion = "v1",
                isDirty = false,
                localModifiedAtUtc = "",
                target = new TargetDraftState
                {
                    targetId = "target-ceiling-001",
                    targetName = "Ceiling Marker",
                    displayLabel = "Ceiling Marker",
                    targetImageUrl = "mock://targets/ceiling-marker.png",
                    physicalWidth = 0.5f,
                    posture = WorkspacePosture.Ceiling,
                    publishStatus = "draft",
                    vuforiaTargetName = "ceiling_marker_target",
                    cloudTargetId = ""
                },
                content = new List<ContentDraftState>()
            };
        }
    }
}
