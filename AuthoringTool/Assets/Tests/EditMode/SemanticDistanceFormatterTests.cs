using NUnit.Framework;
using UnityEngine;

public class SemanticDistanceFormatterTests
{
    private const float Epsilon = 1e-5f;

    [Test]
    public void FormatOffset_LeftRightNegative_ShowsCentimetresLeft()
    {
        string text = SemanticDistanceFormatter.FormatOffset(
            PlacementBoundsCalculator.SemanticAxis.LeftRight,
            -0.2f);
        Assert.AreEqual("20 cm left", text);
    }

    [Test]
    public void FormatOffset_UpDownPositive_ShowsCentimetresUp()
    {
        string text = SemanticDistanceFormatter.FormatOffset(
            PlacementBoundsCalculator.SemanticAxis.UpDown,
            0.35f);
        Assert.AreEqual("35 cm up", text);
    }

    [Test]
    public void FormatOffset_CloserFurtherNegative_ShowsInFrontOfTarget()
    {
        string text = SemanticDistanceFormatter.FormatOffset(
            PlacementBoundsCalculator.SemanticAxis.CloserFurther,
            -0.54f);
        Assert.AreEqual("54 cm in front of target", text);
    }

    [Test]
    public void FormatOffset_NearZero_ShowsAtCenterPhrase()
    {
        string text = SemanticDistanceFormatter.FormatOffset(
            PlacementBoundsCalculator.SemanticAxis.LeftRight,
            0.001f);
        Assert.AreEqual("Centered horizontally", text);
    }

    [Test]
    public void FormatOffset_LargeDistance_UsesMetres()
    {
        string text = SemanticDistanceFormatter.FormatOffset(
            PlacementBoundsCalculator.SemanticAxis.UpDown,
            1.25f);
        Assert.AreEqual("1.3 m up", text);
    }

    [Test]
    public void FormatOffsets_FromLocalPosition_MapsAxes()
    {
        SemanticDistanceFormatter.FormatOffsets(
            new Vector3(-0.2f, 0.35f, -0.54f),
            out string leftRight,
            out string upDown,
            out string closerFurther);

        Assert.AreEqual("20 cm left", leftRight);
        Assert.AreEqual("35 cm up", upDown);
        Assert.AreEqual("54 cm in front of target", closerFurther);
    }

    [Test]
    public void FormatUniformScale_ReturnsMultiplierPhrase()
    {
        Assert.AreEqual("1.2× size", SemanticDistanceFormatter.FormatUniformScale(1.2f));
    }

    [Test]
    public void FormatMagnitude_SubMetre_UsesCentimetres()
    {
        Assert.AreEqual("8 cm", SemanticDistanceFormatter.FormatMagnitude(0.08f));
    }
}
