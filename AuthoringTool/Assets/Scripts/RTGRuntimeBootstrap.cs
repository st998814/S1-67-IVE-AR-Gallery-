using UnityEngine;
using UnityEngine.SceneManagement;
using RTG;

/// <summary>
/// 在播放模式下自动创建 Runtime Transform Gizmos 所需模块（等价于菜单 Tools/Runtime Transform Gizmos/Initialize）。
/// 仅在存在 Transform Gizmo 相关控制器的场景中启用，避免影响其它 scenes。
/// <see cref="EnsureRTGModules"/> 可重复调用（幂等），供 <see cref="TransformGizmoController"/> 在引擎尚未就绪时重试。
/// </summary>
public static class RTGRuntimeBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRTGModulesAfterSceneLoad()
    {
        EnsureRTGModules();
    }

    /// <summary>
    /// Creates RTG modules if this scene needs them and <see cref="RTGApp"/> does not exist yet.
    /// Safe to call from gameplay code when <see cref="RTGizmosEngine.Get"/> is still null.
    /// </summary>
    public static void EnsureRTGModules()
    {
        if (RTGApp.Get != null)
            return;

        if (!SceneNeedsRtgAutoBootstrap())
            return;

        Camera mainCam = ResolveBootstrapCamera();
        if (mainCam == null)
        {
            Debug.LogWarning("RTGRuntimeBootstrap: No Camera found; cannot initialize Runtime Transform Gizmos.");
            return;
        }

        GameObject appGo = new GameObject("RTGApp");
        appGo.AddComponent<RTGApp>();
        Transform root = appGo.transform;

        CreateChildModule<RTGizmosEngine>(root);
        CreateChildModule<RTScene>(root);
        CreateChildModule<RTSceneGrid>(root);
        GameObject focusGo = new GameObject("RTFocusCamera");
        focusGo.SetActive(false);
        focusGo.transform.SetParent(root, false);
        RTFocusCamera focus = focusGo.AddComponent<RTFocusCamera>();
        focus.SetTargetCamera(mainCam);
        focus.Settings.CanProcessInput = false;
        focusGo.SetActive(true);
        CreateChildModule<RTCameraBackground>(root);
        CreateChildModule<RTInputDevice>(root);
        CreateChildModule<RTUndoRedo>(root);
    }

    private static bool SceneNeedsRtgAutoBootstrap()
    {
        Scene active = SceneManager.GetActiveScene();
        if (!active.IsValid())
            return false;

        string sceneName = active.name;
        if (string.Equals(sceneName, "AuthoringToolScene", System.StringComparison.Ordinal)
            || string.Equals(sceneName, "TransformSandboxScene", System.StringComparison.Ordinal))
        {
            return true;
        }

        return FindController<TransformGizmoController>()
            || FindController<AuthoringTransformCoordinator>();
    }

    private static bool FindController<T>() where T : Object
    {
#if UNITY_2022_3_OR_NEWER
        return Object.FindFirstObjectByType<T>(FindObjectsInactive.Include) != null;
#else
        return Object.FindObjectOfType<T>(true) != null;
#endif
    }

    private static Camera ResolveBootstrapCamera()
    {
        Camera c = Camera.main;
        if (c != null)
            return c;

        GameObject tagged = GameObject.FindGameObjectWithTag("MainCamera");
        if (tagged != null && tagged.TryGetComponent(out Camera taggedCam))
            return taggedCam;

#if UNITY_2022_3_OR_NEWER
        Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
        Camera[] cameras = Object.FindObjectsOfType<Camera>();
#endif
        return cameras != null && cameras.Length > 0 ? cameras[0] : null;
    }

    private static T CreateChildModule<T>(Transform parent) where T : MonoBehaviour
    {
        string objectName = typeof(T).Name;
        GameObject go = new GameObject(objectName);
        go.transform.SetParent(parent, false);
        return go.AddComponent<T>();
    }
}
