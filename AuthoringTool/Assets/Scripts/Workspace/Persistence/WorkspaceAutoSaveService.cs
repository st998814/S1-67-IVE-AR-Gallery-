using System.Collections;
using ARGallery.AppFlow;
using UnityEngine;

namespace ARGallery.Workspace.Persistence
{
    /// <summary>
    /// Debounced write of <see cref="WorkspaceSnapshot"/> to disk via <see cref="WorkspaceSnapshotRepository"/>.
    /// Does not call backend APIs.
    /// </summary>
    public sealed class WorkspaceAutoSaveService : MonoBehaviour
    {
        private const string LogPrefix = "[WorkspacePersistence] ";

        [SerializeField] [Min(0.5f)] private float debounceSeconds = 3f;

        private readonly WorkspaceSnapshotRepository snapshotRepository = new WorkspaceSnapshotRepository();
        private Coroutine debounceCoroutine;
        private bool saveInProgress;

        /// <summary>Call after edits; schedules a save after <see cref="debounceSeconds"/> (timer resets on each call).</summary>
        public void NotifyWorkspaceChanged()
        {
            if (!isActiveAndEnabled)
            {
                Debug.Log($"{LogPrefix}NotifyWorkspaceChanged skipped (component inactive/disabled).");
                return;
            }

            if (debounceCoroutine != null)
                StopCoroutine(debounceCoroutine);
            debounceCoroutine = StartCoroutine(DebounceThenSave());
            Debug.Log($"{LogPrefix}NotifyWorkspaceChanged → debounced save in {debounceSeconds}s.");
        }

        private IEnumerator DebounceThenSave()
        {
            yield return new WaitForSeconds(debounceSeconds);
            debounceCoroutine = null;
            Debug.Log($"{LogPrefix}Debounce elapsed → SaveNow().");
            SaveNow();
        }

        /// <summary>Flush snapshot immediately. Skips if a save is already in progress.</summary>
        public void SaveNow()
        {
            if (!isActiveAndEnabled)
            {
                Debug.Log($"{LogPrefix}SaveNow skipped (component inactive/disabled).");
                return;
            }
            FlushSnapshotToDisk();
        }

        /// <summary>
        /// Writes <see cref="WorkspaceSnapshot"/> immediately, cancelling any pending debounce.
        /// Does not require the component to be enabled (call before scene unload or after session is still valid).
        /// </summary>
        public void FlushSnapshotToDisk()
        {
            Debug.Log($"{LogPrefix}FlushSnapshotToDisk begin | persistentDataPath={Application.persistentDataPath}");

            if (debounceCoroutine != null)
            {
                StopCoroutine(debounceCoroutine);
                debounceCoroutine = null;
                Debug.Log($"{LogPrefix}Cancelled pending debounce coroutine.");
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

                int targetCount = registry.GetTargetsOrdered().Count;
                int contentCount = registry.GetContentsOrdered().Count;
                Debug.Log($"{LogPrefix}Session workspaceId={workspaceId} name={workspaceName} | registry targets={targetCount} contents={contentCount}");

                WorkspaceSnapshot existing = null;
                snapshotRepository.TryLoadSnapshot(workspaceId, out existing);

                WorkspaceSnapshot snapshot = WorkspaceStateSerializer.BuildSnapshot(
                    workspaceId,
                    workspaceName,
                    registry,
                    existing);

                int snapTargets = snapshot.targets != null ? snapshot.targets.Length : 0;
                int snapContents = snapshot.contents != null ? snapshot.contents.Length : 0;
                Debug.Log($"{LogPrefix}Built snapshot: targets={snapTargets} contents={snapContents}");

                if (!snapshotRepository.TrySaveSnapshot(snapshot, out string err))
                    Debug.LogWarning($"{LogPrefix}TrySaveSnapshot failed: {err}");
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
