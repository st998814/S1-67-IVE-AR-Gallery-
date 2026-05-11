using UnityEngine;

namespace FrostweepGames.Plugins.WebGLFileBrowser
{
    //[CreateAssetMenu(fileName = "GeneralConfig", menuName = "FrostweepGames/MicrophonePro/GeneralConfig", order = 3)]
    public class GeneralConfig : ScriptableObject
    {
        private static GeneralConfig _Config;
        public static GeneralConfig Config
        {
            get
            {
                if (_Config == null)
                    _Config = GetConfig();
                return _Config;
            }
        }

        public bool showWelcomeDialogAtStartup = true;

        [Tooltip("Auto modify index.html temlate (useful for default templates)")]
        public bool usePostProcess = false;

        public bool saveToPersistentDataPathOnPlatformsExceptWebGL = true;

        private static GeneralConfig GetConfig()
        {
            string path = "WebGLFileBrowser/GeneralConfig";
            var config = Resources.Load<GeneralConfig>(path);

            if(config == null)
            {
                Debug.LogError($"WebGL File Browser General Config not found in {path} Resources folder. Will use default.");

                config = (GeneralConfig)CreateInstance("GeneralConfig");

#if UNITY_EDITOR
                string pathToFolder = "Assets/Plugins/FrostweepGames/WebGLFileBrowser/Resources/WebGLFileBrowser";
                string filename = "GeneralConfig.asset";

                if (!System.IO.Directory.Exists(Application.dataPath + "/../" + pathToFolder))
                {
                    System.IO.Directory.CreateDirectory(pathToFolder);
                    UnityEditor.AssetDatabase.ImportAsset(pathToFolder);
                }

                if (!System.IO.File.Exists(Application.dataPath + "/../" + pathToFolder + "/" + filename))
                {
                    UnityEditor.AssetDatabase.CreateAsset(config, pathToFolder + "/" + filename);
                }
                UnityEditor.AssetDatabase.SaveAssets();
#endif
            }

            return config;
        }
    }
}
