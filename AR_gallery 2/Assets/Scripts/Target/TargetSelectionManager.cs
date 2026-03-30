using UnityEngine;
using UnityEngine.InputSystem;
public class TargetSelectionManager : MonoBehaviour
{
    [SerializeField] private GameObject[] targets;
    [SerializeField] private int activeTargetIndex = 0;

    private void Start()
    {
        ShowOnlyActiveTarget();
    }

    private void Update()
{
    if (Keyboard.current.digit1Key.wasPressedThisFrame)
    {
        SetActiveTarget(0);
    }

    if (Keyboard.current.digit2Key.wasPressedThisFrame)
    {
        SetActiveTarget(1);
    }
}
    public void SetActiveTarget(int index)
    {
        if (index < 0 || index >= targets.Length)
            return;

        activeTargetIndex = index;
        ShowOnlyActiveTarget();
    }

    private void ShowOnlyActiveTarget()
    {
        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] != null)
            {
                targets[i].SetActive(i == activeTargetIndex);
            }
        }
    }
    public GameObject GetActiveTarget()
{
    if (activeTargetIndex < 0 || activeTargetIndex >= targets.Length)
        return null;

    return targets[activeTargetIndex];
}
}