using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using System.Text;

// This class matches your PostgreSQL database schema
[System.Serializable]
public class ARContentData
{
    public string ContentType;
    public float PosX;
    public float PosY;
    public float PosZ;
    public float Scale;
    public string MediaURL;
}

public class DatabaseManager : MonoBehaviour
{
    // Your local Flask server URL
    private string apiUrl = "http://127.0.0.1:5000/api/content";

    // Call this method from your UI buttons to save an item
    public void SaveContentToDatabase(string type, Vector3 position, float scale, string url)
    {
        ARContentData data = new ARContentData
        {
            ContentType = type,
            PosX = position.x,
            PosY = position.y,
            PosZ = position.z,
            Scale = scale,
            MediaURL = url
        };

        StartCoroutine(PostRequest(data));
    }

    private IEnumerator PostRequest(ARContentData data)
    {
        string jsonData = JsonUtility.ToJson(data);
        
        using (UnityWebRequest request = new UnityWebRequest(apiUrl, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Successfully saved to database: " + request.downloadHandler.text);
            }
            else
            {
                Debug.LogError("Error saving to database: " + request.error);
            }
        }
    }


}