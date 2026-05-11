using System;
using System.Collections;
using System.Threading.Tasks;
using GLTFast;
using UnityEngine;
using UnityEngine.Networking;

namespace ARGallery.Content
{
    /// <summary>
    /// Runtime GLB load via glTFast: download with <see cref="UnityWebRequest"/>, import, parent under <see cref="ModelContentContainerRoot"/>.
    /// Independent of <see cref="IApiClient"/> — pass any HTTP URL (e.g. upload response URL).
    /// </summary>
    public static class ModelLoadService
    {
        public struct LoadOutcome
        {
            public bool success;
            public string message;
        }

        /// <summary>
        /// Downloads a .glb and instantiates its main scene under <paramref name="container"/>'s ContentBody.
        /// </summary>
        public static void BeginLoadGlb(
            MonoBehaviour runner,
            string glbUrl,
            ModelContentContainerRoot container,
            Action<LoadOutcome> onCompleted)
        {
            if (runner == null)
            {
                onCompleted?.Invoke(new LoadOutcome { success = false, message = "ModelLoadService: runner is null." });
                return;
            }

            runner.StartCoroutine(LoadGlbRoutine(glbUrl, container, onCompleted));
        }

        /// <summary>
        /// Loads a .glb directly from local bytes and instantiates under container ContentBody.
        /// </summary>
        public static void BeginLoadGlbBytes(
            MonoBehaviour runner,
            byte[] glbBytes,
            string sourceName,
            ModelContentContainerRoot container,
            Action<LoadOutcome> onCompleted)
        {
            if (runner == null)
            {
                onCompleted?.Invoke(new LoadOutcome { success = false, message = "ModelLoadService: runner is null." });
                return;
            }

            runner.StartCoroutine(LoadGlbBytesRoutine(glbBytes, sourceName, container, onCompleted));
        }

        private static IEnumerator LoadGlbRoutine(
            string glbUrl,
            ModelContentContainerRoot container,
            Action<LoadOutcome> onCompleted)
        {
            if (string.IsNullOrWhiteSpace(glbUrl) || container == null)
            {
                onCompleted?.Invoke(new LoadOutcome { success = false, message = "Invalid URL or container." });
                yield break;
            }

            Transform attach = container.ContentBody;
            if (attach == null)
            {
                onCompleted?.Invoke(new LoadOutcome { success = false, message = "ContentBody missing." });
                yield break;
            }

            using (UnityWebRequest req = UnityWebRequest.Get(glbUrl))
            {
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    string err = string.IsNullOrEmpty(req.error) ? req.result.ToString() : req.error;
                    onCompleted?.Invoke(new LoadOutcome { success = false, message = $"Download failed: {err}" });
                    yield break;
                }

                byte[] data = req.downloadHandler.data;
                if (data == null || data.Length == 0)
                {
                    onCompleted?.Invoke(new LoadOutcome { success = false, message = "Download returned empty data." });
                    yield break;
                }

                Task<(bool ok, string err)> task = LoadGltfBinaryIntoParentAsync(data, glbUrl, attach);
                while (!task.IsCompleted)
                    yield return null;

                if (task.IsFaulted && task.Exception != null)
                {
                    onCompleted?.Invoke(new LoadOutcome
                    {
                        success = false,
                        message = task.Exception.InnerException != null
                            ? task.Exception.InnerException.Message
                            : task.Exception.Message
                    });
                    yield break;
                }

                (bool ok, string errMsg) = task.Result;
                onCompleted?.Invoke(ok
                    ? new LoadOutcome { success = true, message = "GLB loaded." }
                    : new LoadOutcome { success = false, message = errMsg ?? "glTF load failed." });
            }
        }

        private static IEnumerator LoadGlbBytesRoutine(
            byte[] glbBytes,
            string sourceName,
            ModelContentContainerRoot container,
            Action<LoadOutcome> onCompleted)
        {
            if (glbBytes == null || glbBytes.Length == 0 || container == null)
            {
                onCompleted?.Invoke(new LoadOutcome { success = false, message = "Invalid GLB bytes or container." });
                yield break;
            }

            Transform attach = container.ContentBody;
            if (attach == null)
            {
                onCompleted?.Invoke(new LoadOutcome { success = false, message = "ContentBody missing." });
                yield break;
            }

            string source = string.IsNullOrWhiteSpace(sourceName) ? "local.glb" : sourceName.Trim();
            if (!source.EndsWith(".glb", StringComparison.OrdinalIgnoreCase))
                source += ".glb";
            string resolverUri = "https://local-runtime/" + source;

            Task<(bool ok, string err)> task = LoadGltfBinaryIntoParentAsync(glbBytes, resolverUri, attach);
            while (!task.IsCompleted)
                yield return null;

            if (task.IsFaulted && task.Exception != null)
            {
                onCompleted?.Invoke(new LoadOutcome
                {
                    success = false,
                    message = task.Exception.InnerException != null
                        ? task.Exception.InnerException.Message
                        : task.Exception.Message
                });
                yield break;
            }

            (bool ok, string errMsg) = task.Result;
            onCompleted?.Invoke(ok
                ? new LoadOutcome { success = true, message = "GLB loaded from local bytes." }
                : new LoadOutcome { success = false, message = errMsg ?? "glTF local load failed." });
        }

        private static async Task<(bool ok, string err)> LoadGltfBinaryIntoParentAsync(
            byte[] glbBytes,
            string originalUriString,
            Transform attachParent)
        {
            Uri uri;
            try
            {
                uri = new Uri(originalUriString);
            }
            catch (Exception e)
            {
                return (false, $"Invalid URL for glTF resolver: {e.Message}");
            }

            var gltf = new GltfImport();
            try
            {
                bool loaded = await gltf.Load(glbBytes, uri, importSettings: null, cancellationToken: default)
                    .ConfigureAwait(true);

                if (!loaded || gltf.LoadingError || !gltf.LoadingDone)
                    return (false, "GltfImport.Load failed (see Unity console for glTFast logs).");

                bool instanced = await gltf.InstantiateMainSceneAsync(attachParent).ConfigureAwait(true);
                if (!instanced)
                    return (false, "InstantiateMainSceneAsync returned false.");

                return (true, null);
            }
            catch (Exception e)
            {
                return (false, e.Message);
            }
            // Intentionally not calling GltfImport.Dispose here: disposing right after instantiate can break materials;
            // tie disposal to container lifetime in a later iteration if needed.
        }
    }
}
