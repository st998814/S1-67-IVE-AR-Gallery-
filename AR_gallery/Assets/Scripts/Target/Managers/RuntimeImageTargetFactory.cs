using UnityEngine;

[DisallowMultipleComponent]
/// <summary>
/// Creates AR image-target hierarchy objects at runtime under a shared root.
/// </summary>
public class RuntimeImageTargetFactory : MonoBehaviour
{
    [Header("Hierarchy")]
    [SerializeField] private Transform imageTargetRoot;
    [SerializeField] private string imageTargetRootName = "ImageTargetRoot";

    [Header("Target Visual Defaults")]
    [SerializeField] private Vector3 targetVisualLocalPosition = Vector3.zero;
    [SerializeField] private Vector3 targetVisualLocalEuler = Vector3.zero;
    [SerializeField] private Vector3 targetVisualLocalScale = Vector3.one;
    [SerializeField] private Material targetVisualMaterial;

    /// <summary>
    /// Creates a new target root with required children:
    /// ImageTargetPlaceholder, TargetVisual and ContentRoot.
    /// </summary>
    public GameObject CreateTarget(string targetName, string targetId, string displayLabel = null)
    {
        string safeName = string.IsNullOrWhiteSpace(targetName) ? "NewTarget" : targetName.Trim();
        string safeId = string.IsNullOrWhiteSpace(targetId) ? safeName : targetId.Trim();

        Transform root = EnsureImageTargetRoot();

        GameObject targetRoot = new GameObject(safeName + "_Target");
        targetRoot.transform.SetParent(root, false);

        ArImageTarget arTarget = targetRoot.AddComponent<ArImageTarget>();
        arTarget.Configure(safeId, displayLabel);

        CreateImageTargetPlaceholder(targetRoot.transform, safeId);
        CreateTargetVisual(targetRoot.transform);
        CreateContentRoot(targetRoot.transform);

        return targetRoot;
    }

    /// <summary>
    /// Resolves the target root transform, creating it if needed.
    /// </summary>
    private Transform EnsureImageTargetRoot()
    {
        if (imageTargetRoot != null)
            return imageTargetRoot;

        GameObject found = GameObject.Find(imageTargetRootName);
        if (found == null)
            found = new GameObject(imageTargetRootName);

        imageTargetRoot = found.transform;
        return imageTargetRoot;
    }

    /// <summary>
    /// Adds a placeholder child that stores the marker target id.
    /// </summary>
    private static void CreateImageTargetPlaceholder(Transform parent, string targetId)
    {
        GameObject go = new GameObject("ImageTargetPlaceholder");
        go.transform.SetParent(parent, false);
        ImageTargetPlaceholder placeholder = go.AddComponent<ImageTargetPlaceholder>();
        placeholder.SetTargetId(targetId);
    }

    /// <summary>
    /// Adds the target visual surface used as frame/alignment reference.
    /// </summary>
    private void CreateTargetVisual(Transform parent)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "TargetVisual";
        go.transform.SetParent(parent, false);
        go.transform.localPosition = targetVisualLocalPosition;
        go.transform.localRotation = Quaternion.Euler(targetVisualLocalEuler);
        go.transform.localScale = targetVisualLocalScale;

        Collider collider = go.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        if (targetVisualMaterial != null)
        {
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = targetVisualMaterial;
        }
    }

    /// <summary>
    /// Adds an empty content parent where authored objects are spawned.
    /// </summary>
    private static void CreateContentRoot(Transform parent)
    {
        GameObject go = new GameObject("ContentRoot");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
    }
}
