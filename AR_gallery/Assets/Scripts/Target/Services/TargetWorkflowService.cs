using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Local-first target workflow:
/// 1) create + register target locally
/// 2) fire async API sync (CreateTarget) if client is available
/// </summary>
public class TargetWorkflowService
{
    public class LocalCreateResult
    {
        public bool success;
        public bool isDuplicate;
        public int duplicateIndex = -1;
        public string targetId;
        public string message;
        public GameObject targetObject;
    }

    public LocalCreateResult CreateAndRegisterLocal(
        RuntimeImageTargetFactory factory,
        TargetSelectionManager targetSelectionManager,
        string targetName,
        string targetId,
        string displayLabel)
    {
        if (factory == null || targetSelectionManager == null)
        {
            return new LocalCreateResult
            {
                success = false,
                message = "Factory or TargetSelectionManager is missing."
            };
        }

        int existingIndex = targetSelectionManager.FindTargetIndexById(targetId);
        if (existingIndex >= 0)
        {
            return new LocalCreateResult
            {
                success = false,
                isDuplicate = true,
                duplicateIndex = existingIndex,
                targetId = targetId,
                message = $"Target ID already exists: {targetId}"
            };
        }

        GameObject newTarget = factory.CreateTarget(targetName, targetId, displayLabel);
        if (newTarget == null)
        {
            return new LocalCreateResult
            {
                success = false,
                targetId = targetId,
                message = "Failed to create local target."
            };
        }

        targetSelectionManager.AddTarget(newTarget, setActive: true);

        return new LocalCreateResult
        {
            success = true,
            targetId = targetId,
            targetObject = newTarget,
            message = $"Created: {targetId}"
        };
    }

    public IApiRequestHandle SyncCreateTarget(
        IApiClient apiClient,
        GameObject targetObject,
        string targetId,
        string targetName,
        string displayLabel,
        string targetImageUrl,
        Action<ApiResult<CreateTargetResponseDto>> onCompleted,
        float timeoutSeconds = 20f)
    {
        if (apiClient == null)
        {
            onCompleted?.Invoke(ApiResult<CreateTargetResponseDto>.Fail(
                ApiErrorCodes.Unknown,
                "CreateTarget sync skipped: no API client available."));
            return null;
        }

        var request = new CreateTargetRequestDto
        {
            targetId = targetId,
            targetName = targetName,
            displayLabel = displayLabel,
            targetImageUrl = targetImageUrl ?? "",
            localPosition = ReadLocalPosition(targetObject),
            localEuler = ReadLocalEuler(targetObject),
            localScale = ReadLocalScale(targetObject),
            meta = new ApiSyncMetaDto
            {
                schemaVersion = "v1",
                clientRequestId = Guid.NewGuid().ToString("N"),
                createdAtUtc = DateTime.UtcNow.ToString("o")
            }
        };

        return apiClient.CreateTarget(request, onCompleted, timeoutSeconds);
    }

    public Coroutine ApplyTargetImageFromUrl(
        MonoBehaviour runner,
        GameObject targetObject,
        string targetImageUrl)
    {
        if (runner == null || targetObject == null)
            return null;
        if (string.IsNullOrWhiteSpace(targetImageUrl))
            return null;

        return runner.StartCoroutine(ApplyTargetImageFromUrlRoutine(targetObject, targetImageUrl.Trim()));
    }

    private IEnumerator ApplyTargetImageFromUrlRoutine(GameObject targetObject, string imageUrl)
    {
        Transform visual = FindTargetVisual(targetObject);
        if (visual == null)
            yield break;

        Renderer renderer = visual.GetComponent<Renderer>();
        if (renderer == null)
            yield break;

        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(imageUrl))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"Target image apply skipped (fallback visual kept). URL={imageUrl}, error={request.error}");
                yield break;
            }

            Texture2D texture = ((DownloadHandlerTexture)request.downloadHandler).texture;
            if (texture == null)
            {
                Debug.LogWarning($"Target image apply skipped: downloaded texture is null. URL={imageUrl}");
                yield break;
            }

            // Runtime-created targets use Unlit/Color by default (no texture slot),
            // so switch to a texture-capable shader before applying the downloaded image.
            Material material = renderer.material;
            if (material == null || !material.HasProperty("_MainTex"))
            {
                Shader textureShader = Shader.Find("Unlit/Texture");
                if (textureShader == null)
                    textureShader = Shader.Find("Standard");
                if (textureShader != null)
                    renderer.material = new Material(textureShader);
            }

            renderer.material.mainTexture = texture;

            // Hide the fallback text label once a real texture is applied.
            Transform label = visual.Find("TargetLabel");
            if (label != null)
                label.gameObject.SetActive(false);
        }
    }

    private static ApiVector3Dto ReadLocalPosition(GameObject targetObject)
    {
        Transform visual = FindTargetVisual(targetObject);
        Vector3 v = visual != null ? visual.localPosition : Vector3.zero;
        return new ApiVector3Dto(v.x, v.y, v.z);
    }

    private static ApiVector3Dto ReadLocalEuler(GameObject targetObject)
    {
        Transform visual = FindTargetVisual(targetObject);
        Vector3 v = visual != null ? visual.localEulerAngles : Vector3.zero;
        return new ApiVector3Dto(v.x, v.y, v.z);
    }

    private static ApiVector3Dto ReadLocalScale(GameObject targetObject)
    {
        Transform visual = FindTargetVisual(targetObject);
        Vector3 v = visual != null ? visual.localScale : Vector3.one;
        return new ApiVector3Dto(v.x, v.y, v.z);
    }

    private static Transform FindTargetVisual(GameObject targetObject)
    {
        if (targetObject == null)
            return null;
        return targetObject.transform.Find("TargetVisual");
    }
}
