using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode tests for <see cref="ContentTransformManipulator"/>.
/// </summary>
public class ContentTransformManipulatorTests
{
    private const float Epsilon = 1e-5f;

    [Test]
    public void SetSemanticAxis_ClampsAndRaisesEvent()
    {
        BuildTestHierarchy(
            out Transform targetRoot,
            out Transform contentRoot,
            out Transform content,
            out PlacementBoundsService bounds,
            out ContentTransformManipulator manipulator,
            out FrontSideConstraint front);

        bool eventRaised = false;
        manipulator.ContentTransformChanged += _ => eventRaised = true;

        try
        {
            manipulator.SetSemanticAxis(content, PlacementBoundsCalculator.SemanticAxis.LeftRight, 5f);

            Assert.IsTrue(eventRaised);
            Assert.AreEqual(0.48f, content.localPosition.x, Epsilon);
            Assert.AreEqual(-0.5f, content.localPosition.z, Epsilon);
        }
        finally
        {
            Cleanup(targetRoot, bounds, manipulator, front);
        }
    }

    [Test]
    public void ApplyGizmoResult_TranslateMode_ClampsPosition()
    {
        BuildTestHierarchy(
            out Transform targetRoot,
            out Transform _,
            out Transform content,
            out PlacementBoundsService bounds,
            out ContentTransformManipulator manipulator,
            out FrontSideConstraint front);

        try
        {
            content.localPosition = new Vector3(2f, 0f, 0f);
            manipulator.ApplyGizmoResult(content, TransformGizmoController.GizmoMode.Translate, enforceUniformScale: false);

            Assert.AreEqual(0.48f, content.localPosition.x, Epsilon);
        }
        finally
        {
            Cleanup(targetRoot, bounds, manipulator, front);
        }
    }

    [Test]
    public void SetUniformScale_ClampsToServiceMinimum()
    {
        BuildTestHierarchy(
            out Transform targetRoot,
            out Transform _,
            out Transform content,
            out PlacementBoundsService bounds,
            out ContentTransformManipulator manipulator,
            out FrontSideConstraint front);

        try
        {
            manipulator.SetUniformScale(content, 0.01f);
            Assert.AreEqual(0.05f, content.localScale.x, Epsilon);
        }
        finally
        {
            Cleanup(targetRoot, bounds, manipulator, front);
        }
    }

    private static void BuildTestHierarchy(
        out Transform targetRoot,
        out Transform contentRoot,
        out Transform content,
        out PlacementBoundsService boundsService,
        out ContentTransformManipulator manipulator,
        out FrontSideConstraint frontConstraint)
    {
        GameObject targetRootGo = new GameObject("TargetRoot");
        targetRoot = targetRootGo.transform;

        GameObject targetVisualGo = new GameObject("TargetVisual");
        targetVisualGo.transform.SetParent(targetRoot, false);
        targetVisualGo.transform.localScale = new Vector3(1f, 1f, 1f);

        GameObject contentRootGo = new GameObject("ContentRoot");
        contentRoot = contentRootGo.transform;
        contentRoot.SetParent(targetRoot, false);

        GameObject contentGo = new GameObject("Cube");
        content = contentGo.transform;
        content.SetParent(contentRoot, false);

        GameObject systemsGo = new GameObject("Systems");
        frontConstraint = systemsGo.AddComponent<FrontSideConstraint>();
        boundsService = systemsGo.AddComponent<PlacementBoundsService>();
        TargetLocalTransformService localService = systemsGo.AddComponent<TargetLocalTransformService>();
        manipulator = systemsGo.AddComponent<ContentTransformManipulator>();

        boundsService.Configure(frontConstraint);
        boundsService.SetTargetContext(targetRoot, contentRoot);
        manipulator.Configure(localService, boundsService, frontConstraint);
    }

    private static void Cleanup(
        Transform targetRoot,
        PlacementBoundsService bounds,
        ContentTransformManipulator manipulator,
        FrontSideConstraint front)
    {
        if (manipulator != null)
            Object.DestroyImmediate(manipulator.gameObject);
        else
        {
            if (bounds != null) Object.DestroyImmediate(bounds.gameObject);
            if (front != null) Object.DestroyImmediate(front.gameObject);
        }

        if (targetRoot != null)
            Object.DestroyImmediate(targetRoot.gameObject);
    }
}
