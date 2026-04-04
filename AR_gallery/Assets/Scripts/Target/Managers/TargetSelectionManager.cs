using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class TargetSelectionManager : MonoBehaviour
{
    [SerializeField] private GameObject[] targets;
    [SerializeField] private int activeTargetIndex = 0;

    /// <summary>切换当前 AR 目标时触发（键盘 1/2、Authoring 下拉共用）。</summary>
    public event Action<int> ActiveTargetChanged;

    public int ActiveTargetIndex => activeTargetIndex;

    public int TargetCount => targets != null ? targets.Length : 0;

    private void Start()
    {
        ShowOnlyActiveTarget();
    }

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        // 在 Authoring 侧栏的 FloatField / TextField 里输入 1、2 时不应触发换 Target，否则会误切到另一张 AR 图。
        if (IsUiToolkitTextOrNumericFieldFocused())
            return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame)
            SetActiveTarget(0);

        if (Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame)
            SetActiveTarget(1);
    }

    private static bool IsUiToolkitTextOrNumericFieldFocused()
    {
        UIDocument[] docs = UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
        foreach (UIDocument doc in docs)
        {
            if (doc == null || !doc.enabled || doc.rootVisualElement == null)
                continue;
            IPanel panel = doc.rootVisualElement.panel;
            FocusController fc = panel?.focusController;
            if (fc?.focusedElement is not VisualElement focused)
                continue;

            for (VisualElement p = focused; p != null; p = p.parent)
            {
                if (p is TextField || p is FloatField || p is IntegerField || p is LongField || p is DoubleField)
                    return true;
            }
        }

        return false;
    }

    public void SetActiveTarget(int index)
    {
        if (targets == null || index < 0 || index >= targets.Length)
            return;

        activeTargetIndex = index;
        ShowOnlyActiveTarget();
        ActiveTargetChanged?.Invoke(activeTargetIndex);
    }

    private void ShowOnlyActiveTarget()
    {
        if (targets == null)
            return;

        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] != null)
                targets[i].SetActive(i == activeTargetIndex);
        }
    }

    public GameObject GetActiveTarget()
    {
        if (targets == null || activeTargetIndex < 0 || activeTargetIndex >= targets.Length)
            return null;

        return targets[activeTargetIndex];
    }

    /// <summary>下拉列表用：无 ArImageTarget 时用物体名。</summary>
    public string GetTargetDisplayName(int index)
    {
        GameObject go = GetTargetAt(index);
        if (go == null)
            return "(empty)";
        var desc = go.GetComponent<ArImageTarget>();
        return desc != null ? desc.DisplayLabel : go.name;
    }

    /// <summary>保存到数据库用：与手机端识别 marker 的键一致。</summary>
    public string GetTargetId(int index)
    {
        GameObject go = GetTargetAt(index);
        if (go == null)
            return "";
        var desc = go.GetComponent<ArImageTarget>();
        return desc != null ? desc.TargetId : go.name;
    }

    public GameObject GetTargetAt(int index)
    {
        if (targets == null || index < 0 || index >= targets.Length)
            return null;
        return targets[index];
    }
}