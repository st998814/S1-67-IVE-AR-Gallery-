using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode regression tests for Vector3Data coordinate conversion (T3.23).
/// Run via: Unity Editor > Window > General > Test Runner > EditMode
/// </summary>
public class Vector3DataTests
{
    // -----------------------------------------------------------------------
    // Constructor: float x, y, z
    // -----------------------------------------------------------------------

    [Test]
    public void Constructor_NormalValues_StoresCorrectly()
    {
        var data = new Vector3Data(1f, 2f, 3f);
        Assert.AreEqual(1f, data.x);
        Assert.AreEqual(2f, data.y);
        Assert.AreEqual(3f, data.z);
    }

    [Test]
    public void Constructor_ZeroValues_StoresZero()
    {
        var data = new Vector3Data(0f, 0f, 0f);
        Assert.AreEqual(0f, data.x);
        Assert.AreEqual(0f, data.y);
        Assert.AreEqual(0f, data.z);
    }

    [Test]
    public void Constructor_NegativeValues_StoresCorrectly()
    {
        var data = new Vector3Data(-5f, -10f, -999f);
        Assert.AreEqual(-5f, data.x);
        Assert.AreEqual(-10f, data.y);
        Assert.AreEqual(-999f, data.z);
    }

    [Test]
    public void Constructor_LargeValues_StoresCorrectly()
    {
        var data = new Vector3Data(1e6f, 1e6f, 1e6f);
        Assert.AreEqual(1e6f, data.x, 0.01f);
        Assert.AreEqual(1e6f, data.y, 0.01f);
        Assert.AreEqual(1e6f, data.z, 0.01f);
    }

    [Test]
    public void Constructor_SmallFractionalValues_Precision()
    {
        var data = new Vector3Data(0.001f, 0.0001f, 0.00001f);
        Assert.AreEqual(0.001f,   data.x, 1e-6f);
        Assert.AreEqual(0.0001f,  data.y, 1e-6f);
        Assert.AreEqual(0.00001f, data.z, 1e-6f);
    }

    // -----------------------------------------------------------------------
    // Constructor: Vector3
    // -----------------------------------------------------------------------

    [Test]
    public void Constructor_FromVector3_StoresCorrectly()
    {
        var v = new Vector3(3f, 6f, 9f);
        var data = new Vector3Data(v);
        Assert.AreEqual(v.x, data.x);
        Assert.AreEqual(v.y, data.y);
        Assert.AreEqual(v.z, data.z);
    }

    [Test]
    public void Constructor_FromVector3Zero_StoresZero()
    {
        var data = new Vector3Data(Vector3.zero);
        Assert.AreEqual(0f, data.x);
        Assert.AreEqual(0f, data.y);
        Assert.AreEqual(0f, data.z);
    }

    [Test]
    public void Constructor_FromVector3One_StoresOne()
    {
        var data = new Vector3Data(Vector3.one);
        Assert.AreEqual(1f, data.x);
        Assert.AreEqual(1f, data.y);
        Assert.AreEqual(1f, data.z);
    }

    [Test]
    public void Constructor_FromVector3Negative_StoresCorrectly()
    {
        var v = new Vector3(-1f, -2f, -3f);
        var data = new Vector3Data(v);
        Assert.AreEqual(v.x, data.x);
        Assert.AreEqual(v.y, data.y);
        Assert.AreEqual(v.z, data.z);
    }

    // -----------------------------------------------------------------------
    // Round-trip: Vector3 -> Vector3Data -> verify no precision loss
    // -----------------------------------------------------------------------

    [Test]
    public void RoundTrip_PositionValues_NoLoss()
    {
        var original = new Vector3(0.123f, -45.6f, 789.0f);
        var data = new Vector3Data(original);
        Assert.AreEqual(original.x, data.x, 1e-5f);
        Assert.AreEqual(original.y, data.y, 1e-5f);
        Assert.AreEqual(original.z, data.z, 1e-5f);
    }

    [Test]
    public void RoundTrip_RotationEulerAngles_NoLoss()
    {
        var euler = new Vector3(0f, 180f, 45f);
        var data = new Vector3Data(euler);
        Assert.AreEqual(0f,   data.x, 1e-5f);
        Assert.AreEqual(180f, data.y, 1e-5f);
        Assert.AreEqual(45f,  data.z, 1e-5f);
    }

    [Test]
    public void RoundTrip_ScaleValues_NoLoss()
    {
        var scale = new Vector3(0.1f, 0.5f, 1.0f);
        var data = new Vector3Data(scale);
        Assert.AreEqual(0.1f, data.x, 1e-5f);
        Assert.AreEqual(0.5f, data.y, 1e-5f);
        Assert.AreEqual(1.0f, data.z, 1e-5f);
    }
}
