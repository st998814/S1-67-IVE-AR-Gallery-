using UnityEngine;

/// <summary>
/// Target-to-content spatial mapping guides in ContentRoot-local space (authoring-only).
/// </summary>
public sealed class SpatialMappingIndicatorRenderer
{
    private const int AxisCount = 3;
    private const int ArrowLinesPerAxis = 2;

    private readonly Color _axisXColor;
    private readonly Color _axisYColor;
    private readonly Color _axisZColor;
    private readonly float _baseEdgeWidth;
    private readonly float _arrowHeadLength;
    private readonly float _arrowHeadAngleDegrees;
    private readonly float _minWidthScale;
    private readonly float _maxWidthScale;
    private readonly float _widthDistanceReference;

    private Transform _contentRoot;
    private Transform _selectedContent;
    private GameObject _visualRoot;
    private Camera _camera;
    private Material _axisXMaterial;
    private Material _axisYMaterial;
    private Material _axisZMaterial;
    private readonly LineRenderer[] _axisLines = new LineRenderer[AxisCount];
    private readonly LineRenderer[] _arrowLines = new LineRenderer[AxisCount * ArrowLinesPerAxis];
    private bool _isVisible;

    public SpatialMappingIndicatorRenderer(
        Color axisXColor,
        Color axisYColor,
        Color axisZColor,
        float baseEdgeWidth = 0.007f,
        float arrowHeadLength = 0.035f,
        float arrowHeadAngleDegrees = 22f,
        float minWidthScale = 0.55f,
        float maxWidthScale = 2.5f,
        float widthDistanceReference = 1.4f)
    {
        _axisXColor = axisXColor;
        _axisYColor = axisYColor;
        _axisZColor = axisZColor;
        _baseEdgeWidth = baseEdgeWidth;
        _arrowHeadLength = arrowHeadLength;
        _arrowHeadAngleDegrees = arrowHeadAngleDegrees;
        _minWidthScale = minWidthScale;
        _maxWidthScale = maxWidthScale;
        _widthDistanceReference = Mathf.Max(0.25f, widthDistanceReference);
    }

    public bool IsAttached => _contentRoot != null && _visualRoot != null;

    public void SetCamera(Camera camera)
    {
        _camera = camera;
    }

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
        _visualRoot = new GameObject("SpatialMappingIndicators");
        _visualRoot.transform.SetParent(contentRoot, false);
        _visualRoot.transform.localPosition = Vector3.zero;
        _visualRoot.transform.localRotation = Quaternion.identity;
        _visualRoot.transform.localScale = Vector3.one;

        _axisXMaterial = AuthoringLineVisualUtility.CreateLineMaterial(_axisXColor);
        _axisYMaterial = AuthoringLineVisualUtility.CreateLineMaterial(_axisYColor);
        _axisZMaterial = AuthoringLineVisualUtility.CreateLineMaterial(_axisZColor);

        float width = ResolveEdgeWidth();
        _axisLines[0] = CreateAxisLine("AxisXProjection", _axisXMaterial, width);
        _axisLines[1] = CreateAxisLine("AxisYProjection", _axisYMaterial, width);
        _axisLines[2] = CreateAxisLine("AxisZProjection", _axisZMaterial, width);

        for (int axis = 0; axis < AxisCount; axis++)
        {
            Material axisMaterial = GetAxisMaterial(axis);
            int arrowBase = axis * ArrowLinesPerAxis;
            _arrowLines[arrowBase] = CreateAxisLine($"Arrow_{axis}_A", axisMaterial, width * 0.9f);
            _arrowLines[arrowBase + 1] = CreateAxisLine($"Arrow_{axis}_B", axisMaterial, width * 0.9f);
        }

        _isVisible = true;
        _visualRoot.SetActive(true);
    }

    public void Hide()
    {
        _isVisible = false;
        _selectedContent = null;
        if (_visualRoot != null)
            _visualRoot.SetActive(false);
    }

    public void Dispose()
    {
        DisposeVisual();
        _contentRoot = null;
        _selectedContent = null;
        _isVisible = false;
    }

    public void SetSelectedContent(Transform content)
    {
        _selectedContent = content;
        if (content == null || _contentRoot == null)
        {
            Hide();
            return;
        }

        if (!_isVisible || _visualRoot == null)
            AttachTo(_contentRoot);

        if (_visualRoot != null)
            _visualRoot.SetActive(true);

        _isVisible = true;
        Refresh();
    }

    public void Refresh()
    {
        if (!_isVisible || _contentRoot == null || _selectedContent == null)
            return;

        float width = ResolveEdgeWidth();
        for (int i = 0; i < AxisCount; i++)
            ApplyWidth(_axisLines[i], width);

        Vector3 localPosition = _contentRoot.InverseTransformPoint(_selectedContent.position);

        Vector3 originLocal = Vector3.zero;
        Vector3 xEnd = new Vector3(localPosition.x, 0f, 0f);
        Vector3 yEnd = new Vector3(0f, localPosition.y, 0f);
        Vector3 zEnd = new Vector3(0f, 0f, localPosition.z);

        UpdateAxisProjection(0, originLocal, xEnd, localPosition.x);
        UpdateAxisProjection(1, originLocal, yEnd, localPosition.y);
        UpdateAxisProjection(2, originLocal, zEnd, localPosition.z);
    }

    private LineRenderer CreateAxisLine(string name, Material material, float width)
    {
        return AuthoringLineVisualUtility.CreateLineRenderer(_visualRoot.transform, name, material, width);
    }

    private Material GetAxisMaterial(int axisIndex)
    {
        switch (axisIndex)
        {
            case 0: return _axisXMaterial;
            case 1: return _axisYMaterial;
            default: return _axisZMaterial;
        }
    }

    private void UpdateAxisProjection(int axisIndex, Vector3 originLocal, Vector3 axisEndLocal, float signedComponent)
    {
        LineRenderer axisLine = _axisLines[axisIndex];
        if (Mathf.Abs(signedComponent) < 0.0005f)
        {
            axisLine.enabled = false;
            SetArrowEnabled(axisIndex, false);
            return;
        }

        axisLine.enabled = true;
        SetWorldSegment(axisLine, originLocal, axisEndLocal);
        UpdateArrowHead(axisIndex, axisEndLocal, GetAxisDirection(axisIndex, Mathf.Sign(signedComponent)));
    }

    private static Vector3 GetAxisDirection(int axisIndex, float sign)
    {
        switch (axisIndex)
        {
            case 0: return new Vector3(sign, 0f, 0f);
            case 1: return new Vector3(0f, sign, 0f);
            default: return new Vector3(0f, 0f, sign);
        }
    }

    private void UpdateArrowHead(int axisIndex, Vector3 tipLocal, Vector3 axisDirection)
    {
        int arrowBase = axisIndex * ArrowLinesPerAxis;
        if (arrowBase + 1 >= _arrowLines.Length)
            return;

        Vector3 forward = axisDirection.normalized;
        Vector3 side = Vector3.Cross(forward, Vector3.up);
        if (side.sqrMagnitude < 1e-6f)
            side = Vector3.Cross(forward, Vector3.right);
        side.Normalize();

        float halfAngle = _arrowHeadAngleDegrees * Mathf.Deg2Rad * 0.5f;
        Vector3 wingA = (-forward * Mathf.Cos(halfAngle) + side * Mathf.Sin(halfAngle)).normalized;
        Vector3 wingB = (-forward * Mathf.Cos(halfAngle) - side * Mathf.Sin(halfAngle)).normalized;
        Vector3 wingEndA = tipLocal + wingA * _arrowHeadLength;
        Vector3 wingEndB = tipLocal + wingB * _arrowHeadLength;

        LineRenderer lineA = _arrowLines[arrowBase];
        LineRenderer lineB = _arrowLines[arrowBase + 1];
        lineA.enabled = true;
        lineB.enabled = true;
        SetWorldSegment(lineA, tipLocal, wingEndA);
        SetWorldSegment(lineB, tipLocal, wingEndB);

        float width = ResolveEdgeWidth();
        ApplyWidth(lineA, width * 0.9f);
        ApplyWidth(lineB, width * 0.9f);
    }

    private void SetArrowEnabled(int axisIndex, bool enabled)
    {
        int arrowBase = axisIndex * ArrowLinesPerAxis;
        if (arrowBase < _arrowLines.Length)
            _arrowLines[arrowBase].enabled = enabled;
        if (arrowBase + 1 < _arrowLines.Length)
            _arrowLines[arrowBase + 1].enabled = enabled;
    }

    private void SetWorldSegment(LineRenderer line, Vector3 localA, Vector3 localB)
    {
        if (line == null || _contentRoot == null)
            return;

        line.positionCount = 2;
        line.SetPosition(0, _contentRoot.TransformPoint(localA));
        line.SetPosition(1, _contentRoot.TransformPoint(localB));
    }

    private float ResolveEdgeWidth()
    {
        float scale = 1f;
        if (_camera != null && _contentRoot != null)
        {
            float distance = Vector3.Distance(_camera.transform.position, _contentRoot.position);
            scale = Mathf.Clamp(distance / _widthDistanceReference, _minWidthScale, _maxWidthScale);
        }

        return _baseEdgeWidth * scale;
    }

    private static void ApplyWidth(LineRenderer line, float width)
    {
        if (line == null)
            return;

        line.startWidth = width;
        line.endWidth = width;
    }

    private void DisposeVisual()
    {
        DestroyMaterial(ref _axisXMaterial);
        DestroyMaterial(ref _axisYMaterial);
        DestroyMaterial(ref _axisZMaterial);

        if (_visualRoot != null)
        {
            Object.Destroy(_visualRoot);
            _visualRoot = null;
        }

        for (int i = 0; i < _axisLines.Length; i++)
            _axisLines[i] = null;
        for (int i = 0; i < _arrowLines.Length; i++)
            _arrowLines[i] = null;
    }

    private static void DestroyMaterial(ref Material material)
    {
        if (material != null)
        {
            Object.Destroy(material);
            material = null;
        }
    }
}
