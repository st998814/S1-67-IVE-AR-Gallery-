/// <summary>
/// Posture-driven placement boundary parameters for ContentRoot-local authoring space.
/// Lives in <see cref="ARGallery.Authoring.Core"/> for use by <see cref="PlacementBoundsCalculator"/>.
/// </summary>
public readonly struct PlacementBoundaryPreset
{
    /// <summary>Multiplier applied to TargetVisual half-extent along local X (when absolute half-extent is not set).</summary>
    public readonly float horizontalScale;

    /// <summary>Multiplier applied to TargetVisual half-extent along local Y (when absolute half-extent is not set).</summary>
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

    /// <summary>When &gt; 0, left/right half-extent in metres (target-relative safe zone), ignoring TargetVisual width.</summary>
    public readonly float absoluteHalfExtentX;

    /// <summary>When &gt; 0, up/down half-extent in metres (target-relative safe zone), ignoring TargetVisual height.</summary>
    public readonly float absoluteHalfExtentY;

    public PlacementBoundaryPreset(
        float horizontalScale,
        float verticalScale,
        float depthMeters,
        float edgeMargin,
        float minStandoffZ = -1f,
        float absoluteHalfExtentX = 0f,
        float absoluteHalfExtentY = 0f)
    {
        this.horizontalScale = horizontalScale;
        this.verticalScale = verticalScale;
        this.depthMeters = depthMeters;
        this.edgeMargin = edgeMargin;
        this.minStandoffZ = minStandoffZ;
        this.absoluteHalfExtentX = absoluteHalfExtentX;
        this.absoluteHalfExtentY = absoluteHalfExtentY;
    }

    /// <summary>
    /// Wall posture: target as spatial anchor with a 1.5 m × 1.0 m × 0.95 m deep safe zone (5 cm–100 cm in front).
    /// </summary>
    public static PlacementBoundaryPreset WallDefault =>
        new PlacementBoundaryPreset(
            horizontalScale: 1f,
            verticalScale: 1f,
            depthMeters: 1f,
            edgeMargin: 0f,
            minStandoffZ: 0.05f,
            absoluteHalfExtentX: 0.75f,
            absoluteHalfExtentY: 0.5f);

    public bool UsesConstraintMinStandoff => minStandoffZ < 0f;

    public bool UsesAbsoluteHalfExtentX => absoluteHalfExtentX > 0f;

    public bool UsesAbsoluteHalfExtentY => absoluteHalfExtentY > 0f;

    public float ResolveMinStandoffZ(float constraintMinimumLocalZ) =>
        UsesConstraintMinStandoff ? constraintMinimumLocalZ : minStandoffZ;
}
