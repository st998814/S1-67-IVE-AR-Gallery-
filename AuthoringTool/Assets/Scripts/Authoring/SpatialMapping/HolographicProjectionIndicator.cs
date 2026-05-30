using UnityEngine;

/// <summary>
/// Subtle target-to-content holographic projection frustum (authoring-only, no placement side effects).
/// </summary>
public sealed class HolographicProjectionIndicator
{
    private const int FrustumEdgeCount = 4;
    private const int FaceEdgeCount = 4;
    private const int FrustumFaceCount = 4;

    private readonly Color _hologramColor;
    private readonly Color _edgeColor;
    private readonly Color _faceEdgeColor;
    private readonly float _baseEdgeWidth;
    private readonly float _dashTextureScale;
    private Transform _fillSpace;

    private Transform _anchorRoot;
    private Transform _targetRoot;
    private Transform _targetVisual;
    private Transform _contentRoot;
    private Transform _selectedContent;
    private Camera _camera;
    private GameObject _visualRoot;
    private Material _edgeMaterial;
    private Material _faceEdgeMaterial;
    private Material _hologramFillMaterial;
    private Texture2D _dashTexture;
    private MeshFilter _fillMeshFilter;
    private MeshRenderer _fillMeshRenderer;
    private Mesh _fillMesh;
    private readonly LineRenderer[] _frustumEdges = new LineRenderer[FrustumEdgeCount];
    private readonly LineRenderer[] _targetFaceEdges = new LineRenderer[FaceEdgeCount];
    private readonly LineRenderer[] _contentFaceEdges = new LineRenderer[FaceEdgeCount];
    private readonly Vector3[] _targetCorners = new Vector3[4];
    private readonly Vector3[] _contentCorners = new Vector3[4];
    private Vector3[] _cornerScratch = new Vector3[4];
    private bool _isVisible;

    public HolographicProjectionIndicator(
        Color hologramColor,
        float baseEdgeWidth = 0.006f,
        float dashTextureScale = 2.4f)
    {
        _hologramColor = hologramColor;
        _edgeColor = ScaleAlpha(hologramColor, Mathf.Min(1f, hologramColor.a + 0.35f));
        _faceEdgeColor = ScaleAlpha(hologramColor, Mathf.Min(1f, hologramColor.a + 0.15f));
        _baseEdgeWidth = baseEdgeWidth;
        _dashTextureScale = dashTextureScale;
    }

    public bool IsAttached => _anchorRoot != null && _visualRoot != null;

    public void SetCamera(Camera camera) => _camera = camera;

    public void AttachTo(Transform anchorRoot, Transform targetRoot, Transform targetVisual, Transform contentRoot)
    {
        if (anchorRoot == null || contentRoot == null)
        {
            Hide();
            return;
        }

        if (_anchorRoot == anchorRoot
            && _targetRoot == targetRoot
            && _targetVisual == targetVisual
            && _contentRoot == contentRoot
            && _visualRoot != null)
            return;

        DisposeVisual();
        _anchorRoot = anchorRoot;
        _targetRoot = targetRoot;
        _targetVisual = targetVisual;
        _contentRoot = contentRoot;

        _visualRoot = new GameObject("HolographicProjection");
        _visualRoot.transform.SetParent(anchorRoot, false);
        _visualRoot.transform.localPosition = Vector3.zero;
        _visualRoot.transform.localRotation = Quaternion.identity;
        _visualRoot.transform.localScale = Vector3.one;

        _dashTexture = AuthoringLineVisualUtility.GetOrCreateDashTexture(4, 3);
        _edgeMaterial = AuthoringLineVisualUtility.CreateLineMaterial(_edgeColor);
        _hologramFillMaterial = AuthoringHologramMaterialUtility.CreateHologramFillMaterial(_hologramColor);

        BuildLineRenderers();
        BuildFillMeshObject();

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
        _anchorRoot = null;
        _targetRoot = null;
        _targetVisual = null;
        _contentRoot = null;
        _selectedContent = null;
        _isVisible = false;
    }

    public void SetSelectedContent(Transform content)
    {
        _selectedContent = content;
        if (content == null)
        {
            Hide();
            return;
        }

        if (!_isVisible || _visualRoot == null)
            AttachTo(_anchorRoot, _targetRoot, _targetVisual, _contentRoot);

        if (_visualRoot != null)
            _visualRoot.SetActive(true);

        _isVisible = true;
        Refresh();
    }

    public void Refresh()
    {
        if (!_isVisible || _anchorRoot == null || _contentRoot == null || _selectedContent == null)
            return;

        if (!TryResolveTargetCorners(out Vector3 targetCenter) || !TryResolveContentCorners(out Vector3 contentCenter))
        {
            SetAllLinesEnabled(false);
            if (_fillMeshRenderer != null)
                _fillMeshRenderer.enabled = false;
            return;
        }

        float edgeWidth = ResolveEdgeWidth((targetCenter + contentCenter) * 0.5f);
        ApplyEdgeWidths(edgeWidth);
        UpdateFrustumEdges();
        UpdateFillMesh();
        AnimateHologramMaterial();
    }

    private void BuildLineRenderers()
    {
        for (int i = 0; i < FrustumEdgeCount; i++)
        {
            _frustumEdges[i] = AuthoringLineVisualUtility.CreateLineRenderer(
                _visualRoot.transform,
                $"FrustumEdge_{i:00}",
                _edgeMaterial,
                _baseEdgeWidth,
                useDashedTexture: false);
        }

        _faceEdgeMaterial = AuthoringLineVisualUtility.CreateLineMaterial(_faceEdgeColor, _dashTexture);
        for (int i = 0; i < FaceEdgeCount; i++)
        {
            _targetFaceEdges[i] = AuthoringLineVisualUtility.CreateLineRenderer(
                _visualRoot.transform,
                $"TargetFace_{i:00}",
                _faceEdgeMaterial,
                _baseEdgeWidth * 0.75f,
                useDashedTexture: true,
                dashTextureScale: _dashTextureScale);

            _contentFaceEdges[i] = AuthoringLineVisualUtility.CreateLineRenderer(
                _visualRoot.transform,
                $"ContentFace_{i:00}",
                _faceEdgeMaterial,
                _baseEdgeWidth * 0.75f,
                useDashedTexture: true,
                dashTextureScale: _dashTextureScale);
        }
    }

    private void BuildFillMeshObject()
    {
        var fillObject = new GameObject("HologramFill");
        fillObject.transform.SetParent(_visualRoot.transform, false);
        _fillSpace = fillObject.transform;
        _fillMeshFilter = fillObject.AddComponent<MeshFilter>();
        _fillMeshRenderer = fillObject.AddComponent<MeshRenderer>();
        _fillMeshRenderer.sharedMaterial = _hologramFillMaterial;
        _fillMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _fillMeshRenderer.receiveShadows = false;
        _fillMesh = new Mesh { name = "HolographicProjectionFill" };
        _fillMeshFilter.sharedMesh = _fillMesh;
    }

    private bool TryResolveTargetCorners(out Vector3 center)
    {
        center = Vector3.zero;
        Transform boundsRoot = _targetVisual != null ? _targetVisual : _targetRoot;
        if (boundsRoot == null)
            return false;

        if (!AuthoringBoundsVisualUtility.TryGetWorldBounds(boundsRoot, out Bounds bounds))
            return false;

        center = bounds.center;
        Vector3 toward = _selectedContent != null ? _selectedContent.position : center + Vector3.forward;
        if (!AuthoringBoundsVisualUtility.TryGetFaceCornersTowardPoint(bounds, toward, out _cornerScratch))
            return false;

        CopyCorners(_cornerScratch, _targetCorners);
        return true;
    }

    private bool TryResolveContentCorners(out Vector3 center)
    {
        center = Vector3.zero;
        if (_selectedContent == null)
            return false;

        if (!AuthoringBoundsVisualUtility.TryGetWorldBounds(_selectedContent, out Bounds bounds))
            return false;

        center = bounds.center;
        Vector3 toward = _targetVisual != null ? _targetVisual.position : center - Vector3.forward;
        if (!AuthoringBoundsVisualUtility.TryGetFaceCornersTowardPoint(bounds, toward, out _cornerScratch))
            return false;

        CopyCorners(_cornerScratch, _contentCorners);
        return true;
    }

    private void UpdateFrustumEdges()
    {
        for (int i = 0; i < FrustumEdgeCount; i++)
        {
            int next = (i + 1) % FrustumEdgeCount;
            SetWorldSegment(_frustumEdges[i], _targetCorners[i], _contentCorners[i]);
            SetWorldSegment(_targetFaceEdges[i], _targetCorners[i], _targetCorners[next]);
            SetWorldSegment(_contentFaceEdges[i], _contentCorners[i], _contentCorners[next]);
        }
    }

    private void UpdateFillMesh()
    {
        if (_fillMesh == null || _fillMeshRenderer == null || _fillSpace == null)
            return;

        var vertices = new Vector3[FrustumFaceCount * 4];
        var triangles = new int[FrustumFaceCount * 6];
        var uvs = new Vector2[vertices.Length];

        for (int face = 0; face < FrustumFaceCount; face++)
        {
            int next = (face + 1) % FrustumFaceCount;
            int vertexBase = face * 4;
            vertices[vertexBase + 0] = _fillSpace.InverseTransformPoint(_targetCorners[face]);
            vertices[vertexBase + 1] = _fillSpace.InverseTransformPoint(_targetCorners[next]);
            vertices[vertexBase + 2] = _fillSpace.InverseTransformPoint(_contentCorners[next]);
            vertices[vertexBase + 3] = _fillSpace.InverseTransformPoint(_contentCorners[face]);

            int triangleBase = face * 6;
            triangles[triangleBase + 0] = vertexBase + 0;
            triangles[triangleBase + 1] = vertexBase + 1;
            triangles[triangleBase + 2] = vertexBase + 2;
            triangles[triangleBase + 3] = vertexBase + 0;
            triangles[triangleBase + 4] = vertexBase + 2;
            triangles[triangleBase + 5] = vertexBase + 3;

            uvs[vertexBase + 0] = new Vector2(0f, 0f);
            uvs[vertexBase + 1] = new Vector2(1f, 0f);
            uvs[vertexBase + 2] = new Vector2(1f, 1f);
            uvs[vertexBase + 3] = new Vector2(0f, 1f);
        }

        _fillMesh.Clear();
        _fillMesh.vertices = vertices;
        _fillMesh.triangles = triangles;
        _fillMesh.uv = uvs;
        _fillMesh.RecalculateBounds();
        _fillMesh.RecalculateNormals();
        _fillMeshRenderer.enabled = true;
    }

    private void AnimateHologramMaterial()
    {
        if (_hologramFillMaterial == null)
            return;

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 0.55f);
        Color animated = _hologramColor;
        animated.a = Mathf.Clamp01(_hologramColor.a * (0.94f + pulse * 0.12f));
        AuthoringHologramMaterialUtility.ApplyAnimatedHologramProperties(_hologramFillMaterial, animated);
    }

    private void SetWorldSegment(LineRenderer line, Vector3 worldA, Vector3 worldB)
    {
        if (line == null)
            return;

        line.enabled = true;
        line.positionCount = 2;
        line.SetPosition(0, worldA);
        line.SetPosition(1, worldB);

        if (line.textureMode == LineTextureMode.Tile)
        {
            float length = Vector3.Distance(worldA, worldB);
            line.textureScale = new Vector2(Mathf.Max(0.4f, length * _dashTextureScale * 0.55f), 1f);
        }
    }

    private static void CopyCorners(Vector3[] source, Vector3[] destination)
    {
        for (int i = 0; i < 4; i++)
            destination[i] = source[i];
    }

    private void SetAllLinesEnabled(bool enabled)
    {
        for (int i = 0; i < FrustumEdgeCount; i++)
        {
            if (_frustumEdges[i] != null)
                _frustumEdges[i].enabled = enabled;
            if (_targetFaceEdges[i] != null)
                _targetFaceEdges[i].enabled = enabled;
            if (_contentFaceEdges[i] != null)
                _contentFaceEdges[i].enabled = enabled;
        }
    }

    private void ApplyEdgeWidths(float width)
    {
        for (int i = 0; i < FrustumEdgeCount; i++)
        {
            AuthoringLineVisualUtility.ApplyWidth(_frustumEdges[i], width);
            AuthoringLineVisualUtility.ApplyWidth(_targetFaceEdges[i], width * 0.8f);
            AuthoringLineVisualUtility.ApplyWidth(_contentFaceEdges[i], width * 0.8f);
        }
    }

    private float ResolveEdgeWidth(Vector3 worldCenter)
    {
        return AuthoringLineVisualUtility.ComputeDistanceScaledWidth(
            _camera,
            worldCenter,
            _baseEdgeWidth,
            minScale: 1.15f,
            maxScale: 3.5f,
            referenceDistance: 1.2f);
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

        if (_faceEdgeMaterial != null)
        {
            Object.Destroy(_faceEdgeMaterial);
            _faceEdgeMaterial = null;
        }

        if (_hologramFillMaterial != null)
        {
            Object.Destroy(_hologramFillMaterial);
            _hologramFillMaterial = null;
        }

        if (_fillMesh != null)
        {
            Object.Destroy(_fillMesh);
            _fillMesh = null;
        }

        if (_visualRoot != null)
        {
            Object.Destroy(_visualRoot);
            _visualRoot = null;
        }

        _fillSpace = null;
        _fillMeshFilter = null;
        _fillMeshRenderer = null;
        for (int i = 0; i < FrustumEdgeCount; i++)
        {
            _frustumEdges[i] = null;
            _targetFaceEdges[i] = null;
            _contentFaceEdges[i] = null;
        }
    }
}
