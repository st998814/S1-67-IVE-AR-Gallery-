using UnityEngine;

public class PlacementDataBinder : MonoBehaviour
{
    [SerializeField] private Transform imageTargetRoot;

    public PlacementDataModel BuildPlacementDataModel()
    {
        PlacementDataModel dataModel = new PlacementDataModel();

        if (imageTargetRoot == null)
        {
            Debug.LogWarning("PlacementDataBinder: imageTargetRoot is not assigned.");
            return dataModel;
        }

        foreach (Transform targetTransform in imageTargetRoot)
        {
            ImageTargetPlaceholder targetPlaceholder =
                targetTransform.GetComponentInChildren<ImageTargetPlaceholder>();

            if (targetPlaceholder == null)
            {
                Debug.LogWarning($"No ImageTargetPlaceholder found under {targetTransform.name}");
                continue;
            }

            string targetId = targetPlaceholder.TargetId;
            TargetPlacementCollection targetCollection = new TargetPlacementCollection(targetId);

            Transform contentRoot = targetTransform.Find("ContentRoot");
            if (contentRoot == null)
            {
                Debug.LogWarning($"No ContentRoot found under {targetTransform.name}");
                continue;
            }

            foreach (Transform contentTransform in contentRoot)
            {
                string contentId = contentTransform.name;
                PlacementRecord record = new PlacementRecord(targetId, contentId, contentTransform);
                targetCollection.placements.Add(record);
            }

            dataModel.targets.Add(targetCollection);
        }

        return dataModel;
    }

    [ContextMenu("Build And Log Placement Data")]
    public void BuildAndLogPlacementData()
    {
        PlacementDataModel model = BuildPlacementDataModel();

        foreach (var target in model.targets)
        {
            Debug.Log($"Target: {target.targetId}, placement count: {target.placements.Count}");

            foreach (var placement in target.placements)
            {
                Debug.Log(
                    $"  Content: {placement.contentId}, " +
                    $"Pos=({placement.position.x}, {placement.position.y}, {placement.position.z}), " +
                    $"Rot=({placement.rotation.x}, {placement.rotation.y}, {placement.rotation.z}), " +
                    $"Scale=({placement.scale.x}, {placement.scale.y}, {placement.scale.z})"
                );
            }
        }
    }
}