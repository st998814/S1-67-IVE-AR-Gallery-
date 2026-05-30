using System;
using System.Collections;
using System.IO;
using ARGallery.AppFlow;
using UnityEngine;

namespace ARGallery.Workspace.Persistence
{
    /// <summary>Bootstrap-style UI phases for <see cref="WorkspaceRemoteSyncService.RemoteSyncToastChanged"/>.</summary>
    public enum WorkspaceRemoteSyncToastKind
    {
        Debouncing,
        Syncing,
        Synced,
        Failed,
        Skipped
    }

    /// <summary>
    /// Layer 3: debounced backend sync after successful local snapshot saves (<see cref="WorkspaceAutoSaveService.SnapshotSaved"/>).
    /// Single-flight with <see cref="SyncNow"/> for immediate flush. Does not modify backend repo layout.
    /// </summary>
    public sealed class WorkspaceRemoteSyncService : MonoBehaviour
    {
        private const string LogPrefix = "[WorkspaceRemoteSync] ";

        /// <summary>Fired on the Unity main thread when remote sync status changes (for UXML toast / alerts).</summary>
        public event Action<WorkspaceRemoteSyncToastKind, string> RemoteSyncToastChanged;

        [SerializeField] private MonoBehaviour apiClientBehaviour;
        [SerializeField] [Min(5f)] private float remoteSyncDebounceSeconds = 20f;
        [SerializeField] private float apiTimeoutSeconds = 25f;

        private WorkspaceAutoSaveService _autoSave;
        private readonly WorkspaceSnapshotRepository _snapshotRepo = new WorkspaceSnapshotRepository();
        private readonly WorkspaceAssetRepository _assetRepo = new WorkspaceAssetRepository();
        private readonly TargetWorkflowService _targetWorkflow = new TargetWorkflowService();
        private readonly ContentWorkflowService _contentWorkflow = new ContentWorkflowService();

        private Coroutine _debounceCoroutine;
        private Coroutine _syncCoroutine;
        private bool _syncInProgress;
        private bool _pendingSyncRequested;

        private string _lastUploadUrl;
        private string _lastFailReason;
        private bool _lastStepOk;

        private float EffectiveDebounceSeconds => Mathf.Max(5f, remoteSyncDebounceSeconds);

        private void RaiseRemoteSyncToast(WorkspaceRemoteSyncToastKind kind, string message)
        {
            RemoteSyncToastChanged?.Invoke(kind, message ?? "");
        }

        private void OnEnable()
        {
            _autoSave = FindFirstObjectByType<WorkspaceAutoSaveService>();
            if (_autoSave != null)
                _autoSave.SnapshotSaved += OnSnapshotSaved;
            else
                Debug.LogWarning($"{LogPrefix}WorkspaceAutoSaveService not found — auto remote sync disabled until present.");
        }

        private void OnDisable()
        {
            if (_autoSave != null)
                _autoSave.SnapshotSaved -= OnSnapshotSaved;

            if (_debounceCoroutine != null)
            {
                StopCoroutine(_debounceCoroutine);
                _debounceCoroutine = null;
            }

            if (_syncCoroutine != null)
            {
                StopCoroutine(_syncCoroutine);
                _syncCoroutine = null;
            }
        }

        private void OnSnapshotSaved(WorkspaceSnapshot _)
        {
            if (_syncInProgress)
            {
                _pendingSyncRequested = true;
                return;
            }

            ScheduleDebouncedRemoteSync();
        }

        private void ScheduleDebouncedRemoteSync()
        {
            if (!isActiveAndEnabled)
                return;

            if (_debounceCoroutine != null)
                StopCoroutine(_debounceCoroutine);

            _debounceCoroutine = StartCoroutine(DebounceThenStartSyncRoutine());
        }

        private IEnumerator DebounceThenStartSyncRoutine()
        {
            yield return new WaitForSeconds(EffectiveDebounceSeconds);
            _debounceCoroutine = null;

            if (_syncInProgress)
            {
                _pendingSyncRequested = true;
                yield break;
            }

            StartSyncCoroutineIfIdle();
        }

        /// <summary>
        /// Forces a local snapshot write without raising <see cref="WorkspaceAutoSaveService.SnapshotSaved"/>,
        /// then runs one remote sync pass (or queues if a pass is already running).
        /// </summary>
        public void SyncNow()
        {
            if (_debounceCoroutine != null)
            {
                StopCoroutine(_debounceCoroutine);
                _debounceCoroutine = null;
            }

            if (_autoSave != null)
                _autoSave.FlushSnapshotToDisk(suppressSnapshotSaved: true);

            if (_syncInProgress)
            {
                _pendingSyncRequested = true;
                return;
            }

            if (_syncCoroutine != null)
                StopCoroutine(_syncCoroutine);
            _syncCoroutine = StartCoroutine(SyncCoroutineWrapper());
        }

        private void StartSyncCoroutineIfIdle()
        {
            if (_syncInProgress)
                return;

            if (_syncCoroutine != null)
                StopCoroutine(_syncCoroutine);
            _syncCoroutine = StartCoroutine(SyncCoroutineWrapper());
        }

        private IEnumerator SyncCoroutineWrapper()
        {
            _syncInProgress = true;
            bool runAgain = false;
            try
            {
                yield return StartCoroutine(RunRemoteSyncPass());
            }
            finally
            {
                _syncInProgress = false;
                _syncCoroutine = null;
                if (_pendingSyncRequested)
                {
                    _pendingSyncRequested = false;
                    runAgain = true;
                }
            }

            if (runAgain)
                _syncCoroutine = StartCoroutine(SyncCoroutineWrapper());
        }

        private IEnumerator RunRemoteSyncPass()
        {
            IApiClient api = ResolveApiClient();
            if (api == null)
            {
                PersistRemoteStateFailed("No API client (assign apiClientBehaviour implementing IApiClient).");
                yield break;
            }

            if (!AppFlowController.TryGetWorkspaceSession(out WorkspaceSessionContext session) || session == null
                || string.IsNullOrWhiteSpace(session.workspaceId))
            {
                Debug.LogWarning($"{LogPrefix}Sync skipped: no workspace session.");
                RaiseRemoteSyncToast(WorkspaceRemoteSyncToastKind.Skipped, "Cloud sync skipped: no workspace session.");
                yield break;
            }

            AuthoredObjectRegistry registry = AuthoredObjectRegistry.Instance;
            if (registry == null)
            {
                PersistRemoteStateFailed("AuthoredObjectRegistry missing.");
                yield break;
            }

            string workspaceId = session.workspaceId.Trim();
            string workspaceName = string.IsNullOrWhiteSpace(session.workspaceName) ? workspaceId : session.workspaceName.Trim();

            foreach (AuthoredTargetInstance target in registry.GetTargetsOrdered())
            {
                if (target == null)
                    continue;
                yield return StartCoroutine(SyncTargetToBackend(api, workspaceId, workspaceName, target));
                if (!_lastStepOk)
                {
                    string detail = string.IsNullOrWhiteSpace(_lastFailReason) ? "" : $"\n{_lastFailReason}";
                    PersistRemoteStateFailed($"Target sync failed for '{target.LocalTargetId}'.{detail}");
                    yield break;
                }
            }

            foreach (AuthoredContentInstance content in registry.GetContentsOrdered())
            {
                if (content == null)
                    continue;
                yield return StartCoroutine(SyncContentToBackend(api, workspaceId, content));
                if (!_lastStepOk)
                {
                    string detail = string.IsNullOrWhiteSpace(_lastFailReason) ? "" : $"\n{_lastFailReason}";
                    PersistRemoteStateFailed($"Content sync failed for '{content.LocalContentId}'.{detail}");
                    yield break;
                }
            }

            PersistRemoteStateSuccess(workspaceId, workspaceName, registry);
        }

        private IEnumerator SyncTargetToBackend(IApiClient api, string workspaceId, string workspaceName, AuthoredTargetInstance target)
        {
            _lastStepOk = false;
            string targetId = string.IsNullOrWhiteSpace(target.LocalTargetId) ? target.ServerTargetId : target.LocalTargetId;
            string targetName = string.IsNullOrWhiteSpace(target.TargetName) ? targetId : target.TargetName.Trim();
            string displayLabel = targetName;

            if (!target.RemoteDirty && !string.IsNullOrWhiteSpace(target.LastRemoteSyncedAtUtc))
            {
                _lastStepOk = true;
                yield break;
            }

            string imageUrl = "";
            if (!string.IsNullOrWhiteSpace(target.TargetImageLocalPath))
            {
                string full = _assetRepo.ResolveFullPath(workspaceId, target.TargetImageLocalPath);
                if (File.Exists(full))
                {
                    byte[] bytes;
                    try
                    {
                        bytes = File.ReadAllBytes(full);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"{LogPrefix}Target image read failed: {ex.Message}");
                        yield break;
                    }

                    string uploadName = string.IsNullOrWhiteSpace(target.OriginalFileName)
                        ? Path.GetFileName(full)
                        : target.OriginalFileName.Trim();
                    string stableUploadName = StableTargetDiskFileName(targetId, uploadName);
                    yield return StartCoroutine(UploadBytesAndWait(api, bytes, stableUploadName, GuessMimeTypeFromName(stableUploadName), "target", targetId));
                    if (string.IsNullOrWhiteSpace(_lastUploadUrl))
                    {
                        Debug.LogWarning($"{LogPrefix}Target image upload failed for '{targetId}'.");
                        yield break;
                    }

                    imageUrl = _lastUploadUrl.Trim();
                }
            }

            bool apiOk = false;
            _lastFailReason = null;
            IApiRequestHandle handle = _targetWorkflow.SyncCreateTarget(
                api,
                target.gameObject,
                targetId,
                targetName,
                displayLabel,
                imageUrl,
                workspaceId,
                workspaceName,
                result =>
                {
                    apiOk = result != null && result.success;
                    if (!apiOk && result != null)
                        _lastFailReason = BuildHttpFailDetail(result.statusCode, result.errorCode, result.message);
                },
                apiTimeoutSeconds);

            yield return WaitForRequest(handle);
            if (handle != null && handle.IsCancelled)
                apiOk = false;

            if (apiOk)
            {
                target.RemoteDirty = false;
                target.LastRemoteSyncedAtUtc = DateTime.UtcNow.ToString("o");
            }

            _lastStepOk = apiOk;
        }

        private IEnumerator SyncContentToBackend(IApiClient api, string workspaceId, AuthoredContentInstance c)
        {
            _lastStepOk = false;
            string contentType = string.IsNullOrWhiteSpace(c.ContentType) ? "unknown" : c.ContentType.Trim();
            if (string.Equals(contentType, "unknown", StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning($"{LogPrefix}Skip content with unknown type (localId={c.LocalContentId}).");
                _lastStepOk = true;
                yield break;
            }

            if (string.IsNullOrWhiteSpace(c.ServerContentId))
            {
                if (string.IsNullOrWhiteSpace(c.LocalContentId))
                    c.LocalContentId = Guid.NewGuid().ToString("N");
                c.ServerContentId = c.LocalContentId;
            }

            if (!c.RemoteDirty && !string.IsNullOrWhiteSpace(c.LastRemoteSyncedAtUtc))
            {
                _lastStepOk = true;
                yield break;
            }

            string targetId = c.TargetId ?? "";
            Transform tr = c.transform;
            Vector3 pos = tr.localPosition;
            Vector3 euler = tr.localEulerAngles;
            Vector3 scale = tr.localScale;

            string mediaUrl = "";
            string normalized = contentType.ToLowerInvariant();

            if (normalized == "text")
            {
                mediaUrl = string.IsNullOrEmpty(c.TextBody) ? (c.Title ?? "") : c.TextBody;
            }
            else if (normalized == "image" || normalized == "video" || normalized == "model")
            {
                if (!string.IsNullOrWhiteSpace(c.MediaUrl) && (c.MediaUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                                                              || c.MediaUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                {
                    mediaUrl = c.MediaUrl.Trim();
                }
                else if (!string.IsNullOrWhiteSpace(c.AssetLocalPath))
                {
                    string full = _assetRepo.ResolveFullPath(workspaceId, c.AssetLocalPath);
                    if (File.Exists(full))
                    {
                        byte[] bytes;
                        try
                        {
                            bytes = File.ReadAllBytes(full);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"{LogPrefix}Content asset read failed: {ex.Message}");
                            c.UploadPending = true;
                            yield break;
                        }

                        string uploadName = string.IsNullOrWhiteSpace(c.OriginalFileName)
                            ? Path.GetFileName(full)
                            : c.OriginalFileName.Trim();
                        yield return StartCoroutine(UploadBytesAndWait(api, bytes, uploadName, GuessMimeTypeFromName(uploadName), "content", null, c.ServerContentId));
                        if (string.IsNullOrWhiteSpace(_lastUploadUrl))
                        {
                            Debug.LogWarning($"{LogPrefix}Content upload failed for '{c.LocalContentId}'.");
                            c.UploadPending = true;
                            yield break;
                        }

                        mediaUrl = _lastUploadUrl.Trim();
                    }
                    else
                    {
                        Debug.LogWarning($"{LogPrefix}Best-effort skip: no file for '{normalized}' content '{c.LocalContentId}'.");
                        c.UploadPending = true;
                        _lastStepOk = true;
                        yield break;
                    }
                }
                else
                {
                    Debug.LogWarning($"{LogPrefix}Best-effort skip: no media URL or asset path for '{c.LocalContentId}'.");
                    c.UploadPending = true;
                    _lastStepOk = true;
                    yield break;
                }
            }
            else
            {
                Debug.LogWarning($"{LogPrefix}Unsupported content type '{contentType}' for remote sync — skipped.");
                _lastStepOk = true;
                yield break;
            }

            bool requiresMedia = normalized == "image" || normalized == "video" || normalized == "model";
            if (requiresMedia && string.IsNullOrWhiteSpace(mediaUrl))
            {
                c.UploadPending = true;
                yield break;
            }

            bool apiOk = false;
            _lastFailReason = null;
            IApiRequestHandle handle = _contentWorkflow.SyncCreateContent(
                api,
                contentType,
                pos,
                euler,
                scale,
                mediaUrl ?? "",
                targetId,
                result =>
                {
                    apiOk = result != null && result.success;
                    if (!apiOk && result != null)
                        _lastFailReason = BuildHttpFailDetail(result.statusCode, result.errorCode, result.message);
                },
                apiTimeoutSeconds,
                c.ServerContentId,
                c.Title,
                c.Description,
                c.TextBody,
                c.LocalContentId);

            yield return WaitForRequest(handle);
            if (handle != null && handle.IsCancelled)
                apiOk = false;

            if (apiOk)
            {
                if (!string.IsNullOrWhiteSpace(mediaUrl))
                    c.MediaUrl = mediaUrl;
                c.UploadPending = false;
                c.PersistPending = false;
                c.IsUnsaved = false;
                c.RemoteDirty = false;
                c.LastRemoteSyncedAtUtc = DateTime.UtcNow.ToString("o");
            }

            _lastStepOk = apiOk;
        }

        private IEnumerator UploadBytesAndWait(IApiClient api, byte[] bytes, string fileName, string mimeType, string uploadCategory = "content", string stableTargetId = null, string stableContentId = null)
        {
            _lastUploadUrl = null;
            if (bytes == null || bytes.Length == 0)
                yield break;

            string cat = string.IsNullOrWhiteSpace(uploadCategory) ? "content" : uploadCategory.Trim().ToLowerInvariant();
            var request = new UploadFileRequestDto
            {
                fileName = string.IsNullOrWhiteSpace(fileName) ? "upload.bin" : fileName.Trim(),
                mimeType = string.IsNullOrWhiteSpace(mimeType) ? "application/octet-stream" : mimeType,
                fileBytes = bytes,
                uploadCategory = cat,
                targetId = cat == "target" && !string.IsNullOrWhiteSpace(stableTargetId) ? stableTargetId.Trim() : "",
                contentId = cat == "content" && !string.IsNullOrWhiteSpace(stableContentId) ? stableContentId.Trim() : "",
                meta = new ApiSyncMetaDto
                {
                    schemaVersion = "v1",
                    clientRequestId = Guid.NewGuid().ToString("N"),
                    createdAtUtc = DateTime.UtcNow.ToString("o")
                }
            };

            IApiRequestHandle handle = api.UploadFile(
                request,
                result =>
                {
                    if (result != null && result.success && result.payload != null && !string.IsNullOrWhiteSpace(result.payload.url))
                        _lastUploadUrl = result.payload.url.Trim();
                    else if (result != null && !result.success)
                        _lastFailReason = BuildHttpFailDetail(result.statusCode, result.errorCode, result.message);
                },
                apiTimeoutSeconds);

            yield return WaitForRequest(handle);
        }

        private static IEnumerator WaitForRequest(IApiRequestHandle handle)
        {
            if (handle == null)
                yield break;

            while (!handle.IsDone)
                yield return null;
        }

        private void PersistRemoteStateSuccess(string workspaceId, string workspaceName, AuthoredObjectRegistry registry)
        {
            WorkspaceSnapshot existing = null;
            _snapshotRepo.TryLoadSnapshot(workspaceId, out existing);
            WorkspaceSnapshot snap = WorkspaceStateSerializer.BuildSnapshot(workspaceId, workspaceName, registry, existing);
            snap.remoteDirty = false;
            snap.lastRemoteSyncError = "";
            snap.lastRemoteSyncedAtUtc = DateTime.UtcNow.ToString("o");
            snap.remoteSyncStatus = RemoteSyncStatus.Synced;

            string snapshotPath = WorkspacePersistencePaths.GetSnapshotPath(workspaceId);
            if (!_snapshotRepo.TrySaveSnapshot(snap, out string err, logSuccess: false))
            {
                Debug.LogWarning($"{LogPrefix}Post-sync snapshot save failed: {err}");
            }
            else
            {
                _assetRepo.PruneUnreferencedContentAssets(workspaceId, snap.contents);
                // One line after upload (avoids duplicate TrySaveSnapshot OK in the same frame as completion).
                Debug.Log($"[WorkspacePersistence] Remote sync completed successfully for workspace '{workspaceId}' | path={snapshotPath}");
            }

            RaiseRemoteSyncToast(WorkspaceRemoteSyncToastKind.Synced, "Workspace synchronized with the server.");
        }

        private void PersistRemoteStateFailed(string message)
        {
            string m = string.IsNullOrWhiteSpace(message) ? "Remote sync failed." : message.Trim();
            Debug.LogWarning($"{LogPrefix}{m}");
            RaiseRemoteSyncToast(WorkspaceRemoteSyncToastKind.Failed, m);

            if (!AppFlowController.TryGetWorkspaceSession(out WorkspaceSessionContext session) || session == null
                || string.IsNullOrWhiteSpace(session.workspaceId))
                return;

            AuthoredObjectRegistry registry = AuthoredObjectRegistry.Instance;
            if (registry == null)
                return;

            string workspaceId = session.workspaceId.Trim();
            string workspaceName = string.IsNullOrWhiteSpace(session.workspaceName) ? workspaceId : session.workspaceName.Trim();

            WorkspaceSnapshot existing = null;
            _snapshotRepo.TryLoadSnapshot(workspaceId, out existing);
            WorkspaceSnapshot snap = WorkspaceStateSerializer.BuildSnapshot(workspaceId, workspaceName, registry, existing);
            snap.remoteDirty = true;
            snap.lastRemoteSyncError = m;
            snap.remoteSyncStatus = RemoteSyncStatus.Failed;

            if (!_snapshotRepo.TrySaveSnapshot(snap, out string err, logSuccess: false))
                Debug.LogWarning($"{LogPrefix}Failed-state snapshot save error: {err}");
            else
                _assetRepo.PruneUnreferencedContentAssets(workspaceId, snap.contents);
        }

        private IApiClient ResolveApiClient()
        {
            if (apiClientBehaviour is IApiClient client)
                return client;

            Debug.LogWarning($"{LogPrefix}apiClientBehaviour must implement IApiClient.");
            return null;
        }

        private static string BuildHttpFailDetail(int statusCode, string errorCode, string message)
        {
            var sb = new System.Text.StringBuilder();
            if (statusCode > 0) sb.Append($"HTTP {statusCode}");
            if (!string.IsNullOrWhiteSpace(errorCode))
            {
                if (sb.Length > 0) sb.Append(" · ");
                sb.Append(errorCode);
            }
            string header = sb.ToString();
            string body = string.IsNullOrWhiteSpace(message) ? "" : message.Trim();
            if (string.IsNullOrWhiteSpace(header)) return body;
            if (string.IsNullOrWhiteSpace(body)) return header;
            return $"{header}\n{body}";
        }

        private static string StableTargetDiskFileName(string targetId, string originalNameForExt)
        {
            string ext = Path.GetExtension(string.IsNullOrWhiteSpace(originalNameForExt) ? "" : originalNameForExt);
            ext = string.IsNullOrEmpty(ext) ? ".jpg" : ext.ToLowerInvariant();
            if (ext != ".png" && ext != ".jpg" && ext != ".jpeg" && ext != ".gif" && ext != ".webp")
                ext = ".jpg";

            string id = string.IsNullOrWhiteSpace(targetId) ? "target" : targetId.Trim();
            char[] chArr = id.ToCharArray();
            for (int i = 0; i < chArr.Length; i++)
            {
                char c = chArr[i];
                if (char.IsWhiteSpace(c) || c == '/' || c == '\\' || c == ':' || c == '*' || c == '?' || c == '"' || c == '<' || c == '>' || c == '|')
                    chArr[i] = '_';
            }

            string safe = new string(chArr).Trim('_');
            if (string.IsNullOrEmpty(safe))
                safe = "target";
            return $"{safe}{ext}";
        }

        private static string GuessMimeTypeFromName(string fileName) =>
            UploadWorkflowService.GuessMimeTypeFromExtension(Path.GetExtension(fileName ?? ""));
    }
}
