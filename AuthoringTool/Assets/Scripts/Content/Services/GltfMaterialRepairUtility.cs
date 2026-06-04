using UnityEngine;
using UnityEngine.Rendering;

namespace ARGallery.Content
{
    /// <summary>
    /// Replaces glTFast materials whose shaders were stripped from WebGL builds (pink meshes).
    /// </summary>
    public static class GltfMaterialRepairUtility
    {
        private const string FallbackResourcePath = "GltfRuntimeLit";

        private static Material _fallbackTemplate;
        private static Shader _cachedFallbackShader;

        public static void RepairHierarchy(Transform root)
        {
            if (root == null)
                return;

            Shader fallbackShader = ResolveFallbackShader();
            if (fallbackShader == null)
                return;

            Material template = GetFallbackTemplate();
            if (template != null && (template.shader == null || !IsUsableShader(template.shader)))
                template = null;

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                Renderer renderer = renderers[r];
                if (renderer == null)
                    continue;

                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (!ShouldRemapMaterial(materials[i]))
                        continue;

                    materials[i] = CreateFallbackMaterial(materials[i], template, fallbackShader);
                    changed = true;
                }

                if (changed)
                    renderer.sharedMaterials = materials;
            }
        }

        private static Material GetFallbackTemplate()
        {
            if (_fallbackTemplate == null)
                _fallbackTemplate = Resources.Load<Material>(FallbackResourcePath);
            return _fallbackTemplate;
        }

        private static Shader ResolveFallbackShader()
        {
            if (_cachedFallbackShader != null)
                return _cachedFallbackShader;

            Material template = GetFallbackTemplate();
            if (template != null && IsUsableShader(template.shader))
            {
                _cachedFallbackShader = template.shader;
                return _cachedFallbackShader;
            }

            _cachedFallbackShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Lit");
            return _cachedFallbackShader;
        }

        private static bool ShouldRemapMaterial(Material material)
        {
            if (material == null)
                return false;

            if (Application.platform == RuntimePlatform.WebGLPlayer)
                return true;

            return NeedsRepair(material);
        }

        private static bool NeedsRepair(Material material)
        {
            Shader shader = material.shader;
            if (shader == null)
                return true;

            if (!IsUsableShader(shader))
                return true;

            string name = shader.name;
            if (string.IsNullOrEmpty(name))
                return true;

            return name.IndexOf("glTF", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("InternalError", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsUsableShader(Shader shader)
        {
            return shader != null
                && shader.isSupported
                && shader.name.IndexOf("InternalError", System.StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static Material CreateFallbackMaterial(Material source, Material template, Shader fallbackShader)
        {
            Material result;
            if (template != null && IsUsableShader(template.shader))
                result = new Material(template);
            else
                result = new Material(fallbackShader);

            result.name = source != null ? $"{source.name} (RuntimeLit)" : "GltfRuntimeLit (Runtime)";
            CopyColorAndTextures(source, result);
            return result;
        }

        private static void CopyColorAndTextures(Material source, Material target)
        {
            if (source == null || target == null)
                return;

            if (source.HasProperty("_BaseColor") && target.HasProperty("_BaseColor"))
                target.SetColor("_BaseColor", source.GetColor("_BaseColor"));
            else if (source.HasProperty("baseColorFactor") && target.HasProperty("_BaseColor"))
                target.SetColor("_BaseColor", source.GetColor("baseColorFactor"));

            if (source.HasProperty("_Color") && target.HasProperty("_Color"))
                target.SetColor("_Color", target.HasProperty("_BaseColor") ? target.GetColor("_BaseColor") : source.GetColor("_Color"));

            Texture baseMap = null;
            if (source.HasProperty("baseColorTexture"))
                baseMap = source.GetTexture("baseColorTexture");
            if (baseMap == null && source.HasProperty("_BaseMap"))
                baseMap = source.GetTexture("_BaseMap");
            if (baseMap == null && source.HasProperty("_MainTex"))
                baseMap = source.GetTexture("_MainTex");

            if (baseMap != null)
            {
                if (target.HasProperty("_BaseMap"))
                    target.SetTexture("_BaseMap", baseMap);
                if (target.HasProperty("_MainTex"))
                    target.SetTexture("_MainTex", baseMap);
            }

            if (source.HasProperty("_Metallic") && target.HasProperty("_Metallic"))
                target.SetFloat("_Metallic", source.GetFloat("_Metallic"));
            if (source.HasProperty("_Smoothness") && target.HasProperty("_Smoothness"))
                target.SetFloat("_Smoothness", source.GetFloat("_Smoothness"));

            CopySurfaceState(source, target);
        }

        private static void CopySurfaceState(Material source, Material target)
        {
            if (source == null || target == null)
                return;

            if (source.HasProperty("_Surface") && target.HasProperty("_Surface"))
            {
                float surface = source.GetFloat("_Surface");
                target.SetFloat("_Surface", surface);
                if (surface > 0.5f)
                    ConfigureTransparent(target);
            }
        }

        private static void ConfigureTransparent(Material material)
        {
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.renderQueue = (int)RenderQueue.Transparent;
            if (material.HasProperty("_Surface"))
                material.SetFloat("_Surface", 1f);
        }
    }
}
