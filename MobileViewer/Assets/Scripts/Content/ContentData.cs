using UnityEngine;

namespace MobileViewer.Content
{
    /// <summary>
    /// Runtime metadata for AR content attached to a detected target.
    /// This class is populated by content services and consumed by the renderer.
    /// </summary>
    [System.Serializable]
    public class ContentData
    {
        public string targetName;
        public string title;
        public string description;
        public string contentType;
        public string mediaUrl;
        public float targetPhysicalWidthM = 1f;
        public Vector3 localPosition = new(0f, 0.05f, 0f);
        public Vector3 localEuler = Vector3.zero;
        public Vector3 localScale = Vector3.one * 0.3f;
        public Vector3 targetLocalEuler = Vector3.zero;
        public string targetPosture = "wall";
        public Color mockColor = Color.white;
        public string displayLabel;
    }
}
