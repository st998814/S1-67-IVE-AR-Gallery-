using UnityEngine;

/// <summary>
/// Factory that creates runtime content instances from prefabs.
/// </summary>
public class RuntimeContentFactory
{
    public struct ContentCreateResult
    {
        public bool success;
        public string message;
        public GameObject instance;
        public DraggableObject draggable;
    }

    public ContentCreateResult CreateImageContent(GameObject picturePrefab)
    {
        return CreateFromPrefab(picturePrefab, "Picture prefab is not assigned.");
    }

    public ContentCreateResult CreateTextContent(GameObject textPrefab, string textToDisplay)
    {
        ContentCreateResult result = CreateFromPrefab(textPrefab, "Text prefab is not assigned.");
        if (!result.success || result.instance == null)
            return result;

        string text = textToDisplay ?? "";
        TextMesh textMesh = result.instance.GetComponent<TextMesh>();
        if (textMesh != null)
            textMesh.text = text;

        TMPro.TextMeshPro tmp = result.instance.GetComponent<TMPro.TextMeshPro>();
        if (tmp != null)
            tmp.text = text;

        return result;
    }

    private static ContentCreateResult CreateFromPrefab(GameObject prefab, string nullMessage)
    {
        if (prefab == null)
        {
            return new ContentCreateResult
            {
                success = false,
                message = nullMessage
            };
        }

        GameObject instance = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
        return new ContentCreateResult
        {
            success = true,
            message = "Content instance created.",
            instance = instance,
            draggable = instance.GetComponent<DraggableObject>()
        };
    }
}
