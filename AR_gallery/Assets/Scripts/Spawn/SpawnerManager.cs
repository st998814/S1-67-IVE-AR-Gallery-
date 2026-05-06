using System;
using ARGallery.Content;
using UnityEngine;

namespace ARGallery.Spawning
{
    /// <summary>
    /// Thin orchestration layer that routes spawn requests to existing creation workflows.
    /// Supports Text, Image, Model (GLB), and Video.
    /// </summary>
    public class SpawnerManager : ISpawnerManager
    {
        private const float DefaultForwardOffsetFromWall = 0.008f;
        private const float MinimumSurfaceClearance = 0.06f;

        private readonly MonoBehaviour runner;
        private readonly GameObject picturePrefab;
        private readonly GameObject textPrefab;
        private readonly GameObject modelContainerPrefab;
        private readonly GameObject videoPrefab; // Added for Video support
        private readonly ITargetContextResolver targetContextResolver;
        private readonly ContentCreationCoordinator contentCoordinator;
        private readonly TargetWorkflowService targetWorkflowService;
        private readonly float forwardOffsetFromWall;

        public SpawnerManager(
            MonoBehaviour runner,
            GameObject picturePrefab,
            GameObject textPrefab,
            GameObject modelContainerPrefab,
            GameObject videoPrefab, // Video prefab passed from UI Controller
            ITargetContextResolver targetContextResolver,
            ContentCreationCoordinator contentCoordinator = null,
            TargetWorkflowService targetWorkflowService = null,
            float forwardOffsetFromWall = DefaultForwardOffsetFromWall)
        {
            this.runner = runner;
            this.picturePrefab = picturePrefab;
            this.textPrefab = textPrefab;
            this.modelContainerPrefab = modelContainerPrefab;
            this.videoPrefab = videoPrefab; 
            this.targetContextResolver = targetContextResolver;
            this.contentCoordinator = contentCoordinator ?? new ContentCreationCoordinator();
            this.targetWorkflowService = targetWorkflowService ?? new TargetWorkflowService();
            this.forwardOffsetFromWall = Mathf.Max(0f, forwardOffsetFromWall);
        }

        public SpawnContentResult CreateContent(SpawnRequest request)
        {
            if (request == null)
            {
                return FailContent("SpawnRequest is null.", SpawnContentType.Image, SpawnRenderKind.Surface);
            }

            switch (request.contentType)
            {
                case SpawnContentType.Text:
                    return CreateTextContent(request);

                case SpawnContentType.Image:
                case SpawnContentType.Model:
                case SpawnContentType.Video:
                    return CreateMediaContent(request);

                default:
                    return FailContent(
                        "Unsupported content type for spawn routing.",
                        request.contentType,
                        ResolveRenderKind(request.contentType));
            }
        }

        // Restored: Fixed the CS0103 error by re-adding this method
        private SpawnContentResult CreateTextContent(SpawnRequest request)
        {
            ContentWorkflowService.LocalTextSpawnResult textResult =
                contentCoordinator.SpawnText(textPrefab, request.textPayload);

            if (!textResult.success || textResult.spawnedObject == null)
            {
                return FailContent(textResult.message, SpawnContentType.Text, SpawnRenderKind.Surface);
            }

            if (!TryIntegrateSpawnedContent(textResult.spawnedObject, request, alignToTargetFrame: false, out string integrationMessage))
            {
                contentCoordinator.ReleaseSpawnedContent(textResult.spawnedObject);
                return FailContent(integrationMessage, SpawnContentType.Text, SpawnRenderKind.Surface);
            }

            return new SpawnContentResult
            {
                success = true,
                message = textResult.message,
                spawnedObject = textResult.spawnedObject,
                draggableObject = textResult.draggableObject,
                contentType = SpawnContentType.Text,
                renderKind = SpawnRenderKind.Surface
            };
        }

        private SpawnContentResult CreateMediaContent(SpawnRequest request)
        {
            if (runner == null)
            {
                return FailContent("SpawnerManager requires a runner.", request.contentType, ResolveRenderKind(request.contentType));
            }

            string originalFileName = string.IsNullOrWhiteSpace(request.originalFileName)
                ? request.mediaUrl
                : request.originalFileName;

            // Routes to Coordinator with videoPrefab support
            ContentCreationCoordinator.LocalContentSpawnOutcome outcome = (request.localFileBytes != null && request.localFileBytes.Length > 0)
                ? contentCoordinator.SpawnFromLocalFile(
                    runner,
                    picturePrefab,
                    modelContainerPrefab,
                    videoPrefab, 
                    request.localFileBytes,
                    originalFileName,
                    request.localMimeType)
                : contentCoordinator.SpawnFromContentUpload(
                    runner,
                    picturePrefab,
                    modelContainerPrefab,
                    videoPrefab, 
                    request.mediaUrl?.Trim(),
                    originalFileName);

            if (!outcome.success || outcome.spawnedObject == null)
            {
                return FailContent(outcome.message, request.contentType, MapRenderKind(outcome.renderKind));
            }

            if (!TryIntegrateSpawnedContent(outcome.spawnedObject, request, alignToTargetFrame: true, out string integrationMessage))
            {
                contentCoordinator.ReleaseSpawnedContent(outcome.spawnedObject);
                return FailContent(integrationMessage, request.contentType, MapRenderKind(outcome.renderKind));
            }

            return new SpawnContentResult
            {
                success = true,
                message = outcome.message,
                spawnedObject = outcome.spawnedObject,
                draggableObject = outcome.draggableObject,
                contentType = request.contentType,
                renderKind = MapRenderKind(outcome.renderKind)
            };
        }

        private bool TryIntegrateSpawnedContent(GameObject spawnedObject, SpawnRequest request, bool alignToTargetFrame, out string message)
        {
            message = null;
            if (spawnedObject == null) return false;
            if (targetContextResolver == null) return false;

            if (!targetContextResolver.TryGetContentRoot(request?.targetId ?? "", out Transform contentRoot) || contentRoot == null)
            {
                message = "Unable to resolve ContentRoot.";
                return false;
            }

            spawnedObject.transform.SetParent(contentRoot, false);
            ApplyDefaultPlacement(spawnedObject, contentRoot, alignToTargetFrame);

            if (request != null && request.hasTransformOverride)
            {
                spawnedObject.transform.localPosition = request.transformOverride.localPosition;
                spawnedObject.transform.localRotation = Quaternion.Euler(request.transformOverride.localEuler);
                spawnedObject.transform.localScale = request.transformOverride.localScale;
            }

            return true;
        }

        private void ApplyDefaultPlacement(GameObject instance, Transform contentRoot, bool alignToTargetFrame)
        {
            Transform targetVisual = contentRoot.parent?.Find("TargetVisual");
            if (alignToTargetFrame && targetVisual != null)
            {
                instance.transform.localPosition = targetVisual.localPosition;
                instance.transform.localRotation = targetVisual.localRotation;
                instance.transform.localScale = targetVisual.localScale;

                // Align spawn direction with FrontSideConstraint axis (+/- local Z).
                float frontSign = ResolveFrontDirectionSign(contentRoot);
                Vector3 localFront = Vector3.forward * frontSign;
                Vector3 normal = contentRoot.TransformDirection(localFront);
                if (normal.sqrMagnitude < 0.0001f)
                    normal = Vector3.forward;
                else
                    normal.Normalize();
                float offset = Mathf.Max(0.001f, forwardOffsetFromWall);

                if (TryGetProjectedHalfDepth(targetVisual, normal, out float targetHalfDepth))
                    offset += targetHalfDepth;
                if (TryGetProjectedHalfDepth(instance.transform, normal, out float contentHalfDepth))
                    offset += contentHalfDepth;
                else
                    offset += MinimumSurfaceClearance;

                offset += MinimumSurfaceClearance;

                // Place relative to target frame origin, but along ContentRoot canonical front axis.
                instance.transform.localPosition = targetVisual.localPosition + localFront * offset;
            }
            else
            {
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;
            }
        }

        private float ResolveFrontDirectionSign(Transform contentRoot)
        {
            FrontSideConstraint constraint = null;

            if (contentRoot != null)
                constraint = contentRoot.GetComponentInParent<FrontSideConstraint>();

            if (constraint == null && runner != null)
                constraint = runner.GetComponentInParent<FrontSideConstraint>();

            if (constraint == null)
                constraint = UnityEngine.Object.FindFirstObjectByType<FrontSideConstraint>();

            if (constraint == null)
                return -1f;

            return constraint.FrontDirectionSign >= 0f ? 1f : -1f;
        }


        private static bool TryGetProjectedHalfDepth(Transform root, Vector3 normal, out float halfDepth)
        {
            halfDepth = 0f;
            if (root == null)
                return false;

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(includeInactive: false);
            if (renderers == null || renderers.Length == 0)
                return false;

            Vector3 n = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.forward;
            float min = float.MaxValue;
            float max = float.MinValue;
            bool hasValue = false;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null || !r.enabled)
                    continue;

                Bounds b = r.bounds;
                Vector3 e = b.extents;
                Vector3 c = b.center;
                Vector3[] corners =
                {
                    c + new Vector3(-e.x, -e.y, -e.z),
                    c + new Vector3(-e.x, -e.y,  e.z),
                    c + new Vector3(-e.x,  e.y, -e.z),
                    c + new Vector3(-e.x,  e.y,  e.z),
                    c + new Vector3( e.x, -e.y, -e.z),
                    c + new Vector3( e.x, -e.y,  e.z),
                    c + new Vector3( e.x,  e.y, -e.z),
                    c + new Vector3( e.x,  e.y,  e.z)
                };

                for (int j = 0; j < corners.Length; j++)
                {
                    float d = Vector3.Dot(corners[j], n);
                    if (d < min) min = d;
                    if (d > max) max = d;
                    hasValue = true;
                }
            }

            if (!hasValue)
                return false;

            halfDepth = Mathf.Max(0f, (max - min) * 0.5f);
            return true;
        }

        public SpawnTargetResult CreateTarget(SpawnTargetRequest request)
        {
            var result = targetWorkflowService.CreateAndRegisterLocal(runner, request.targetName, request.targetId, request.displayLabel);
            return new SpawnTargetResult { success = result.success, targetId = result.targetId, targetObject = result.targetObject, message = result.message };
        }

        public IApiRequestHandle BeginSyncCreateTarget(IApiClient apiClient, SpawnTargetRequest request, GameObject targetObject, Action<ApiResult<CreateTargetResponseDto>> onCompleted = null, float timeoutSeconds = 20f)
        {
            return targetWorkflowService.SyncCreateTarget(apiClient, targetObject, request.targetId ?? "", request.targetName ?? "", request.displayLabel ?? request.targetName, request.targetImageUrl ?? "", onCompleted, timeoutSeconds);
        }

        public IApiRequestHandle BeginSyncCreateContent(IApiClient apiClient, SpawnRequest request, Transform spawnedTransform, Action<ApiResult<CreateContentResponseDto>> onCompleted = null, float timeoutSeconds = 20f)
        {
            string contentType = ToApiContentType(request.contentType);
            string mediaUrl = request.contentType == SpawnContentType.Text ? request.textPayload : request.mediaUrl;
            return contentCoordinator.SyncCreateContent(apiClient, contentType, spawnedTransform.localPosition, spawnedTransform.localEulerAngles, spawnedTransform.localScale, mediaUrl, request.targetId, onCompleted, timeoutSeconds);
        }

        private static string ToApiContentType(SpawnContentType type)
        {
            switch (type)
            {
                case SpawnContentType.Text: return "text";
                case SpawnContentType.Model: return "model";
                case SpawnContentType.Video: return "video";
                default: return "image";
            }
        }

        private static SpawnContentResult FailContent(string message, SpawnContentType type, SpawnRenderKind renderKind)
        {
            return new SpawnContentResult { success = false, message = message, contentType = type, renderKind = renderKind };
        }

        private static SpawnRenderKind ResolveRenderKind(SpawnContentType type) => type == SpawnContentType.Model ? SpawnRenderKind.Volumetric : SpawnRenderKind.Surface;
        private static SpawnRenderKind MapRenderKind(ContentRenderKind kind) => kind == ContentRenderKind.Volumetric ? SpawnRenderKind.Volumetric : SpawnRenderKind.Surface;
    }
}