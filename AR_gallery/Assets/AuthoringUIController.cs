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
    private TextField mediaUrlInput;
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
        mediaUrlInput = root.Q<TextField>("MediaUrlInput");
        saveButton = root.Q<Button>("SaveButton");

        saveButton.clicked += OnSaveButtonClicked;
    }

void OnSaveButtonClicked()
    {
        string type = contentTypeInput.value;
        Vector3 position = new Vector3(posXInput.value, posYInput.value, posZInput.value);
        float scale = scaleInput.value;
        string url = mediaUrlInput.value;

        // Send the data to the database
        dbManager.SaveContentToDatabase(type, position, scale, url);
        
        // --- NEW VISUAL FEEDBACK ---
        string originalText = saveButton.text;
        
        // Change text and turn the button green
        saveButton.text = "Saved Successfully! ✓";
        saveButton.style.backgroundColor = new StyleColor(new Color(0.1f, 0.6f, 0.1f)); 

        // Wait 2 seconds (2000 milliseconds), then change it back to blue
        saveButton.schedule.Execute(() => {
            saveButton.text = originalText;
            saveButton.style.backgroundColor = new StyleColor(new Color32(0, 120, 200, 255)); 
        }).StartingIn(2000);
        
        Debug.Log("UI form submitted and feedback shown!");
    }
}