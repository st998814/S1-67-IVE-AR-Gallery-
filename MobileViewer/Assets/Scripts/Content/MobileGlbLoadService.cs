using System;
using System.Collections;
using System.Threading.Tasks;
using GLTFast;
using UnityEngine;
using UnityEngine.Networking;

namespace MobileViewer.Content
{
    /// <summary>
    /// Downloads a remote .glb via glTFast and instantiates under a target-local attach transform.
    /// </summary>
    public static class MobileGlbLoadService
    {
        public struct LoadOutcome
        {
            public bool success;
            public string message;
        }

        public static void BeginLoadGlb(
            MonoBehaviour runner,
            string glbUrl,
            Transform attachParent,
            Action<LoadOutcome> onCompleted)
        {
            if (runner == null)
            {
                onCompleted?.Invoke(new LoadOutcome { success = false, message = "MobileGlbLoadService: runner is null." });
                return;
            }

            if (attachParent == null)
            {
                onCompleted?.Invoke(new LoadOutcome { success = false, message = "MobileGlbLoadService: attachParent is null." });
                return;
            }

            runner.StartCoroutine(LoadGlbRoutine(glbUrl, attachParent, onCompleted));
        }

        private static IEnumerator LoadGlbRoutine(string glbUrl, Transform attachParent, Action<LoadOutcome> onCompleted)
        {
            if (string.IsNullOrWhiteSpace(glbUrl))
            {
                onCompleted?.Invoke(new LoadOutcome { success = false, message = "GLB URL is empty." });
                yield break;
            }

            using (var req = UnityWebRequest.Get(glbUrl))
            {
                yield return req.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
                var failed = req.result != UnityWebRequest.Result.Success;
#else
                var failed = req.isNetworkError || req.isHttpError;
#endif
                if (failed)
                {
                    string err = string.IsNullOrEmpty(req.error) ? req.result.ToString() : req.error;
                    onCompleted?.Invoke(new LoadOutcome { success = false, message = $"Download failed: {err}" });
                    yield break;
                }

                byte[] data = req.downloadHandler?.data;
                if (data == null || data.Length == 0)
                {
                    onCompleted?.Invoke(new LoadOutcome { success = false, message = "Download returned empty data." });
                    yield break;
                }

                Task<(bool ok, string err)> task = LoadGltfBinaryIntoParentAsync(data, glbUrl, attachParent);
                while (!task.IsCompleted)
                {
                    yield return null;
                }

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
                {
                    return (false, "GltfImport.Load failed (see Unity console for glTFast logs).");
                }

                bool instanced = await gltf.InstantiateMainSceneAsync(attachParent).ConfigureAwait(true);
                if (!instanced)
                {
                    return (false, "InstantiateMainSceneAsync returned false.");
                }

                return (true, null);
            }
            catch (Exception e)
            {
                return (false, e.Message);
            }
        }
    }
}
