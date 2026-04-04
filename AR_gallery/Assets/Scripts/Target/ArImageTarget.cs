using UnityEngine;

/// <summary>
/// 挂在每个 AR Image Target 根物体上：为侧栏下拉提供显示名，为数据库/手机端提供稳定 TargetId。
/// 未挂此组件时，<see cref="TargetSelectionManager"/> 会退回使用 GameObject.name。
/// </summary>
public class ArImageTarget : MonoBehaviour
{
    [Tooltip("写入数据库的稳定 ID；留空则用 Hierarchy 物体名")]
    [SerializeField] private string targetId;

    [Tooltip("下拉菜单显示文字；留空则用 Target Id")]
    [SerializeField] private string displayLabel;

    public string TargetId
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(targetId))
                return targetId.Trim();
            return gameObject.name;
        }
    }

    public string DisplayLabel
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(displayLabel))
                return displayLabel.Trim();
            return TargetId;
        }
    }
}
