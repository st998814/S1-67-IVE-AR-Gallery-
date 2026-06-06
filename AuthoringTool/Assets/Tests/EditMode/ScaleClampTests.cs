using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode regression tests for scale clamping logic (T3.23).
/// EditMode regression tests for per-axis scale minimum clamping (T3.23).
/// Authoring runtime uses uniform scale via <see cref="TargetLocalTransformService"/>; these tests guard the per-axis floor pattern.
/// Run via: Unity Editor > Window > General > Test Runner > EditMode
/// </summary>
public class ScaleClampTests
{
    // Per-axis minimum scale floor used in legacy keyboard nudge paths.
    private const float MinScale = 0.1f;

    private Vector3 ApplyScaleClamp(Vector3 scale)
    {
        scale.x = Mathf.Max(MinScale, scale.x);
        scale.y = Mathf.Max(MinScale, scale.y);
        scale.z = Mathf.Max(MinScale, scale.z);
        return scale;
    }

    // -----------------------------------------------------------------------
    // Normal values — should pass through unchanged
    // -----------------------------------------------------------------------

    [Test]
    public void Clamp_NormalScale_Unchanged()
    {
        var result = ApplyScaleClamp(new Vector3(1f, 1f, 1f));
        Assert.AreEqual(new Vector3(1f, 1f, 1f), result);
    }

    [Test]
    public void Clamp_LargeScale_Unchanged()
    {
        var result = ApplyScaleClamp(new Vector3(10f, 20f, 30f));
        Assert.AreEqual(new Vector3(10f, 20f, 30f), result);
    }

    // -----------------------------------------------------------------------
    // Boundary: exactly at minimum — should remain 0.1f
    // -----------------------------------------------------------------------

    [Test]
    public void Clamp_ExactlyAtMinimum_Unchanged()
    {
        var result = ApplyScaleClamp(new Vector3(0.1f, 0.1f, 0.1f));
        Assert.AreEqual(0.1f, result.x, 1e-6f);
        Assert.AreEqual(0.1f, result.y, 1e-6f);
        Assert.AreEqual(0.1f, result.z, 1e-6f);
    }

    // -----------------------------------------------------------------------
    // Below minimum — should be clamped to 0.1f
    // -----------------------------------------------------------------------

    [Test]
    public void Clamp_ZeroScale_ClampedToMinimum()
    {
        var result = ApplyScaleClamp(Vector3.zero);
        Assert.AreEqual(MinScale, result.x, 1e-6f);
        Assert.AreEqual(MinScale, result.y, 1e-6f);
        Assert.AreEqual(MinScale, result.z, 1e-6f);
    }

    [Test]
    public void Clamp_NegativeScale_ClampedToMinimum()
    {
        var result = ApplyScaleClamp(new Vector3(-1f, -5f, -100f));
        Assert.AreEqual(MinScale, result.x, 1e-6f);
        Assert.AreEqual(MinScale, result.y, 1e-6f);
        Assert.AreEqual(MinScale, result.z, 1e-6f);
    }

    [Test]
    public void Clamp_JustBelowMinimum_ClampedToMinimum()
    {
        var result = ApplyScaleClamp(new Vector3(0.09f, 0.09f, 0.09f));
        Assert.AreEqual(MinScale, result.x, 1e-6f);
        Assert.AreEqual(MinScale, result.y, 1e-6f);
        Assert.AreEqual(MinScale, result.z, 1e-6f);
    }

    // -----------------------------------------------------------------------
    // Mixed: only some axes below minimum
    // -----------------------------------------------------------------------

    [Test]
    public void Clamp_MixedAxes_OnlyBelowMinimumClamped()
    {
        var result = ApplyScaleClamp(new Vector3(0f, 1f, -1f));
        Assert.AreEqual(MinScale, result.x, 1e-6f); // clamped
        Assert.AreEqual(1f,       result.y, 1e-6f); // unchanged
        Assert.AreEqual(MinScale, result.z, 1e-6f); // clamped
    }
}
