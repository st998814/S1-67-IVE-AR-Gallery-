using System;
using System.Collections;
using ARGallery.AppFlow;
using UnityEngine;

namespace ARGallery.Workspace.Persistence
{
    /// <summary>
    /// Debounces authoring edits and signals <see cref="WorkspaceRemoteSyncService"/> via <see cref="DebouncedWorkspaceChanged"/>.
    /// Local snapshot.json / asset disk writes (Layer 2) are disabled; backend sync is Layer 3.
    /// </summary>
    public sealed class WorkspaceAutoSaveService : MonoBehaviour
    {
        private const string LogPrefix = "[WorkspacePersistence] ";

        /// <summary>Fired after the debounce quiet period when a remote sync pass should run.</summary>
        public event Action DebouncedWorkspaceChanged;

        [SerializeField] [Min(0.5f)] private float debounceSeconds = 3f;

        private Coroutine debounceCoroutine;
        private float debounceQuietAfterRealtime;

        /// <summary>
        /// Call after edits. Each call extends the quiet period by <see cref="debounceSeconds"/>, then raises
        /// <see cref="DebouncedWorkspaceChanged"/> once.
        /// </summary>
        public void NotifyWorkspaceChanged()
        {
            if (!isActiveAndEnabled)
            {
                Debug.Log($"{LogPrefix}NotifyWorkspaceChanged skipped (component inactive/disabled).");
                return;
            }

            debounceQuietAfterRealtime = Time.realtimeSinceStartup + debounceSeconds;
            if (debounceCoroutine == null)
                debounceCoroutine = StartCoroutine(DebounceUntilQuietThenNotify());
        }

        private IEnumerator DebounceUntilQuietThenNotify()
        {
            while (Time.realtimeSinceStartup < debounceQuietAfterRealtime)
                yield return null;

            debounceCoroutine = null;
            RaiseDebouncedChanged();
        }

        /// <summary>Cancel debounce and request an immediate remote sync pass.</summary>
        public void SaveNow()
        {
            if (!isActiveAndEnabled)
            {
                Debug.Log($"{LogPrefix}SaveNow skipped (component inactive/disabled).");
                return;
            }

            CancelDebounce();
            RaiseDebouncedChanged();
        }

        /// <summary>Legacy Layer 2 entry point — no disk write; triggers debounced sync unless suppressed.</summary>
        [Obsolete("Local snapshot persistence is disabled. Use NotifyWorkspaceChanged or SaveNow.")]
        public void FlushSnapshotToDisk(bool suppressSnapshotSaved = false)
        {
            CancelDebounce();
            if (!suppressSnapshotSaved)
                RaiseDebouncedChanged();
        }

        private void CancelDebounce()
        {
            if (debounceCoroutine != null)
            {
                StopCoroutine(debounceCoroutine);
                debounceCoroutine = null;
            }
        }

        private void RaiseDebouncedChanged()
        {
            if (!AppFlowController.TryGetWorkspaceSession(out WorkspaceSessionContext session) || session == null
                || string.IsNullOrWhiteSpace(session.workspaceId))
            {
                Debug.LogWarning($"{LogPrefix}Debounced sync skipped: no workspace session.");
                return;
            }

            if (AuthoredObjectRegistry.Instance == null)
            {
                Debug.LogWarning($"{LogPrefix}Debounced sync skipped: AuthoredObjectRegistry.Instance is null.");
                return;
            }

            DebouncedWorkspaceChanged?.Invoke();
        }

        private void OnDisable()
        {
            CancelDebounce();
        }
    }
}
