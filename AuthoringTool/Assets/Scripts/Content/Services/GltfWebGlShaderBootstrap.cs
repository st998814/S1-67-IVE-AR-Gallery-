using UnityEngine;

namespace ARGallery.Content
{
    /// <summary>
    /// Ensures the WebGL fallback URP Lit material is loaded and referenced at startup
    /// so the shader is present in the player build.
    /// </summary>
    public static class GltfWebGlShaderBootstrap
    {
        private static Material _warmupMaterial;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Warmup()
        {
            _warmupMaterial = Resources.Load<Material>("GltfRuntimeLit");
        }
    }
}
