using UnityEngine;

/// <summary>
/// Shared LineRenderer setup for authoring-only spatial guides (no colliders, transparent unlit lines).
/// </summary>
public static class AuthoringLineVisualUtility
{
    public static Material CreateTransparentLineMaterial(Color color)
    {
        Shader shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Standard");

        var material = new Material(shader);
        material.name = "AuthoringSpatialLine (Runtime)";
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);
        ApplyColor(material, color);
        return material;
    }

    public static LineRenderer CreateLineRenderer(
        Transform parent,
        string objectName,
        Material sharedMaterial,
        float width)
    {
        var go = new GameObject(objectName);
        go.transform.SetParent(parent, false);
        var line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = sharedMaterial;
        line.useWorldSpace = true;
        line.loop = false;
        line.positionCount = 2;
        line.startWidth = width;
        line.endWidth = width;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        line.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        return line;
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

    public static void ApplyColor(LineRenderer line, Color color)
    {
        if (line == null)
            return;

        if (line.sharedMaterial != null)
            ApplyColor(line.sharedMaterial, color);
    }
}
