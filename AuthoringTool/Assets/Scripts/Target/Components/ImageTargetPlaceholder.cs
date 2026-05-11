using UnityEngine;

public class ImageTargetPlaceholder : MonoBehaviour
{
    [SerializeField] private string targetId;

    public string TargetId => targetId;

    public void SetTargetId(string value)
    {
        targetId = string.IsNullOrWhiteSpace(value) ? "" : value.Trim();
    }
}
