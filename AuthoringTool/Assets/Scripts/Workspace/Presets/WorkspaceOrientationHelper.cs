using UnityEngine;

namespace ARGallery.Workspace.Presets
{
    /// <summary>
    /// Lightweight axis helper that can be toggled for authoring orientation.
    /// </summary>
    public static class WorkspaceOrientationHelper
    {
        private const string HelperRootName = "OrientationHelper";

        public static void Apply(Transform targetRoot, bool isEnabled, float axisLength, float axisThickness)
        {
            if (targetRoot == null)
                return;

            Transform helperRoot = targetRoot.Find(HelperRootName);
            if (!isEnabled)
            {
                if (helperRoot != null)
                    helperRoot.gameObject.SetActive(false);
                return;
            }

            if (helperRoot == null)
                helperRoot = CreateHelperRoot(targetRoot, axisLength, axisThickness);

            helperRoot.gameObject.SetActive(true);
        }

        private static Transform CreateHelperRoot(Transform targetRoot, float axisLength, float axisThickness)
        {
            GameObject helperRoot = new GameObject(HelperRootName);
            helperRoot.transform.SetParent(targetRoot, false);
            helperRoot.transform.localPosition = Vector3.zero;
            helperRoot.transform.localRotation = Quaternion.identity;
            helperRoot.transform.localScale = Vector3.one;

            CreateAxis(helperRoot.transform, "AxisX", new Vector3(axisLength * 0.5f, 0f, 0f), new Vector3(axisLength, axisThickness, axisThickness), new Color(1f, 0.35f, 0.35f, 0.55f));
            CreateAxis(helperRoot.transform, "AxisY", new Vector3(0f, axisLength * 0.5f, 0f), new Vector3(axisThickness, axisLength, axisThickness), new Color(0.35f, 1f, 0.35f, 0.55f));
            CreateAxis(helperRoot.transform, "AxisZ", new Vector3(0f, 0f, axisLength * 0.5f), new Vector3(axisThickness, axisThickness, axisLength), new Color(0.35f, 0.6f, 1f, 0.55f));

            return helperRoot.transform;
        }

        private static void CreateAxis(Transform parent, string axisName, Vector3 localPosition, Vector3 localScale, Color color)
        {
            GameObject axis = GameObject.CreatePrimitive(PrimitiveType.Cube);
            axis.name = axisName;
            axis.transform.SetParent(parent, false);
            axis.transform.localPosition = localPosition;
            axis.transform.localRotation = Quaternion.identity;
            axis.transform.localScale = localScale;

            Collider collider = axis.GetComponent<Collider>();
            if (collider != null)
                Object.Destroy(collider);

            Renderer renderer = axis.GetComponent<Renderer>();
            if (renderer != null)
            {
                Shader shader = Shader.Find("Unlit/Color");
                if (shader == null)
                    shader = Shader.Find("Standard");
                if (shader != null)
                {
                    Material mat = new Material(shader);
                    if (mat.HasProperty("_Color"))
                        mat.color = color;
                    renderer.sharedMaterial = mat;
                }
            }
        }
    }
}
