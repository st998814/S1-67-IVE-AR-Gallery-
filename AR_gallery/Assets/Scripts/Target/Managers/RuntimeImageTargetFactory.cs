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
    [SerializeField] private bool makeTargetVisualDraggable = true;
    [SerializeField] private Color targetBackgroundColor = Color.white;
    [SerializeField] private Color targetTextColor = Color.black;
    [SerializeField] private int targetLabelFontSize = 64;
    [SerializeField] private float targetLabelCharacterSize = 0.03f;

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

        CreateImageTargetPlaceholder(targetRoot.transform, arTarget.TargetId);
        CreateTargetVisual(targetRoot.transform, safeName);
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
    private void CreateTargetVisual(Transform parent, string targetName)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "TargetVisual";
        go.transform.SetParent(parent, false);
        go.transform.localPosition = targetVisualLocalPosition;
        go.transform.localRotation = Quaternion.Euler(targetVisualLocalEuler);
        go.transform.localScale = targetVisualLocalScale;

        if (makeTargetVisualDraggable && go.GetComponent<DraggableObject>() == null)
        {
            DraggableObject draggable = go.AddComponent<DraggableObject>();
            draggable.ConfigureConstraints(shouldLockLocalZ: true, shouldAllowScrollScale: true); // the target  be tagged onto the surface 
            draggable.ConfigureDragBinding(shouldMoveParentOnDrag: true);
        }

        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer == null)
            return;

        Material visualMaterial = CreateBackgroundMaterial();
        if (visualMaterial != null)
            renderer.sharedMaterial = visualMaterial;

        CreateTargetLabel(go.transform, targetName);
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

    private Material CreateBackgroundMaterial()
    {
        Shader shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            return null;

        Material mat = new Material(shader);
        if (mat.HasProperty("_Color"))
            mat.color = targetBackgroundColor;
        return mat;
    }

    private void CreateTargetLabel(Transform targetVisual, string targetName)
    {
        GameObject label = new GameObject("TargetLabel");
        label.transform.SetParent(targetVisual, false);
        label.transform.localPosition = new Vector3(0f, 0f, 0.001f);
        label.transform.localRotation = Quaternion.identity;
        label.transform.localScale = Vector3.one;

        TextMesh textMesh = label.AddComponent<TextMesh>();
        textMesh.text = string.IsNullOrWhiteSpace(targetName) ? "Target" : targetName;
        textMesh.color = targetTextColor;
        textMesh.alignment = TextAlignment.Center;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.fontSize = targetLabelFontSize;
        textMesh.characterSize = targetLabelCharacterSize;
        textMesh.richText = false;

        Font runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (runtimeFont == null)
            runtimeFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (runtimeFont != null)
        {
            textMesh.font = runtimeFont;
            MeshRenderer renderer = label.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.sharedMaterial = runtimeFont.material;
        }
    }
}
