using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightweight wireframe placement volume drawn in ContentRoot-local space (authoring-only).
/// </summary>
public sealed class PlacementSpaceVisualizer
{
    private const int BoxEdgeCount = 12;

    private readonly Color _volumeColor;
    private readonly float _edgeWidth;
    private readonly bool _showFrontPlaneGrid;
    private readonly int _gridDivisions;
    private readonly float _cornerTickLength;

    private Transform _contentRoot;
    private GameObject _visualRoot;
    private Material _lineMaterial;
    private Material _gridMaterial;
    private readonly List<LineRenderer> _boxEdges = new List<LineRenderer>(BoxEdgeCount);
    private readonly List<LineRenderer> _gridLines = new List<LineRenderer>();
    private readonly List<LineRenderer> _cornerTicks = new List<LineRenderer>();
    private readonly Vector3[] _localCorners = new Vector3[8];
    private Vector3 _lastTargetVisualScale = Vector3.negativeInfinity;
    private bool _isVisible;

    public PlacementSpaceVisualizer(
        Color volumeColor,
        float edgeWidth = 0.006f,
        bool showFrontPlaneGrid = true,
        int gridDivisions = 4,
        float cornerTickLength = 0.04f)
    {
        _volumeColor = volumeColor;
        _edgeWidth = edgeWidth;
        _showFrontPlaneGrid = showFrontPlaneGrid;
        _gridDivisions = Mathf.Clamp(gridDivisions, 2, 8);
        _cornerTickLength = Mathf.Max(0.01f, cornerTickLength);
    }

    public bool IsAttached => _contentRoot != null && _visualRoot != null;

    public void AttachTo(Transform contentRoot)
    {
        if (contentRoot == null)
        {
            Hide();
            return;
        }

        if (_contentRoot == contentRoot && _visualRoot != null)
            return;

        DisposeVisual();
        _contentRoot = contentRoot;
        _visualRoot = new GameObject("PlacementVolumeVisual");
        _visualRoot.transform.SetParent(contentRoot, false);
        _visualRoot.transform.localPosition = Vector3.zero;
        _visualRoot.transform.localRotation = Quaternion.identity;
        _visualRoot.transform.localScale = Vector3.one;

        _lineMaterial = AuthoringLineVisualUtility.CreateTransparentLineMaterial(_volumeColor);
        BuildBoxEdges();
        if (_showFrontPlaneGrid)
            BuildFrontPlaneGrid();
        BuildCornerTicks();
        _isVisible = true;
        _visualRoot.SetActive(true);
    }

    public void Hide()
    {
        _isVisible = false;
        if (_visualRoot != null)
            _visualRoot.SetActive(false);
    }

    public void Dispose()
    {
        DisposeVisual();
        _contentRoot = null;
        _isVisible = false;
    }

    /// <summary>
    /// Updates wireframe geometry from a ContentRoot-local bounds snapshot.
    /// </summary>
    public void Refresh(PlacementBoundsCalculator.Snapshot bounds)
    {
        if (!_isVisible || _contentRoot == null || _visualRoot == null)
            return;

        PlacementBoundsCalculator.FillLocalBoxCorners(bounds, _localCorners);
        UpdateBoxEdges();
        if (_showFrontPlaneGrid)
            UpdateFrontPlaneGrid(bounds);
        UpdateCornerTicks();
    }

    public bool TryRefreshFromTargetVisualScale()
    {
        if (_contentRoot == null)
            return false;

        Transform targetVisual = _contentRoot.parent != null ? _contentRoot.parent.Find("TargetVisual") : null;
        Vector3 scale = targetVisual != null ? targetVisual.localScale : Vector3.one;
        if (scale == _lastTargetVisualScale)
            return false;

        _lastTargetVisualScale = scale;
        return true;
    }

    public void InvalidateTargetVisualScaleCache()
    {
        _lastTargetVisualScale = Vector3.negativeInfinity;
    }

    private void BuildBoxEdges()
    {
        _boxEdges.Clear();
        for (int i = 0; i < BoxEdgeCount; i++)
        {
            LineRenderer line = AuthoringLineVisualUtility.CreateLineRenderer(
                _visualRoot.transform,
                $"VolumeEdge_{i:00}",
                _lineMaterial,
                _edgeWidth);
            _boxEdges.Add(line);
        }
    }

    private void BuildFrontPlaneGrid()
    {
        _gridLines.Clear();
        Color gridColor = _volumeColor;
        gridColor.a *= 0.45f;
        _gridMaterial = AuthoringLineVisualUtility.CreateTransparentLineMaterial(gridColor);

        int lineCount = (_gridDivisions + 1) * 2;
        for (int i = 0; i < lineCount; i++)
        {
            LineRenderer line = AuthoringLineVisualUtility.CreateLineRenderer(
                _visualRoot.transform,
                $"VolumeGrid_{i:00}",
                _gridMaterial,
                _edgeWidth * 0.65f);
            _gridLines.Add(line);
        }
    }

    private void BuildCornerTicks()
    {
        _cornerTicks.Clear();
        for (int corner = 0; corner < 8; corner++)
        {
            for (int axis = 0; axis < 3; axis++)
            {
                LineRenderer line = AuthoringLineVisualUtility.CreateLineRenderer(
                    _visualRoot.transform,
                    $"VolumeCorner_{corner}_{axis}",
                    _lineMaterial,
                    _edgeWidth * 0.85f);
                _cornerTicks.Add(line);
            }
        }
    }

    private void UpdateBoxEdges()
    {
        SetEdge(_boxEdges, 0, 0, 1);
        SetEdge(_boxEdges, 1, 1, 2);
        SetEdge(_boxEdges, 2, 2, 3);
        SetEdge(_boxEdges, 3, 3, 0);
        SetEdge(_boxEdges, 4, 4, 5);
        SetEdge(_boxEdges, 5, 5, 6);
        SetEdge(_boxEdges, 6, 6, 7);
        SetEdge(_boxEdges, 7, 7, 4);
        SetEdge(_boxEdges, 8, 0, 4);
        SetEdge(_boxEdges, 9, 1, 5);
        SetEdge(_boxEdges, 10, 2, 6);
        SetEdge(_boxEdges, 11, 3, 7);
    }

    private void UpdateFrontPlaneGrid(PlacementBoundsCalculator.Snapshot bounds)
    {
        if (_gridLines.Count == 0)
            return;

        float z = bounds.z.max;
        float x0 = bounds.x.min;
        float x1 = bounds.x.max;
        float y0 = bounds.y.min;
        float y1 = bounds.y.max;

        int lineIndex = 0;
        for (int i = 0; i <= _gridDivisions; i++)
        {
            float t = i / (float)_gridDivisions;
            float y = Mathf.Lerp(y0, y1, t);
            if (lineIndex < _gridLines.Count)
                SetWorldEdge(_gridLines[lineIndex++], new Vector3(x0, y, z), new Vector3(x1, y, z));
        }

        for (int i = 0; i <= _gridDivisions; i++)
        {
            float t = i / (float)_gridDivisions;
            float x = Mathf.Lerp(x0, x1, t);
            if (lineIndex < _gridLines.Count)
                SetWorldEdge(_gridLines[lineIndex++], new Vector3(x, y0, z), new Vector3(x, y1, z));
        }

        for (; lineIndex < _gridLines.Count; lineIndex++)
            _gridLines[lineIndex].enabled = false;
    }

    private void UpdateCornerTicks()
    {
        if (_cornerTicks.Count < 24)
            return;

        int lineIndex = 0;
        for (int corner = 0; corner < 8; corner++)
        {
            Vector3 cornerLocal = _localCorners[corner];
            float signX = cornerLocal.x >= 0f ? -1f : 1f;
            float signY = cornerLocal.y >= 0f ? -1f : 1f;
            float signZ = cornerLocal.z >= 0f ? -1f : 1f;

            Vector3 tickXEnd = cornerLocal + new Vector3(signX * _cornerTickLength, 0f, 0f);
            Vector3 tickYEnd = cornerLocal + new Vector3(0f, signY * _cornerTickLength, 0f);
            Vector3 tickZEnd = cornerLocal + new Vector3(0f, 0f, signZ * _cornerTickLength);

            SetWorldEdge(_cornerTicks[lineIndex++], cornerLocal, tickXEnd);
            SetWorldEdge(_cornerTicks[lineIndex++], cornerLocal, tickYEnd);
            SetWorldEdge(_cornerTicks[lineIndex++], cornerLocal, tickZEnd);
        }
    }

    private void SetEdge(List<LineRenderer> edges, int edgeIndex, int cornerA, int cornerB)
    {
        if (edgeIndex < 0 || edgeIndex >= edges.Count)
            return;

        SetWorldEdge(edges[edgeIndex], _localCorners[cornerA], _localCorners[cornerB]);
    }

    private void SetWorldEdge(LineRenderer line, Vector3 localA, Vector3 localB)
    {
        if (line == null || _contentRoot == null)
            return;

        line.enabled = true;
        line.SetPosition(0, _contentRoot.TransformPoint(localA));
        line.SetPosition(1, _contentRoot.TransformPoint(localB));
    }

    private void DisposeVisual()
    {
        if (_lineMaterial != null)
        {
            Object.Destroy(_lineMaterial);
            _lineMaterial = null;
        }

        if (_gridMaterial != null)
        {
            Object.Destroy(_gridMaterial);
            _gridMaterial = null;
        }

        if (_visualRoot != null)
        {
            Object.Destroy(_visualRoot);
            _visualRoot = null;
        }

        _boxEdges.Clear();
        _gridLines.Clear();
        _cornerTicks.Clear();
    }
}
