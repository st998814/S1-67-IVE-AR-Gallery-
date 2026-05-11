using UnityEngine;

[System.Serializable]
public class PlacementRecord
{
    public string targetId;
    public string contentId;
    public Vector3Data position;
    public Vector3Data rotation;
    public Vector3Data scale;

    public PlacementRecord(string targetId, string contentId, Transform contentTransform)
    {
        this.targetId = targetId;
        this.contentId = contentId;
        position = new Vector3Data(contentTransform.localPosition);
        rotation = new Vector3Data(contentTransform.localEulerAngles);
        scale = new Vector3Data(contentTransform.localScale);
    }
}