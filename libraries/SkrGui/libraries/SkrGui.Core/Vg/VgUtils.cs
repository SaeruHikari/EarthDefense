namespace SkrGui;

public struct VGVertex(Offsetf pos, float converge = 0)
{
    public Offsetf Pos = pos;
    public float Converge = converge;
}
public sealed class VGBackend
{
    public Action<ulong, ulong>? Reserve;
    public Action<VGVertex>? PushVertex;
    public Action<uint, uint, uint>? PushTriangle;
    public bool IsValid() => Reserve is not null && PushVertex is not null && PushTriangle is not null;
    public void CallReserve(ulong vertexCount, ulong triangleCount) => Reserve!(vertexCount, triangleCount);
    public void CallPushVertex(VGVertex vertex) => PushVertex!(vertex);
    public void CallPushTriangle(uint i0, uint i1, uint i2) => PushTriangle!(i0, i1, i2);
}
public static class VGAAHelper
{
    public static float ResolveLogicalRadius(float physicalRadius, float pixelRatio)
    {
        if (!float.IsFinite(physicalRadius) || physicalRadius <= 0) return 0;
        return !float.IsFinite(pixelRatio) || pixelRatio <= 0 ? physicalRadius : physicalRadius / pixelRatio;
    }
}
public enum EVGFillRule : byte { NonZero, EvenOdd }
public enum EVGPathWinding : byte { CW, CCW }
public enum EVGStrokeCap : byte { Butt, Square, Round }
public enum EVGStrokeJoin : byte { Miter, Bevel, Round }
public struct VGFillStyle { public EVGFillRule Rule; }
public struct VGDashPattern
{
    public ReadOnlyMemory<float> Values;
    public float Offset;
}
public struct VGStrokeStyle
{
    public float Width = 1, MiterLimit = 10;
    public EVGStrokeCap Cap;
    public EVGStrokeJoin Join;
    public VGDashPattern Dash;
    public VGStrokeStyle() { }
}
