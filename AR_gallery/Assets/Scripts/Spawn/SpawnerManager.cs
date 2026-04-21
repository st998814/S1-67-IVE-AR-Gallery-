using System;
using ARGallery.Content;
using UnityEngine;

namespace ARGallery.Spawning
{
    /// <summary>
    /// Thin orchestration layer that routes spawn requests to existing creation workflows.
    /// </summary>
    public class SpawnerManager : ISpawnerManager
    {
        // the default forward offset from the wall , so the content is not too close to the wall
        private const float DefaultForwardOffsetFromWall = 0.008f;

        private readonly MonoBehaviour runner;
        private readonly GameObject picturePrefab;
        private readonly GameObject textPrefab;
        private readonly GameObject modelContainerPrefab;
        private readonly ITargetContextResolver targetContextResolver;
        private readonly ContentCreationCoordinator contentCoordinator;
        private readonly TargetWorkflowService targetWorkflowService;
        private readonly float forwardOffsetFromWall;

        public SpawnerManager(
            MonoBehaviour runner,
            GameObject picturePrefab,
            GameObject textPrefab,
            GameObject modelContainerPrefab,
            ITargetContextResolver targetContextResolver,
            ContentCreationCoordinator contentCoordinator = null,
            TargetWorkflowService targetWorkflowService = null,
            float forwardOffsetFromWall = DefaultForwardOffsetFromWall)
        {
            this.runner = runner;
            this.picturePrefab = picturePrefab;
            this.textPrefab = textPrefab;
            this.modelContainerPrefab = modelContainerPrefab;
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
                    return CreateMediaContent(request);

                default:
                    return FailContent(
                        "Unsupported content type for spawn routing.",
                        request.contentType,
                        ResolveRenderKind(request.contentType));
            }
        }

        public SpawnTargetResult CreateTarget(SpawnTargetRequest request)
        {
            if (request == null)
            {
                return new SpawnTargetResult
                {
                    success = false,
                    message = "SpawnTargetRequest is null."
                };
            }

            var result = targetWorkflowService.CreateAndRegisterLocal(
                runner,
                request.targetName,
                request.targetId,
                request.displayLabel);

            return new SpawnTargetResult
            {
                success = result.success,
                isDuplicate = result.isDuplicate,
                duplicateIndex = result.duplicateIndex,
                targetId = result.targetId,
                message = result.message,
                targetObject = result.targetObject
            };
        }

        public IApiRequestHandle BeginSyncCreateContent(
            IApiClient apiClient,
            SpawnRequest request,
            Transform spawnedTransform,
            Action<ApiResult<CreateContentResponseDto>> onCompleted = null,
            float timeoutSeconds = 20f)
        {
            if (request == null)
            {
                onCompleted?.Invoke(ApiResult<CreateContentResponseDto>.Fail(
                    ApiErrorCodes.ValidationError,
                    "SyncCreateContent skipped: SpawnRequest is null."));
                return null;
            }

            string resolvedTargetId = targetContextResolver != null
                ? targetContextResolver.ResolveTargetIdOrActive(request.targetId)
                : (request.targetId ?? "");
            string contentType = ToApiContentType(request.contentType);
            string mediaUrl = request.contentType == SpawnContentType.Text
                ? (request.textPayload ?? "")
                : (request.mediaUrl ?? "");
            Vector3 syncPosition = spawnedTransform != null ? spawnedTransform.localPosition : Vector3.zero;
            Vector3 syncEuler = spawnedTransform != null ? spawnedTransform.localEulerAngles : Vector3.zero;
            Vector3 syncScale = spawnedTransform != null ? spawnedTransform.localScale : Vector3.one;

            if (spawnedTransform == null && request.hasTransformOverride)
            {
                syncPosition = request.transformOverride.localPosition;
                syncEuler = request.transformOverride.localEuler;
                syncScale = request.transformOverride.localScale;
            }

            return contentCoordinator.SyncCreateContent(
                apiClient,
                contentType,
                syncPosition,
                syncEuler,
                syncScale,
                mediaUrl,
                resolvedTargetId,
                onCompleted,
                timeoutSeconds);
        }

        public IApiRequestHandle BeginSyncCreateTarget(
            IApiClient apiClient,
            SpawnTargetRequest request,
            GameObject targetObject,
            Action<ApiResult<CreateTargetResponseDto>> onCompleted = null,
            float timeoutSeconds = 20f)
        {
            if (request == null)
            {
                onCompleted?.Invoke(ApiResult<CreateTargetResponseDto>.Fail(
                    ApiErrorCodes.ValidationError,
                    "SyncCreateTarget skipped: SpawnTargetRequest is null."));
                return null;
            }

            return targetWorkflowService.SyncCreateTarget(
                apiClient,
                targetObject,
                request.targetId ?? "",
                request.targetName ?? "",
                string.IsNullOrWhiteSpace(request.displayLabel) ? request.targetName ?? "" : request.displayLabel,
                request.targetImageUrl ?? "",
                onCompleted,
                timeoutSeconds);
        }

        private SpawnContentResult CreateTextContent(SpawnRequest request)
        {
            ContentWorkflowService.LocalTextSpawnResult textResult =
                contentCoordinator.SpawnText(textPrefab, request.textPayload);

            if (!textResult.success || textResult.spawnedObject == null)
            {
                return FailContent(
                    textResult.message,
                    SpawnContentType.Text,
                    SpawnRenderKind.Surface);
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
                return FailContent(
                    "SpawnerManager requires a MonoBehaviour runner for media content creation.",
                    request.contentType,
                    ResolveRenderKind(request.contentType));
            }

            bool hasRemoteUrl = !string.IsNullOrWhiteSpace(request.mediaUrl);
            bool hasLocalBytes = request.localFileBytes != null && request.localFileBytes.Length > 0;
            if (!hasRemoteUrl && !hasLocalBytes)
            {
                return FailContent(
                    "Either mediaUrl or localFileBytes is required for image/model content creation.",
                    request.contentType,
                    ResolveRenderKind(request.contentType));
            }

            string originalFileName = string.IsNullOrWhiteSpace(request.originalFileName)
                ? request.mediaUrl
                : request.originalFileName;

            ContentCreationCoordinator.LocalContentSpawnOutcome outcome = hasLocalBytes
                ? contentCoordinator.SpawnFromLocalFile(
                    runner,
                    picturePrefab,
                    modelContainerPrefab,
                    request.localFileBytes,
                    originalFileName,
                    request.localMimeType)
                : contentCoordinator.SpawnFromContentUpload(
                    runner,
                    picturePrefab,
                    modelContainerPrefab,
                    request.mediaUrl.Trim(),
                    originalFileName);

            if (!outcome.success || outcome.spawnedObject == null)
            {
                return FailContent(
                    outcome.message,
                    request.contentType,
                    MapRenderKind(outcome.renderKind));
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
                contentType = MapContentType(outcome.mediaKind, request.contentType),
                renderKind = MapRenderKind(outcome.renderKind)
            };
        }

        private bool TryIntegrateSpawnedContent(
            GameObject spawnedObject,
            SpawnRequest request,
            bool alignToTargetFrame,
            out string message)
        {
            message = null;
            if (spawnedObject == null)
            {
                message = "Spawned object is null.";
                return false;
            }

            if (targetContextResolver == null)
            {
                message = "Target context resolver is missing.";
                return false;
            }
           
            if (!targetContextResolver.TryGetContentRoot(request != null ? request.targetId : "", out Transform contentRoot) || contentRoot == null)
            {
                message = "Unable to resolve target ContentRoot for spawned content.";
                return false;
            }
            // parent the content to the content root
            spawnedObject.transform.SetParent(contentRoot, false);
            ApplyDefaultPlacement(spawnedObject, contentRoot, alignToTargetFrame);

            if (request != null && request.hasTransformOverride)
                ApplyTransformOverride(spawnedObject.transform, request.transformOverride);

            return true;
        }

        private void ApplyDefaultPlacement(GameObject instance, Transform contentRoot, bool alignToTargetFrame)
        {
            if (instance == null || contentRoot == null)
                return;
            // find the target visual that sibling of the content root
            Transform targetVisual = contentRoot.parent != null ? contentRoot.parent.Find("TargetVisual") : null;

            if (alignToTargetFrame && targetVisual != null)
            {
                instance.transform.localPosition = targetVisual.localPosition;
                instance.transform.localRotation = targetVisual.localRotation;
                instance.transform.localScale = targetVisual.localScale;
                // pushes content slightly forward from the wall
                if (forwardOffsetFromWall > 0f)
                    instance.transform.position += instance.transform.forward * forwardOffsetFromWall;
                return;
            }

            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
        }

        private static void ApplyTransformOverride(Transform target, SpawnTransformData overrideTransform)
        {
            if (target == null)
                return;

            target.localPosition = overrideTransform.localPosition;
            target.localRotation = Quaternion.Euler(overrideTransform.localEuler);
            target.localScale = overrideTransform.localScale;
        }

        private static SpawnContentResult FailContent(string message, SpawnContentType type, SpawnRenderKind renderKind)
        {
            return new SpawnContentResult
            {
                success = false,
                message = string.IsNullOrWhiteSpace(message) ? "Spawn failed." : message,
                contentType = type,
                renderKind = renderKind
            };
        }

        private static SpawnRenderKind ResolveRenderKind(SpawnContentType type)
        {
            return type == SpawnContentType.Model ? SpawnRenderKind.Volumetric : SpawnRenderKind.Surface;
        }

        private static SpawnRenderKind MapRenderKind(ContentRenderKind kind)
        {
            return kind == ContentRenderKind.Volumetric ? SpawnRenderKind.Volumetric : SpawnRenderKind.Surface;
        }

        private static SpawnContentType MapContentType(ContentMediaKind kind, SpawnContentType fallback)
        {
            switch (kind)
            {
                case ContentMediaKind.Text:
                    return SpawnContentType.Text;
                case ContentMediaKind.Model:
                    return SpawnContentType.Model;
                case ContentMediaKind.Image:
                default:
                    return fallback;
            }
        }

        private static string ToApiContentType(SpawnContentType type)
        {
            switch (type)
            {
                case SpawnContentType.Text:
                    return "text";
                case SpawnContentType.Model:
                    return "model";
                case SpawnContentType.Image:
                default:
                    return "image";
            }
        }
    }
}
