using UnityEngine;
using UnityEngine.UIElements;

public class AuthoringUIController : MonoBehaviour
{
    public DatabaseManager dbManager; 
    [SerializeField] private MonoBehaviour uploadBridgeBehaviour;
    private static readonly Color32 SaveButtonDefaultColor = new Color32(0, 120, 200, 255);
    private static readonly Color SaveButtonSuccessColor = new Color(0.1f, 0.6f, 0.1f);
    private static readonly Color SaveButtonErrorColor = new Color(0.8f, 0.2f, 0.2f);

    private TextField contentTypeInput;
    private FloatField posXInput;
    private FloatField posYInput;
    private FloatField posZInput;
    private FloatField scaleInput;
    
    // Changed these to match our new UI
    private TextField filePathInput;
    private Button browseButton;
    private Button saveButton;
    private Button uploadImageButton;
    private Button uploadVideoButton;
    private IWebGLUploadBridge uploadBridge;
    private bool isSaving;

    void OnEnable()
    {
        var uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null)
        {
            Debug.LogWarning("AuthoringUIController: UIDocument is missing.");
            return;
        }

        var root = uiDocument.rootVisualElement;
        ResolveUploadBridge();

        contentTypeInput = root.Q<TextField>("ContentTypeInput");
        posXInput = root.Q<FloatField>("PosXInput");
        posYInput = root.Q<FloatField>("PosYInput");
        posZInput = root.Q<FloatField>("PosZInput");
        scaleInput = root.Q<FloatField>("ScaleInput");
        
        // Connect the new UI elements
        filePathInput = root.Q<TextField>("FilePathInput");
        browseButton = root.Q<Button>("BrowseButton");
        saveButton = root.Q<Button>("SaveButton");

        uploadImageButton = root.Q<Button>("UploadImageButton");
        uploadVideoButton = root.Q<Button>("UploadVideoButton");

        if (saveButton != null)
        {
            saveButton.clicked += OnSaveButtonClicked;
        }

        if (browseButton != null)
        {
            browseButton.clicked += OnBrowseButtonClicked;
        }

        if (uploadImageButton != null)
        {
            uploadImageButton.clicked += OnUploadImageClicked;
        }

        if (uploadVideoButton != null)
        {
            uploadVideoButton.clicked += OnUploadVideoClicked;
        }

        if (dbManager != null)
        {
            dbManager.SaveCompleted += OnSaveCompleted;
        }
    }

    void OnDisable()
    {
        if (saveButton != null)
        {
            saveButton.clicked -= OnSaveButtonClicked;
        }

        if (browseButton != null)
        {
            browseButton.clicked -= OnBrowseButtonClicked;
        }

        if (uploadImageButton != null)
        {
            uploadImageButton.clicked -= OnUploadImageClicked;
        }

        if (uploadVideoButton != null)
        {
            uploadVideoButton.clicked -= OnUploadVideoClicked;
        }

        if (dbManager != null)
        {
            dbManager.SaveCompleted -= OnSaveCompleted;
        }
    }

    // This method is called by the draggable square
    public void UpdateCoordinatesFromDrag(Vector3 newPosition)
    {
        if (posXInput == null || posYInput == null || posZInput == null)
        {
            return;
        }

        posXInput.value = (float)System.Math.Round(newPosition.x, 2);
        posYInput.value = (float)System.Math.Round(newPosition.y, 2);
        posZInput.value = 0f; 
    }

    // --- NEW: The Browse Button Click Event ---
    void OnBrowseButtonClicked()
    {
        SetFilePath(uploadBridge.BrowseMedia());
    }

    void OnUploadImageClicked()
    {
        SetFilePath(uploadBridge.UploadImage());
    }

    void OnUploadVideoClicked()
    {
        SetFilePath(uploadBridge.UploadVideo());
    }

    private void ResolveUploadBridge()
    {
        uploadBridge = uploadBridgeBehaviour as IWebGLUploadBridge;
        if (uploadBridge != null)
        {
            return;
        }

        MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IWebGLUploadBridge bridge)
            {
                uploadBridge = bridge;
                return;
            }
        }

        uploadBridge = new FallbackUploadBridge();
    }

    private void SetFilePath(string path)
    {
        if (filePathInput == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        filePathInput.value = path;
    }

    private class FallbackUploadBridge : IWebGLUploadBridge
    {
        public string BrowseMedia()
        {
            Debug.Log("BrowseMedia fallback bridge called.");
            return "placeholder://browse-media";
        }

        public string UploadImage()
        {
            Debug.Log("UploadImage fallback bridge called.");
            return "placeholder://upload-image";
        }

        public string UploadVideo()
        {
            Debug.Log("UploadVideo fallback bridge called.");
            return "placeholder://upload-video";
        }
    }

    void OnSaveButtonClicked()
    {
        if (contentTypeInput == null || posXInput == null || posYInput == null || posZInput == null || scaleInput == null || filePathInput == null || saveButton == null)
        {
            Debug.LogWarning("AuthoringUIController: required UI fields are not ready.");
            return;
        }

        if (dbManager == null)
        {
            Debug.LogWarning("AuthoringUIController: DatabaseManager is not assigned.");
            ShowSaveErrorFeedback("No DatabaseManager");
            return;
        }

        if (isSaving)
        {
            return;
        }

        string type = contentTypeInput.value;
        Vector3 position = new Vector3(posXInput.value, posYInput.value, posZInput.value);
        float scale = scaleInput.value;
        
        // Grab the string from our new file path box
        string url = filePathInput.value;

        // Send the data to the database
        dbManager.SaveContentToDatabase(type, position, scale, url);
        isSaving = true;
        saveButton.text = "Saving...";
        saveButton.SetEnabled(false);
        saveButton.style.backgroundColor = new StyleColor(SaveButtonDefaultColor);
    }

    private void OnSaveCompleted(bool success, string responseText)
    {
        if (saveButton == null)
        {
            return;
        }

        isSaving = false;
        saveButton.SetEnabled(true);

        if (success)
        {
            saveButton.text = "Saved Successfully! ✓";
            saveButton.style.backgroundColor = new StyleColor(SaveButtonSuccessColor);
        }
        else
        {
            Debug.LogWarning("AuthoringUIController: Save failed: " + responseText);
            saveButton.text = "Save Failed";
            saveButton.style.backgroundColor = new StyleColor(SaveButtonErrorColor);
        }

        saveButton.schedule.Execute(() =>
        {
            if (saveButton == null)
            {
                return;
            }

            saveButton.text = "Save to Database";
            saveButton.style.backgroundColor = new StyleColor(SaveButtonDefaultColor);
        }).StartingIn(2000);
    }

    private void ShowSaveErrorFeedback(string reason)
    {
        if (saveButton == null)
        {
            return;
        }

        Debug.LogWarning("AuthoringUIController: " + reason);
        saveButton.text = "Save Failed";
        saveButton.style.backgroundColor = new StyleColor(SaveButtonErrorColor);
        saveButton.schedule.Execute(() =>
        {
            if (saveButton == null)
            {
                return;
            }

            saveButton.text = "Save to Database";
            saveButton.style.backgroundColor = new StyleColor(SaveButtonDefaultColor);
        }).StartingIn(2000);
    }
}