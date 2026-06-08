using System;
using UnityEngine;

namespace ARGallery.Content
{
    /// <summary>
    /// Resolves backend media paths to absolute URLs for <see cref="UnityWebRequest"/>.
    /// </summary>
    public static class ContentMediaUrlUtility
    {
        public const string DefaultBackendBaseUrl = "http://172.20.10.2:5050";

        public static string ResolveAbsoluteUrl(string mediaUrl, string backendBaseUrl = null)
        {
            if (string.IsNullOrWhiteSpace(mediaUrl))
                return mediaUrl;

            string trimmed = mediaUrl.Trim();
            if (Uri.TryCreate(trimmed, UriKind.Absolute, out Uri absolute)
                && (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps))
            {
                return trimmed;
            }

            string baseUrl = string.IsNullOrWhiteSpace(backendBaseUrl)
                ? DefaultBackendBaseUrl
                : backendBaseUrl.Trim().TrimEnd('/');
            string path = trimmed.TrimStart('/');
            return $"{baseUrl}/{path}";
        }

        public static string FileNameFromUrl(string mediaUrl, string fallback = "asset.glb")
        {
            if (string.IsNullOrWhiteSpace(mediaUrl))
                return fallback;

            string path = mediaUrl;
            if (Uri.TryCreate(mediaUrl.Trim(), UriKind.Absolute, out Uri uri))
                path = uri.AbsolutePath;

            string name = System.IO.Path.GetFileName(path);
            return string.IsNullOrWhiteSpace(name) ? fallback : name;
        }

        public static string ResolveBackendBaseUrl(string inspectorOverride = null)
        {
            return string.IsNullOrWhiteSpace(inspectorOverride)
                ? DefaultBackendBaseUrl
                : inspectorOverride.Trim().TrimEnd('/');
        }

        /// <summary>
        /// Returns false when <paramref name="inspectorOverride"/> is empty (offline / skip backend).
        /// </summary>
        public static bool TryResolveConfiguredBackendBaseUrl(string inspectorOverride, out string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(inspectorOverride))
            {
                baseUrl = null;
                return false;
            }

            baseUrl = inspectorOverride.Trim().TrimEnd('/');
            return true;
        }

        public static string BuildWorkspaceDetailUrl(string workspaceId, string inspectorOverride = null)
        {
            string baseUrl = ResolveBackendBaseUrl(inspectorOverride);
            return $"{baseUrl}/api/workspaces/{Uri.EscapeDataString(workspaceId.Trim())}";
        }

        public static string BuildWorkspaceListUrl(string inspectorOverride = null)
        {
            string baseUrl = ResolveBackendBaseUrl(inspectorOverride);
            return $"{baseUrl}/api/workspaces";
        }
    }
}
