using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ARGallery.Workspace.Persistence
{
    /// <summary>
    /// Scene registry for <see cref="AuthoredTargetInstance"/> and <see cref="AuthoredContentInstance"/> used when building <see cref="WorkspaceSnapshot"/>.
    /// Place one instance in the authoring scene (e.g. on a bootstrap object).
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class AuthoredObjectRegistry : MonoBehaviour
    {
        public static AuthoredObjectRegistry Instance { get; private set; }

        private readonly HashSet<AuthoredTargetInstance> targets = new HashSet<AuthoredTargetInstance>();
        private readonly HashSet<AuthoredContentInstance> contents = new HashSet<AuthoredContentInstance>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("AuthoredObjectRegistry: duplicate registry in scene; destroying duplicate.");
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public static void RegisterTarget(AuthoredTargetInstance instance)
        {
            if (instance == null)
                return;
            if (Instance == null)
            {
                Debug.LogWarning("AuthoredObjectRegistry: RegisterTarget skipped — no AuthoredObjectRegistry in scene.");
                return;
            }

            Instance.targets.Add(instance);
        }

        public static void UnregisterTarget(AuthoredTargetInstance instance)
        {
            if (instance == null || Instance == null)
                return;
            Instance.targets.Remove(instance);
        }

        public static void RegisterContent(AuthoredContentInstance instance)
        {
            if (instance == null)
                return;
            if (Instance == null)
            {
                Debug.LogWarning("AuthoredObjectRegistry: RegisterContent skipped — no AuthoredObjectRegistry in scene.");
                return;
            }

            Instance.contents.Add(instance);
        }

        public static void UnregisterContent(AuthoredContentInstance instance)
        {
            if (instance == null || Instance == null)
                return;
            Instance.contents.Remove(instance);
        }

        /// <summary>Stable order for deterministic JSON.</summary>
        public IReadOnlyList<AuthoredTargetInstance> GetTargetsOrdered()
        {
            return targets
                .Where(t => t != null)
                .OrderBy(t => t.LocalTargetId ?? "")
                .ToList();
        }

        public IReadOnlyList<AuthoredContentInstance> GetContentsOrdered()
        {
            return contents
                .Where(c => c != null && c.gameObject != null && c.gameObject.activeInHierarchy)
                .OrderBy(c => c.TargetId ?? "")
                .ThenBy(c => c.ServerContentId ?? "")
                .ThenBy(c => c.LocalContentId ?? "")
                .ToList();
        }
    }
}
