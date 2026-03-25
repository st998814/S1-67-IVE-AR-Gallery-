using UnityEngine;
using UnityEngine.UIElements;

public class AuthoringUIController : MonoBehaviour
{
    public DatabaseManager dbManager; 

    private TextField contentTypeInput;
    private FloatField posXInput;
    private FloatField posYInput;
    private FloatField posZInput;
    private FloatField scaleInput;
    
    // Changed these to match our new UI
    private TextField filePathInput;
    private Button browseButton;
    private Button saveButton;

    void OnEnable()
    {
        var uiDocument = GetComponent<UIDocument>();
        var root = uiDocument.rootVisualElement;

        contentTypeInput = root.Q<TextField>("ContentTypeInput");
        posXInput = root.Q<FloatField>("PosXInput");
        posYInput = root.Q<FloatField>("PosYInput");
        posZInput = root.Q<FloatField>("PosZInput");
        scaleInput = root.Q<FloatField>("ScaleInput");
        
        // Connect the new UI elements
        filePathInput = root.Q<TextField>("FilePathInput");
        browseButton = root.Q<Button>("BrowseButton");
        saveButton = root.Q<Button>("SaveButton");

        saveButton.clicked += OnSaveButtonClicked;
        browseButton.clicked += OnBrowseButtonClicked;
    }

    // This method is called by the draggable square
    public void UpdateCoordinatesFromDrag(Vector3 newPosition)
    {
        posXInput.value = (float)System.Math.Round(newPosition.x, 2);
        posYInput.value = (float)System.Math.Round(newPosition.y, 2);
        posZInput.value = 0f; 
    }

    // --- NEW: The Browse Button Click Event ---
    void OnBrowseButtonClicked()
    {
        Debug.Log("Browse button clicked! Waiting for WebGL file plugin...");
        
        // TEMPORARY: Just to show how it will work once the plugin is installed
        // Later, the plugin will return the actual file path here.
        filePathInput.value = "C:/fake_path/my_poster.jpg";
    }

    void OnSaveButtonClicked()
    {
        string type = contentTypeInput.value;
        Vector3 position = new Vector3(posXInput.value, posYInput.value, posZInput.value);
        float scale = scaleInput.value;
        
        // Grab the string from our new file path box
        string url = filePathInput.value;

        // Send the data to the database
        dbManager.SaveContentToDatabase(type, position, scale, url);
        
        // Visual Feedback
        string originalText = saveButton.text;
        saveButton.text = "Saved Successfully! ✓";
        saveButton.style.backgroundColor = new StyleColor(new Color(0.1f, 0.6f, 0.1f)); 

        saveButton.schedule.Execute(() => {
            saveButton.text = originalText;
            saveButton.style.backgroundColor = new StyleColor(new Color32(0, 120, 200, 255)); 
        }).StartingIn(2000);
    }
}