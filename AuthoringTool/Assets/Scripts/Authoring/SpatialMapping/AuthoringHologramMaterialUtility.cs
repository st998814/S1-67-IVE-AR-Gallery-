using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Runtime materials for authoring holographic projection visuals (URP + fallback).
/// Uses stripe alpha mask + rim/scroll similar to common hologram shader tutorials.
/// </summary>
public static class AuthoringHologramMaterialUtility
{
    private const string HologramShaderName = "ARGallery/Authoring/HologramProjection";

    private static Texture2D _sharedStripeAlphaTexture;

    /// <summary>
    /// Horizontal stripe alpha mask (transparent gaps) for holographic segmentation.
    /// </summary>
    public static Texture2D GetOrCreateStripeAlphaTexture(
        int stripePixels = 2,
        int gapPixels = 5,
        int height = 64)
    {
        if (_sharedStripeAlphaTexture != null)
            return _sharedStripeAlphaTexture;

        int period = Mathf.Max(2, stripePixels + gapPixels);
        height = Mathf.Max(8, height);
        var texture = new Texture2D(1, height, TextureFormat.RGBA32, false)
        {
            name = "AuthoringHologramStripeAlpha (Runtime)",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };

        var pixels = new Color[height];
        for (int y = 0; y < height; y++)
        {
            int phase = y % period;
            float alpha = phase < stripePixels ? 1f : 0.38f;
            pixels[y] = new Color(alpha, alpha, alpha, 1f);
        }

        texture.SetPixels(pixels);
        texture.Apply();
        _sharedStripeAlphaTexture = texture;
        return _sharedStripeAlphaTexture;
    }

    public static Material CreateHologramFillMaterial(Color color)
    {
        Shader shader = Shader.Find(HologramShaderName);
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");

        var material = new Material(shader) { name = "AuthoringHologramFill (Runtime)" };
        ConfigureTransparent(material);

        Texture2D stripeMask = GetOrCreateStripeAlphaTexture();
        if (material.HasProperty("_AlphaTexture"))
            material.SetTexture("_AlphaTexture", stripeMask);
        if (material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", stripeMask);

        ApplyHologramProperties(material, color);

        if (material.HasProperty("_AlphaScale"))
            material.SetFloat("_AlphaScale", 3.5f);
        if (material.HasProperty("_ScrollSpeedV"))
            material.SetFloat("_ScrollSpeedV", 0.35f);
        if (material.HasProperty("_GlowIntensity"))
            material.SetFloat("_GlowIntensity", 0.1f);
        if (material.HasProperty("_RimStrength"))
            material.SetFloat("_RimStrength", 0.18f);
        if (material.HasProperty("_ScanlineFrequency"))
            material.SetFloat("_ScanlineFrequency", 42f);
        if (material.HasProperty("_ScanlineStrength"))
            material.SetFloat("_ScanlineStrength", 0.08f);
        if (material.HasProperty("_PulseSpeed"))
            material.SetFloat("_PulseSpeed", 0.55f);
        if (material.HasProperty("_PulseAmount"))
            material.SetFloat("_PulseAmount", 0.03f);
        if (material.HasProperty("_GlitchIntensity"))
            material.SetFloat("_GlitchIntensity", 0f);
        if (material.HasProperty("_GlitchSpeed"))
            material.SetFloat("_GlitchSpeed", 0f);

        return material;
    }

    public static void ApplyAnimatedHologramProperties(Material material, Color color)
    {
        ApplyHologramProperties(material, color);
    }

    private static void ApplyHologramProperties(Material material, Color color)
    {
        if (material == null)
            return;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
    }

    private static void ConfigureTransparent(Material material)
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
