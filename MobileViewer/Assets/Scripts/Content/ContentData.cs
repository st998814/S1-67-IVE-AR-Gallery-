using UnityEngine;

namespace MobileViewer.Content
{
    [System.Serializable]
    public class ContentData
    {
        public string targetName;
        public string title;
        public string description;
        public string contentType;
        public string mediaUrl;
        public Vector3 localPosition = new(0f, 0.05f, 0f);
        public Vector3 localEuler = Vector3.zero;
        public Vector3 localScale = Vector3.one * 0.3f;
        public Color mockColor = Color.white;
        public string displayLabel;
    }
}
