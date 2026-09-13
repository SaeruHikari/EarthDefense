using System.Runtime.InteropServices;

namespace SkrGui;

[Flags] public enum EVGPathFlattenNodeFlags : byte { None = 0, Corner = 1, DashCutStart = 2, DashCutEnd = 4, Left = 8 }
[Flags] public enum EVGPathFlattenNodeCacheFlags : byte { None = 0, Bevel = 1, InnerBevel = 2, Collinear = 4 }
public struct VGPathFlattenNode
{
    public Offsetf Position, NextDirection, JoinDirection;
    public float ContourDistance, NextLength;
    public EVGPathFlattenNodeFlags Flags;
    public EVGPathFlattenNodeCacheFlags CacheFlags;
}
public struct VGPathFlattenContour
{
    public uint NodeBegin, NodeCount, BevelCount;
    public float TotalLength;
    public bool Closed;
    public EVGPathWinding? WindingHint;
    public bool Convex;
}
public struct VGPathFlattenLine { public Offsetf Position, Direction; }
public struct VGStrokeOptions
{
    public float Width = 1, MiterLimit = 4, PixelRatio = 1, TessellationFactor = 1, AaRadius;
    public EVGStrokeCap Cap;
    public EVGStrokeJoin Join;
    public VGStrokeOptions() { }
}
public struct VGFillOptions
{
    public EVGFillRule FillRule;
    public float PixelRatio = 1, AaRadius, AaMiterLimit = 2.4f;
    public bool UseDelaunay = true, OptimizeForSingleConvex;
    public VGFillOptions() { }
}
public sealed class VGFillAllocator
{
    public object? UserData;
    public Func<object?, ulong, nint>? Alloc;
    public Func<object?, nint, ulong, nint>? Realloc;
    public Action<object?, nint>? Free;
    public static unsafe VGFillAllocator Default() => new()
    {
        UserData = "SkrGuiCore.VGPathFlatten.libtess2",
        Alloc = (user, size) => (nint)NativeMemory.Alloc((nuint)size),
        Realloc = (user, ptr, size) => (nint)NativeMemory.Realloc((void*)ptr, (nuint)size),
        Free = (user, ptr) => NativeMemory.Free((void*)ptr)
    };
}
public readonly struct VGFillVertexKey(float x,float y):IEquatable<VGFillVertexKey>
{
    public readonly float X=x,Y=y;
    public bool Equals(VGFillVertexKey rhs)=>X==rhs.X&&Y==rhs.Y;
    public override bool Equals(object? rhs)=>rhs is VGFillVertexKey key&&Equals(key);
    public override int GetHashCode()=>HashCode.Combine(X,Y);
    public static bool operator==(VGFillVertexKey lhs,VGFillVertexKey rhs)=>lhs.Equals(rhs);
    public static bool operator!=(VGFillVertexKey lhs,VGFillVertexKey rhs)=>!lhs.Equals(rhs);
}
public sealed class VGFillWorkspace
{
    public VGFillAllocator? Allocator;
    public readonly List<float> ScratchContourVertices = [];
    public readonly Dictionary<VGFillVertexKey, uint> VertexLookup = [];
    public void Clear() { ScratchContourVertices.Clear(); VertexLookup.Clear(); }
    public void Release() { ScratchContourVertices.Clear(); ScratchContourVertices.TrimExcess(); VertexLookup.Clear(); VertexLookup.TrimExcess(); }
}
public sealed partial class VGPathFlatten
{
    public VGPathFlatten() {}
    public VGPathFlatten(VGPathFlatten source) { PointEqualsTolerance=source.PointEqualsTolerance;_bounds=source._bounds;_finalized=source._finalized;foreach(var node in source._nodes.AsSpan())_nodes.PushBack(node);foreach(var contour in source._contours.AsSpan())_contours.PushBack(contour); }
    public float PointEqualsTolerance = .01f;
    private const float KEpsilon = .000001f;
    internal readonly VgBuffer<VGPathFlattenNode> _nodes = new();
    internal readonly VgBuffer<VGPathFlattenContour> _contours = new();
    private Rectf _bounds;
    private bool _finalized = true;
}
