using ARGallery.Content;
using UnityEngine;

namespace ARGallery.Spawning
{
    /// <summary>
    /// Thin orchestration layer that routes spawn requests to existing creation workflows.
    /// </summary>
    public class SpawnerManager : ISpawnerManager
    {
        private readonly MonoBehaviour runner;
        private readonly GameObject picturePrefab;
        private readonly GameObject textPrefab;
        private readonly GameObject modelContainerPrefab;
        private readonly ITargetContextResolver targetContextResolver;
        private readonly ContentCreationCoordinator contentCoordinator;
        private readonly TargetWorkflowService targetWorkflowService;

        public SpawnerManager(
            MonoBehaviour runner,
            GameObject picturePrefab,
            GameObject textPrefab,
            GameObject modelContainerPrefab,
            ITargetContextResolver targetContextResolver,
            ContentCreationCoordinator contentCoordinator = null,
            TargetWorkflowService targetWorkflowService = null)
        {
            this.runner = runner;
            this.picturePrefab = picturePrefab;
            this.textPrefab = textPrefab;
            this.modelContainerPrefab = modelContainerPrefab;
            this.targetContextResolver = targetContextResolver;
            this.contentCoordinator = contentCoordinator ?? new ContentCreationCoordinator();
            this.targetWorkflowService = targetWorkflowService ?? new TargetWorkflowService();
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
                    return CreateUploadedContent(request);

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

        private SpawnContentResult CreateUploadedContent(SpawnRequest request)
        {
            if (runner == null)
            {
                return FailContent(
                    "SpawnerManager requires a MonoBehaviour runner for upload-based content creation.",
                    request.contentType,
                    ResolveRenderKind(request.contentType));
            }

            if (string.IsNullOrWhiteSpace(request.mediaUrl))
            {
                return FailContent(
                    "mediaUrl is required for image/model content creation.",
                    request.contentType,
                    ResolveRenderKind(request.contentType));
            }

            string originalFileName = string.IsNullOrWhiteSpace(request.originalFileName)
                ? request.mediaUrl
                : request.originalFileName;

            ContentCreationCoordinator.LocalContentSpawnOutcome outcome =
                contentCoordinator.SpawnFromContentUpload(
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
    }
}
