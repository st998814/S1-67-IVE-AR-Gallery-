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
    /// Layer 3: remote sync from in-scene <see cref="AuthoredObjectRegistry"/> (no local snapshot.json writes).
    /// Debounced via <see cref="WorkspaceAutoSaveService.DebouncedWorkspaceChanged"/>.
    /// Uses single-flight execution with queued reruns when changes land during an active pass.
    /// </summary>
    public sealed class WorkspaceRemoteSyncService : MonoBehaviour
    {
        private const string LogPrefix = "[WorkspaceRemoteSync] ";

        /// <summary>Fired on the Unity main thread when remote sync status changes (for UXML toast / alerts).</summary>
        public event Action<WorkspaceRemoteSyncToastKind, string> RemoteSyncToastChanged;

        [SerializeField] private MonoBehaviour apiClientBehaviour;
        [SerializeField] private float apiTimeoutSeconds = 25f;

        private WorkspaceAutoSaveService _autoSave;
        private readonly TargetWorkflowService _targetWorkflow = new TargetWorkflowService();
        private readonly ContentWorkflowService _contentWorkflow = new ContentWorkflowService();

        private Coroutine _syncCoroutine;
        private bool _syncInProgress;
        private bool _pendingSyncRequested;

        private string _lastUploadUrl;
        private string _lastFailReason;
        private bool _lastStepOk;

        /// <summary>Workspace ids for the active <see cref="RunRemoteSyncPass"/> (explicit or from session).</summary>
        private string _activePassWorkspaceId;
        private string _activePassWorkspaceName;

        private void RaiseRemoteSyncToast(WorkspaceRemoteSyncToastKind kind, string message)
        {
            RemoteSyncToastChanged?.Invoke(kind, message ?? "");
        }

        private void OnEnable()
        {
            _autoSave = FindFirstObjectByType<WorkspaceAutoSaveService>();
            if (_autoSave != null)
                _autoSave.DebouncedWorkspaceChanged += OnDebouncedWorkspaceChanged;
            else
                Debug.LogWarning($"{LogPrefix}WorkspaceAutoSaveService not found — debounced remote sync disabled until present.");
        }

        private void OnDisable()
        {
            if (_autoSave != null)
                _autoSave.DebouncedWorkspaceChanged -= OnDebouncedWorkspaceChanged;

            if (_syncCoroutine != null)
            {
                StopCoroutine(_syncCoroutine);
                _syncCoroutine = null;
            }
        }

        private void OnDebouncedWorkspaceChanged()
        {
            if (_syncInProgress)
            {
                _pendingSyncRequested = true;
                return;
            }

            StartSyncCoroutineIfIdle();
        }

        /// <summary>
        /// Runs one remote sync pass immediately (or queues if a pass is already running). No local snapshot write.
        /// </summary>
        public void SyncNow()
        {
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
                yield return StartCoroutine(RunRemoteSyncPass(null, null));
            }
            finally
            {
                ClearActivePassWorkspaceContext();
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

        /// <summary>
        /// Runs one remote sync pass for <paramref name="workspaceId"/> without requiring app session after capture.
        /// Does not flush local snapshots. Waits if another sync pass is already running.
        /// </summary>
        public IEnumerator SyncWorkspaceAndWait(string workspaceId, string workspaceName)
        {
            if (string.IsNullOrWhiteSpace(workspaceId))
                yield break;

            while (_syncInProgress)
                yield return null;

            _syncInProgress = true;
            try
            {
                yield return StartCoroutine(RunRemoteSyncPass(workspaceId, workspaceName));
            }
            finally
            {
                ClearActivePassWorkspaceContext();
                _syncInProgress = false;
            }
        }

        private void ClearActivePassWorkspaceContext()
        {
            _activePassWorkspaceId = null;
            _activePassWorkspaceName = null;
        }

        private bool TryResolveWorkspaceContext(string workspaceId, string workspaceName, out string resolvedId, out string resolvedName)
        {
            resolvedId = string.IsNullOrWhiteSpace(workspaceId) ? "" : workspaceId.Trim();
            resolvedName = string.IsNullOrWhiteSpace(workspaceName) ? resolvedId : workspaceName.Trim();

            if (!string.IsNullOrWhiteSpace(resolvedId))
                return true;

            if (!AppFlowController.TryGetWorkspaceSession(out WorkspaceSessionContext session) || session == null
                || string.IsNullOrWhiteSpace(session.workspaceId))
            {
                resolvedId = "";
                resolvedName = "";
                return false;
            }

            resolvedId = session.workspaceId.Trim();
            resolvedName = string.IsNullOrWhiteSpace(session.workspaceName) ? resolvedId : session.workspaceName.Trim();
            return true;
        }

        private IEnumerator RunRemoteSyncPass(string workspaceId, string workspaceName)
        {
            IApiClient api = ResolveApiClient();
            if (api == null)
            {
                PersistRemoteStateFailed("No API client (assign apiClientBehaviour implementing IApiClient).");
                yield break;
            }

            if (!TryResolveWorkspaceContext(workspaceId, workspaceName, out string resolvedWorkspaceId, out string resolvedWorkspaceName))
            {
                Debug.LogWarning($"{LogPrefix}Sync skipped: no workspace session.");
                RaiseRemoteSyncToast(WorkspaceRemoteSyncToastKind.Skipped, "Cloud sync skipped: no workspace session.");
                yield break;
            }

            _activePassWorkspaceId = resolvedWorkspaceId;
            _activePassWorkspaceName = resolvedWorkspaceName;

            AuthoredObjectRegistry registry = AuthoredObjectRegistry.Instance;
            if (registry == null)
            {
                PersistRemoteStateFailed("AuthoredObjectRegistry missing.");
                yield break;
            }

            string workspaceIdForPass = resolvedWorkspaceId;
            string workspaceNameForPass = resolvedWorkspaceName;

            foreach (AuthoredTargetInstance target in registry.GetTargetsOrdered())
            {
                if (target == null)
                    continue;
                yield return StartCoroutine(SyncTargetToBackend(api, workspaceIdForPass, workspaceNameForPass, target));
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
                yield return StartCoroutine(SyncContentToBackend(api, workspaceIdForPass, content));
                if (!_lastStepOk)
                {
                    string detail = string.IsNullOrWhiteSpace(_lastFailReason) ? "" : $"\n{_lastFailReason}";
                    PersistRemoteStateFailed($"Content sync failed for '{content.LocalContentId}'.{detail}");
                    yield break;
                }
            }

            PersistRemoteStateSuccess(workspaceIdForPass, workspaceNameForPass, registry);
        }

        private IEnumerator SyncTargetToBackend(IApiClient api, string workspaceId, string workspaceName, AuthoredTargetInstance target)
        {
            _lastStepOk = false;
            string targetId = string.IsNullOrWhiteSpace(target.LocalTargetId) ? target.ServerTargetId : target.LocalTargetId;
            string targetName = string.IsNullOrWhiteSpace(target.TargetName) ? targetId : target.TargetName.Trim();
            string displayLabel = targetName;

            bool targetRowNeedsSync = target.RemoteDirty || string.IsNullOrWhiteSpace(target.LastRemoteSyncedAtUtc);

            if (targetRowNeedsSync)
            {
                string imageUrl = string.IsNullOrWhiteSpace(target.TargetImageUrl) ? "" : target.TargetImageUrl.Trim();
                byte[] targetImageBytes = PersistenceByteUtility.CloneBytes(target.TargetImageBytes);
                if (PersistenceByteUtility.HasBytes(targetImageBytes))
                {
                    string uploadName = string.IsNullOrWhiteSpace(target.OriginalFileName)
                        ? StableTargetDiskFileName(targetId, "target.jpg")
                        : target.OriginalFileName.Trim();
                    string stableUploadName = StableTargetDiskFileName(targetId, uploadName);
                    yield return StartCoroutine(UploadBytesAndWait(api, targetImageBytes, stableUploadName, GuessMimeTypeFromName(stableUploadName), "target", targetId));
                    if (string.IsNullOrWhiteSpace(_lastUploadUrl))
                    {
                        Debug.LogWarning($"{LogPrefix}Target image upload failed for '{targetId}'.");
                        yield break;
                    }

                    imageUrl = _lastUploadUrl.Trim();
                    target.TargetImageBytes = null;
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
                    if (!string.IsNullOrWhiteSpace(imageUrl))
                        target.TargetImageUrl = imageUrl.Trim();
                    target.RemoteDirty = false;
                    target.LastRemoteSyncedAtUtc = DateTime.UtcNow.ToString("o");
                }

                _lastStepOk = apiOk;
                if (!_lastStepOk)
                    yield break;
            }
            else
            {
                _lastStepOk = true;
            }

            yield return StartCoroutine(SyncTargetReferenceToBackend(api, workspaceId, target));
        }

        private IEnumerator SyncTargetReferenceToBackend(IApiClient api, string workspaceId, AuthoredTargetInstance target)
        {
            _lastStepOk = false;
            byte[] bytes = PersistenceByteUtility.CloneBytes(target?.TargetReferenceBytes);
            if (target == null || !PersistenceByteUtility.HasBytes(bytes))
            {
                _lastStepOk = true;
                yield break;
            }

            if (!target.TargetReferenceRemoteDirty && !string.IsNullOrWhiteSpace(target.TargetReferenceImageUrl))
            {
                _lastStepOk = true;
                yield break;
            }

            string targetId = string.IsNullOrWhiteSpace(target.LocalTargetId) ? target.ServerTargetId : target.LocalTargetId;
            string uploadName = StableTargetDiskFileName(
                targetId,
                string.IsNullOrWhiteSpace(target.TargetReferenceOriginalFileName)
                    ? "target-reference.jpg"
                    : target.TargetReferenceOriginalFileName);

            bool apiOk = false;
            _lastFailReason = null;
            var uploadRequest = new UploadFileRequestDto
            {
                fileName = uploadName,
                mimeType = GuessMimeTypeFromName(uploadName),
                fileBytes = bytes
            };

            IApiRequestHandle handle = api.UploadTargetReference(
                targetId,
                uploadRequest,
                result =>
                {
                    apiOk = result != null && result.success && result.payload != null;
                    if (apiOk)
                        target.TargetReferenceImageUrl = result.payload.targetReferenceImageUrl.Trim();
                    else if (result != null)
                        _lastFailReason = BuildHttpFailDetail(result.statusCode, result.errorCode, result.message);
                },
                apiTimeoutSeconds);

            yield return WaitForRequest(handle);
            if (handle != null && handle.IsCancelled)
                apiOk = false;

            if (apiOk)
            {
                target.TargetReferenceRemoteDirty = false;
                target.TargetReferenceBytes = null;
                target.TargetReferenceLocalPath = "";
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
                byte[] contentBytes = PersistenceByteUtility.CloneBytes(c.AssetBytes);
                if (PersistenceByteUtility.HasBytes(contentBytes))
                {
                    string uploadName = string.IsNullOrWhiteSpace(c.OriginalFileName)
                        ? $"{c.ServerContentId}.bin"
                        : c.OriginalFileName.Trim();
                    yield return StartCoroutine(UploadBytesAndWait(api, contentBytes, uploadName, GuessMimeTypeFromName(uploadName), "content", null, c.ServerContentId));
                    if (string.IsNullOrWhiteSpace(_lastUploadUrl))
                    {
                        Debug.LogWarning($"{LogPrefix}Content upload failed for '{c.LocalContentId}'.");
                        c.UploadPending = true;
                        yield break;
                    }

                    mediaUrl = _lastUploadUrl.Trim();
                    c.AssetBytes = null;
                    c.AssetLocalPath = "";
                }
                else if (!string.IsNullOrWhiteSpace(c.MediaUrl) && (c.MediaUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                                                                     || c.MediaUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                {
                    mediaUrl = c.MediaUrl.Trim();
                }
                else
                {
                    Debug.LogWarning($"{LogPrefix}Best-effort skip: no in-memory bytes or http(s) media URL for '{c.LocalContentId}'.");
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
            Debug.Log($"{LogPrefix}Remote sync completed successfully for workspace '{workspaceId}'.");
            RaiseRemoteSyncToast(WorkspaceRemoteSyncToastKind.Synced, "Workspace synchronized with the server.");
        }

        private void PersistRemoteStateFailed(string message)
        {
            string m = string.IsNullOrWhiteSpace(message) ? "Remote sync failed." : message.Trim();
            Debug.LogWarning($"{LogPrefix}{m}");
            RaiseRemoteSyncToast(WorkspaceRemoteSyncToastKind.Failed, m);
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
