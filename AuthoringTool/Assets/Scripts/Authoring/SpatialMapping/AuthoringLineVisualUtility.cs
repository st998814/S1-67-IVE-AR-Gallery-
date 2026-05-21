using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Shared LineRenderer helpers for authoring spatial guides.
/// Uses the same Unlit/Color + alpha blend path as selection bounds (works in this URP project).
/// </summary>
public static class AuthoringLineVisualUtility
{
    private static Texture2D _sharedDashTexture;

    public static Texture2D GetOrCreateDashTexture(int dashPixels = 4, int gapPixels = 4)
    {
        if (_sharedDashTexture != null)
            return _sharedDashTexture;

        int width = Mathf.Max(2, dashPixels + gapPixels);
        var texture = new Texture2D(width, 1, TextureFormat.RGBA32, false)
        {
            name = "AuthoringSpatialDash (Runtime)",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Point,
            hideFlags = HideFlags.HideAndDontSave
        };

        var pixels = new Color[width];
        for (int i = 0; i < width; i++)
            pixels[i] = i < dashPixels ? Color.white : new Color(1f, 1f, 1f, 0f);

        texture.SetPixels(pixels);
        texture.Apply();
        _sharedDashTexture = texture;
        return _sharedDashTexture;
    }

    /// <summary>Line material; pass dash texture for tiled dashes (Sprites/Default), omit for solid lines (Unlit/Color).</summary>
    public static Material CreateLineMaterial(Color color, Texture2D dashTexture = null)
    {
        bool useDash = dashTexture != null;
        Shader shader = useDash
            ? Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color")
            : Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");

        if (shader == null)
            shader = Shader.Find("Standard");

        var material = new Material(shader)
        {
            name = useDash ? "AuthoringSpatialDashedLine (Runtime)" : "AuthoringSpatialLine (Runtime)"
        };

        ConfigureAlphaBlending(material);
        ApplyColor(material, color);

        if (useDash && !shader.name.Contains("Unlit/Color"))
            material.mainTexture = dashTexture;

        return material;
    }

    public static LineRenderer CreateLineRenderer(
        Transform parent,
        string objectName,
        Material sharedMaterial,
        float width,
        bool useDashedTexture = false,
        float dashTextureScale = 2.5f)
    {
        var go = new GameObject(objectName);
        go.transform.SetParent(parent, false);
        var line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = sharedMaterial;
        line.useWorldSpace = true;
        line.loop = false;
        line.positionCount = 2;
        line.numCapVertices = 0;
        line.numCornerVertices = 0;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.lightProbeUsage = LightProbeUsage.Off;
        line.reflectionProbeUsage = ReflectionProbeUsage.Off;

        bool canTile = useDashedTexture
            && sharedMaterial != null
            && sharedMaterial.mainTexture != null;

        if (canTile)
        {
            line.textureMode = LineTextureMode.Tile;
            line.textureScale = new Vector2(Mathf.Max(0.5f, dashTextureScale), 1f);
        }

        ApplyWidth(line, width);
        return line;
    }

    public static void ApplyWidth(LineRenderer line, float width)
    {
        if (line == null)
            return;

        float w = Mathf.Max(0.001f, width);
        line.startWidth = w;
        line.endWidth = w;
    }

    /// <summary>Only scales up with distance so lines never shrink below base width.</summary>
    public static float ComputeDistanceScaledWidth(
        Camera camera,
        Vector3 worldPoint,
        float baseWidth,
        float minScale = 1f,
        float maxScale = 2.2f,
        float referenceDistance = 1.5f)
    {
        if (camera == null)
            return baseWidth;

        float distance = Vector3.Distance(camera.transform.position, worldPoint);
        float scale = Mathf.Clamp(distance / Mathf.Max(0.25f, referenceDistance), minScale, maxScale);
        return baseWidth * scale;
    }

    public static void ApplyColor(Material material, Color color)
    {
        if (material == null)
            return;

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
    }

    private static void ConfigureAlphaBlending(Material material)
    {
        material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        material.SetInt("_Cull", (int)CullMode.Off);
        material.renderQueue = (int)RenderQueue.Transparent;
        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);
    }
}
