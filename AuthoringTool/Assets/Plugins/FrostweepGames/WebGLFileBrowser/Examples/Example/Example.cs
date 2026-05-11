using UnityEngine;
using UnityEngine.UI;

namespace FrostweepGames.Plugins.WebGLFileBrowser.Examples
{
    public class Example : MonoBehaviour
    {
        public Image contentImage;

        public Button openFileDialogButton;

        public Button saveOpenedFileButton;

        public Button cleanupButton;

        public Button openFolderDialogButton;

        public Toggle isMultipleSelectionToggle;

        public InputField filterOfTypesField;

        public Text fileNameText,
                    fileInfoText;

        private string _enteredFileExtensions;

        private File[] _loadedFiles;

        private void Start()
        {
            openFileDialogButton.onClick.AddListener(OpenFileDialogButtonOnClickHandler);
            openFolderDialogButton.onClick.AddListener(OpenFolderDialogButtonOnClickHandler);
            saveOpenedFileButton.onClick.AddListener(SaveOpenedFileButtonOnClickHandler);
            cleanupButton.onClick.AddListener(CleanupButtonOnClickHandler);
            filterOfTypesField.onValueChanged.AddListener(FilterOfTypesFieldOnValueChangedHandler);

            WebGLFileBrowser.FilesWereOpenedEvent += FilesWereOpenedEventHandler;
            WebGLFileBrowser.FilePopupWasClosedEvent += FilePopupWasClosedEventHandler;
            WebGLFileBrowser.FileOpenFailedEvent += FileOpenFailedEventHandler;
            WebGLFileBrowser.FolderOpenFailedEvent += FolderOpenFailedEventHandler;
            WebGLFileBrowser.FileWasSavedEvent += FileWasSavedEventHandler;
            WebGLFileBrowser.FileSaveFailedEvent += FileSaveFailedEventHandler;

            // if you want to set custom localization for file browser popup -> use that function:
            // WebGLFileBrowser.SetLocalization(LocalizationKey.DESCRIPTION_TEXT, "Select file for loading:");
        }

        private void OnDestroy()
		{
            WebGLFileBrowser.FilesWereOpenedEvent -= FilesWereOpenedEventHandler;
            WebGLFileBrowser.FilePopupWasClosedEvent -= FilePopupWasClosedEventHandler;
            WebGLFileBrowser.FileOpenFailedEvent -= FileOpenFailedEventHandler;
            WebGLFileBrowser.FolderOpenFailedEvent -= FolderOpenFailedEventHandler;
            WebGLFileBrowser.FileWasSavedEvent -= FileWasSavedEventHandler;
            WebGLFileBrowser.FileSaveFailedEvent -= FileSaveFailedEventHandler;
        }

        private void SaveOpenedFileButtonOnClickHandler()
        {
            if(_loadedFiles != null && _loadedFiles.Length > 0)
                WebGLFileBrowser.SaveFile(_loadedFiles[0]);

            // if you want to save custom file use this flow:
            //File file = new File()
            //{
            //    fileInfo = new FileInfo()
            //    {
            //        fullName = "Myfile.txt"
            //    },
            //    data = System.Text.Encoding.UTF8.GetBytes("my text content!")
            //};
            //WebGLFileBrowser.SaveFile(file);
        }

        private void OpenFileDialogButtonOnClickHandler()
        {
            WebGLFileBrowser.SetLocalization(LocalizationKey.DESCRIPTION_TEXT, "Select file to load or use drag & drop");

            // you could paste types like: ".png,.jpg,.pdf,.txt,.json"
            // WebGLFileBrowser.OpenFilePanelWithFilters(".png,.jpg,.pdf,.txt,.json");
            WebGLFileBrowser.OpenFilePanelWithFilters(WebGLFileBrowser.GetFilteredFileExtensions(_enteredFileExtensions), isMultipleSelectionToggle.isOn);
        }

        private void OpenFolderDialogButtonOnClickHandler()
        {
            WebGLFileBrowser.SetLocalization(LocalizationKey.DESCRIPTION_TEXT, "Select folder to load files in or use drag & drop");

            // you could paste types like: ".png,.jpg,.pdf,.txt,.json"
            // WebGLFileBrowser.OpenFolderPanelWithFilters(".png,.jpg,.pdf,.txt,.json");
            WebGLFileBrowser.OpenFolderPanelWithFilters(WebGLFileBrowser.GetFilteredFileExtensions(_enteredFileExtensions));
        }

        private void CleanupButtonOnClickHandler()
		{
            _loadedFiles = null; // you have to remove link to file and then GarbageCollector will think that there no links to that object
            saveOpenedFileButton.gameObject.SetActive(false);
			cleanupButton.gameObject.SetActive(false);

            fileInfoText.text = string.Empty;
            fileNameText.text = string.Empty;
			contentImage.color = new Color(1, 1, 1, 0);
			contentImage.sprite = null;

            WebGLFileBrowser.FreeMemory(); // free used memory and destroy created content
        }

        private void FilesWereOpenedEventHandler(File[] files)
        {
            _loadedFiles = files;

            if (_loadedFiles != null && _loadedFiles.Length > 0)
            {
                var file = _loadedFiles[0];

                if (_loadedFiles.Length > 1)
                {
                    fileInfoText.text = $"Loaded files amount: {files.Length}\n";
                }

                foreach (var loadedFile in _loadedFiles)
                {
                    fileInfoText.text += $"Name: {loadedFile.fileInfo.name}.{loadedFile.fileInfo.extension}, Size: {loadedFile.fileInfo.SizeToString()}\n";
                }

                saveOpenedFileButton.gameObject.SetActive(true);
                cleanupButton.gameObject.SetActive(true);

                if (_loadedFiles.Length == 1)
                {
                    if (file.IsImage())
                    {
                        contentImage.color = new Color(1, 1, 1, 1);
                        contentImage.sprite = file.ToSprite(); // dont forget to delete unused objects to free memory!

                        WebGLFileBrowser.RegisterFileObject(contentImage.sprite); // add sprite with texture to cache list. should be used with  WebGLFileBrowser.FreeMemory() when its no need anymore
                    }
                    else
                    {
                        contentImage.color = new Color(1, 1, 1, 0);
                    }

                    if (file.IsText())
                    {
                        string content = file.ToStringContent();
                        fileInfoText.text += $"\nFile content: {content.Substring(0, Mathf.Min(30, content.Length))}...";
                    }

                    if (file.IsAudio(AudioType.WAV))
                    {
                        Debug.Log("Its audio. try play it");

                        AudioClip clip = file.ToWavAudioClip();

                        WebGLFileBrowser.RegisterFileObject(clip); // add audio clip to cache list. should be used with  WebGLFileBrowser.FreeMemory() when its no need anymore

                        // be careful, PlayClipAtPoint will still play an audio clip if it even was destroyed. use custom AudioSource instead to have better control of memory
                        AudioSource.PlayClipAtPoint(clip, transform.position);
                    }
                }
            }
        }

		private void FilePopupWasClosedEventHandler()
        {
            if(_loadedFiles == null)
                saveOpenedFileButton.gameObject.SetActive(false);
        }

        private void FileWasSavedEventHandler(File file)
		{
            Debug.Log($"file {file.fileInfo.fullName} was saved");
		}

        private void FileSaveFailedEventHandler(string error)
        {
            Debug.Log(error);
        }

        private void FileOpenFailedEventHandler(string error)
		{
            Debug.Log(error);
        }

        private void FolderOpenFailedEventHandler(string error)
        {
            Debug.Log(error);
        }

        private void FilterOfTypesFieldOnValueChangedHandler(string value)
        {
            _enteredFileExtensions = value;
        }
    }
}