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
    public void Compute_FloorDefaultPreset_UsesAbsoluteTargetRelativeSafeZone()
    {
        var bounds = PlacementBoundsCalculator.Compute(
            new Vector3(0.2f, 0.28f, 1f),
            PlacementBoundaryPreset.FloorDefault,
            constraintMinimumLocalZ: 0.5f,
            negativeFrontLocalZ: true);

        Assert.AreEqual(-0.75f, bounds.x.min, Epsilon);
        Assert.AreEqual(0.75f, bounds.x.max, Epsilon);
        Assert.AreEqual(-0.5f, bounds.y.min, Epsilon);
        Assert.AreEqual(0.5f, bounds.y.max, Epsilon);
        Assert.AreEqual(-1.25f, bounds.z.min, Epsilon);
        Assert.AreEqual(-0.35f, bounds.z.max, Epsilon);
    }

    [Test]
    public void Compute_FloorDefaultPreset_IgnoresSmallTargetVisualScale()
    {
        var bounds = PlacementBoundsCalculator.Compute(
            new Vector3(0.1f, 0.1f, 1f),
            PlacementBoundaryPreset.FloorDefault,
            constraintMinimumLocalZ: 0.5f,
            negativeFrontLocalZ: true);

        Assert.AreEqual(-0.75f, bounds.x.min, Epsilon);
        Assert.AreEqual(0.75f, bounds.x.max, Epsilon);
    }

    [Test]
    public void Clamp_FloorDefault_ZBehindMinimumStandoff_ClampedTo35Cm()
    {
        var bounds = PlacementBoundsCalculator.Compute(
            new Vector3(1f, 1f, 1f),
            PlacementBoundaryPreset.FloorDefault,
            constraintMinimumLocalZ: 0.5f,
            negativeFrontLocalZ: true);

        Vector3 clamped = bounds.Clamp(new Vector3(0f, 0f, -0.1f));
        Assert.AreEqual(-0.35f, clamped.z, Epsilon);
    }

    [Test]
    public void Compute_WallDefaultPreset_UsesAbsoluteTargetRelativeSafeZone()
    {
        var bounds = PlacementBoundsCalculator.Compute(
            new Vector3(0.2f, 0.28f, 1f),
            PlacementBoundaryPreset.WallDefault,
            constraintMinimumLocalZ: 0.5f,
            negativeFrontLocalZ: true);

        Assert.AreEqual(-0.75f, bounds.x.min, Epsilon);
        Assert.AreEqual(0.75f, bounds.x.max, Epsilon);
        Assert.AreEqual(-0.5f, bounds.y.min, Epsilon);
        Assert.AreEqual(0.5f, bounds.y.max, Epsilon);
        Assert.AreEqual(-1f, bounds.z.min, Epsilon);
        Assert.AreEqual(-0.05f, bounds.z.max, Epsilon);
    }

    [Test]
    public void Compute_WallDefaultPreset_IgnoresSmallTargetVisualScale()
    {
        var bounds = PlacementBoundsCalculator.Compute(
            new Vector3(0.1f, 0.1f, 1f),
            PlacementBoundaryPreset.WallDefault,
            constraintMinimumLocalZ: 0.5f,
            negativeFrontLocalZ: true);

        Assert.AreEqual(-0.75f, bounds.x.min, Epsilon);
        Assert.AreEqual(0.75f, bounds.x.max, Epsilon);
    }

    [Test]
    public void ResolveMinStandoffZ_WallDefault_UsesFiveCmStandoff()
    {
        var preset = PlacementBoundaryPreset.WallDefault;
        Assert.IsFalse(preset.UsesConstraintMinStandoff);
        Assert.AreEqual(0.05f, preset.ResolveMinStandoffZ(0.5f), Epsilon);
    }

    [Test]
    public void ResolveMinStandoffZ_PositivePresetValue_UsesPreset()
    {
        var preset = new PlacementBoundaryPreset(1f, 1f, 1.25f, 0.03f, 0.35f);
        Assert.IsFalse(preset.UsesConstraintMinStandoff);
        Assert.AreEqual(0.35f, preset.ResolveMinStandoffZ(0.5f), Epsilon);
    }

    [Test]
    public void Compute_WithTargetVisualOffset_CentersBoundsOnTarget()
    {
        var bounds = PlacementBoundsCalculator.Compute(
            new Vector3(1f, 1f, 1f),
            edgeMargin: 0f,
            effectiveMinimumLocalZ: 0.5f,
            negativeFrontLocalZ: true,
            maxDepthFromTarget: 2f,
            boundsCenterLocal: new Vector3(0.2f, -0.1f, 0.05f));

        Assert.AreEqual(-0.3f, bounds.x.min, Epsilon);
        Assert.AreEqual(0.7f, bounds.x.max, Epsilon);
        Assert.AreEqual(-0.6f, bounds.y.min, Epsilon);
        Assert.AreEqual(0.4f, bounds.y.max, Epsilon);
        Assert.AreEqual(-1.95f, bounds.z.min, Epsilon);
        Assert.AreEqual(-0.45f, bounds.z.max, Epsilon);
        Assert.AreEqual(0.2f, bounds.LocalCenter.x, Epsilon);
        Assert.AreEqual(-0.1f, bounds.LocalCenter.y, Epsilon);
        Assert.AreEqual(-1.2f, bounds.LocalCenter.z, Epsilon);
    }

    [Test]
    public void ConvertSnapshotLocalSpace_SiblingOffset_MatchesTargetVisualPosition()
    {
        var source = new GameObject("ContentRoot").transform;
        var targetRoot = new GameObject("TargetRoot").transform;
        var targetVisual = new GameObject("TargetVisual").transform;
        targetVisual.SetParent(targetRoot, false);
        source.SetParent(targetRoot, false);
        targetVisual.localPosition = new Vector3(2f, -0.1f, -0.3f);
        source.localPosition = Vector3.zero;

        var bounds = PlacementBoundsCalculator.Compute(
            new Vector3(0.4f, 0.6f, 1f),
            edgeMargin: 0f,
            effectiveMinimumLocalZ: 0.5f,
            negativeFrontLocalZ: true,
            maxDepthFromTarget: 1f,
            boundsCenterLocal: new Vector3(2f, -0.1f, -0.3f));

        PlacementBoundsCalculator.Snapshot targetRootBounds =
            PlacementBoundsCalculator.ConvertSnapshotLocalSpace(source, targetRoot, bounds);

        Assert.AreEqual(1.8f, targetRootBounds.x.min, Epsilon);
        Assert.AreEqual(2.2f, targetRootBounds.x.max, Epsilon);
        Assert.AreEqual(-0.4f, targetRootBounds.y.min, Epsilon);
        Assert.AreEqual(-0.2f, targetRootBounds.y.max, Epsilon);

        Object.DestroyImmediate(source.gameObject);
        Object.DestroyImmediate(targetVisual.gameObject);
        Object.DestroyImmediate(targetRoot.gameObject);
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
