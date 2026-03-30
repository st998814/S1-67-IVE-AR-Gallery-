using UnityEngine;

public class WebGLUploadBridgePlaceholder : MonoBehaviour, IWebGLUploadBridge
{
    public string BrowseMedia()
    {
        Debug.Log("BrowseMedia placeholder called. Replace with WebGL plugin bridge.");
        return "placeholder://browse-media";
    }

    public string UploadImage()
    {
        Debug.Log("UploadImage placeholder called. Replace with WebGL plugin bridge.");
        return "placeholder://upload-image";
    }

    public string UploadVideo()
    {
        Debug.Log("UploadVideo placeholder called. Replace with WebGL plugin bridge.");
        return "placeholder://upload-video";
    }
}
