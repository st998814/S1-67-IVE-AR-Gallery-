using UnityEngine;

namespace MobileViewer.Content
{
    /// <summary>
    /// Ensures URP Lit fallback material is loaded at startup so the shader is included in player builds.
    /// This prevents pink materials when loading glTF content on mobile players.
    /// </summary>
    public static class GltfRuntimeShaderBootstrap
    {
        private static Material _warmupMaterial;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Warmup()
        {
            _warmupMaterial = Resources.Load<Material>("GltfRuntimeLit");
        }
    }
}
