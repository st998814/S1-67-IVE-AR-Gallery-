using UnityEngine;

/// <summary>
/// Maps UI semantic move slots (left/right, middle, standoff) to ContentRoot-local axes per workspace posture.
/// Wall uses target-local X/Y/Z as printed on a vertical surface; floor/ceiling remap the middle and standoff slots.
/// </summary>
public static class SemanticAxisMapping
{
    public enum PlacementPosture
    {
        Wall = 0,
        Floor = 1,
        Ceiling = 2
    }

    public readonly struct RowLabels
    {
        public readonly string leftRight;
        public readonly string middle;
        public readonly string standoff;

        public RowLabels(string leftRight, string middle, string standoff)
        {
            this.leftRight = leftRight;
            this.middle = middle;
            this.standoff = standoff;
        }
    }

    public static PlacementPosture FromWorkspacePosture(int workspacePosture)
    {
        if (workspacePosture == (int)PlacementPosture.Floor)
            return PlacementPosture.Floor;
        if (workspacePosture == (int)PlacementPosture.Ceiling)
            return PlacementPosture.Ceiling;
        return PlacementPosture.Wall;
    }

    public static RowLabels GetRowLabels(PlacementPosture posture)
    {
        switch (posture)
        {
            case PlacementPosture.Floor:
                return new RowLabels("Left / Right", "Forward / Back", "Height");
            case PlacementPosture.Ceiling:
                return new RowLabels("Left / Right", "Forward / Back", "Height");
            default:
                return new RowLabels("Left / Right", "Up / Down", "Closer / Further");
        }
    }

    public static int GetLocalComponentIndex(PlacementPosture posture, PlacementBoundsCalculator.SemanticAxis axis)
    {
        // All postures use the same ContentRoot-local component slots; posture changes labels and phrasing only.
        _ = posture;
        return PlacementBoundsCalculator.GetLocalPositionComponentIndex(axis);
    }

    public static float GetComponentValue(
        PlacementPosture posture,
        PlacementBoundsCalculator.SemanticAxis axis,
        Vector3 localPosition)
    {
        return localPosition[GetLocalComponentIndex(posture, axis)];
    }

    public static Vector3 SetComponentValue(
        PlacementPosture posture,
        PlacementBoundsCalculator.SemanticAxis axis,
        Vector3 localPosition,
        float value)
    {
        localPosition[GetLocalComponentIndex(posture, axis)] = value;
        return localPosition;
    }

    public static PlacementBoundsCalculator.AxisRange GetSemanticRange(
        PlacementBoundsCalculator.Snapshot bounds,
        PlacementPosture posture,
        PlacementBoundsCalculator.SemanticAxis axis)
    {
        switch (GetLocalComponentIndex(posture, axis))
        {
            case 0:
                return bounds.x;
            case 1:
                return bounds.y;
            case 2:
                return bounds.z;
            default:
                return bounds.x;
        }
    }
}
