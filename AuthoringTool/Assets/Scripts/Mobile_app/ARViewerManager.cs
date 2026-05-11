using UnityEngine;
using UnityEngine.XR.ARFoundation;
using ARGallery.Content; 

public class ARViewerManager : MonoBehaviour
{
    public ARTrackedImageManager imageManager;
    public DatabaseManager dbManager; 
    
    public GameObject videoPrefab; 
    public GameObject picturePrefab;

    private void OnEnable()
    {
        imageManager.trackedImagesChanged += OnTrackedImagesChanged;
    }

    private void OnDisable()
    {
        imageManager.trackedImagesChanged -= OnTrackedImagesChanged;
    }

    private void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs eventArgs)
    {
        foreach (var trackedImage in eventArgs.added)
        {
            // The name of the physical image target the camera just saw
            string targetName = trackedImage.referenceImage.name; 
            Debug.Log($"Camera recognized target: {targetName}");

            // Call the NEW method we just added to DatabaseManager
            StartCoroutine(dbManager.FetchContentForTarget(targetName, (placements) =>
            {
                foreach (var placement in placements)
                {
                    SpawnContent(placement, trackedImage.transform);
                }
            }));
        }
    }

    // Notice we are now using ARContentData to match your script!
    private void SpawnContent(ARContentData data, Transform physicalTargetTransform)
    {
        GameObject newObj = null;

        // Matches ContentType exactly as spelled in ARContentData
        if (data.ContentType == "Video") 
            newObj = Instantiate(videoPrefab);
        else if (data.ContentType == "Picture") 
            newObj = Instantiate(picturePrefab);

        if (newObj != null)
        {
            newObj.transform.SetParent(physicalTargetTransform);

            // Reconstruct the Vector3 from your PosX, PosY, PosZ floats
            newObj.transform.localPosition = new Vector3(data.PosX, data.PosY, data.PosZ);
            
            // Your script only saved one float for scale, so apply it evenly to x,y,z
            newObj.transform.localScale = new Vector3(data.Scale, data.Scale, data.Scale);

            // Note: Since ARContentData doesn't seem to store Rotation, 
            // the object will spawn facing completely forward based on the poster.

            // Inject the YouTube URL if it's a video
            if (data.ContentType == "Video" && !string.IsNullOrEmpty(data.MediaURL))
            {
                var ytPlayer = newObj.GetComponent("YoutubePlayer");
                if (ytPlayer != null)
                {
                    ytPlayer.GetType().GetField("youtubeUrl")?.SetValue(ytPlayer, data.MediaURL);
                    ytPlayer.GetType().GetMethod("Play")?.Invoke(ytPlayer, null);
                }
            }
        }
    }
}