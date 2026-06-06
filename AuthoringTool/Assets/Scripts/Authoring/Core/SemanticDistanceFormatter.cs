using System;
using UnityEngine;

/// <summary>
/// Formats ContentRoot-local placement offsets as human-readable real-world distances.
/// Unity units are interpreted as metres.
/// </summary>
public static class SemanticDistanceFormatter
{
    private const float MetresPerCentimetre = 0.01f;
    private const float UseCentimetresBelowMetres = 1f;
    private const float NearZeroMetres = 0.005f;

    /// <summary>
    /// Formats a signed offset along a semantic placement axis for wall posture (legacy overload).
    /// </summary>
    public static string FormatOffset(PlacementBoundsCalculator.SemanticAxis axis, float metres)
    {
        return FormatOffset(SemanticAxisMapping.PlacementPosture.Wall, axis, metres);
    }

    /// <summary>
    /// Formats a signed offset along a semantic placement axis (e.g. "20 cm left", "15 cm forward").
    /// </summary>
    public static string FormatOffset(
        SemanticAxisMapping.PlacementPosture posture,
        PlacementBoundsCalculator.SemanticAxis axis,
        float metres)
    {
        if (Mathf.Abs(metres) < NearZeroMetres)
            return GetAtCenterPhrase(posture, axis);

        string magnitude = FormatMagnitude(Mathf.Abs(metres));
        string direction = GetDirectionPhrase(posture, axis, metres);
        return $"{magnitude} {direction}";
    }

    /// <summary>
    /// Formats all three semantic offsets from a ContentRoot-local position (wall posture).
    /// </summary>
    public static void FormatOffsets(
        Vector3 localPosition,
        out string leftRight,
        out string upDown,
        out string closerFurther)
    {
        FormatOffsets(SemanticAxisMapping.PlacementPosture.Wall, localPosition, out leftRight, out upDown, out closerFurther);
    }

    /// <summary>
    /// Formats all three semantic UI slots from a ContentRoot-local position using posture-aware axis mapping.
    /// </summary>
    public static void FormatOffsets(
        SemanticAxisMapping.PlacementPosture posture,
        Vector3 localPosition,
        out string leftRight,
        out string upDown,
        out string closerFurther)
    {
        leftRight = FormatOffset(
            posture,
            PlacementBoundsCalculator.SemanticAxis.LeftRight,
            SemanticAxisMapping.GetComponentValue(posture, PlacementBoundsCalculator.SemanticAxis.LeftRight, localPosition));
        upDown = FormatOffset(
            posture,
            PlacementBoundsCalculator.SemanticAxis.UpDown,
            SemanticAxisMapping.GetComponentValue(posture, PlacementBoundsCalculator.SemanticAxis.UpDown, localPosition));
        closerFurther = FormatOffset(
            posture,
            PlacementBoundsCalculator.SemanticAxis.CloserFurther,
            SemanticAxisMapping.GetComponentValue(posture, PlacementBoundsCalculator.SemanticAxis.CloserFurther, localPosition));
    }

    /// <summary>
    /// Formats target-root local position for the target inspector (metres, no semantic left/right).
    /// </summary>
    public static string FormatTargetAxisComponent(char axisName, float metres)
    {
        if (Mathf.Abs(metres) < NearZeroMetres)
            return $"{axisName}: at origin";

        return $"{axisName}: {FormatMagnitude(Mathf.Abs(metres))} from origin";
    }

    /// <summary>
    /// Formats uniform content scale for authoring UI (e.g. "1.2× size").
    /// </summary>
    public static string FormatUniformScale(float scale)
    {
        float clamped = Mathf.Max(0.01f, scale);
        return $"{clamped:0.##}× size";
    }

    public static string FormatMagnitude(float absMetres)
    {
        float abs = Mathf.Abs(absMetres);
        if (abs < UseCentimetresBelowMetres)
        {
            int cm = Mathf.RoundToInt(abs / MetresPerCentimetre);
            return $"{cm} cm";
        }

        float roundedMetres = Mathf.Round(abs * 10f) / 10f;
        return $"{roundedMetres:0.#} m";
    }

    private static string GetDirectionPhrase(
        SemanticAxisMapping.PlacementPosture posture,
        PlacementBoundsCalculator.SemanticAxis axis,
        float signedMetres)
    {
        switch (posture)
        {
            case SemanticAxisMapping.PlacementPosture.Floor:
                return GetFloorDirectionPhrase(axis, signedMetres);
            case SemanticAxisMapping.PlacementPosture.Ceiling:
                return GetCeilingDirectionPhrase(axis, signedMetres);
            default:
                return GetWallDirectionPhrase(axis, signedMetres);
        }
    }

    private static string GetWallDirectionPhrase(PlacementBoundsCalculator.SemanticAxis axis, float signedMetres)
    {
        switch (axis)
        {
            case PlacementBoundsCalculator.SemanticAxis.LeftRight:
                return signedMetres < 0f ? "left" : "right";
            case PlacementBoundsCalculator.SemanticAxis.UpDown:
                return signedMetres < 0f ? "down" : "up";
            case PlacementBoundsCalculator.SemanticAxis.CloserFurther:
                return signedMetres < 0f ? "in front of target" : "behind target";
            default:
                throw new ArgumentOutOfRangeException(nameof(axis), axis, null);
        }
    }

    private static string GetFloorDirectionPhrase(PlacementBoundsCalculator.SemanticAxis axis, float signedMetres)
    {
        switch (axis)
        {
            case PlacementBoundsCalculator.SemanticAxis.LeftRight:
                return signedMetres < 0f ? "left" : "right";
            case PlacementBoundsCalculator.SemanticAxis.UpDown:
                return signedMetres < 0f ? "back" : "forward";
            case PlacementBoundsCalculator.SemanticAxis.CloserFurther:
                return signedMetres < 0f ? "lower" : "higher";
            default:
                throw new ArgumentOutOfRangeException(nameof(axis), axis, null);
        }
    }

    private static string GetCeilingDirectionPhrase(PlacementBoundsCalculator.SemanticAxis axis, float signedMetres)
    {
        switch (axis)
        {
            case PlacementBoundsCalculator.SemanticAxis.LeftRight:
                return signedMetres < 0f ? "left" : "right";
            case PlacementBoundsCalculator.SemanticAxis.UpDown:
                return signedMetres < 0f ? "back" : "forward";
            case PlacementBoundsCalculator.SemanticAxis.CloserFurther:
                return signedMetres < 0f ? "toward ceiling" : "toward floor";
            default:
                throw new ArgumentOutOfRangeException(nameof(axis), axis, null);
        }
    }

    private static string GetAtCenterPhrase(
        SemanticAxisMapping.PlacementPosture posture,
        PlacementBoundsCalculator.SemanticAxis axis)
    {
        switch (posture)
        {
            case SemanticAxisMapping.PlacementPosture.Floor:
            case SemanticAxisMapping.PlacementPosture.Ceiling:
                switch (axis)
                {
                    case PlacementBoundsCalculator.SemanticAxis.LeftRight:
                        return "Centered horizontally";
                    case PlacementBoundsCalculator.SemanticAxis.UpDown:
                        return "Centered on target plane";
                    case PlacementBoundsCalculator.SemanticAxis.CloserFurther:
                        return "On marker surface";
                    default:
                        return "At center";
                }

            default:
                switch (axis)
                {
                    case PlacementBoundsCalculator.SemanticAxis.LeftRight:
                        return "Centered horizontally";
                    case PlacementBoundsCalculator.SemanticAxis.UpDown:
                        return "At target height";
                    case PlacementBoundsCalculator.SemanticAxis.CloserFurther:
                        return "On target plane";
                    default:
                        return "At center";
                }
        }
    }
}
