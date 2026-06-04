using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 判断屏幕坐标是否点在 UIDocument 面板上（用于与场景点击选中区分）。
/// </summary>
public static class AuthoringUiPickHelper
{
    public static bool IsOverUiDocument(UIDocument doc, Vector2 screenPosition)
    {
        if (doc == null)
            return false;

        VisualElement root = doc.rootVisualElement;
        if (root == null || root.panel == null)
            return false;

        Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(root.panel, screenPosition);
        VisualElement picked = root.panel.Pick(panelPos);
        if (picked == null)
            return false;

        return IsBlockingPick(picked, root);
    }

    private static bool IsBlockingPick(VisualElement picked, VisualElement root)
    {
        for (VisualElement element = picked; element != null; element = element.parent)
        {
            if (element.resolvedStyle.display == DisplayStyle.None
                || element.resolvedStyle.visibility == Visibility.Hidden)
                continue;

            if (element.pickingMode == PickingMode.Position)
                return true;

            if (element == root)
                break;
        }

        return false;
    }
}
