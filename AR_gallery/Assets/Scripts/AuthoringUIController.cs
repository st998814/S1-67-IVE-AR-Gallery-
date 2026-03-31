using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Networking;
using TMPro; // NEW: Required for TextMeshPro
using System.Collections;
using System.Collections.Generic;
using FrostweepGames.Plugins.WebGLFileBrowser; // NEW: Access the plugin

public class AuthoringUIController : MonoBehaviour
{
    public DatabaseManager dbManager; 

    // --- NEW: Prefab Templates (Drag these in the Inspector) ---
    public GameObject picturePrefab;
    public GameObject textPrefab;
    
    // --- UI Fields ---
    private TextField contentTypeInput;
    private FloatField posXInput, posYInput, posZInput, scaleInput;
    private TextField filePathInput;
    
    // --- NEW: Text Spawning Fields ---
    private TextField spawningTextInput;
    private Button spawnTextButton;

    private Button browseButton, saveButton;

    // Track the object that is currently "active" in the UI (being dragged)
    private DraggableObject activeDraggedObject;
    private Dictionary<DraggableObject, string> spawnedMediaUrls = new Dictionary<DraggableObject, string>();

    private string uploadApiUrl = "http://127.0.0.1:5000/api/upload";

    void OnEnable()
    {
        var uiDocument = GetComponent<UIDocument>();
        var root = uiDocument.rootVisualElement;

        // Basic Fields
        contentTypeInput = root.Q<TextField>("ContentTypeInput");
        posXInput = root.Q<FloatField>("PosXInput");
        posYInput = root.Q<FloatField>("PosYInput");
        posZInput = root.Q<FloatField>("PosZInput");
        scaleInput = root.Q<FloatField>("ScaleInput");
        filePathInput = root.Q<TextField>("FilePathInput");
        
        // NEW: Text Spawning UI elements
        spawningTextInput = root.Q<TextField>("SpawningTextInput");
        spawnTextButton = root.Q<Button>("SpawnTextButton");
        
        browseButton = root.Q<Button>("BrowseButton");
        saveButton = root.Q<Button>("SaveButton");

        // Event Listeners
        browseButton.clicked += OnBrowseButtonClicked;
        saveButton.clicked += OnSaveButtonClicked;
        
        // NEW: Event Listener for spawning text
        spawnTextButton.clicked += OnSpawnTextButtonClicked;

        // NEW: Listen for when the user selects a file in the browser
        WebGLFileBrowser.FilesWereOpenedEvent += OnFilesOpened;
    }

    void OnDisable()
    {
        // Cleanup event listeners
        WebGLFileBrowser.FilesWereOpenedEvent -= OnFilesOpened;
    }

    // --- NEW: Text Spawning ---
    void OnSpawnTextButtonClicked()
    {
        if (textPrefab == null) { Debug.LogError("Text Prefab is not assigned in the Inspector!"); return; }

        string textToDisplay = spawningTextInput.value;

        // Instantiation! Spawn the text prefab into the scene origin.
        GameObject spawnedTextObj = Instantiate(textPrefab, new Vector3(0, 0, 10f), Quaternion.identity);
        
        // Assign the text value to the TextMeshPro component
        spawnedTextObj.GetComponent<TextMeshPro>().text = textToDisplay;
        
        DraggableObject dragHandler = spawnedTextObj.GetComponent<DraggableObject>();
        if (dragHandler != null)
        {
            SetActiveAuthoringObject(dragHandler, textToDisplay, "Text");
        }
    }

    void OnBrowseButtonClicked()
    {
        // Open browser for images (.png and .jpg)
        WebGLFileBrowser.OpenFilePanelWithFilters(".png,.jpg", false);
    }

    // This runs automatically when an image is selected
    private void OnFilesOpened(File[] files)
    {
        if (files != null && files.Length > 0)
        {
            var selectedFile = files[0];
            filePathInput.value = "Uploading: " + selectedFile.fileInfo.name;
            
            // Start the upload routine to your Flask server
            StartCoroutine(UploadFileCoroutine(selectedFile));
        }
    }

    private IEnumerator UploadFileCoroutine(File file)
    {
        // Form creation
        WWWForm form = new WWWForm();
        form.AddBinaryData("file", file.data, file.fileInfo.name + "." + file.fileInfo.extension);

        using (UnityWebRequest request = UnityWebRequest.Post(uploadApiUrl, form))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                // Parse JSON
                string jsonResponse = request.downloadHandler.text;
                string uploadedUrl = JsonUtility.FromJson<UploadResponse>(jsonResponse).url;
                
                filePathInput.value = uploadedUrl; // Put real URL in UI
                Debug.Log("Upload complete! URL: " + uploadedUrl);

                // --- NEW: Instant Authoring Feedback ---
                // Now that we have a valid URL, we can spawn the visual object!
                InstantiatePictureAtOrigin(uploadedUrl, file.fileInfo.name);
            }
            else
            {
                filePathInput.value = "Upload Failed!";
                Debug.LogError("Error: " + request.error);
            }
        }
    }

    // --- NEW: Spawning Images ---
    private void InstantiatePictureAtOrigin(string url, string filename)
    {
        if (picturePrefab == null) { Debug.LogError("Picture Prefab is not assigned in the Inspector!"); return; }

        // 1. Instantiation! Spawn the picture prefab.
        GameObject spawnedPicObj = Instantiate(picturePrefab, Vector3.zero, Quaternion.identity);
        
        DraggableObject dragHandler = spawnedPicObj.GetComponent<DraggableObject>();
        if (dragHandler != null)
        {
            // 2. We use a Coroutine to load the new texture from the server onto the spawned object.
            StartCoroutine(ApplyTextureToSpawningObject(spawnedPicObj, url));

            // 3. Mark this spawned object as the active one in the UI.
            SetActiveAuthoringObject(dragHandler, url, "Image (" + filename + ")");
        }
    }

    // Coroutine to download the texture and apply it to the Unlit/Texture shader
    private IEnumerator ApplyTextureToSpawningObject(GameObject objToTex, string url)
    {
        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
        {
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                Texture2D texture = ((DownloadHandlerTexture)request.downloadHandler).texture;
                // Apply the texture directly to the Renderer's material.
                objToTex.GetComponent<Renderer>().material.mainTexture = texture;
            }
            else { Debug.LogError("Error loading image for preview: " + request.error); }
        }
    }

    // Helper: When an object is spawned or selected, update UI fields
    private void SetActiveAuthoringObject(DraggableObject targetObj, string mediaValue, string contentType)
    {
        activeDraggedObject = targetObj;

        // Round coordinates to clean numbers in the UI
        Vector3 newPosition = targetObj.transform.localPosition;
        posXInput.value = (float)System.Math.Round(newPosition.x, 2);
        posYInput.value = (float)System.Math.Round(newPosition.y, 2);
        posZInput.value = 0f;

        // Scale should match the spawned object (1)
        scaleInput.value = targetObj.transform.localScale.x;

        // Set 'Media File:' to the Text string or the Image URL
        filePathInput.value = mediaValue;
        
        // Set Content Type
        contentTypeInput.value = contentType;
        
        Debug.Log("Now authoring " + targetObj.gameObject.name);
    }

    // Helper: Required helper class to parse the JSON response from your Flask server
    [System.Serializable]
    public class UploadResponse { public string url; }

    // Coroutine and SaveButton method from earlier
    void OnSaveButtonClicked()
    {
        string type = contentTypeInput.value;
        Vector3 position = new Vector3(posXInput.value, posYInput.value, posZInput.value);
        float scale = scaleInput.value;
        string url = filePathInput.value;

        dbManager.SaveContentToDatabase(type, position, scale, url);
        
        saveButton.text = "Saved Successfully! ✓";
        saveButton.schedule.Execute(() => { saveButton.text = "Save to Database"; }).StartingIn(2000);
    }

    // This MUST be public so the DraggableObject can see it!
    public void UpdateCoordinatesFromDrag(Vector3 newPosition)
    {
        posXInput.value = (float)System.Math.Round(newPosition.x, 2);
        posYInput.value = (float)System.Math.Round(newPosition.y, 2);
        posZInput.value = 0f; 
    }
}