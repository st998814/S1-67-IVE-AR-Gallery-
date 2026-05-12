using UnityEngine;

namespace ARGallery.Workspace.Persistence
{
    /// <summary>
    /// Ensures an <see cref="AuthoredObjectRegistry"/> exists early in the authoring scene so
    /// <see cref="AuthoredTargetInstance"/> / <see cref="AuthoredContentInstance"/> can register in <c>Start</c>.
    /// Place once per authoring scene; optional prefab duplicates the default runtime-created setup for Inspector clarity.
    /// </summary>
    [DefaultExecutionOrder(-300)]
    public sealed class WorkspacePersistenceBootstrap : MonoBehaviour
    {
        [Tooltip("Prefab whose root has AuthoredObjectRegistry (see Prefabs/Workspace/AuthoredObjectRegistryRoot). If null, a GameObject is created at runtime.")]
        [SerializeField] private GameObject authoredRegistryPrefab;

        [Tooltip("If true, registry survives scene unload (e.g. TargetInstantiation round-trip).")]
        [SerializeField] private bool persistRegistryAcrossScenes;

        private void Awake()
        {
            EnsureRegistry();
        }

        /// <summary>Idempotent; safe to call from other bootstrap code.</summary>
        public static AuthoredObjectRegistry EnsureRegistry(GameObject authoredRegistryPrefab = null, bool persistAcrossScenes = false)
        {
            AuthoredObjectRegistry existing = FindFirstObjectByType<AuthoredObjectRegistry>();
            if (existing != null)
            {
                ApplyDontDestroyIfNeeded(existing.gameObject, persistAcrossScenes);
                return existing;
            }

            GameObject host;
            if (authoredRegistryPrefab != null)
            {
                host = Object.Instantiate(authoredRegistryPrefab);
                host.name = "AuthoredObjectRegistry";
                if (host.GetComponent<AuthoredObjectRegistry>() == null)
                    host.AddComponent<AuthoredObjectRegistry>();
            }
            else
            {
                host = new GameObject("AuthoredObjectRegistry");
                host.AddComponent<AuthoredObjectRegistry>();
            }

            ApplyDontDestroyIfNeeded(host, persistAcrossScenes);
            return host.GetComponent<AuthoredObjectRegistry>();
        }

        private void EnsureRegistry()
        {
            EnsureRegistry(authoredRegistryPrefab, persistRegistryAcrossScenes);
        }

        private static void ApplyDontDestroyIfNeeded(GameObject host, bool persist)
        {
            if (host == null || !persist)
                return;
            Object.DontDestroyOnLoad(host);
        }
    }
}
