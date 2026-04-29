using UnityEngine;

namespace ARGallery.Workspace.Presets
{
    /// <summary>
    /// Target orientation preset driven by workspace posture.
    /// </summary>
    public readonly struct WorkspaceTargetPreset
    {
        public readonly Vector3 targetLocalEuler;

        public WorkspaceTargetPreset(Vector3 targetLocalEuler)
        {
            this.targetLocalEuler = targetLocalEuler;
        }
    }

    /// <summary>
    /// Camera view preset for authoring scene initialization.
    /// Position is expressed in target-local space.
    /// </summary>
    public readonly struct WorkspaceCameraPreset
    {
        public readonly Vector3 localPositionOffset;
        public readonly Vector3 localLookAtOffset;
        public readonly float tiltDegrees;

        public WorkspaceCameraPreset(Vector3 localPositionOffset, Vector3 localLookAtOffset, float tiltDegrees)
        {
            this.localPositionOffset = localPositionOffset;
            this.localLookAtOffset = localLookAtOffset;
            this.tiltDegrees = tiltDegrees;
        }
    }

    /// <summary>
    /// Reserved interaction constraints for later tickets.
    /// </summary>
    public readonly struct WorkspaceInteractionPreset
    {
        public readonly bool lockRoll;
        public readonly bool clampVerticalPan;

        public WorkspaceInteractionPreset(bool lockRoll, bool clampVerticalPan)
        {
            this.lockRoll = lockRoll;
            this.clampVerticalPan = clampVerticalPan;
        }
    }

    public readonly struct WorkspacePreset
    {
        public readonly WorkspacePosture posture;
        public readonly WorkspaceTargetPreset target;
        public readonly WorkspaceCameraPreset camera;
        public readonly WorkspaceInteractionPreset interaction;

        public WorkspacePreset(
            WorkspacePosture posture,
            WorkspaceTargetPreset target,
            WorkspaceCameraPreset camera,
            WorkspaceInteractionPreset interaction)
        {
            this.posture = posture;
            this.target = target;
            this.camera = camera;
            this.interaction = interaction;
        }
    }
}
