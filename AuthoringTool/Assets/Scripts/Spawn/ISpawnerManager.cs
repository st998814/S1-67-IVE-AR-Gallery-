using System;
using UnityEngine;

namespace ARGallery.Spawning
{
    /// <summary>
    /// Unified entry point for runtime target/content creation.
    /// Implementations route requests to concrete creation workflows.
    /// </summary>
    public interface ISpawnerManager
    {
        SpawnContentResult CreateContent(SpawnRequest request);
        SpawnTargetResult CreateTarget(SpawnTargetRequest request);
        IApiRequestHandle BeginSyncCreateContent(
            IApiClient apiClient,
            SpawnRequest request,
            Transform spawnedTransform,
            Action<ApiResult<CreateContentResponseDto>> onCompleted = null,
            float timeoutSeconds = 20f);
        IApiRequestHandle BeginSyncCreateTarget(
            IApiClient apiClient,
            SpawnTargetRequest request,
            GameObject targetObject,
            Action<ApiResult<CreateTargetResponseDto>> onCompleted = null,
            float timeoutSeconds = 20f);

        /// <summary>Returns pooled shells to the runtime pool; destroys non-pooled instances.</summary>
        bool ReleaseSpawnedContent(GameObject instance);
    }
}
