using NUnit.Framework;
using UnityEngine;

public class SemanticAxisMappingTests
{
    [Test]
    public void GetRowLabels_Floor_UsesForwardBackAndHeight()
    {
        SemanticAxisMapping.RowLabels labels = SemanticAxisMapping.GetRowLabels(SemanticAxisMapping.PlacementPosture.Floor);
        Assert.AreEqual("Left / Right", labels.leftRight);
        Assert.AreEqual("Forward / Back", labels.middle);
        Assert.AreEqual("Height", labels.standoff);
    }

    [Test]
    public void GetRowLabels_Wall_KeepsWallWording()
    {
        SemanticAxisMapping.RowLabels labels = SemanticAxisMapping.GetRowLabels(SemanticAxisMapping.PlacementPosture.Wall);
        Assert.AreEqual("Left / Right", labels.leftRight);
        Assert.AreEqual("Up / Down", labels.middle);
        Assert.AreEqual("Closer / Further", labels.standoff);
    }

    [Test]
    public void GetComponentValue_Floor_MapsMiddleSlotToLocalY()
    {
        Vector3 localPosition = new Vector3(0.1f, 0.2f, 0.3f);
        float middle = SemanticAxisMapping.GetComponentValue(
            SemanticAxisMapping.PlacementPosture.Floor,
            PlacementBoundsCalculator.SemanticAxis.UpDown,
            localPosition);
        Assert.AreEqual(0.2f, middle, 1e-5f);
    }

    [Test]
    public void SetComponentValue_Floor_MapsStandoffSlotToLocalZ()
    {
        Vector3 updated = SemanticAxisMapping.SetComponentValue(
            SemanticAxisMapping.PlacementPosture.Floor,
            PlacementBoundsCalculator.SemanticAxis.CloserFurther,
            Vector3.zero,
            -0.45f);
        Assert.AreEqual(-0.45f, updated.z, 1e-5f);
    }

    [Test]
    public void FormatOffset_FloorForwardBack_UsesForwardPhrase()
    {
        string text = SemanticDistanceFormatter.FormatOffset(
            SemanticAxisMapping.PlacementPosture.Floor,
            PlacementBoundsCalculator.SemanticAxis.UpDown,
            0.25f);
        Assert.AreEqual("25 cm forward", text);
    }

    [Test]
    public void FormatOffset_FloorHeight_UsesHigherPhrase()
    {
        string text = SemanticDistanceFormatter.FormatOffset(
            SemanticAxisMapping.PlacementPosture.Floor,
            PlacementBoundsCalculator.SemanticAxis.CloserFurther,
            0.12f);
        Assert.AreEqual("12 cm higher", text);
    }
}
