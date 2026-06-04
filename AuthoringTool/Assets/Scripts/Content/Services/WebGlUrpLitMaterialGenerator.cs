using GLTFast;
using GLTFast.Logging;
using GLTFast.Materials;
using GLTFast.Schema;
using UnityEngine;
using UnityEngine.Rendering;

namespace ARGallery.Content
{
    /// <summary>
    /// WebGL-safe glTFast material generator: always uses URP Lit instead of glTF shader graphs
    /// (which often compile but still render magenta in WebGL builds).
    /// </summary>
    public sealed class WebGlUrpLitMaterialGenerator : IMaterialGenerator
    {
        public static readonly WebGlUrpLitMaterialGenerator Instance = new WebGlUrpLitMaterialGenerator();

        private ICodeLogger _logger;
        private UnityEngine.Material _defaultMaterial;
        private static Shader _cachedUrpLitShader;

        public void SetLogger(ICodeLogger logger) => _logger = logger;

        public UnityEngine.Material GetDefaultMaterial(bool pointsSupport = false)
        {
            if (pointsSupport)
                _logger?.Warning(LogCode.TopologyPointsMaterialUnsupported);

            if (_defaultMaterial == null)
            {
                Shader shader = ResolveUrpLitShader();
                if (shader == null)
                    return null;

                _defaultMaterial = new UnityEngine.Material(shader) { name = MaterialGenerator.DefaultMaterialName };
                if (_defaultMaterial.HasProperty("_BaseColor"))
                    _defaultMaterial.SetColor("_BaseColor", new Color(0.85f, 0.85f, 0.85f, 1f));
            }

            return _defaultMaterial;
        }

        public UnityEngine.Material GenerateMaterial(MaterialBase gltfMaterial, IGltfReadable gltf, bool pointsSupport = false)
        {
            if (pointsSupport)
                _logger?.Warning(LogCode.TopologyPointsMaterialUnsupported);

            Shader shader = ResolveUrpLitShader();
            if (shader == null)
                return null;

            string matName = gltfMaterial != null && !string.IsNullOrEmpty(gltfMaterial.name)
                ? gltfMaterial.name
                : "gltf-material";
            var material = new UnityEngine.Material(shader) { name = matName };

            PbrMetallicRoughnessBase pbr = gltfMaterial?.PbrMetallicRoughness;
            if (pbr != null)
            {
                if (material.HasProperty("_BaseColor"))
                    material.SetColor("_BaseColor", pbr.BaseColor);
                if (material.HasProperty("_Color"))
                    material.SetColor("_Color", pbr.BaseColor);
                if (material.HasProperty("_Metallic"))
                    material.SetFloat("_Metallic", pbr.metallicFactor);
                if (material.HasProperty("_Smoothness"))
                    material.SetFloat("_Smoothness", Mathf.Clamp01(1f - pbr.roughnessFactor));

                TryAssignTexture(pbr.BaseColorTexture, gltf, material, "_BaseMap");
                TryAssignTexture(pbr.BaseColorTexture, gltf, material, "_MainTex");
                TryAssignTexture(pbr.MetallicRoughnessTexture, gltf, material, "_MetallicGlossMap");
            }

            if (gltfMaterial != null)
            {
                switch (gltfMaterial.GetAlphaMode())
                {
                    case MaterialBase.AlphaMode.Blend:
                        ConfigureTransparent(material);
                        break;
                    case MaterialBase.AlphaMode.Mask:
                        if (material.HasProperty("_AlphaClip"))
                            material.SetFloat("_AlphaClip", 1f);
                        if (material.HasProperty("_Cutoff"))
                            material.SetFloat("_Cutoff", gltfMaterial.alphaCutoff);
                        break;
                }
            }

            return material;
        }

        private static void TryAssignTexture(
            TextureInfoBase textureInfo,
            IGltfReadable gltf,
            UnityEngine.Material material,
            string propertyName)
        {
            if (textureInfo == null || textureInfo.index < 0 || gltf == null || material == null)
                return;
            if (!material.HasProperty(propertyName))
                return;

            Texture2D texture = gltf.GetTexture(textureInfo.index);
            if (texture != null)
                material.SetTexture(propertyName, texture);
        }

        private static void ConfigureTransparent(UnityEngine.Material material)
        {
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.renderQueue = (int)RenderQueue.Transparent;
            if (material.HasProperty("_Surface"))
                material.SetFloat("_Surface", 1f);
        }

        private static Shader ResolveUrpLitShader()
        {
            if (_cachedUrpLitShader != null)
                return _cachedUrpLitShader;

            UnityEngine.Material template = Resources.Load<UnityEngine.Material>("GltfRuntimeLit");
            if (template != null && template.shader != null)
            {
                _cachedUrpLitShader = template.shader;
                return _cachedUrpLitShader;
            }

            _cachedUrpLitShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Lit");
            return _cachedUrpLitShader;
        }
    }
}
