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
                    // View from the upper rear side of the target.
                    localPositionOffset: new Vector3(0f, 0.95f, -0.7f),
                    localLookAtOffset: new Vector3(0f, 0f, 0f),
                    tiltDegrees: 18f),
                new WorkspaceInteractionPreset(
                    lockRoll: true,
                    clampVerticalPan: false),
                new WorkspacePlacementBoundaryPreset(PlacementBoundaryPreset.WallDefault));
        }

        private static WorkspacePreset BuildFloorPreset()
        {
            return new WorkspacePreset(
                WorkspacePosture.Floor,
                // Floor posture intent: target appears on floor plane from above.
                new WorkspaceTargetPreset(new Vector3(90f, 0f, 0f)),
                new WorkspaceCameraPreset(
                    // Use the same camera framing as wall posture for demo stability.
                    localPositionOffset: new Vector3(0f, 0.95f, -0.7f),
                    localLookAtOffset: new Vector3(0f, 0f, 0f),
                    tiltDegrees: 18f),
                new WorkspaceInteractionPreset(
                    lockRoll: true,
                    clampVerticalPan: true),
                new WorkspacePlacementBoundaryPreset(
                    new PlacementBoundaryPreset(
                        horizontalScale: 1.15f,
                        verticalScale: 1.1f,
                        depthMeters: 1.25f,
                        edgeMargin: 0.03f,
                        minStandoffZ: 0.35f)));
        }

        private static WorkspacePreset BuildCeilingPreset()
        {
            return new WorkspacePreset(
                WorkspacePosture.Ceiling,
                // Ceiling posture intent: user looks upward from under the target.
                new WorkspaceTargetPreset(new Vector3(-90f, 0f, 0f)),
                new WorkspaceCameraPreset(
                    // Use the same camera framing as wall posture for demo stability.
                    localPositionOffset: new Vector3(0f, 0.95f, -0.7f),
                    localLookAtOffset: new Vector3(0f, 0f, 0f),
                    tiltDegrees: 18f),
                new WorkspaceInteractionPreset(
                    lockRoll: true,
                    clampVerticalPan: true),
                new WorkspacePlacementBoundaryPreset(
                    new PlacementBoundaryPreset(
                        horizontalScale: 1.15f,
                        verticalScale: 1.1f,
                        depthMeters: 1.25f,
                        edgeMargin: 0.03f,
                        minStandoffZ: 0.35f)));
        }
    }
}
