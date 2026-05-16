using System;
using System.Collections;
using System.IO;
using ARGallery.AppFlow;
using ARGallery.Spawning;
using UnityEngine;
using UnityEngine.Video;

namespace ARGallery.Workspace.Persistence
{
    /// <summary>
    /// Loads a <see cref="WorkspaceSnapshot"/> and recreates targets + content using local assets under persistentDataPath.
    /// Does not call backend APIs.
    /// </summary>
    public sealed class WorkspaceSceneReconstructor : MonoBehaviour
    {
        [SerializeField] private AuthoringUIController authoringUi;
        [SerializeField] private TargetSelectionManager targetSelectionManager;
        [SerializeField] private float spawnForwardOffsetFromWall = 0.008f;

        [Header("Optional overrides when Authoring UI is not assigned")]
        [SerializeField] private GameObject picturePrefabOverride;
        [SerializeField] private GameObject textPrefabOverride;
        [SerializeField] private GameObject videoPrefabOverride;
        [SerializeField] private GameObject modelContainerPrefabOverride;

        private readonly TargetWorkflowService targetWorkflowService = new TargetWorkflowService();
        private readonly WorkspaceAssetRepository assetRepository = new WorkspaceAssetRepository();

        /// <summary>Loads snapshot.json for <paramref name="workspaceId"/> then rebuilds asynchronously.</summary>
        public Coroutine BeginRebuildFromDisk(string workspaceId, Action<bool> onCompleted = null)
        {
            var repo = new WorkspaceSnapshotRepository();
            if (!repo.TryLoadSnapshot(workspaceId, out WorkspaceSnapshot snapshot))
            {
                Debug.LogWarning($"WorkspaceSceneReconstructor: no snapshot for workspace '{workspaceId}'.");
                onCompleted?.Invoke(false);
                return null;
            }

            return StartCoroutine(RebuildCoroutine(snapshot, onCompleted));
        }

        public Coroutine BeginRebuild(WorkspaceSnapshot snapshot, Action<bool> onCompleted = null)
        {
            if (snapshot == null)
            {
                onCompleted?.Invoke(false);
                return null;
            }

            return StartCoroutine(RebuildCoroutine(snapshot, onCompleted));
        }

        private IEnumerator RebuildCoroutine(WorkspaceSnapshot snapshot, Action<bool> onCompleted)
        {
            WorkspacePersistenceBootstrap.EnsureRegistry();
            ResolveTargetSelectionManager();
            ISpawnerManager spawner = BuildSpawnerManager();

            bool ok = true;

            if (snapshot.targets != null)
            {
                for (int i = 0; i < snapshot.targets.Length; i++)
                {
                    TargetSnapshot ts = snapshot.targets[i];
                    if (ts == null)
                        continue;
                    if (!TryRebuildTarget(ts, snapshot.workspaceId))
                        ok = false;
                    yield return null;
                }
            }

            if (snapshot.contents != null)
            {
                for (int i = 0; i < snapshot.contents.Length; i++)
                {
                    ContentSnapshot cs = snapshot.contents[i];
                    if (cs == null)
                        continue;
                    if (!TryRebuildContent(cs, snapshot.workspaceId, spawner))
                        ok = false;
                    yield return null;
                }
            }

            onCompleted?.Invoke(ok);
        }

        private void ResolveTargetSelectionManager()
        {
            if (targetSelectionManager != null)
                return;
            targetSelectionManager = FindFirstObjectByType<TargetSelectionManager>();
            if (targetSelectionManager == null)
                Debug.LogWarning("WorkspaceSceneReconstructor: TargetSelectionManager not found.");
        }

        private ISpawnerManager BuildSpawnerManager()
        {
            ResolveTargetSelectionManager();
            var resolver = new TargetSelectionContextResolver(targetSelectionManager);
            return new SpawnerManager(
                this,
                ResolvePicturePrefab(),
                ResolveTextPrefab(),
                ResolveModelPrefab(),
                ResolveVideoPrefab(),
                resolver,
                contentCoordinator: null,
                targetWorkflowService,
                spawnForwardOffsetFromWall);
        }

        private GameObject ResolvePicturePrefab() =>
            picturePrefabOverride != null ? picturePrefabOverride : authoringUi != null ? authoringUi.ResolvePersistencePicturePrefab() : null;

        private GameObject ResolveTextPrefab() =>
            textPrefabOverride != null ? textPrefabOverride : authoringUi != null ? authoringUi.ResolvePersistenceTextPrefab() : null;

        private GameObject ResolveVideoPrefab() =>
            videoPrefabOverride != null ? videoPrefabOverride : authoringUi != null ? authoringUi.ResolvePersistenceVideoPrefab() : null;

        private GameObject ResolveModelPrefab() =>
            modelContainerPrefabOverride != null ? modelContainerPrefabOverride : authoringUi != null ? authoringUi.ResolvePersistenceModelContainerPrefab() : null;

        private bool TryRebuildTarget(TargetSnapshot ts, string workspaceId)
        {
            string canonicalId = CanonicalTargetId(ts);
            if (string.IsNullOrWhiteSpace(canonicalId))
            {
                Debug.LogWarning("WorkspaceSceneReconstructor: Target snapshot missing id.");
                return false;
            }

            TargetWorkflowService.LocalCreateResult created = targetWorkflowService.CreateAndRegisterLocal(
                this,
                string.IsNullOrWhiteSpace(ts.targetName) ? canonicalId : ts.targetName.Trim(),
                canonicalId,
                string.IsNullOrWhiteSpace(ts.targetName) ? canonicalId : ts.targetName.Trim(),
                ts.physicalWidthM > 1e-5f ? ts.physicalWidthM : 0.2f);

            GameObject targetGo = created.targetObject;
            if (!created.success || targetGo == null)
            {
                if (created.isDuplicate && created.duplicateIndex >= 0 && targetSelectionManager != null)
                    targetGo = targetSelectionManager.GetTargetAt(created.duplicateIndex);

                if (targetGo == null)
                {
                    Debug.LogWarning($"WorkspaceSceneReconstructor: could not create or resolve target '{canonicalId}': {created.message}");
                    return false;
                }
            }

            Transform tr = targetGo.transform;
            tr.localPosition = ts.position.ToVector3();
            tr.localEulerAngles = ts.rotation.ToVector3();
            tr.localScale = ts.scale.ToVector3();

            AuthoredTargetInstance authored = targetGo.GetComponent<AuthoredTargetInstance>() ?? targetGo.AddComponent<AuthoredTargetInstance>();
            authored.LocalTargetId = ts.localTargetId ?? "";
            authored.ServerTargetId = ts.serverTargetId ?? "";
            authored.VuforiaTargetId = ts.vuforiaTargetId ?? "";
            authored.TargetName = ts.targetName ?? "";
            authored.TargetImageLocalPath = ts.targetImageLocalPath ?? "";
            authored.OriginalFileName = ts.originalFileName ?? "";
            authored.PhysicalWidthM = ts.physicalWidthM > 1e-5f ? ts.physicalWidthM : 0.2f;
            authored.RemoteDirty = ts.remoteDirty;
            authored.LastRemoteSyncedAtUtc = ts.lastRemoteSyncedAtUtc ?? "";

            byte[] imgBytes = TryReadAssetBytes(workspaceId, ts.targetImageLocalPath);
            if (imgBytes != null && imgBytes.Length > 0)
                targetWorkflowService.ApplyTargetImageBytes(targetGo, imgBytes);
            else
                Debug.LogWarning($"WorkspaceSceneReconstructor: missing target image bytes for '{canonicalId}' (path '{ts.targetImageLocalPath}').");

            if (imgBytes == null || imgBytes.Length == 0)
                TargetVisualPhysicalLayout.ApplyFromTargetRoot(targetGo, null);

            return true;
        }

        private bool TryRebuildContent(ContentSnapshot cs, string workspaceId, ISpawnerManager spawner)
        {
            if (spawner == null || targetSelectionManager == null)
                return false;

            string targetKey = cs.targetId ?? "";
            int idx = targetSelectionManager.FindTargetIndexById(targetKey);
            if (idx < 0)
            {
                Debug.LogWarning($"WorkspaceSceneReconstructor: no target '{targetKey}' for content '{cs.localContentId}'.");
                TrySpawnMissingPlaceholder(cs);
                return false;
            }

            targetSelectionManager.SetActiveTarget(idx);

            SpawnContentType spawnType = ParseContentType(cs.contentType);
            var request = new SpawnRequest
            {
                contentType = spawnType,
                targetId = targetKey,
                originalFileName = string.IsNullOrWhiteSpace(cs.originalFileName) ? "asset.bin" : cs.originalFileName,
                hasTransformOverride = true,
                transformOverride = new SpawnTransformData
                {
                    localPosition = cs.position.ToVector3(),
                    localEuler = cs.rotation.ToVector3(),
                    localScale = cs.scale.ToVector3()
                },
                renderKind = cs.renderKind ?? ""
            };

            if (spawnType == SpawnContentType.Text)
            {
                request.textPayload = !string.IsNullOrWhiteSpace(cs.textBody)
                    ? cs.textBody
                    : cs.title ?? "";
            }
            else
            {
                byte[] bytes = TryReadAssetBytes(workspaceId, cs.assetLocalPath);
                if (bytes != null && bytes.Length > 0)
                {
                    request.localFileBytes = bytes;
                }
                else if (!string.IsNullOrWhiteSpace(cs.mediaUrl))
                {
                    request.mediaUrl = cs.mediaUrl.Trim();
                }
                else
                {
                    Debug.LogWarning($"WorkspaceSceneReconstructor: missing asset for content '{cs.localContentId}' — spawning placeholder.");
                    TrySpawnMissingPlaceholder(cs);
                    return true;
                }
            }

            SpawnContentResult result = spawner.CreateContent(request);
            if (!result.success || result.spawnedObject == null)
            {
                Debug.LogWarning($"WorkspaceSceneReconstructor: spawn failed for '{cs.localContentId}': {result.message}");
                TrySpawnMissingPlaceholder(cs);
                return true;
            }

            GameObject go = result.spawnedObject;
            if (spawnType == SpawnContentType.Video)
            {
                string full = assetRepository.ResolveFullPath(workspaceId, cs.assetLocalPath);
                if (!string.IsNullOrWhiteSpace(full) && File.Exists(full))
                    ApplyVideoFileUrl(go, full);
                else if (!string.IsNullOrWhiteSpace(cs.mediaUrl))
                    ApplyVideoStreamUrl(go, cs.mediaUrl.Trim());
            }

            AttachAuthoredContent(go, cs);
            return true;
        }

        private void TrySpawnMissingPlaceholder(ContentSnapshot cs)
        {
            var resolver = new TargetSelectionContextResolver(targetSelectionManager);
            if (!resolver.TryGetContentRoot(cs.targetId ?? "", out Transform contentRoot) || contentRoot == null)
                return;

            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = $"MissingContent_{cs.localContentId}";
            cube.transform.SetParent(contentRoot, false);
            cube.transform.localPosition = cs.position.ToVector3();
            cube.transform.localEulerAngles = cs.rotation.ToVector3();
            cube.transform.localScale = Vector3.Max(cs.scale.ToVector3(), Vector3.one * 0.02f);

            AttachAuthoredContent(cube, cs);
        }

        private static void AttachAuthoredContent(GameObject go, ContentSnapshot cs)
        {
            if (go == null || cs == null)
                return;

            AuthoredContentInstance ac = go.GetComponent<AuthoredContentInstance>() ?? go.AddComponent<AuthoredContentInstance>();
            ac.LocalContentId = cs.localContentId ?? "";
            ac.ServerContentId = cs.serverContentId ?? "";
            ac.TargetId = cs.targetId ?? "";
            ac.ContentType = cs.contentType ?? "unknown";
            ac.RenderKind = cs.renderKind ?? "";
            ac.Title = cs.title ?? "";
            ac.Description = cs.description ?? "";
            ac.TextBody = cs.textBody ?? "";
            ac.AssetLocalPath = cs.assetLocalPath ?? "";
            ac.OriginalFileName = cs.originalFileName ?? "";
            ac.MediaUrl = cs.mediaUrl ?? "";
            ac.AssetFormat = cs.assetFormat ?? "";
            ac.IsUnsaved = cs.isUnsaved;
            ac.UploadPending = cs.uploadPending;
            ac.PersistPending = cs.persistPending;
            ac.RemoteDirty = cs.remoteDirty;
            ac.LastRemoteSyncedAtUtc = cs.lastRemoteSyncedAtUtc ?? "";
        }

        private static void ApplyVideoFileUrl(GameObject go, string absolutePath)
        {
            if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath))
                return;

            VideoPlayer vp = go.GetComponentInChildren<VideoPlayer>(true);
            if (vp == null)
                return;

            vp.source = VideoSource.Url;
            vp.url = new Uri(absolutePath).AbsoluteUri;
            vp.playOnAwake = true;
            vp.Play();
        }

        private static void ApplyVideoStreamUrl(GameObject go, string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return;

            VideoPlayer vp = go.GetComponentInChildren<VideoPlayer>(true);
            if (vp == null)
                return;

            vp.source = VideoSource.Url;
            vp.url = url.Trim();
            vp.playOnAwake = true;
            vp.Play();
        }

        private byte[] TryReadAssetBytes(string workspaceId, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return null;

            string full = assetRepository.ResolveFullPath(workspaceId, relativePath);
            if (string.IsNullOrEmpty(full) || !File.Exists(full))
                return null;

            try
            {
                return File.ReadAllBytes(full);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"WorkspaceSceneReconstructor: read failed for '{full}': {ex.Message}");
                return null;
            }
        }

        private static string CanonicalTargetId(TargetSnapshot ts)
        {
            if (!string.IsNullOrWhiteSpace(ts.serverTargetId))
                return ts.serverTargetId.Trim();
            return ts.localTargetId != null ? ts.localTargetId.Trim() : "";
        }

        private static SpawnContentType ParseContentType(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return SpawnContentType.Image;

            switch (raw.Trim().ToLowerInvariant())
            {
                case "text":
                    return SpawnContentType.Text;
                case "video":
                    return SpawnContentType.Video;
                case "model":
                case "model(3d)":
                case "model3d":
                    return SpawnContentType.Model;
                case "image":
                case "picture":
                case "poster":
                    return SpawnContentType.Image;
                default:
                    return SpawnContentType.Image;
            }
        }
    }
}
