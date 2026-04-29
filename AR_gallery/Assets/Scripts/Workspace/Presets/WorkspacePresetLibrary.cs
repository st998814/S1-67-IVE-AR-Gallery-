using UnityEngine;

namespace ARGallery.Workspace.Presets
{
    /// <summary>
    /// Deterministic posture-to-preset mapping for authoring initialization.
    /// </summary>
    public static class WorkspacePresetLibrary
    {
        public static WorkspacePreset GetPreset(WorkspacePosture posture)
        {
            switch (posture)
            {
                case WorkspacePosture.Floor:
                    return BuildFloorPreset();
                case WorkspacePosture.Ceiling:
                    return BuildCeilingPreset();
                case WorkspacePosture.Wall:
                default:
                    return BuildWallPreset();
            }
        }

        private static WorkspacePreset BuildWallPreset()
        {
            return new WorkspacePreset(
                WorkspacePosture.Wall,
                new WorkspaceTargetPreset(new Vector3(0f, 0f, 0f)),
                new WorkspaceCameraPreset(
                    localPositionOffset: new Vector3(0f, 0.16f, -1.35f),
                    localLookAtOffset: new Vector3(0f, 0f, 0f),
                    tiltDegrees: 8f),
                new WorkspaceInteractionPreset(
                    lockRoll: true,
                    clampVerticalPan: false));
        }

        private static WorkspacePreset BuildFloorPreset()
        {
            return new WorkspacePreset(
                WorkspacePosture.Floor,
                new WorkspaceTargetPreset(new Vector3(-90f, 0f, 0f)),
                new WorkspaceCameraPreset(
                    localPositionOffset: new Vector3(0f, 1.15f, -0.95f),
                    localLookAtOffset: new Vector3(0f, 0f, 0f),
                    tiltDegrees: 14f),
                new WorkspaceInteractionPreset(
                    lockRoll: true,
                    clampVerticalPan: true));
        }

        private static WorkspacePreset BuildCeilingPreset()
        {
            return new WorkspacePreset(
                WorkspacePosture.Ceiling,
                new WorkspaceTargetPreset(new Vector3(90f, 0f, 0f)),
                new WorkspaceCameraPreset(
                    localPositionOffset: new Vector3(0f, -1.15f, -0.95f),
                    localLookAtOffset: new Vector3(0f, 0f, 0f),
                    tiltDegrees: -14f),
                new WorkspaceInteractionPreset(
                    lockRoll: true,
                    clampVerticalPan: true));
        }
    }
}
