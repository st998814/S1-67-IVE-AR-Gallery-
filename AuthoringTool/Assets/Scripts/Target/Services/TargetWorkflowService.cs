using System;
using System.Collections;
using ARGallery.Workspace.Persistence;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Local-first target workflow:
/// 1) create + register target locally
/// 2) fire async API sync (CreateTarget) if client is available
/// </summary>
public class TargetWorkflowService
{
    private RuntimeImageTargetFactory runtimeImageTargetFactory;
    private TargetSelectionManager targetSelectionManager;

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
        MonoBehaviour context,
        string targetName,
        string targetId,
        string displayLabel,
        float physicalWidthMeters = 0.2f)
    {
        runtimeImageTargetFactory = ResolveRuntimeImageTargetFactory(context);
        targetSelectionManager = ResolveTargetSelectionManager();

        if (runtimeImageTargetFactory == null || targetSelectionManager == null)
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

        GameObject newTarget = runtimeImageTargetFactory.CreateTarget(targetName, targetId, displayLabel, physicalWidthMeters);
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

        WorkspaceAuthoredAttach.EnsureTarget(newTarget, targetId, targetName);
        var authoredTarget = newTarget.GetComponent<AuthoredTargetInstance>();
        if (authoredTarget != null)
            authoredTarget.PhysicalWidthM = Mathf.Max(0.001f, physicalWidthMeters);

        return new LocalCreateResult
        {
            success = true,
            targetId = targetId,
            targetObject = newTarget,
            message = $"Created: {targetId}"
        };
    }

    private TargetSelectionManager ResolveTargetSelectionManager()
    {
        if (targetSelectionManager != null)
            return targetSelectionManager;

        targetSelectionManager = UnityEngine.Object.FindFirstObjectByType<TargetSelectionManager>();
        if (targetSelectionManager != null)
            return targetSelectionManager;

        TargetSelectionManager[] candidates = Resources.FindObjectsOfTypeAll<TargetSelectionManager>();
        foreach (TargetSelectionManager candidate in candidates)
        {
            if (candidate == null || !candidate.gameObject.scene.IsValid())
                continue;
            targetSelectionManager = candidate;
            break;
        }

        return targetSelectionManager;
    }

    private RuntimeImageTargetFactory ResolveRuntimeImageTargetFactory(MonoBehaviour context)
    {
        if (runtimeImageTargetFactory != null)
            return runtimeImageTargetFactory;

        runtimeImageTargetFactory = UnityEngine.Object.FindFirstObjectByType<RuntimeImageTargetFactory>();
        if (runtimeImageTargetFactory != null)
            return runtimeImageTargetFactory;

        RuntimeImageTargetFactory[] candidates = Resources.FindObjectsOfTypeAll<RuntimeImageTargetFactory>();
        foreach (RuntimeImageTargetFactory candidate in candidates)
        {
            if (candidate == null || !candidate.gameObject.scene.IsValid())
                continue;
            runtimeImageTargetFactory = candidate;
            break;
        }

        if (runtimeImageTargetFactory == null && context != null)
        {
            runtimeImageTargetFactory = context.gameObject.AddComponent<RuntimeImageTargetFactory>();
            Debug.LogWarning("TargetWorkflowService: RuntimeImageTargetFactory not found in scene. Added one to context object.");
        }

        return runtimeImageTargetFactory;
    }

    public IApiRequestHandle SyncCreateTarget(
        IApiClient apiClient,
        GameObject targetObject,
        string targetId,
        string targetName,
        string displayLabel,
        string targetImageUrl,
        string workspaceId,
        string workspaceName,
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

        string wid = string.IsNullOrWhiteSpace(workspaceId) ? "default" : workspaceId.Trim();
        string wname = workspaceName != null ? workspaceName.Trim() : "";

        var request = new CreateTargetRequestDto
        {
            targetId = targetId,
            targetName = targetName,
            displayLabel = displayLabel,
            targetImageUrl = targetImageUrl ?? "",
            targetReferenceImageUrl = ReadTargetReferenceImageUrl(targetObject),
            workspaceId = wid,
            workspaceName = wname,
            physicalWidthM = ResolvePhysicalWidthForSync(targetObject),
            physicalWidth = ResolvePhysicalWidthForSync(targetObject),
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

    public bool ApplyTargetImageBytes(GameObject targetObject, byte[] imageBytes)
    {
        if (targetObject == null || imageBytes == null || imageBytes.Length == 0)
            return false;
        Transform visual = FindTargetVisual(targetObject);
        if (visual == null)
            return false;
        Renderer renderer = visual.GetComponent<Renderer>();
        if (renderer == null)
            return false;

        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!texture.LoadImage(imageBytes))
            return false;

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
        Transform label = visual.Find("TargetLabel");
        if (label != null)
            label.gameObject.SetActive(false);

        TargetVisualPhysicalLayout.ApplyFromTargetRoot(targetObject, texture);
        return true;
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

            TargetVisualPhysicalLayout.ApplyFromTargetRoot(targetObject, texture);
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

    private static float ResolvePhysicalWidthForSync(GameObject targetObject)
    {
        if (targetObject == null)
            return 0.2f;

        var auth = targetObject.GetComponent<AuthoredTargetInstance>();
        if (auth != null && auth.PhysicalWidthM > 1e-5f)
            return auth.PhysicalWidthM;

        return 0.2f;
    }

    private static string ReadTargetReferenceImageUrl(GameObject targetObject)
    {
        if (targetObject == null)
            return "";

        var auth = targetObject.GetComponent<AuthoredTargetInstance>();
        return auth != null ? auth.TargetReferenceImageUrl ?? "" : "";
    }
}
