using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class ContentTransformController : MonoBehaviour
{
    [SerializeField] private TargetSelectionManager targetSelectionManager;

    [SerializeField] private float moveStep = 0.1f;
    [SerializeField] private float rotateStep = 10f;
    [SerializeField] private float scaleStep = 0.1f;

    private readonly List<Transform> contentObjects = new List<Transform>();
    private int selectedIndex = -1;

    private void Update()
    {
        RefreshContentList();

        if (Keyboard.current.rKey.wasPressedThisFrame)
        {
            SelectNextContent();
        }

        Transform selected = GetSelectedContent();
        if (selected == null) return;

        HandlePositionInput(selected);
        HandleRotationInput(selected);
        HandleScaleInput(selected);
    }
private void RefreshContentList()
{
    contentObjects.Clear();

    if (targetSelectionManager == null)
    {
        Debug.LogWarning("ContentTransformController: targetSelectionManager is null.");
        return;
    }

    GameObject activeTarget = targetSelectionManager.GetActiveTarget();
    if (activeTarget == null)
    {
        Debug.LogWarning("ContentTransformController: activeTarget is null.");
        return;
    }

    Transform contentRoot = activeTarget.transform.Find("ContentRoot");
    if (contentRoot == null)
    {
        Debug.LogWarning($"ContentTransformController: ContentRoot not found under {activeTarget.name}");
        return;
    }

    foreach (Transform child in contentRoot)
    {
        contentObjects.Add(child);
    }

    // Debug.Log($"Active target: {activeTarget.name}, content count: {contentObjects.Count}");

    if (contentObjects.Count == 0)
    {
        selectedIndex = -1;
    }
    else if (selectedIndex < 0 || selectedIndex >= contentObjects.Count)
    {
        selectedIndex = 0;
    }
}


    private void SelectNextContent()
    {
        if (contentObjects.Count == 0)
        {
            selectedIndex = -1;
            Debug.Log("No content objects available.");
            return;
        }

        selectedIndex++;
        if (selectedIndex >= contentObjects.Count)
        {
            selectedIndex = 0;
        }
        UpdateSelectionVisual();

        Debug.Log($"Selected: {contentObjects[selectedIndex].name}");
    }

    private Transform GetSelectedContent()
    {
        if (selectedIndex < 0 || selectedIndex >= contentObjects.Count)
            return null;
        

        return contentObjects[selectedIndex];
    }

    private void UpdateSelectionVisual()
{
    for (int i = 0; i < contentObjects.Count; i++)
    {
        Renderer renderer = contentObjects[i].GetComponent<Renderer>();
        if (renderer == null) continue;

        if (i == selectedIndex)
        {
            renderer.material.color = Color.yellow;
        }
        else
        {
            renderer.material.color = Color.red;
        }
    }
}

    private void HandlePositionInput(Transform target)
    {
        Vector3 pos = target.localPosition;

        if (Keyboard.current.aKey.isPressed) pos.x -= moveStep * Time.deltaTime * 10f;
        if (Keyboard.current.dKey.isPressed) pos.x += moveStep * Time.deltaTime * 10f;
        if (Keyboard.current.wKey.isPressed) pos.y += moveStep * Time.deltaTime * 10f;
        if (Keyboard.current.sKey.isPressed) pos.y -= moveStep * Time.deltaTime * 10f;
        if (Keyboard.current.qKey.isPressed) pos.z -= moveStep * Time.deltaTime * 10f;
        if (Keyboard.current.eKey.isPressed) pos.z += moveStep * Time.deltaTime * 10f;

        target.localPosition = pos;
    }

    private void HandleRotationInput(Transform target)
    {
        Vector3 rot = target.localEulerAngles;

        if (Keyboard.current.zKey.isPressed) rot.y -= rotateStep * Time.deltaTime * 10f;
        if (Keyboard.current.xKey.isPressed) rot.y += rotateStep * Time.deltaTime * 10f;

        target.localEulerAngles = rot;
    }

    private void HandleScaleInput(Transform target)
    {
        Vector3 scale = target.localScale;

        if (Keyboard.current.cKey.isPressed) scale += Vector3.one * scaleStep * Time.deltaTime * 10f;
        if (Keyboard.current.vKey.isPressed) scale -= Vector3.one * scaleStep * Time.deltaTime * 10f;

        scale.x = Mathf.Max(0.1f, scale.x);
        scale.y = Mathf.Max(0.1f, scale.y);
        scale.z = Mathf.Max(0.1f, scale.z);

        target.localScale = scale;
    }
}