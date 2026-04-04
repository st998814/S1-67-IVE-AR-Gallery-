using System.Collections;
using System;
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
    /// <summary>对应场景里哪一个 AR Image Target（与 Authoring 下拉 / ArImageTarget 一致）。</summary>
    public string TargetId;
}

public class DatabaseManager : MonoBehaviour
{
    [Header("Backend API")]
    [SerializeField] private string apiUrl = "http://127.0.0.1:5050/api/content";

    public event Action<bool, string> SaveCompleted;

    // Call this method from your UI buttons to save an item
    public void SaveContentToDatabase(string type, Vector3 position, float scale, string url, string targetId)
    {
        ARContentData data = new ARContentData
        {
            ContentType = type,
            PosX = position.x,
            PosY = position.y,
            PosZ = position.z,
            Scale = scale,
            MediaURL = url,
            TargetId = targetId ?? ""
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
                SaveCompleted?.Invoke(true, request.downloadHandler.text);
            }
            else
            {
                string body = request.downloadHandler != null ? request.downloadHandler.text : "";
                string errorMessage = string.Format(
                    "HTTP {0}: {1}{2}",
                    request.responseCode,
                    request.error,
                    string.IsNullOrEmpty(body) ? "" : " | Body: " + body
                );
                Debug.LogError("Error saving to database: " + errorMessage);
                SaveCompleted?.Invoke(false, errorMessage);
            }
        }
    }


}