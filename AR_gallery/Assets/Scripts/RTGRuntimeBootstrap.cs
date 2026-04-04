using UnityEngine;
using RTG;

/// <summary>
/// 在播放模式下自动创建 Runtime Transform Gizmos 所需模块（等价于菜单 Tools/Runtime Transform Gizmos/Initialize）。
/// 仅在存在 <see cref="ContentTransformController"/> 的场景中启用，避免影响其它场景。
/// </summary>
public static class RTGRuntimeBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRTGModules()
    {
        if (RTGApp.Get != null)
            return;

        if (Object.FindFirstObjectByType<ContentTransformController>() == null)
            return;

        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            Debug.LogWarning("RTGRuntimeBootstrap: 未找到 Main Camera，无法初始化 Runtime Transform Gizmos。");
            return;
        }

        GameObject appGo = new GameObject("RTGApp");
        appGo.AddComponent<RTGApp>();
        Transform root = appGo.transform;

        CreateChildModule<RTGizmosEngine>(root);
        CreateChildModule<RTScene>(root);
        CreateChildModule<RTSceneGrid>(root);
        // RTFocusCamera.Awake 要求 TargetCamera 已赋值；AddComponent 会立刻触发 Awake，故先挂到未激活物体上，
        // SetTargetCamera 后再激活，避免 “RTCamera: No target camera was specified”。
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

    private static T CreateChildModule<T>(Transform parent) where T : MonoBehaviour
    {
        string objectName = typeof(T).Name;
        GameObject go = new GameObject(objectName);
        go.transform.SetParent(parent, false);
        return go.AddComponent<T>();
    }
}
