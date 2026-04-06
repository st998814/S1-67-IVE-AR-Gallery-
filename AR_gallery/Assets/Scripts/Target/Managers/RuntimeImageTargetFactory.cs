using UnityEngine;

[DisallowMultipleComponent]
/// <summary>
/// Create AR Image Targets at runtime.
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

    public GameObject CreateTarget(string targetName, string targetId, string displayLabel = null)
        /// <summary>
        /// Create a new AR Image Target.
        /// </summary>
        /// <param name="targetName">The name of the target.</param>
        /// <param name="targetId">The ID of the target.</param>
        /// <param name="displayLabel">The label of the target.</param>
        /// <returns>The created target.</returns>
    {   
        /// <summary>
        /// Ensure the target name and ID are valid.
        /// </summary>
        string safeName = string.IsNullOrWhiteSpace(targetName) ? "NewTarget" : targetName.Trim();
        string safeId = string.IsNullOrWhiteSpace(targetId) ? safeName : targetId.Trim();
        Debug.Log("CreateTarget: " + safeName + " " + safeId + " " + displayLabel);

        Transform root = EnsureImageTargetRoot(); /// get root from hierarchy or create a new one if not found

        GameObject targetRoot = new GameObject(safeName + "_Target"); /// create a new target root for dedicated image target
        targetRoot.transform.SetParent(root, false); /// set parent to the "ROOT"

        ArImageTarget arTarget = targetRoot.AddComponent<ArImageTarget>();
        arTarget.Configure(safeId, displayLabel);
        /// create "ImageTargetPlaceholder" , "TargetVisual" , "ContentRoot" under the target root
        CreateImageTargetPlaceholder(targetRoot.transform, safeId);
        CreateTargetVisual(targetRoot.transform);
        CreateContentRoot(targetRoot.transform);

        return targetRoot;
    }

    private Transform EnsureImageTargetRoot()
    {
        /// <summary>
        /// Ensure the image target root is valid , if not found, create a new one.
        /// </summary>
        /// <returns>The image target root.</returns>
        if (imageTargetRoot != null)
            return imageTargetRoot;

        GameObject found = GameObject.Find(imageTargetRootName);

        if (found == null)
        {
            found = new GameObject(imageTargetRootName);
        }

        imageTargetRoot = found.transform;
        return imageTargetRoot;
    }
    private static void CreateImageTargetPlaceholder(Transform parent, string targetId)
    {
        GameObject go = new GameObject("ImageTargetPlaceholder");
        go.transform.SetParent(parent, false);
        ImageTargetPlaceholder placeholder = go.AddComponent<ImageTargetPlaceholder>();
        placeholder.SetTargetId(targetId);
    }

    private void CreateTargetVisual(Transform parent)
    {   /// <summary>
        /// Create a target visual for the target.
        /// </summary>
        /// <param name="parent">The parent of the target visual.</param>
    {   /// <summary>
        /// Create a quad for the target visual.
        /// </summary>
        /// <param name="parent">The parent of the target visual.</param>
        /// <returns>The target visual.</returns>
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad); /// a 2D surface for the target visual
        go.name = $"TargetVisual_{parent.name}";
        go.transform.SetParent(parent, false);
        go.transform.localPosition = targetVisualLocalPosition;
        go.transform.localRotation = Quaternion.Euler(targetVisualLocalEuler);
        go.transform.localScale = targetVisualLocalScale;

        Collider collider = go.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider); /// remove the collider to avoid raycast issues

        if (targetVisualMaterial != null) /// set the material to the target visual
        {
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = targetVisualMaterial;
        }
    }

    private static void CreateContentRoot(Transform parent)
    {
        GameObject go = new GameObject("ContentRoot");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
    }
}
