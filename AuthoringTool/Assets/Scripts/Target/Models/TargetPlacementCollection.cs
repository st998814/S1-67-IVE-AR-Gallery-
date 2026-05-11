using System.Collections.Generic;

[System.Serializable]
public class TargetPlacementCollection
{
    public string targetId;
    public List<PlacementRecord> placements = new List<PlacementRecord>();

    public TargetPlacementCollection(string targetId)
    {
        this.targetId = targetId;
    }
}