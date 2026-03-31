using System;
using System.Collections.Generic;
using System.Linq;
#if (UNITY_WEBGL || FG_FB_WEBGL) && !UNITY_EDITOR
using System.Linq;
#endif
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
#if (UNITY_WEBGL || FG_FB_WEBGL) && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace FrostweepGames.Plugins.WebGLFileBrowser
{
    public class WebGLFileBrowser : MonoBehaviour
    {
        private const string _InstanceName = "[FGFileBrowser]";

        /// <summary>
        /// Will fire when file will successfully be loaded
        /// </summary>
        public static event Action<File[]> FilesWereOpenedEvent;
        /// <summary>
        /// Will fire when native file loading popup was closed
        /// </summary>
        public static event Action FilePopupWasClosedEvent;
        /// <summary>
        /// Will fire when error received during file loading
        /// </summary>
        public static event Action<string> FileOpenFailedEvent;
        /// <summary>
        /// Will fire when error received during folder opening loading
        /// </summary>
        public static event Action<string> FolderOpenFailedEvent;
        /// <summary>
        /// Will fire when file was successfully saved 
        /// </summary>
        public static event Action<File> FileWasSavedEvent;
        /// <summary>
        /// Will fire when error received during file saving
        /// </summary>
        public static event Action<string> FileSaveFailedEvent;

        private static WebGLFileBrowser _Instance;

#if (UNITY_WEBGL || FG_FB_WEBGL) && !UNITY_EDITOR
        private static bool _FileBrowserActive;
        private static bool _IsWasInFullScreen;

        [DllImport("__Internal")]
        private static extern void initialize();

        [DllImport("__Internal")]
        private static extern void openFileBrowserForLoad(string typesFilter, int isMultipleSelection, int isFolder);

        [DllImport("__Internal")]
        private static extern void closeFileBrowserForOpen();

        [DllImport("__Internal")]
        private static extern void saveFile(string fileName, string data);

        [DllImport("__Internal")]
        private static extern void setLocalization(string key, string value);     

        [DllImport("__Internal")]
        private static extern void cleanup();

        [DllImport("__Internal")]
        private static extern IntPtr loadFileData(string fileName);
#endif
        private static List<UnityEngine.Object> _unityObjects = new List<UnityEngine.Object>();
        private static List<File> _files = new List<File>();
        private static Dictionary<string, File> _savingFilesCache = new Dictionary<string, File>();
        private static int _filesToBeLoaded;
        private static int _filesWereLoaded;

        private void Awake() // could be potencial issue with static constructors. set loading of this script with better execution order in project settings
        {
            if (_Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            _Instance = this;
            DontDestroyOnLoad(gameObject);

            gameObject.name = _InstanceName;

#if (UNITY_WEBGL || FG_FB_WEBGL) && !UNITY_EDITOR
            initialize();
#endif
        }

        private void OnDestroy()
		{
            if (_Instance != this)
                return;
            _Instance = null;
		}

		/// <summary>
		/// Opens Native File Browser Dialog for file(s) selection
		/// </summary>
		public static void OpenFilePanelWithFilters(string typesFilter, bool isMultipleSelection = false)
        {
            if (_Instance == null)
                throw new Exception($"Failed to OpenFilePanelWithFilters. Missing {_InstanceName} prefab in scene.");

            _filesWereLoaded = 0;
            _filesToBeLoaded = 0;

#if UNITY_EDITOR

            if(isMultipleSelection)
            {
                if (FileOpenFailedEvent != null)
                    FileOpenFailedEvent("Open multiple files failed. In Editor multiple files selection is only possible via OpenFolderPanelWithFilters.");

                return;
            }

            string path = UnityEditor.EditorUtility.OpenFilePanelWithFilters("Editor File Browser", System.IO.Directory.GetLogicalDrives()[0], new string[] { "User Files", typesFilter.Replace(".", string.Empty) });

            if (path.Length != 0)
            {
                byte[] fileContent = System.IO.File.ReadAllBytes(path);

                File file = new File()
                {
                    fileInfo = new FileInfo()
                    {
                        extension = System.IO.Path.GetExtension(path),
                        name = System.IO.Path.GetFileNameWithoutExtension(path),
                        fullName = System.IO.Path.GetFileName(path),
                        length = fileContent.Length,
                        path = path,
                        size = fileContent.Length
                    },
                    data = fileContent
                };

                _files.Add(file);

                if (FilesWereOpenedEvent != null)
                    FilesWereOpenedEvent(new File[] { file });
            }
			else
			{
                if (FileOpenFailedEvent != null)
                    FileOpenFailedEvent("Open file failed.");
            }
#else

#if (UNITY_WEBGL || FG_FB_WEBGL) && !UNITY_EDITOR
            if (_FileBrowserActive)
                return;

            if (Screen.fullScreen)
            {
                Screen.fullScreen = false;
                _IsWasInFullScreen = true;
            }
            else _IsWasInFullScreen = false;

            _FileBrowserActive = true;
#endif

#if (UNITY_WEBGL || FG_FB_WEBGL) && !UNITY_EDITOR
            openFileBrowserForLoad(typesFilter, isMultipleSelection ? 1 : 0, 0);
#endif

#endif
        }

        /// <summary>
        /// Opens Native File Browser Dialog for folder selection. Supported only in Desktop browsers.
        /// </summary>
        public static void OpenFolderPanelWithFilters(string typesFilter)
        {
            if (_Instance == null)
                throw new Exception($"Failed to OpenFolderPanelWithFilters. Missing {_InstanceName} prefab in scene.");

            _filesWereLoaded = 0;
            _filesToBeLoaded = 0;

#if UNITY_EDITOR
            string path = UnityEditor.EditorUtility.OpenFolderPanel("Editor Folder Browser", System.IO.Directory.GetLogicalDrives()[0], string.Empty);

            if (path.Length != 0)
            {
                var files = System.IO.Directory.GetFiles(path);

                File[] loadedFiles = new File[files.Where(item => !item.EndsWith(".DS_Store")).Count()];
                int pointer = 0;

                foreach (var filePath in files)
                {
                    // ignore Mac hidden file
                    if (filePath.EndsWith(".DS_Store"))
                        continue;

                    byte[] fileContent = System.IO.File.ReadAllBytes(filePath);

                    File file = new File()
                    {
                        fileInfo = new FileInfo()
                        {
                            extension = System.IO.Path.GetExtension(filePath),
                            name = System.IO.Path.GetFileNameWithoutExtension(filePath),
                            fullName = System.IO.Path.GetFileName(filePath),
                            length = fileContent.Length,
                            path = path,
                            size = fileContent.Length
                        },
                        data = fileContent
                    };

                    loadedFiles[pointer++] = file;
                    _files.Add(file);
                }

                if (FilesWereOpenedEvent != null)
                    FilesWereOpenedEvent(loadedFiles);
            }
            else
            {
                if (FolderOpenFailedEvent != null)
                    FolderOpenFailedEvent("Open folder failed.");
            }
#else

#if (UNITY_WEBGL || FG_FB_WEBGL) && !UNITY_EDITOR
            if (_FileBrowserActive)
                return;

            if (Screen.fullScreen)
            {
                Screen.fullScreen = false;
                _IsWasInFullScreen = true;
            }
            else _IsWasInFullScreen = false;

            _FileBrowserActive = true;
#endif

#if (UNITY_WEBGL || FG_FB_WEBGL) && !UNITY_EDITOR
            openFileBrowserForLoad(typesFilter, 0, 1);
#endif

#endif
        }

        /// <summary>
        /// Hides Native File Browser Dialog
        /// </summary>
        public static void HideFileDialog()
        {
            if (_Instance == null)
                throw new Exception($"Failed to HideFileDialog. Missing {_InstanceName} prefab in scene.");

#if (UNITY_WEBGL || FG_FB_WEBGL) && !UNITY_EDITOR
            closeFileBrowserForOpen();
#endif
#if (UNITY_WEBGL || FG_FB_WEBGL) && !UNITY_EDITOR
            _FileBrowserActive = false;
            if (_IsWasInFullScreen)
                Screen.fullScreen = true;
#endif
        }

        /// <summary>
        /// Saves file
        /// </summary>
        /// <param name="file"></param>
        public static void SaveFile(File file)
        {
            if (_Instance == null)
                throw new Exception($"Failed to SaveFile. Missing {_InstanceName} prefab in scene.");

            if (file == null)
			{
                if (FileSaveFailedEvent != null)
                    FileSaveFailedEvent("Save file failed due to: file is null.");
                return;
			}

#if UNITY_EDITOR
            string path = UnityEditor.EditorUtility.SaveFilePanel("Editor File Browser", System.IO.Directory.GetLogicalDrives()[0], file.fileInfo.name, file.fileInfo.extension.Replace(".", string.Empty));

            if (path.Length != 0)
            {
                System.IO.File.WriteAllBytes(path, file.data);

                if (FileWasSavedEvent != null)
                    FileWasSavedEvent.Invoke(file);
            }
            else
            {
                if (FileSaveFailedEvent != null)
                    FileSaveFailedEvent("Save file failed.");
            }
#else

#if (UNITY_WEBGL || FG_FB_WEBGL) && !UNITY_EDITOR
            if (!_savingFilesCache.ContainsKey(file.fileInfo.fullName))
                _savingFilesCache.Add(file.fileInfo.fullName, file);

            saveFile(file.fileInfo.fullName, Convert.ToBase64String(file.data));
#else
            if (GeneralConfig.Config.saveToPersistentDataPathOnPlatformsExceptWebGL)
            {
                if (string.IsNullOrEmpty(file.fileInfo.path))
                {
                    if (string.IsNullOrEmpty(file.fileInfo.fullName))
                        file.fileInfo.fullName = $"File-{DateTime.Now.Day}-{DateTime.Now.Month}-{DateTime.Now.Year}-{DateTime.Now.Hour}-{DateTime.Now.Minute}-{DateTime.Now.Second}.bytes";
                    file.fileInfo.path = $"{Application.persistentDataPath}/{file.fileInfo.fullName}";
                }

                System.IO.File.WriteAllBytes(file.fileInfo.path, file.data);
            }
            else
                throw new Exception($"Failed to save file {file.fileInfo.fullName}. Saving to persistent data path is disabled in GeneralConfig.");
#endif

#endif
        }

        /// <summary>
        /// Filters string by extensions and prepare for file browser
        /// </summary>
        /// <param name="extensions"></param>
        /// <returns></returns>
        public static string GetFilteredFileExtensions(string extensions)
        {
            string[] types = new string[] { string.Empty };
            if (!string.IsNullOrEmpty(extensions))
            {
                extensions = extensions.Replace(" ", string.Empty);
                types = extensions.Split(',');
            }

            if (!string.IsNullOrEmpty(types[0]))
            {
                types[0] = types[0].Insert(0, ".");
            }

            return string.Join(",.", types);
        }

        /// <summary>
        /// Set localization of popup components
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public static void SetLocalization(LocalizationKey key, string value)
        {
            if (_Instance == null)
                throw new Exception($"Failed to SetLocalization. Missing {_InstanceName} prefab in scene.");

            if (string.IsNullOrEmpty(value))
                return;
#if (UNITY_WEBGL || FG_FB_WEBGL) && !UNITY_EDITOR
            setLocalization(key.ToString(), value);
#endif
        }

        /// <summary>
        /// Will add unity object to cache list
        /// </summary>
        /// <param name="unityObject"></param>
        public static void RegisterFileObject(UnityEngine.Object unityObject)
        {
            if (_Instance == null)
                throw new Exception($"Failed to RegisterFileObject. Missing {_InstanceName} prefab in scene.");

            if (unityObject == null)
                return;

            _unityObjects.Add(unityObject);
        }

        /// <summary>
        /// Will clean all cached unity objects, files and will use GC.Collect with Resources.UnloadUnusedAssets
        /// </summary>
        public static void FreeMemory()
        {
            if (_Instance == null)
                throw new Exception($"Failed to FreeMemory. Missing {_InstanceName} prefab in scene.");

            for (int i = 0; i < _unityObjects.Count; i++)
			{
                if (_unityObjects[i] != null && _unityObjects[i])
                {
                    if (_unityObjects[i] is Sprite sprite)
                    {
                        MonoBehaviour.Destroy(sprite.texture);
                        MonoBehaviour.Destroy(sprite);
                    }
                    else
                    {
                        MonoBehaviour.Destroy(_unityObjects[i]);
                    }
                }
            }

            for (int i = 0; i < _files.Count; i++)
            {
                _files[i].data = null;
                _files[i].fileInfo = null;
            }
            _files.Clear();
            _unityObjects.Clear();
            GC.Collect();
            Resources.UnloadUnusedAssets();
        }

        /// <summary>
        /// Event handler from Native code
        /// </summary>
        private void HandleLoadedFile(string jsonData)
        {
            _filesWereLoaded++;

            FileInfo fileInfo = JsonUtility.FromJson<FileInfo>(jsonData);           

            bool fileLoaded = false;

#if (UNITY_WEBGL || FG_FB_WEBGL) && !UNITY_EDITOR
            var length = fileInfo.length;
            var dataPointer = loadFileData(fileInfo.fullName);
            var data = new byte[length];
            try
            {
                Marshal.Copy(dataPointer, data, 0, data.Length);
                fileLoaded = true;
            }
            catch(Exception ex)
			{
                Debug.LogException(ex);

                if (FileOpenFailedEvent != null)
                    FileOpenFailedEvent(ex.Message);
			}
#endif
            HideFileDialog();

            if (fileLoaded)
            {
                File file = new File()
                {
                    fileInfo = fileInfo,
#if (UNITY_WEBGL || FG_FB_WEBGL) && !UNITY_EDITOR
                    data = data
#endif
                };

#if (UNITY_WEBGL || FG_FB_WEBGL) && !UNITY_EDITOR
                cleanup();
#endif

                _files.Add(file);

                if (_filesWereLoaded >= _filesToBeLoaded)
                {
                    if (FilesWereOpenedEvent != null)
                        FilesWereOpenedEvent(_files.GetRange(_files.Count - _filesToBeLoaded, _filesToBeLoaded).ToArray());
                }
            }
        }

        /// <summary>
        /// Event handler from Native code
        /// </summary>
        private void CloseFileBrowserForOpen()
        {
            HideFileDialog();

            if (FilePopupWasClosedEvent != null)
                FilePopupWasClosedEvent();
        }

        /// <summary>
        /// Event handler from Native code
        /// </summary>
        private void HandleFileSaved(string jsonData)
        {
            FileSaveInfo fileSaveInfo = JsonUtility.FromJson<FileSaveInfo>(jsonData);

            if (fileSaveInfo.status)
            {
                if (FileWasSavedEvent != null)
                {
                    if (_savingFilesCache.ContainsKey(fileSaveInfo.name))
                    {
                        FileWasSavedEvent?.Invoke(_savingFilesCache[fileSaveInfo.name]);
                    }
                    else
                    {
                        Debug.LogWarning($"Saved file {fileSaveInfo.name} not found in cache to be in an event!");
                        FileWasSavedEvent?.Invoke(null);
                    }
                }
            }
            else
            {
                if (FileSaveFailedEvent != null)
                    FileSaveFailedEvent?.Invoke(fileSaveInfo.message);
            }

            if(_savingFilesCache.ContainsKey(fileSaveInfo.name))
                _savingFilesCache.Remove(fileSaveInfo.name);
        }

        /// <summary>
        /// Event handler from Native code
        /// </summary>
        private void SetAmountOfFilesToBeLoaded(int amount)
        {
            _filesToBeLoaded = amount;
        }
    }

    public enum LocalizationKey
	{
        HEADER_TITLE,
        DESCRIPTION_TEXT,
        SELECT_BUTTON_CONTENT,
        CLOSE_BUTTON_CONTENT
    }

    public enum TextureType
    {
        PNG,
        JPEG,
        TGA,
        EXR
    }

    public enum AudioType
    {
        WAV,
    }

    public class FileInfo
    {
        public string fullName;
        public string name;
        public string path;
        public long length;
        public long size;
        public string extension;


        /// <summary>
        /// Returns size of file converted to formates string
        /// </summary>
        /// <returns></returns>
        public string SizeToString()
		{
            string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" }; //Longs run out around EB
            if (size == 0)
                return "0" + suf[0];
            long bytes = Math.Abs(size);
            int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            double num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return (Math.Sign(size) * num).ToString() + suf[place];
        }
    }

    public class FileSaveInfo
	{
        public bool status;
        public string message;
        public string name;
    }

    public class File
	{
        public FileInfo fileInfo;
        public byte[] data;
    }

    public static class FileExtension
	{
        public static readonly string[] ImageTypes = new string[] { "BMP", "TIF", "TGA", "JPG", "PSD", "PNG", "JPEG", "EXR" };
        public static readonly string[] AudioTypes = new string[] { "WAV" };
        public static readonly string[] TextTypes = new string[] { "TXT", "JSON" };

        /// <summary>
        /// Will convert file content to TExture2D
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static Texture2D ToTexture2D(this File file)
        {
            if (file.data == null)
                return null;

            var texture = new Texture2D(2, 2, TextureFormat.ARGB32, false, true);

            texture.name = file.fileInfo.name;
            texture.LoadImage(file.data);

            return texture;
        }

        /// <summary>
        /// Will convert file content to Sprite
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static Sprite ToSprite(this File file)
        {
            var texture = file.ToTexture2D();

            if (texture == null)
                return null;

            var sprite = Sprite.Create(texture,
                                        new Rect(Vector2.zero, new Vector2(texture.width, texture.height)),
                                        Vector2.one / 2f,
                                        100,
                                        1,
                                        SpriteMeshType.FullRect,
                                        Vector4.zero);
            return sprite;
        }

        /// <summary>
        /// Will convert Texture2D to base64 data
        /// </summary>
        /// <param name="texture"></param>
        /// <param name="isJPEG"></param>
        /// <returns></returns>
        public static string TextureToBase64(this Texture2D texture, TextureType textureType = TextureType.PNG)
        {
            if (texture == null)
                return string.Empty;

            switch(textureType)
            {
                case TextureType.PNG:
                    return Convert.ToBase64String(texture.EncodeToPNG());
                case TextureType.JPEG:
                    return Convert.ToBase64String(texture.EncodeToJPG());
                case TextureType.TGA:
                    return Convert.ToBase64String(texture.EncodeToTGA());
                case TextureType.EXR:
                    return Convert.ToBase64String(texture.EncodeToEXR());
            }

            throw new Exception($"textureType {textureType} not implemented.");
        }

        /// <summary>
        /// Will convert file content to string
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static string ToStringContent(this File file)
		{
            return System.Text.Encoding.UTF8.GetString(file.data);
        }

        /// <summary>
        /// Will convert Wav file content to audio clip.
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static AudioClip ToWavAudioClip(this File file)
        {
            if (file == null || file.data == null)
                throw new Exception($"Failed to create an Audio Clip. File or it's data is null.");

            return WavFileToUnityAudioClip.ToAudioClip(file.data, file.fileInfo.name);
        }

        public static bool IsImage(this File file)
		{
            return IsFileTyped(file, ImageTypes);
        }

        public static bool IsText(this File file)
        {
            return IsFileTyped(file, TextTypes);
        }

        public static bool IsAudio(this File file)
        {
            return IsFileTyped(file, AudioTypes);
        }

        public static bool IsAudio(this File file, AudioType audioType)
        {
            return IsFileTyped(file, new string[] { audioType.ToString().ToUpper() });
        }

        private static bool IsFileTyped(File file, string[] types)
        {
            return types.Where(type => file.fileInfo.extension.ToUpper().EndsWith($"{type.ToUpper()}")).Count() > 0;
        }
    }
}