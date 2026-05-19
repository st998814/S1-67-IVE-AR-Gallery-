using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode tests for <see cref="PlacementBoundsCalculator"/> and <see cref="PlacementBoundsService"/>.
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
    // PlacementBoundsService — scene hierarchy integration
    // -----------------------------------------------------------------------

    [Test]
    public void Service_ResolvesTargetVisualScale_AndClampsContent()
    {
        GameObject targetRootGo = new GameObject("TargetRoot");
        GameObject targetVisualGo = new GameObject("TargetVisual");
        targetVisualGo.transform.SetParent(targetRootGo.transform, false);
        targetVisualGo.transform.localScale = new Vector3(1f, 0.8f, 1f);

        GameObject contentRootGo = new GameObject("ContentRoot");
        contentRootGo.transform.SetParent(targetRootGo.transform, false);

        GameObject contentGo = new GameObject("Cube");
        contentGo.transform.SetParent(contentRootGo.transform, false);
        contentGo.transform.localPosition = new Vector3(5f, 5f, 0f);

        GameObject serviceGo = new GameObject("PlacementBoundsService");
        PlacementBoundsService service = serviceGo.AddComponent<PlacementBoundsService>();
        FrontSideConstraint front = serviceGo.AddComponent<FrontSideConstraint>();

        try
        {
            service.Configure(front);
            service.SetTargetContext(targetRootGo.transform, contentRootGo.transform);

            Assert.IsTrue(service.TryGetBoundsForContent(contentGo.transform, out PlacementBoundsCalculator.Snapshot bounds));
            Assert.AreEqual(-0.48f, bounds.x.min, Epsilon);
            Assert.AreEqual(0.48f, bounds.x.max, Epsilon);

            Vector3 clamped = service.ClampLocalPosition(contentGo.transform, contentGo.transform.localPosition);
            Assert.AreEqual(0.48f, clamped.x, Epsilon);
            Assert.AreEqual(0.38f, clamped.y, Epsilon);
            Assert.AreEqual(-0.5f, clamped.z, Epsilon);
        }
        finally
        {
            Object.DestroyImmediate(serviceGo);
            Object.DestroyImmediate(contentGo);
            Object.DestroyImmediate(contentRootGo);
            Object.DestroyImmediate(targetVisualGo);
            Object.DestroyImmediate(targetRootGo);
        }
    }
}
