using UnityEngine;

/// <summary>
/// Pure placement-bound math for content <see cref="Transform.localPosition"/> in ContentRoot space.
/// Lives in <see cref="ARGallery.Authoring.Core"/> so EditMode tests can reference it without Assembly-CSharp.
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

        public Vector3 LocalMin => new Vector3(x.min, y.min, z.min);

        public Vector3 LocalMax => new Vector3(x.max, y.max, z.max);

        public Vector3 LocalCenter => (LocalMin + LocalMax) * 0.5f;

        public Vector3 LocalSize => LocalMax - LocalMin;
    }

    /// <summary>
    /// Re-expresses an axis-aligned box from <paramref name="source"/> local space into <paramref name="destination"/> local space.
    /// </summary>
    public static Snapshot ConvertSnapshotLocalSpace(Transform source, Transform destination, Snapshot bounds)
    {
        if (source == null || destination == null || source == destination)
            return bounds;

        var corners = new Vector3[8];
        FillLocalBoxCorners(bounds, corners);
        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 world = source.TransformPoint(corners[i]);
            corners[i] = destination.InverseTransformPoint(world);
        }

        return SnapshotFromCorners(corners);
    }

    public static Snapshot SnapshotFromCorners(Vector3[] corners)
    {
        if (corners == null || corners.Length < 8)
            throw new System.ArgumentException("Expected eight corner slots.", nameof(corners));

        Vector3 min = corners[0];
        Vector3 max = corners[0];
        for (int i = 1; i < corners.Length; i++)
        {
            min = Vector3.Min(min, corners[i]);
            max = Vector3.Max(max, corners[i]);
        }

        return new Snapshot(
            new AxisRange(min.x, max.x),
            new AxisRange(min.y, max.y),
            new AxisRange(min.z, max.z));
    }

    public static void FillLocalBoxCorners(Snapshot bounds, Vector3[] corners)
    {
        if (corners == null || corners.Length < 8)
            throw new System.ArgumentException("Expected eight corner slots.", nameof(corners));

        Vector3 min = bounds.LocalMin;
        Vector3 max = bounds.LocalMax;
        corners[0] = new Vector3(min.x, min.y, min.z);
        corners[1] = new Vector3(max.x, min.y, min.z);
        corners[2] = new Vector3(max.x, max.y, min.z);
        corners[3] = new Vector3(min.x, max.y, min.z);
        corners[4] = new Vector3(min.x, min.y, max.z);
        corners[5] = new Vector3(max.x, min.y, max.z);
        corners[6] = new Vector3(max.x, max.y, max.z);
        corners[7] = new Vector3(min.x, max.y, max.z);
    }

    /// <summary>
    /// Builds axis ranges from TargetVisual half-extents (ContentRoot-local) and front-side Z limits.
    /// </summary>
    public static Snapshot Compute(
        Vector3 targetVisualLocalScale,
        float edgeMargin,
        float effectiveMinimumLocalZ,
        bool negativeFrontLocalZ,
        float maxDepthFromTarget,
        Vector3 boundsCenterLocal = default)
    {
        return Compute(
            targetVisualLocalScale,
            new PlacementBoundaryPreset(1f, 1f, maxDepthFromTarget, edgeMargin, effectiveMinimumLocalZ),
            effectiveMinimumLocalZ,
            negativeFrontLocalZ,
            boundsCenterLocal);
    }

    /// <summary>
    /// Builds axis ranges using a posture-specific <see cref="PlacementBoundaryPreset"/>.
    /// </summary>
    public static Snapshot Compute(
        Vector3 targetVisualLocalScale,
        PlacementBoundaryPreset preset,
        float constraintMinimumLocalZ,
        bool negativeFrontLocalZ,
        Vector3 boundsCenterLocal = default)
    {
        float edgeMargin = Mathf.Max(0f, preset.edgeMargin);
        float horizontalScale = Mathf.Max(0f, preset.horizontalScale);
        float verticalScale = Mathf.Max(0f, preset.verticalScale);
        float effectiveMinimumLocalZ = preset.ResolveMinStandoffZ(constraintMinimumLocalZ);
        float maxDepthFromTarget = Mathf.Max(0f, preset.depthMeters);

        float halfX = preset.UsesAbsoluteHalfExtentX
            ? preset.absoluteHalfExtentX
            : Mathf.Max(0f, targetVisualLocalScale.x * 0.5f * horizontalScale - edgeMargin);
        float halfY = preset.UsesAbsoluteHalfExtentY
            ? preset.absoluteHalfExtentY
            : Mathf.Max(0f, targetVisualLocalScale.y * 0.5f * verticalScale - edgeMargin);

        GetLocalZRange(effectiveMinimumLocalZ, negativeFrontLocalZ, maxDepthFromTarget, out float minZ, out float maxZ);

        Vector3 center = boundsCenterLocal;
        return new Snapshot(
            new AxisRange(center.x - halfX, center.x + halfX),
            new AxisRange(center.y - halfY, center.y + halfY),
            new AxisRange(center.z + minZ, center.z + maxZ));
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
