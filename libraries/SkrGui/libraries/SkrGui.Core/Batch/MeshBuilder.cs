namespace SkrGui;

// Source: SkrGuiCore/batch/mesh.hpp, MeshBuilder.
public sealed class MeshBuilder
{
    private readonly Mesh _mesh;
    private Rectf _boundRect;
    private Rectf? _uvRect;
    private ColorGradient? _gradient;
    private bool _premultiplyColor;
    private ulong _vertexStart, _indexStart;
    public MeshBuilder(Mesh mesh, Rectf boundRect = default, Rectf? uvRect = null, ColorGradient? gradient = null, bool premultiplyColor = true)
    { _mesh = mesh; _boundRect = boundRect; _uvRect = uvRect; _gradient = gradient; _premultiplyColor = premultiplyColor; RefreshStart(); }
    public Mesh Mesh() => _mesh;
    public Rectf BoundRect() => _boundRect;
    public Rectf? UvRect() => _uvRect;
    public ColorGradient? Gradient() => _gradient;
    public bool PremultiplyColor() => _premultiplyColor;
    public ulong VertexStart() => _vertexStart;
    public ulong IndexStart() => _indexStart;
    public uint MeshVertexStart() => (uint)_vertexStart;
    public void SetBoundRect(Rectf rect) => _boundRect = rect;
    public void SetUvRect(Rectf? rect) => _uvRect = rect;
    public void SetGradient(ColorGradient? gradient) => _gradient = gradient;
    public void SetPremultiplyColor(bool value) => _premultiplyColor = value;
    public void RefreshStart() { _vertexStart = (ulong)_mesh.Vertices.Count; _indexStart = (ulong)_mesh.Indices.Count; }
    public void RollbackToStart() { _mesh.Vertices.RemoveRange((int)_vertexStart, _mesh.Vertices.Count - (int)_vertexStart); _mesh.Indices.RemoveRange((int)_indexStart, _mesh.Indices.Count - (int)_indexStart); }
    public void Reserve(ulong vertexCount, ulong triangleCount)
    { _mesh.Vertices.EnsureCapacity(checked(_mesh.Vertices.Count + (int)vertexCount)); _mesh.Indices.EnsureCapacity(checked(_mesh.Indices.Count + (int)(triangleCount * 3))); }
    public uint PushVertex(Offsetf position, float coverage = 1) => PushVertex(position, MapUv(position), MapColor(position, coverage), 1);
    public uint PushVertex(Offsetf position, SRGBColor color, float coverage = 1) => PushVertex(position, MapUv(position), color, coverage);
    public uint PushVertex(Offsetf position, Offsetf uv, SRGBColor color, float coverage = 1)
    {
        var meshColor = ApplyCoverage(color, coverage); if (_premultiplyColor) meshColor = meshColor.Premultiplied();
        return PushVertexRaw(position, uv, meshColor.ToRgba32());
    }
    public uint PushVertexRaw(Offsetf position, Offsetf uv, uint packedColor)
    { uint index = (uint)_mesh.Vertices.Count; _mesh.Vertices.Add(new MeshVertex(position, uv, packedColor)); return index; }
    public void PushTriangle(uint i0, uint i1, uint i2) => _mesh.PushTriangle(i0, i1, i2);
    public void PushQuad(uint i0, uint i1, uint i2, uint i3) => _mesh.PushQuad(i0, i1, i2, i3);
    public void PushLocalTriangle(uint i0, uint i1, uint i2) { uint begin = MeshVertexStart(); PushTriangle(begin + i0, begin + i1, begin + i2); }
    public void PushLocalQuad(uint i0, uint i1, uint i2, uint i3) { PushLocalTriangle(i0, i1, i2); PushLocalTriangle(i0, i2, i3); }
    public VGBackend MakeVgBackend() => new()
    {
        Reserve = Reserve,
        PushVertex = vertex => PushVertex(vertex.Pos, vertex.Converge),
        PushTriangle = PushLocalTriangle
    };
    private Offsetf MapUv(Offsetf position)
    {
        if (_uvRect is not { } uv || !_boundRect.IsFinite() || _boundRect.IsEmpty()) return Offsetf.Zero();
        float u = CppMath.Clamp((position.X - _boundRect.Left) / _boundRect.Width(), 0, 1), v = CppMath.Clamp((position.Y - _boundRect.Top) / _boundRect.Height(), 0, 1);
        return new Offsetf(uv.Left + uv.Width() * u, uv.Top + uv.Height() * v);
    }
    private SRGBColor MapColor(Offsetf position, float coverage) => ApplyCoverage(_gradient is null ? new SRGBColor(1, 1, 1, 1) : _gradient.Sample(_boundRect, position), coverage);
    private static SRGBColor ApplyCoverage(SRGBColor color, float coverage) { color.A *= CppMath.Clamp(coverage, 0, 1); return color; }
}
