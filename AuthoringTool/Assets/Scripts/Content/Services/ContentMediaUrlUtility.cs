using System;
using UnityEngine;

namespace ARGallery.Content
{
    /// <summary>
    /// Resolves backend media paths to absolute URLs for <see cref="UnityWebRequest"/>.
    /// </summary>
    public static class ContentMediaUrlUtility
    {
        public const string DefaultBackendBaseUrl = "http://127.0.0.1:5050";

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

        public static string ResolveBackendBaseUrl()
        {
            return DefaultBackendBaseUrl;
        }
    }
}
