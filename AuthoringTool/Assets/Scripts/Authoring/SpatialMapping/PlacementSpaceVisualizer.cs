using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Subtle dashed wireframe placement volume in ContentRoot-local space (authoring-only guide).
/// </summary>
public sealed class PlacementSpaceVisualizer
{
    private const int BoxEdgeCount = 12;
    private const int CornerCount = 8;

    private readonly Color _edgeColor;
    private readonly Color _cornerColor;
    private readonly Color _gridColor;
    private readonly float _baseEdgeWidth;
    private readonly float _cornerAccentLength;
    private readonly bool _showFrontPlaneGrid;
    private readonly int _gridDivisions;
    private readonly float _dashTextureScale;

    private Transform _lineSpaceRoot;
    private Transform _contentRoot;
    private Camera _camera;
    private GameObject _visualRoot;
    private Material _edgeMaterial;
    private Material _cornerMaterial;
    private Material _gridMaterial;
    private Texture2D _dashTexture;
    private readonly List<LineRenderer> _boxEdges = new List<LineRenderer>(BoxEdgeCount);
    private readonly List<LineRenderer> _gridLines = new List<LineRenderer>();
    private readonly List<LineRenderer> _cornerAccents = new List<LineRenderer>(CornerCount);
    private readonly Vector3[] _localCorners = new Vector3[8];
    private Vector3 _lastTargetVisualScale = Vector3.negativeInfinity;
    private Vector3 _lastTargetVisualCenterLocal = Vector3.negativeInfinity;
    private Vector3 _lastLineSpacePosition = Vector3.negativeInfinity;
    private Quaternion _lastLineSpaceRotation = Quaternion.identity;
    private bool _isVisible;

    public PlacementSpaceVisualizer(
        Color edgeColor,
        float baseEdgeWidth = 0.0045f,
        bool showFrontPlaneGrid = false,
        int gridDivisions = 3,
        float cornerAccentLength = 0.022f,
        float dashTextureScale = 2.8f)
    {
        _edgeColor = edgeColor;
        _cornerColor = ScaleAlpha(edgeColor, Mathf.Min(1f, edgeColor.a + 0.2f));
        _gridColor = ScaleAlpha(edgeColor, edgeColor.a * 0.7f);
        _baseEdgeWidth = baseEdgeWidth;
        _showFrontPlaneGrid = showFrontPlaneGrid;
        _gridDivisions = Mathf.Clamp(gridDivisions, 2, 6);
        _cornerAccentLength = Mathf.Max(0.008f, cornerAccentLength);
        _dashTextureScale = dashTextureScale;
    }

    public bool IsAttached => _lineSpaceRoot != null && _visualRoot != null;

    public void SetCamera(Camera camera) => _camera = camera;

    /// <param name="lineSpaceRoot">Target root (or ContentRoot) whose local axes drive line positions.</param>
    /// <param name="contentRootForSizing">Optional ContentRoot used for camera distance sizing.</param>
    public void AttachTo(Transform lineSpaceRoot, Transform contentRootForSizing = null)
    {
        if (lineSpaceRoot == null)
        {
            Hide();
            return;
        }

        if (_lineSpaceRoot == lineSpaceRoot && _visualRoot != null)
            return;

        DisposeVisual();
        _lineSpaceRoot = lineSpaceRoot;
        _contentRoot = contentRootForSizing != null ? contentRootForSizing : lineSpaceRoot;
        _visualRoot = new GameObject("PlacementVolumeVisual");
        _visualRoot.transform.SetParent(lineSpaceRoot, false);
        _visualRoot.transform.localPosition = Vector3.zero;
        _visualRoot.transform.localRotation = Quaternion.identity;
        _visualRoot.transform.localScale = Vector3.one;

        _edgeMaterial = AuthoringLineVisualUtility.CreateLineMaterial(_edgeColor);
        _cornerMaterial = AuthoringLineVisualUtility.CreateLineMaterial(_cornerColor);
        if (_showFrontPlaneGrid)
        {
            _dashTexture = AuthoringLineVisualUtility.GetOrCreateDashTexture();
            _gridMaterial = AuthoringLineVisualUtility.CreateLineMaterial(_gridColor, _dashTexture);
        }

        BuildBoxEdges();
        if (_showFrontPlaneGrid)
            BuildFrontPlaneGrid();
        BuildCornerAccents();
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
        _lineSpaceRoot = null;
        _contentRoot = null;
        _isVisible = false;
    }

    public void Refresh(PlacementBoundsCalculator.Snapshot bounds)
    {
        if (!_isVisible || _lineSpaceRoot == null || _visualRoot == null)
            return;

        PlacementBoundsCalculator.FillLocalBoxCorners(bounds, _localCorners);
        UpdateBoxEdges();
        if (_showFrontPlaneGrid)
            UpdateFrontPlaneGrid(bounds);
        ApplyDynamicSizing(bounds);
    }

    /// <summary>Updates line width for camera distance without rebuilding geometry.</summary>
    public void ApplyDynamicSizing(PlacementBoundsCalculator.Snapshot bounds)
    {
        if (!_isVisible || _lineSpaceRoot == null)
            return;

        float edgeWidth = ResolveEdgeWidth(bounds.LocalCenter);
        ApplyEdgeWidths(edgeWidth);
        UpdateCornerAccents(edgeWidth);
    }

    public void SetFrontPlaneGridVisible(bool isVisible)
    {
        for (int i = 0; i < _gridLines.Count; i++)
        {
            if (_gridLines[i] != null)
                _gridLines[i].enabled = isVisible && _showFrontPlaneGrid;
        }
    }

    public bool TryRefreshFromTargetVisualLayout()
    {
        if (_lineSpaceRoot == null)
            return false;

        Transform targetVisual = _lineSpaceRoot.Find("TargetVisual");

        Vector3 scale = targetVisual != null ? targetVisual.localScale : Vector3.one;
        Vector3 centerLocal = Vector3.zero;
        if (targetVisual != null && _contentRoot != null)
        {
            centerLocal = targetVisual.parent == _contentRoot.parent
                ? targetVisual.localPosition - _contentRoot.localPosition
                : _contentRoot.InverseTransformPoint(targetVisual.position);
        }

        Vector3 rootPos = _lineSpaceRoot.position;
        Quaternion rootRot = _lineSpaceRoot.rotation;
        if (scale == _lastTargetVisualScale
            && centerLocal == _lastTargetVisualCenterLocal
            && rootPos == _lastLineSpacePosition
            && rootRot == _lastLineSpaceRotation)
        {
            return false;
        }

        _lastTargetVisualScale = scale;
        _lastTargetVisualCenterLocal = centerLocal;
        _lastLineSpacePosition = rootPos;
        _lastLineSpaceRotation = rootRot;
        return true;
    }

    public void InvalidateTargetVisualLayoutCache()
    {
        _lastTargetVisualScale = Vector3.negativeInfinity;
        _lastTargetVisualCenterLocal = Vector3.negativeInfinity;
        _lastLineSpacePosition = Vector3.negativeInfinity;
        _lastLineSpaceRotation = Quaternion.identity;
    }

    private float ResolveEdgeWidth(Vector3 localCenter)
    {
        Transform sizingRoot = _contentRoot != null ? _contentRoot : _lineSpaceRoot;
        Vector3 worldCenter = sizingRoot.TransformPoint(localCenter);
        return AuthoringLineVisualUtility.ComputeDistanceScaledWidth(_camera, worldCenter, _baseEdgeWidth);
    }

    private void ApplyEdgeWidths(float edgeWidth)
    {
        for (int i = 0; i < _boxEdges.Count; i++)
            AuthoringLineVisualUtility.ApplyWidth(_boxEdges[i], edgeWidth);

        float gridWidth = edgeWidth * 0.85f;
        for (int i = 0; i < _gridLines.Count; i++)
            AuthoringLineVisualUtility.ApplyWidth(_gridLines[i], gridWidth);
    }

    private void BuildBoxEdges()
    {
        _boxEdges.Clear();
        for (int i = 0; i < BoxEdgeCount; i++)
        {
            LineRenderer line = AuthoringLineVisualUtility.CreateLineRenderer(
                _visualRoot.transform,
                $"VolumeEdge_{i:00}",
                _edgeMaterial,
                _baseEdgeWidth);
            _boxEdges.Add(line);
        }
    }

    private void BuildFrontPlaneGrid()
    {
        _gridLines.Clear();
        int lineCount = (_gridDivisions + 1) * 2;
        for (int i = 0; i < lineCount; i++)
        {
            LineRenderer line = AuthoringLineVisualUtility.CreateLineRenderer(
                _visualRoot.transform,
                $"VolumeGrid_{i:00}",
                _gridMaterial,
                _baseEdgeWidth * 0.85f,
                useDashedTexture: true,
                dashTextureScale: _dashTextureScale * 1.15f);
            _gridLines.Add(line);
        }
    }

    private void BuildCornerAccents()
    {
        _cornerAccents.Clear();
        for (int i = 0; i < CornerCount; i++)
        {
            LineRenderer line = AuthoringLineVisualUtility.CreateLineRenderer(
                _visualRoot.transform,
                $"VolumeCorner_{i:00}",
                _cornerMaterial,
                _baseEdgeWidth * 1.1f,
                useDashedTexture: false);
            _cornerAccents.Add(line);
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
                SetWorldSegment(_gridLines[lineIndex++], new Vector3(x0, y, z), new Vector3(x1, y, z));
        }

        for (int i = 0; i <= _gridDivisions; i++)
        {
            float t = i / (float)_gridDivisions;
            float x = Mathf.Lerp(x0, x1, t);
            if (lineIndex < _gridLines.Count)
                SetWorldSegment(_gridLines[lineIndex++], new Vector3(x, y0, z), new Vector3(x, y1, z));
        }

        for (; lineIndex < _gridLines.Count; lineIndex++)
            _gridLines[lineIndex].enabled = false;
    }

    private void UpdateCornerAccents(float edgeWidth)
    {
        if (_cornerAccents.Count < CornerCount)
            return;

        float accentLen = _cornerAccentLength;
        for (int corner = 0; corner < CornerCount; corner++)
        {
            Vector3 c = _localCorners[corner];
            float signX = c.x >= 0f ? -1f : 1f;
            float signY = c.y >= 0f ? -1f : 1f;
            float signZ = c.z >= 0f ? -1f : 1f;
            Vector3 accentEnd = c + new Vector3(signX, signY, signZ).normalized * accentLen;

            LineRenderer line = _cornerAccents[corner];
            line.enabled = true;
            AuthoringLineVisualUtility.ApplyWidth(line, edgeWidth * 1.15f);
            SetWorldSegment(line, c, accentEnd);
        }
    }

    private void SetEdge(List<LineRenderer> edges, int edgeIndex, int cornerA, int cornerB)
    {
        if (edgeIndex < 0 || edgeIndex >= edges.Count)
            return;

        SetWorldSegment(edges[edgeIndex], _localCorners[cornerA], _localCorners[cornerB]);
    }

    private void SetWorldSegment(LineRenderer line, Vector3 localA, Vector3 localB)
    {
        if (line == null || _lineSpaceRoot == null)
            return;

        line.enabled = true;
        line.positionCount = 2;
        line.SetPosition(0, _lineSpaceRoot.TransformPoint(localA));
        line.SetPosition(1, _lineSpaceRoot.TransformPoint(localB));

        if (line.textureMode == LineTextureMode.Tile)
        {
            float length = Vector3.Distance(line.GetPosition(0), line.GetPosition(1));
            line.textureScale = new Vector2(Mathf.Max(0.35f, length * _dashTextureScale * 0.65f), 1f);
        }
    }

    private static Color ScaleAlpha(Color color, float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }

    private void DisposeVisual()
    {
        if (_edgeMaterial != null)
        {
            Object.Destroy(_edgeMaterial);
            _edgeMaterial = null;
        }

        if (_cornerMaterial != null)
        {
            Object.Destroy(_cornerMaterial);
            _cornerMaterial = null;
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
        _cornerAccents.Clear();
    }
}
