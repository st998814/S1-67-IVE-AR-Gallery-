using UnityEngine;

/// <summary>
/// World-space bounds helpers for authoring projection / frustum visuals.
/// </summary>
public static class AuthoringBoundsVisualUtility
{
    private static readonly Vector3[] BoxCornerOffsets =
    {
        new Vector3(-1f, -1f, -1f),
        new Vector3(1f, -1f, -1f),
        new Vector3(1f, 1f, -1f),
        new Vector3(-1f, 1f, -1f),
        new Vector3(-1f, -1f, 1f),
        new Vector3(1f, -1f, 1f),
        new Vector3(1f, 1f, 1f),
        new Vector3(-1f, 1f, 1f)
    };

    private static readonly int[][] FaceCornerIndices =
    {
        new[] { 0, 1, 2, 3 },
        new[] { 5, 4, 7, 6 },
        new[] { 4, 0, 3, 7 },
        new[] { 1, 5, 6, 2 },
        new[] { 4, 5, 1, 0 },
        new[] { 3, 2, 6, 7 }
    };

    private static readonly Vector3[] FaceNormalsLocal =
    {
        Vector3.back,
        Vector3.forward,
        Vector3.left,
        Vector3.right,
        Vector3.down,
        Vector3.up
    };

    public static bool TryGetWorldBounds(Transform root, out Bounds bounds)
    {
        bounds = default;
        if (root == null)
            return false;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(includeInactive: false);
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds)
        {
            bounds = new Bounds(root.position, Vector3.one * 0.08f);
            return true;
        }

        return true;
    }

    /// <summary>Returns four world corners for the bounds face that most directly faces <paramref name="towardWorldPoint"/>.</summary>
    public static bool TryGetFaceCornersTowardPoint(Bounds bounds, Vector3 towardWorldPoint, out Vector3[] cornersWorld)
    {
        cornersWorld = new Vector3[4];
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;
        if (extents.sqrMagnitude < 1e-10f)
            extents = Vector3.one * 0.04f;

        Vector3 dir = towardWorldPoint - center;
        if (dir.sqrMagnitude < 1e-8f)
            dir = Vector3.forward;
        dir.Normalize();

        int bestFace = 0;
        float bestDot = float.NegativeInfinity;
        for (int face = 0; face < FaceNormalsLocal.Length; face++)
        {
            Vector3 faceCenter = center + Vector3.Scale(FaceNormalsLocal[face], extents);
            Vector3 faceNormal = (faceCenter - center).normalized;
            float dot = Vector3.Dot(faceNormal, dir);
            if (dot > bestDot)
            {
                bestDot = dot;
                bestFace = face;
            }
        }

        int[] indices = FaceCornerIndices[bestFace];
        for (int i = 0; i < 4; i++)
        {
            Vector3 localOffset = Vector3.Scale(BoxCornerOffsets[indices[i]], extents);
            cornersWorld[i] = center + localOffset;
        }

        return true;
    }

    public static void FillQuadMesh(Mesh mesh, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        mesh.Clear();
        mesh.vertices = new[] { a, b, c, d };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
    }
}
