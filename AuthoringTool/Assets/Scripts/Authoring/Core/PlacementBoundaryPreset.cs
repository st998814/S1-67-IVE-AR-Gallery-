/// <summary>
/// Posture-driven placement boundary parameters for ContentRoot-local authoring space.
/// Lives in <see cref="ARGallery.Authoring.Core"/> for use by <see cref="PlacementBoundsCalculator"/>.
/// </summary>
public readonly struct PlacementBoundaryPreset
{
    /// <summary>Multiplier applied to TargetVisual half-extent along local X.</summary>
    public readonly float horizontalScale;

    /// <summary>Multiplier applied to TargetVisual half-extent along local Y.</summary>
    public readonly float verticalScale;

    /// <summary>Maximum depth from the target plane along local Z (metres).</summary>
    public readonly float depthMeters;

    /// <summary>Inset from scaled TargetVisual edges along X and Y (metres).</summary>
    public readonly float edgeMargin;

    /// <summary>
    /// Minimum standoff from target plane along local Z (metres).
    /// Values &lt; 0 mean "use <see cref="FrontSideConstraint"/> effective minimum".
    /// </summary>
    public readonly float minStandoffZ;

    public PlacementBoundaryPreset(
        float horizontalScale,
        float verticalScale,
        float depthMeters,
        float edgeMargin,
        float minStandoffZ = -1f)
    {
        this.horizontalScale = horizontalScale;
        this.verticalScale = verticalScale;
        this.depthMeters = depthMeters;
        this.edgeMargin = edgeMargin;
        this.minStandoffZ = minStandoffZ;
    }

    /// <summary>Wall posture defaults (matches legacy serialized PlacementBoundsService values).</summary>
    public static PlacementBoundaryPreset WallDefault =>
        new PlacementBoundaryPreset(1f, 1f, 2f, 0.02f);

    public bool UsesConstraintMinStandoff => minStandoffZ < 0f;

    public float ResolveMinStandoffZ(float constraintMinimumLocalZ) =>
        UsesConstraintMinStandoff ? constraintMinimumLocalZ : minStandoffZ;
}
