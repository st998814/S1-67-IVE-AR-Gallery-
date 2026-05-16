using System;
using System.Collections.Generic;
using UnityEngine;

namespace ARGallery.Workspace
{
    public class MockWorkspaceProvider : IWorkspaceProvider
    {
        public const string DefaultWorkspaceId = "ws-wall-001";

        /// <summary>Built-in demo workspaces shown in the switcher unless removed via delete or <see cref="OnDeletedFromDisk"/>.</summary>
        public static readonly string[] BuiltInSeedWorkspaceIds = { "ws-wall-001", "ws-floor-001", "ws-ceiling-001" };

        private const string HiddenSeedPrefsKey = "ARGallery.MockWorkspace.HiddenSeedIds";

        private readonly Dictionary<string, WorkspaceDraftState> workspaces = new Dictionary<string, WorkspaceDraftState>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> orderedIds = new List<string>();

        public MockWorkspaceProvider()
        {
            AddSeed(CreateWallWorkspace());
            AddSeed(CreateFloorWorkspace());
            AddSeed(CreateCeilingWorkspace());
            PruneSeedsHiddenByUser();
        }

        /// <summary>Seeds the user removed with the switcher delete action (survives restarts).</summary>
        public static HashSet<string> LoadHiddenSeedWorkspaceIds()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string raw = PlayerPrefs.GetString(HiddenSeedPrefsKey, "");
            if (string.IsNullOrWhiteSpace(raw))
                return set;
            foreach (string part in raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string id = part.Trim();
                if (!string.IsNullOrEmpty(id))
                    set.Add(id);
            }

            return set;
        }

        private static void SaveHiddenSeedWorkspaceIds(HashSet<string> ids)
        {
            if (ids == null || ids.Count == 0)
            {
                PlayerPrefs.DeleteKey(HiddenSeedPrefsKey);
                PlayerPrefs.Save();
                return;
            }

            PlayerPrefs.SetString(HiddenSeedPrefsKey, string.Join(",", ids));
            PlayerPrefs.Save();
        }

        /// <summary>Clears the “hidden demo workspace” list so all three seeds show again after the next domain reload / play session.</summary>
        public static void ClearHiddenSeedIdsForDev()
        {
            PlayerPrefs.DeleteKey(HiddenSeedPrefsKey);
            PlayerPrefs.Save();
        }

        private void PruneSeedsHiddenByUser()
        {
            foreach (string id in LoadHiddenSeedWorkspaceIds())
            {
                workspaces.Remove(id);
                orderedIds.RemoveAll(s => string.Equals(s, id, StringComparison.OrdinalIgnoreCase));
            }
        }

        /// <summary>Call after local workspace delete so built-in seeds stay gone in the switcher and <see cref="GetAvailableWorkspaces"/>.</summary>
        public void OnDeletedFromDisk(string workspaceId)
        {
            if (string.IsNullOrWhiteSpace(workspaceId))
                return;
            string id = workspaceId.Trim();

            workspaces.Remove(id);
            orderedIds.RemoveAll(s => string.Equals(s, id, StringComparison.OrdinalIgnoreCase));

            foreach (string seed in BuiltInSeedWorkspaceIds)
            {
                if (!string.Equals(id, seed, StringComparison.OrdinalIgnoreCase))
                    continue;
                HashSet<string> hidden = LoadHiddenSeedWorkspaceIds();
                hidden.Add(seed);
                SaveHiddenSeedWorkspaceIds(hidden);
                break;
            }
        }

        public WorkspaceDraftState GetWorkspace(string workspaceId)
        {
            string resolvedId = string.IsNullOrWhiteSpace(workspaceId) ? DefaultWorkspaceId : workspaceId.Trim();
            if (workspaces.TryGetValue(resolvedId, out WorkspaceDraftState state))
                return state.Clone();

            if (workspaces.TryGetValue(DefaultWorkspaceId, out WorkspaceDraftState fallback))
                return fallback.Clone();

            for (int i = 0; i < orderedIds.Count; i++)
            {
                if (workspaces.TryGetValue(orderedIds[i], out WorkspaceDraftState any))
                    return any.Clone();
            }

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
                workspaceName = "Target on Wall",
                schemaVersion = "v1",
                isDirty = false,
                localModifiedAtUtc = "",
                target = new TargetDraftState
                {
                    targetId = "target-wall-001",
                    targetName = "Wall Poster",
                    displayLabel = "Wall Target",
                    targetImageUrl = "",
                    physicalWidth = 0.45f,
                    posture = WorkspacePosture.Wall,
                    vuforiaTargetName = "wall_poster_target"
                },
                content = new List<ContentDraftState>
                {
                }
            };
        }

        private static WorkspaceDraftState CreateFloorWorkspace()
        {
            return new WorkspaceDraftState
            {
                workspaceId = "ws-floor-001",
                workspaceName = "Target on Floor",
                schemaVersion = "v1",
                isDirty = false,
                localModifiedAtUtc = "",
                target = new TargetDraftState
                {
                    targetId = "target-floor-001",
                    targetName = "Floor Target",
                    displayLabel = "Floor Target",
                    targetImageUrl = "",
                    physicalWidth = 0.6f,
                    posture = WorkspacePosture.Floor,
                    vuforiaTargetName = "floor_marker_target"
                },
                content = new List<ContentDraftState>()
            };
        }

        private static WorkspaceDraftState CreateCeilingWorkspace()
        {
            return new WorkspaceDraftState
            {
                workspaceId = "ws-ceiling-001",
                workspaceName = "Target on Ceiling",
                schemaVersion = "v1",
                isDirty = false,
                localModifiedAtUtc = "",
                target = new TargetDraftState
                {
                    targetId = "target-ceiling-001",
                    targetName = "Ceiling Target",
                    displayLabel = "Ceiling Target",
                    targetImageUrl = "",
                    physicalWidth = 0.5f,
                    posture = WorkspacePosture.Ceiling,
                    vuforiaTargetName = "ceiling_marker_target"
                },
                content = new List<ContentDraftState>()
            };
        }
    }
}
