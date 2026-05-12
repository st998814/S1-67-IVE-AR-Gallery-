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
        [SerializeField] [Min(0.5f)] private float debounceSeconds = 3f;

        private readonly WorkspaceSnapshotRepository snapshotRepository = new WorkspaceSnapshotRepository();
        private Coroutine debounceCoroutine;
        private bool saveInProgress;

        /// <summary>Call after edits; schedules a save after <see cref="debounceSeconds"/> (timer resets on each call).</summary>
        public void NotifyWorkspaceChanged()
        {
            if (!isActiveAndEnabled)
                return;

            if (debounceCoroutine != null)
                StopCoroutine(debounceCoroutine);
            debounceCoroutine = StartCoroutine(DebounceThenSave());
        }

        private IEnumerator DebounceThenSave()
        {
            yield return new WaitForSeconds(debounceSeconds);
            debounceCoroutine = null;
            SaveNow();
        }

        /// <summary>Flush snapshot immediately. Skips if a save is already in progress.</summary>
        public void SaveNow()
        {
            if (!isActiveAndEnabled)
                return;
            if (saveInProgress)
                return;

            saveInProgress = true;
            try
            {
                if (!AppFlowController.TryGetWorkspaceSession(out WorkspaceSessionContext session) || session == null
                    || string.IsNullOrWhiteSpace(session.workspaceId))
                {
                    Debug.LogWarning("WorkspaceAutoSaveService: no workspace session; skipped.");
                    return;
                }

                AuthoredObjectRegistry registry = AuthoredObjectRegistry.Instance;
                if (registry == null)
                {
                    Debug.LogWarning("WorkspaceAutoSaveService: AuthoredObjectRegistry missing; skipped.");
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
                    Debug.LogWarning($"WorkspaceAutoSaveService: save failed: {err}");
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
