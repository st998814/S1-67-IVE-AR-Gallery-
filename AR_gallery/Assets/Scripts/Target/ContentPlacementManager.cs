using UnityEngine;
using UnityEngine.InputSystem;
public class ContentPlacementManager : MonoBehaviour
{
    [SerializeField] private TargetSelectionManager targetSelectionManager;

    private int contentCounter = 1;


    private void Update()
{
    if (Keyboard.current.nKey.wasPressedThisFrame)
    {
        AddContentToActiveTarget();
    }
}

 
    public void AddContentToActiveTarget()
    {
        if (targetSelectionManager == null)
        {
            Debug.LogWarning("TargetSelectionManager is not assigned.");
            return;
        }

        GameObject activeTarget = targetSelectionManager.GetActiveTarget();

        if (activeTarget == null)
        {
            Debug.LogWarning("No active target found.");
            return;
        }

        Transform contentRoot = activeTarget.transform.Find("ContentRoot");

        if (contentRoot == null)
        {
            Debug.LogWarning($"ContentRoot not found under {activeTarget.name}");
            return;
        }

        int existingCount = contentRoot.childCount;

        GameObject contentObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        contentObject.name = $"Content_{contentCounter++}";
        contentObject.transform.SetParent(contentRoot, false);

        float offsetX = existingCount * 0.5f;
        contentObject.transform.localPosition = new Vector3(offsetX, 0.8f, -0.6f);
        contentObject.transform.localRotation = Quaternion.identity;
        contentObject.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);

        Renderer renderer = contentObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = Color.red;
        }

        Debug.Log($"Added {contentObject.name} to {activeTarget.name}");
    }
}