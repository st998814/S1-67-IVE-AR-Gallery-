using System;
using System.Collections;
using ARGallery.AppFlow;
using UnityEngine;

namespace ARGallery.Workspace.Persistence
{
    /// <summary>
    /// Debounced write of <see cref="WorkspaceSnapshot"/> to disk via <see cref="WorkspaceSnapshotRepository"/>.
    /// Does not call backend APIs. After a successful save (unless suppressed), raises <see cref="SnapshotSaved"/>
    /// so Layer 3 can schedule remote sync without coupling HTTP here.
    /// </summary>
    public sealed class WorkspaceAutoSaveService : MonoBehaviour
    {
        private const string LogPrefix = "[WorkspacePersistence] ";

        /// <summary>
        /// Fired once <see cref="WorkspaceSnapshot"/> was written successfully to disk.
        /// Use <see cref="FlushSnapshotToDisk"/> with <c>suppressSnapshotSaved: true</c> (or save via repository directly)
        /// when persisting merged server state so Layer 3 does not re-enter its own debounce loop.
        /// </summary>
        public event Action<WorkspaceSnapshot> SnapshotSaved;

        [SerializeField] [Min(0.5f)] private float debounceSeconds = 3f;

        private readonly WorkspaceSnapshotRepository snapshotRepository = new WorkspaceSnapshotRepository();
        private readonly WorkspaceAssetRepository assetRepository = new WorkspaceAssetRepository();
        private Coroutine debounceCoroutine;
        private float debounceQuietAfterRealtime;
        private bool saveInProgress;

        /// <summary>
        /// Call after edits. Uses a <b>quiet period</b> after the last call: each call pushes the save time forward by
        /// <see cref="debounceSeconds"/> (same as classic debounce), but without restarting a <see cref="WaitForSeconds"/>
        /// coroutine. High-frequency notifies (e.g. held keyboard nudges) no longer prevent a snapshot from ever writing.
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
                debounceCoroutine = StartCoroutine(DebounceUntilQuietThenSave());
        }

        private IEnumerator DebounceUntilQuietThenSave()
        {
            while (Time.realtimeSinceStartup < debounceQuietAfterRealtime)
                yield return null;

            debounceCoroutine = null;
            // Flush directly so behaviour matches BackToSwitcher (SaveNow would skip when this behaviour is disabled).
            FlushSnapshotToDisk(suppressSnapshotSaved: false);
        }

        /// <summary>Flush snapshot immediately. Skips if a save is already in progress.</summary>
        public void SaveNow()
        {
            if (!isActiveAndEnabled)
            {
                Debug.Log($"{LogPrefix}SaveNow skipped (component inactive/disabled).");
                return;
            }
            FlushSnapshotToDisk(suppressSnapshotSaved: false);
        }

        /// <summary>
        /// Writes <see cref="WorkspaceSnapshot"/> immediately, cancelling any pending debounce.
        /// Does not require the component to be enabled (call before scene unload or after session is still valid).
        /// </summary>
        /// <param name="suppressSnapshotSaved">If true, successful disk write does not raise <see cref="SnapshotSaved"/>.</param>
        public void FlushSnapshotToDisk(bool suppressSnapshotSaved = false)
        {
            if (debounceCoroutine != null)
            {
                StopCoroutine(debounceCoroutine);
                debounceCoroutine = null;
            }

            if (saveInProgress)
            {
                Debug.LogWarning($"{LogPrefix}FlushSnapshotToDisk skipped (save already in progress).");
                return;
            }

            saveInProgress = true;
            try
            {
                if (!AppFlowController.TryGetWorkspaceSession(out WorkspaceSessionContext session) || session == null
                    || string.IsNullOrWhiteSpace(session.workspaceId))
                {
                    Debug.LogWarning($"{LogPrefix}FlushSnapshotToDisk aborted: no workspace session (TryGetWorkspaceSession false or empty workspaceId).");
                    return;
                }

                AuthoredObjectRegistry registry = AuthoredObjectRegistry.Instance;
                if (registry == null)
                {
                    Debug.LogWarning($"{LogPrefix}FlushSnapshotToDisk aborted: AuthoredObjectRegistry.Instance is null (bootstrap missing in scene?).");
                    return;
                }

                string workspaceId = session.workspaceId.Trim();
                string workspaceName = string.IsNullOrWhiteSpace(session.workspaceName) ? workspaceId : session.workspaceName.Trim();

                WorkspaceSnapshot existing = null;
                snapshotRepository.TryLoadSnapshot(workspaceId, out existing);

                WorkspaceSnapshot snapshot = WorkspaceStateSerializer.BuildSnapshot(
                    workspaceId,
                    workspaceName,
                    registry,
                    existing);

                if (!snapshotRepository.TrySaveSnapshot(snapshot, out string err))
                {
                    Debug.LogWarning($"{LogPrefix}TrySaveSnapshot failed: {err}");
                    return;
                }

                assetRepository.PruneUnreferencedContentAssets(workspaceId, snapshot.contents);

                if (!suppressSnapshotSaved)
                    SnapshotSaved?.Invoke(snapshot);
            }
            finally
            {
                saveInProgress = false;
            }
        }

        private void OnDisable()
        {
            if (debounceCoroutine != null)
            {
                StopCoroutine(debounceCoroutine);
                debounceCoroutine = null;
            }
        }
    }
}
