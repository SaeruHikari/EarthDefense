namespace SkrGui;

// Source: SkrGuiCore/batch/mesh.hpp. MeshIndex remains uint.
public struct MeshVertex
{
    public Offsetf Pos, Uv;
    public uint PackedColor;
    public MeshVertex(Offsetf pos, Offsetf uv, uint packedColor) { Pos = pos; Uv = uv; PackedColor = packedColor; }
}
public sealed class Mesh
{
    public readonly List<MeshVertex> Vertices = [];
    public readonly List<uint> Indices = [];
    public void PushTriangle(uint i0, uint i1, uint i2) { Indices.Add(i0); Indices.Add(i1); Indices.Add(i2); }
    public void PushQuad(uint i0, uint i1, uint i2, uint i3) { PushTriangle(i0, i1, i2); PushTriangle(i0, i2, i3); }
}

public struct BasicMeshOptions
{
    public float PixelRatio = 1, TessellationFactor = 1, AaRadius = 1;
    public bool PremultiplyColor = true;
    public Rectf UvRect;
    public VGPath? ReusePath;
    public VGPathFlatten? ReuseFlattenPath;
    public VGFillWorkspace? ReuseFillWorkspace;
    public BasicMeshOptions() { }
}
