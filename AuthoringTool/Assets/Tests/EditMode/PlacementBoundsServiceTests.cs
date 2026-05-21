using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode tests for <see cref="PlacementBoundsCalculator"/> (pure math; no Assembly-CSharp dependency).
/// Run via: Unity Editor &gt; Window &gt; General &gt; Test Runner &gt; EditMode
/// </summary>
public class PlacementBoundsServiceTests
{
    private const float Epsilon = 1e-5f;

    // -----------------------------------------------------------------------
    // PlacementBoundsCalculator — XY from TargetVisual scale
    // -----------------------------------------------------------------------

    [Test]
    public void Compute_TargetVisual1x1WithMargin_XYHalfExtents()
    {
        var bounds = PlacementBoundsCalculator.Compute(
            targetVisualLocalScale: new Vector3(1f, 1f, 1f),
            edgeMargin: 0.02f,
            effectiveMinimumLocalZ: 0.5f,
            negativeFrontLocalZ: true,
            maxDepthFromTarget: 2f);

        Assert.AreEqual(-0.48f, bounds.x.min, Epsilon);
        Assert.AreEqual(0.48f, bounds.x.max, Epsilon);
        Assert.AreEqual(-0.48f, bounds.y.min, Epsilon);
        Assert.AreEqual(0.48f, bounds.y.max, Epsilon);
    }

    [Test]
    public void Compute_WideTarget_XYUsesHalfWidth()
    {
        var bounds = PlacementBoundsCalculator.Compute(
            new Vector3(0.6f, 0.45f, 1f),
            edgeMargin: 0f,
            effectiveMinimumLocalZ: 0.5f,
            negativeFrontLocalZ: true,
            maxDepthFromTarget: 2f);

        Assert.AreEqual(-0.3f, bounds.x.min, Epsilon);
        Assert.AreEqual(0.3f, bounds.x.max, Epsilon);
        Assert.AreEqual(-0.225f, bounds.y.min, Epsilon);
        Assert.AreEqual(0.225f, bounds.y.max, Epsilon);
    }

    [Test]
    public void Clamp_PositionOutsideXY_ClampedInside()
    {
        var bounds = PlacementBoundsCalculator.Compute(
            new Vector3(1f, 1f, 1f),
            edgeMargin: 0f,
            effectiveMinimumLocalZ: 0.5f,
            negativeFrontLocalZ: true,
            maxDepthFromTarget: 2f);

        Vector3 clamped = bounds.Clamp(new Vector3(2f, -2f, -1f));
        Assert.AreEqual(0.5f, clamped.x, Epsilon);
        Assert.AreEqual(-0.5f, clamped.y, Epsilon);
        Assert.AreEqual(-1f, clamped.z, Epsilon);
    }

    // -----------------------------------------------------------------------
    // PlacementBoundsCalculator — Z (negative front, matches FrontSideConstraint)
    // -----------------------------------------------------------------------

    [Test]
    public void GetLocalZRange_NegativeFront_MatchesFrontSideConvention()
    {
        PlacementBoundsCalculator.GetLocalZRange(
            effectiveMinimumLocalZ: 0.5f,
            negativeFrontLocalZ: true,
            maxDepthFromTarget: 2f,
            out float minZ,
            out float maxZ);

        Assert.AreEqual(-2f, minZ, Epsilon);
        Assert.AreEqual(-0.5f, maxZ, Epsilon);
    }

    [Test]
    public void GetLocalZRange_PositiveFront_MinIsOffset()
    {
        PlacementBoundsCalculator.GetLocalZRange(
            effectiveMinimumLocalZ: 0.5f,
            negativeFrontLocalZ: false,
            maxDepthFromTarget: 2f,
            out float minZ,
            out float maxZ);

        Assert.AreEqual(0.5f, minZ, Epsilon);
        Assert.AreEqual(2f, maxZ, Epsilon);
    }

    [Test]
    public void Clamp_ZBehindWall_ClampedToFrontMinimum()
    {
        var bounds = PlacementBoundsCalculator.Compute(
            new Vector3(1f, 1f, 1f),
            edgeMargin: 0f,
            effectiveMinimumLocalZ: 0.5f,
            negativeFrontLocalZ: true,
            maxDepthFromTarget: 2f);

        Vector3 clamped = bounds.Clamp(new Vector3(0f, 0f, 0.5f));
        Assert.AreEqual(-0.5f, clamped.z, Epsilon);
    }

    [Test]
    public void Clamp_ZTooFarFromWall_ClampedToMaxDepth()
    {
        var bounds = PlacementBoundsCalculator.Compute(
            new Vector3(1f, 1f, 1f),
            edgeMargin: 0f,
            effectiveMinimumLocalZ: 0.5f,
            negativeFrontLocalZ: true,
            maxDepthFromTarget: 2f);

        Vector3 clamped = bounds.Clamp(new Vector3(0f, 0f, -5f));
        Assert.AreEqual(-2f, clamped.z, Epsilon);
    }

    // -----------------------------------------------------------------------
    // Semantic axis mapping
    // -----------------------------------------------------------------------

    [Test]
    public void SetAxisComponent_LeftRight_UpdatesXOnly()
    {
        Vector3 result = PlacementBoundsCalculator.SetAxisComponent(
            new Vector3(1f, 2f, 3f),
            PlacementBoundsCalculator.SemanticAxis.LeftRight,
            0.25f);

        Assert.AreEqual(0.25f, result.x, Epsilon);
        Assert.AreEqual(2f, result.y, Epsilon);
        Assert.AreEqual(3f, result.z, Epsilon);
    }

    [Test]
    public void SetAxisComponent_CloserFurther_UpdatesZOnly()
    {
        Vector3 result = PlacementBoundsCalculator.SetAxisComponent(
            new Vector3(1f, 2f, 3f),
            PlacementBoundsCalculator.SemanticAxis.CloserFurther,
            -1f);

        Assert.AreEqual(1f, result.x, Epsilon);
        Assert.AreEqual(2f, result.y, Epsilon);
        Assert.AreEqual(-1f, result.z, Epsilon);
    }

    // -----------------------------------------------------------------------
    // PlacementBoundaryPreset — posture multipliers
    // -----------------------------------------------------------------------

    [Test]
    public void Compute_WithFloorPreset_ScalesHorizontalVerticalAndDepth()
    {
        var preset = new PlacementBoundaryPreset(
            horizontalScale: 1.15f,
            verticalScale: 1.1f,
            depthMeters: 1.25f,
            edgeMargin: 0.03f,
            minStandoffZ: 0.35f);

        var bounds = PlacementBoundsCalculator.Compute(
            new Vector3(1f, 1f, 1f),
            preset,
            constraintMinimumLocalZ: 0.5f,
            negativeFrontLocalZ: true);

        Assert.AreEqual(-0.545f, bounds.x.min, Epsilon);
        Assert.AreEqual(0.545f, bounds.x.max, Epsilon);
        Assert.AreEqual(-0.52f, bounds.y.min, Epsilon);
        Assert.AreEqual(0.52f, bounds.y.max, Epsilon);
        Assert.AreEqual(-1.25f, bounds.z.min, Epsilon);
        Assert.AreEqual(-0.35f, bounds.z.max, Epsilon);
    }

    [Test]
    public void Compute_WallDefaultPreset_MatchesLegacyCompute()
    {
        var legacy = PlacementBoundsCalculator.Compute(
            new Vector3(1f, 1f, 1f),
            edgeMargin: 0.02f,
            effectiveMinimumLocalZ: 0.5f,
            negativeFrontLocalZ: true,
            maxDepthFromTarget: 2f);

        var fromPreset = PlacementBoundsCalculator.Compute(
            new Vector3(1f, 1f, 1f),
            PlacementBoundaryPreset.WallDefault,
            constraintMinimumLocalZ: 0.5f,
            negativeFrontLocalZ: true);

        Assert.AreEqual(legacy.x.min, fromPreset.x.min, Epsilon);
        Assert.AreEqual(legacy.x.max, fromPreset.x.max, Epsilon);
        Assert.AreEqual(legacy.y.min, fromPreset.y.min, Epsilon);
        Assert.AreEqual(legacy.y.max, fromPreset.y.max, Epsilon);
        Assert.AreEqual(legacy.z.min, fromPreset.z.min, Epsilon);
        Assert.AreEqual(legacy.z.max, fromPreset.z.max, Epsilon);
    }

    [Test]
    public void ResolveMinStandoffZ_NegativePresetValue_UsesConstraint()
    {
        var preset = PlacementBoundaryPreset.WallDefault;
        Assert.IsTrue(preset.UsesConstraintMinStandoff);
        Assert.AreEqual(0.5f, preset.ResolveMinStandoffZ(0.5f), Epsilon);
    }

    [Test]
    public void ResolveMinStandoffZ_PositivePresetValue_UsesPreset()
    {
        var preset = new PlacementBoundaryPreset(1f, 1f, 1.25f, 0.03f, 0.35f);
        Assert.IsFalse(preset.UsesConstraintMinStandoff);
        Assert.AreEqual(0.35f, preset.ResolveMinStandoffZ(0.5f), Epsilon);
    }

    [Test]
    public void FillLocalBoxCorners_ProducesExpectedMinMaxCorners()
    {
        var bounds = PlacementBoundsCalculator.Compute(
            new Vector3(1f, 1f, 1f),
            edgeMargin: 0f,
            effectiveMinimumLocalZ: 0.5f,
            negativeFrontLocalZ: true,
            maxDepthFromTarget: 2f);

        var corners = new Vector3[8];
        PlacementBoundsCalculator.FillLocalBoxCorners(bounds, corners);

        Assert.AreEqual(new Vector3(-0.5f, -0.5f, -2f), corners[0]);
        Assert.AreEqual(new Vector3(0.5f, 0.5f, -0.5f), corners[6]);
    }

}
