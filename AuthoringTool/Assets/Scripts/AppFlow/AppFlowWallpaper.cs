using UnityEngine;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ARGallery.AppFlow
{
    /// <summary>
    /// Applies the shared app-flow background wallpaper to a UI Toolkit root.
    /// </summary>
    internal static class AppFlowWallpaper
    {
        private const string ResourcePath = "AppFlowWallpaper";
#if UNITY_EDITOR
        private const string AssetPath = "Assets/Resources/AppFlowWallpaper.jpg";
#endif
        private static Texture2D cachedTexture;

        public static void Apply(VisualElement root)
        {
            if (root == null)
                return;

            root.AddToClassList("app-flow-wallpaper");

            Texture2D texture = GetTexture();
            if (texture == null)
            {
                Debug.LogWarning("AppFlowWallpaper: texture was not found.");
                return;
            }

            root.style.backgroundImage = Background.FromTexture2D(texture);
            root.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
            root.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
            root.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
            root.style.backgroundRepeat = new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat);
        }

        private static Texture2D GetTexture()
        {
            if (cachedTexture != null)
                return cachedTexture;

            cachedTexture = Resources.Load<Texture2D>(ResourcePath);
#if UNITY_EDITOR
            if (cachedTexture == null)
                cachedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetPath);
#endif
            return cachedTexture;
        }
    }
}
