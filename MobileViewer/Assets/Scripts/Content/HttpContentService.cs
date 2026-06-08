using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace MobileViewer.Content
{
    public class HttpContentService : MonoBehaviour, IContentService
    {
        [Header("HTTP")]
        [SerializeField] private string baseApiUrl = "http://172.20.10.2:5050"; // No trailing slash
        [SerializeField] private string contentPath = "/api/mobileviewer/content/by-target/"; // Must match docs/api/mobileviewer/MobileViewerContentRuntime.md
        [SerializeField] private string apiKeyHeaderName;
        [SerializeField] private string apiKeyValue;

        [Header("Logging")]
        [SerializeField] private bool logRequests = true;
        [SerializeField] private bool logResponses = true;

        [Serializable]
        private class ContentDto
        {
            [Serializable]
            public class Vector3Dto
            {
                public float x;
                public float y;
                public float z;
            }

            public string targetName;
            public string title;
            public string description;
            public string contentType;
            public string mediaUrl;
            public float targetPhysicalWidthM = 1f;
            public Vector3Dto localPosition;
            public Vector3Dto localEuler;
            public Vector3Dto localScale;
            public Vector3Dto targetLocalEuler;
            public string targetPosture;
            public string color;
            public string displayLabel;
        }

        public async Task<ContentData> GetContentForTargetAsync(string targetName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(targetName))
            {
                if (logRequests)
                {
                    Debug.LogWarning("[HttpContentService] GetContentForTargetAsync called with empty targetName.");
                }

                return BuildFallbackContent("unknown-target");
            }

            var targetKey = Uri.EscapeDataString(targetName);
            var url = $"{baseApiUrl.TrimEnd('/')}{contentPath}{targetKey}";

            if (logRequests)
            {
                Debug.Log($"[HttpContentService] GET {url}");
            }

            using (var request = UnityWebRequest.Get(url))
            {
                if (!string.IsNullOrEmpty(apiKeyHeaderName) && !string.IsNullOrEmpty(apiKeyValue))
                {
                    request.SetRequestHeader(apiKeyHeaderName, apiKeyValue);
                }

                var operation = request.SendWebRequest();

                while (!operation.isDone && !cancellationToken.IsCancellationRequested)
                {
                    await Task.Yield();
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    if (logRequests)
                    {
                        Debug.Log($"[HttpContentService] Request cancelled for target '{targetName}'.");
                    }

                    return null;
                }

#if UNITY_2020_1_OR_NEWER
                var failed = request.result != UnityWebRequest.Result.Success;
#else
                var failed = request.isNetworkError || request.isHttpError;
#endif

                if (failed)
                {
                    if (request.responseCode == 404)
                    {
                        if (logResponses)
                        {
                            Debug.LogWarning($"[HttpContentService] No content for target '{targetName}' (404).");
                        }

                        // “No content” is not a hard error; caller can show a toast and keep scanning.
                        return null;
                    }

                    Debug.LogWarning($"[HttpContentService] HTTP error for target '{targetName}': {request.responseCode} {request.error}");
                    return BuildFallbackContent(targetName);
                }

                var json = request.downloadHandler.text;
                if (logResponses)
                {
                    Debug.Log($"[HttpContentService] Response for '{targetName}': {json}");
                }

                try
                {
                    var dto = JsonUtility.FromJson<ContentDto>(json);
                    if (dto == null)
                    {
                        Debug.LogWarning($"[HttpContentService] Failed to deserialize content for '{targetName}'.");
                        return BuildFallbackContent(targetName);
                    }

                    return MapToContentData(dto, targetName);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[HttpContentService] Exception deserializing content for '{targetName}': {ex}");
                    return BuildFallbackContent(targetName);
                }
            }
        }

        private static ContentData MapToContentData(ContentDto dto, string fallbackTargetName)
        {
            var data = new ContentData
            {
                targetName = string.IsNullOrWhiteSpace(dto.targetName) ? fallbackTargetName : dto.targetName,
                title = dto.title ?? string.Empty,
                description = dto.description ?? string.Empty,
                contentType = string.IsNullOrWhiteSpace(dto.contentType) ? "cube" : dto.contentType,
                mediaUrl = dto.mediaUrl ?? string.Empty,
                targetPhysicalWidthM = dto.targetPhysicalWidthM > 0f ? dto.targetPhysicalWidthM : 1f,
                localPosition = ToVector3(dto.localPosition, new Vector3(0f, 0.05f, 0f)),
                localEuler = ToVector3(dto.localEuler, Vector3.zero),
                localScale = ToVector3(dto.localScale, Vector3.one * 0.3f),
                targetLocalEuler = ToVector3(dto.targetLocalEuler, Vector3.zero),
                targetPosture = string.IsNullOrWhiteSpace(dto.targetPosture) ? "wall" : dto.targetPosture,
                mockColor = ParseColorOrDefault(dto.color, Color.white),
                displayLabel = string.IsNullOrWhiteSpace(dto.displayLabel)
                    ? (string.IsNullOrWhiteSpace(dto.targetName) ? fallbackTargetName : dto.targetName)
                    : dto.displayLabel
            };

            return data;
        }

        private static Color ParseColorOrDefault(string hex, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(hex))
            {
                return fallback;
            }

            if (ColorUtility.TryParseHtmlString(hex, out var color))
            {
                return color;
            }

            return fallback;
        }

        private static ContentData BuildFallbackContent(string targetName)
        {
            return new ContentData
            {
                targetName = string.IsNullOrWhiteSpace(targetName) ? "unknown-target" : targetName,
                title = "Content unavailable",
                description = "The backend did not return content for this target.",
                contentType = "cube",
                mediaUrl = string.Empty,
                targetPhysicalWidthM = 1f,
                localPosition = new Vector3(0f, 0.05f, 0f),
                localEuler = Vector3.zero,
                localScale = Vector3.one * 0.3f,
                targetLocalEuler = Vector3.zero,
                targetPosture = "wall",
                mockColor = new Color(0.9f, 0.5f, 0.5f),
                displayLabel = "!"
            };
        }

        private static Vector3 ToVector3(ContentDto.Vector3Dto source, Vector3 fallback)
        {
            return source == null ? fallback : new Vector3(source.x, source.y, source.z);
        }
    }
}

