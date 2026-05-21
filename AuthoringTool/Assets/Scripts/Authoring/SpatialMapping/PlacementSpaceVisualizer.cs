using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Subtle corner-only placement boundary guides in target-local space (authoring-only).
/// </summary>
public sealed class PlacementSpaceVisualizer
{
    private const int CornerCount = 8;
    private const int LegsPerCorner = 3;
    private const int BracketLineCount = CornerCount * LegsPerCorner;

    private readonly Color _cornerColor;
    private readonly float _baseEdgeWidth;
    private readonly float _cornerLegLength;

    private Transform _lineSpaceRoot;
    private Transform _contentRoot;
    private Camera _camera;
    private GameObject _visualRoot;
    private Material _cornerMaterial;
    private readonly List<LineRenderer> _cornerBrackets = new List<LineRenderer>(BracketLineCount);
    private readonly Vector3[] _localCorners = new Vector3[8];
    private Vector3 _lastTargetVisualScale = Vector3.negativeInfinity;
    private Vector3 _lastTargetVisualCenterLocal = Vector3.negativeInfinity;
    private Vector3 _lastLineSpacePosition = Vector3.negativeInfinity;
    private Quaternion _lastLineSpaceRotation = Quaternion.identity;
    private bool _isVisible;

    public PlacementSpaceVisualizer(
        Color cornerColor,
        float baseEdgeWidth = 0.003f,
        float cornerLegLength = 0.035f)
    {
        _cornerColor = cornerColor;
        _baseEdgeWidth = baseEdgeWidth;
        _cornerLegLength = Mathf.Max(0.012f, cornerLegLength);
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
        _visualRoot = new GameObject("PlacementBoundaryVisual");
        _visualRoot.transform.SetParent(lineSpaceRoot, false);
        _visualRoot.transform.localPosition = Vector3.zero;
        _visualRoot.transform.localRotation = Quaternion.identity;
        _visualRoot.transform.localScale = Vector3.one;

        _cornerMaterial = AuthoringLineVisualUtility.CreateLineMaterial(_cornerColor);
        BuildCornerBrackets();
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
        UpdateCornerBrackets(ResolveEdgeWidth(bounds.LocalCenter));
    }

    /// <summary>Updates line width for camera distance without rebuilding geometry.</summary>
    public void ApplyDynamicSizing(PlacementBoundsCalculator.Snapshot bounds)
    {
        if (!_isVisible || _lineSpaceRoot == null)
            return;

        UpdateCornerBrackets(ResolveEdgeWidth(bounds.LocalCenter));
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
        return AuthoringLineVisualUtility.ComputeDistanceScaledWidth(
            _camera,
            worldCenter,
            _baseEdgeWidth,
            minScale: 1f,
            maxScale: 2f,
            referenceDistance: 1.4f);
    }

    private void BuildCornerBrackets()
    {
        _cornerBrackets.Clear();
        for (int corner = 0; corner < CornerCount; corner++)
        {
            for (int leg = 0; leg < LegsPerCorner; leg++)
            {
                LineRenderer line = AuthoringLineVisualUtility.CreateLineRenderer(
                    _visualRoot.transform,
                    $"BoundaryCorner_{corner:00}_Leg_{leg}",
                    _cornerMaterial,
                    _baseEdgeWidth,
                    useDashedTexture: false);
                _cornerBrackets.Add(line);
            }
        }
    }

    private void UpdateCornerBrackets(float edgeWidth)
    {
        if (_cornerBrackets.Count < BracketLineCount)
            return;

        Vector3 center = Vector3.zero;
        for (int i = 0; i < CornerCount; i++)
            center += _localCorners[i];
        center /= CornerCount;

        int lineIndex = 0;
        for (int corner = 0; corner < CornerCount; corner++)
        {
            Vector3 c = _localCorners[corner];
            Vector3 inward = center - c;
            if (inward.sqrMagnitude < 1e-8f)
                inward = Vector3.one;

            Vector3 axisX = new Vector3(Mathf.Sign(inward.x) * _cornerLegLength, 0f, 0f);
            Vector3 axisY = new Vector3(0f, Mathf.Sign(inward.y) * _cornerLegLength, 0f);
            Vector3 axisZ = new Vector3(0f, 0f, Mathf.Sign(inward.z) * _cornerLegLength);

            SetBracketLeg(lineIndex++, c, c + axisX, edgeWidth);
            SetBracketLeg(lineIndex++, c, c + axisY, edgeWidth);
            SetBracketLeg(lineIndex++, c, c + axisZ, edgeWidth);
        }
    }

    private void SetBracketLeg(int lineIndex, Vector3 cornerLocal, Vector3 legEndLocal, float edgeWidth)
    {
        if (lineIndex < 0 || lineIndex >= _cornerBrackets.Count)
            return;

        LineRenderer line = _cornerBrackets[lineIndex];
        line.enabled = true;
        AuthoringLineVisualUtility.ApplyWidth(line, edgeWidth);
        SetWorldSegment(line, cornerLocal, legEndLocal);
    }

    private void SetWorldSegment(LineRenderer line, Vector3 localA, Vector3 localB)
    {
        if (line == null || _lineSpaceRoot == null)
            return;

        line.positionCount = 2;
        line.SetPosition(0, _lineSpaceRoot.TransformPoint(localA));
        line.SetPosition(1, _lineSpaceRoot.TransformPoint(localB));
    }

    private void DisposeVisual()
    {
        if (_cornerMaterial != null)
        {
            Object.Destroy(_cornerMaterial);
            _cornerMaterial = null;
        }

        if (_visualRoot != null)
        {
            Object.Destroy(_visualRoot);
            _visualRoot = null;
        }

        _cornerBrackets.Clear();
    }
}
