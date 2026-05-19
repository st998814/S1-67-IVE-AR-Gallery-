using UnityEngine;

/// <summary>
/// Pure placement-bound math for content <see cref="Transform.localPosition"/> in ContentRoot space.
/// Used by <see cref="PlacementBoundsService"/> and EditMode tests.
/// </summary>
public static class PlacementBoundsCalculator
{
    public enum SemanticAxis
    {
        LeftRight = 0,
        UpDown = 1,
        CloserFurther = 2
    }

    public readonly struct AxisRange
    {
        public readonly float min;
        public readonly float max;

        public AxisRange(float min, float max)
        {
            this.min = min;
            this.max = max;
        }

        public float Clamp(float value) => Mathf.Clamp(value, min, max);
    }

    public readonly struct Snapshot
    {
        public readonly AxisRange x;
        public readonly AxisRange y;
        public readonly AxisRange z;

        public Snapshot(AxisRange x, AxisRange y, AxisRange z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public AxisRange GetRange(SemanticAxis axis)
        {
            switch (axis)
            {
                case SemanticAxis.LeftRight: return x;
                case SemanticAxis.UpDown: return y;
                case SemanticAxis.CloserFurther: return z;
                default: return x;
            }
        }

        public Vector3 Clamp(Vector3 localPosition)
        {
            return new Vector3(
                x.Clamp(localPosition.x),
                y.Clamp(localPosition.y),
                z.Clamp(localPosition.z));
        }
    }

    /// <summary>
    /// Builds axis ranges from TargetVisual half-extents (ContentRoot-local) and front-side Z limits.
    /// </summary>
    public static Snapshot Compute(
        Vector3 targetVisualLocalScale,
        float edgeMargin,
        float effectiveMinimumLocalZ,
        bool negativeFrontLocalZ,
        float maxDepthFromTarget)
    {
        float halfX = Mathf.Max(0f, targetVisualLocalScale.x * 0.5f - edgeMargin);
        float halfY = Mathf.Max(0f, targetVisualLocalScale.y * 0.5f - edgeMargin);

        GetLocalZRange(effectiveMinimumLocalZ, negativeFrontLocalZ, maxDepthFromTarget, out float minZ, out float maxZ);

        return new Snapshot(
            new AxisRange(-halfX, halfX),
            new AxisRange(-halfY, halfY),
            new AxisRange(minZ, maxZ));
    }

    public static void GetLocalZRange(
        float effectiveMinimumLocalZ,
        bool negativeFrontLocalZ,
        float maxDepthFromTarget,
        out float minLocalZ,
        out float maxLocalZ)
    {
        float minOffset = Mathf.Max(0f, effectiveMinimumLocalZ);
        float depth = Mathf.Max(minOffset, maxDepthFromTarget);

        if (negativeFrontLocalZ)
        {
            // Content must stay on negative-Z front side: z <= -minOffset, with a max protrusion depth.
            maxLocalZ = -minOffset;
            minLocalZ = -depth;
        }
        else
        {
            minLocalZ = minOffset;
            maxLocalZ = depth;
        }

        if (minLocalZ > maxLocalZ)
            (minLocalZ, maxLocalZ) = (maxLocalZ, minLocalZ);
    }

    public static int GetLocalPositionComponentIndex(SemanticAxis axis)
    {
        switch (axis)
        {
            case SemanticAxis.LeftRight: return 0;
            case SemanticAxis.UpDown: return 1;
            case SemanticAxis.CloserFurther: return 2;
            default: return 0;
        }
    }

    public static Vector3 SetAxisComponent(Vector3 localPosition, SemanticAxis axis, float value)
    {
        int i = GetLocalPositionComponentIndex(axis);
        localPosition[i] = value;
        return localPosition;
    }
}
