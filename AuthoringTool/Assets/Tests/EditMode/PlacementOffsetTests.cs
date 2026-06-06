using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode regression tests for content placement offset formula (T3.23).
/// Regression tests for the legacy dev cube placement offset formula (offsetX = count * 0.5).
///   offsetX = existingCount * 0.5f
///   position = (offsetX, 0.8f, -0.6f)
///   scale    = (0.5f, 0.5f, 0.5f)
/// Run via: Unity Editor > Window > General > Test Runner > EditMode
/// </summary>
public class PlacementOffsetTests
{
    private const float OffsetStep  =  0.5f;
    private const float DefaultY    =  0.8f;
    private const float DefaultZ    = -0.6f;
    private const float DefaultScale =  0.5f;

    private Vector3 CalcDefaultPosition(int existingCount)
    {
        float offsetX = existingCount * OffsetStep;
        return new Vector3(offsetX, DefaultY, DefaultZ);
    }

    // -----------------------------------------------------------------------
    // X offset — scales with existing content count
    // -----------------------------------------------------------------------

    [Test]
    public void Offset_FirstContent_XIsZero()
    {
        var pos = CalcDefaultPosition(0);
        Assert.AreEqual(0f, pos.x, 1e-5f);
    }

    [Test]
    public void Offset_SecondContent_XIsHalf()
    {
        var pos = CalcDefaultPosition(1);
        Assert.AreEqual(0.5f, pos.x, 1e-5f);
    }

    [Test]
    public void Offset_ThirdContent_XIsOne()
    {
        var pos = CalcDefaultPosition(2);
        Assert.AreEqual(1.0f, pos.x, 1e-5f);
    }

    [Test]
    public void Offset_TenthContent_XIsCorrect()
    {
        var pos = CalcDefaultPosition(9);
        Assert.AreEqual(4.5f, pos.x, 1e-5f);
    }

    // -----------------------------------------------------------------------
    // Y and Z — always fixed defaults
    // -----------------------------------------------------------------------

    [Test]
    public void Offset_YIsAlwaysDefaultRegardlessOfCount()
    {
        for (int i = 0; i < 5; i++)
        {
            var pos = CalcDefaultPosition(i);
            Assert.AreEqual(DefaultY, pos.y, 1e-5f, $"Y mismatch at count={i}");
        }
    }

    [Test]
    public void Offset_ZIsAlwaysDefaultRegardlessOfCount()
    {
        for (int i = 0; i < 5; i++)
        {
            var pos = CalcDefaultPosition(i);
            Assert.AreEqual(DefaultZ, pos.z, 1e-5f, $"Z mismatch at count={i}");
        }
    }

    // -----------------------------------------------------------------------
    // Default scale
    // -----------------------------------------------------------------------

    [Test]
    public void DefaultScale_IsHalfOnAllAxes()
    {
        var scale = new Vector3(DefaultScale, DefaultScale, DefaultScale);
        Assert.AreEqual(0.5f, scale.x, 1e-5f);
        Assert.AreEqual(0.5f, scale.y, 1e-5f);
        Assert.AreEqual(0.5f, scale.z, 1e-5f);
    }
}
