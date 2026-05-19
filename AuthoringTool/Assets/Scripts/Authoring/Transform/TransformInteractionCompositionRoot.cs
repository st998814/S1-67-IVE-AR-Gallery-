using UnityEngine;

/// <summary>
/// Reusable scene composition helper for transform interaction components.
/// It only wires references; interaction logic remains in dedicated components.
/// </summary>
public sealed class TransformInteractionCompositionRoot : MonoBehaviour
{
    [Header("Scene Anchors")]
    [SerializeField] private Transform targetRoot;
    [SerializeField] private Transform contentRoot;
    [Tooltip("Optional. Passed to target drag so the posture plane matches e.g. the wall quad transform.")]
    [SerializeField] private Transform targetPlaneAnchor;
    [SerializeField] private Camera mainCamera;

    [Header("Interaction Components")]
    [SerializeField] private ObjectSelectionManager selectionManager;
    [SerializeField] private TransformGizmoController gizmoController;
    [SerializeField] private TargetMovementController targetMovementController;
    [SerializeField] private TargetLocalTransformService targetLocalTransformService;
    [SerializeField] private FrontSideConstraint frontSideConstraint;
    [SerializeField] private PlacementBoundsService placementBoundsService;

    private void Awake()
    {
        AutoResolveAnchors();
        AutoResolveComponents();
        ApplyWiring();
    }

    private void OnValidate()
    {
        AutoResolveAnchors();
        AutoResolveComponents();
        ApplyWiring();
    }

    private void AutoResolveAnchors()
    {
        if (targetRoot == null)
        {
            GameObject targetGo = GameObject.Find("TargetRoot");
            if (targetGo != null)
                targetRoot = targetGo.transform;
        }

        if (contentRoot == null && targetRoot != null)
            contentRoot = targetRoot.Find("ContentRoot");

        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    private void AutoResolveComponents()
    {
        if (selectionManager == null)
            selectionManager = FindFirstObjectByType<ObjectSelectionManager>();
        if (gizmoController == null)
            gizmoController = FindFirstObjectByType<TransformGizmoController>();
        if (targetMovementController == null)
            targetMovementController = FindFirstObjectByType<TargetMovementController>();
        if (targetLocalTransformService == null)
            targetLocalTransformService = FindFirstObjectByType<TargetLocalTransformService>();
        if (frontSideConstraint == null)
            frontSideConstraint = FindFirstObjectByType<FrontSideConstraint>();
        if (placementBoundsService == null)
            placementBoundsService = FindFirstObjectByType<PlacementBoundsService>();
    }

    private void ApplyWiring()
    {
        if (selectionManager != null)
            selectionManager.Configure(mainCamera, contentRoot);

        if (placementBoundsService != null)
        {
            placementBoundsService.Configure(frontSideConstraint);
            placementBoundsService.SetTargetContext(targetRoot, contentRoot);
        }

        if (gizmoController != null)
            gizmoController.ConfigureDependencies(selectionManager, targetLocalTransformService, frontSideConstraint);

        if (targetMovementController != null)
            targetMovementController.ConfigureDependencies(targetRoot, contentRoot, mainCamera, gizmoController, targetPlaneAnchor);
    }
}
