using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Diagnostics;
namespace SkrGui;

internal static unsafe class TessNative
{
    public const int TESS_WINDING_ODD=0,TESS_WINDING_NONZERO=1,TESS_POLYGONS=0,TESS_BOUNDARY_CONTOURS=2,TESS_CONSTRAINED_DELAUNAY_TRIANGULATION=0,TESS_UNDEF=-1;
    public const float TESS_MAX_VALID_INPUT_VALUE=8388608,TESS_MIN_VALID_INPUT_VALUE=-8388608;
    [StructLayout(LayoutKind.Sequential)] public struct Alloc
    {
        public delegate* unmanaged[Cdecl]<nint,uint,nint> Memalloc;
        public delegate* unmanaged[Cdecl]<nint,nint,uint,nint> Memrealloc;
        public delegate* unmanaged[Cdecl]<nint,nint,void> Memfree;
        public nint UserData;
        public int MeshEdgeBucketSize,MeshVertexBucketSize,MeshFaceBucketSize,DictNodeBucketSize,RegionBucketSize,ExtraVertices;
    }
    [DllImport("skrgui_native",CallingConvention=CallingConvention.Cdecl,EntryPoint="tessNewTess")] public static extern void* NewTess(Alloc* alloc);
    [DllImport("skrgui_native",CallingConvention=CallingConvention.Cdecl,EntryPoint="tessDeleteTess")] public static extern void DeleteTess(void* tess);
    [DllImport("skrgui_native",CallingConvention=CallingConvention.Cdecl,EntryPoint="tessAddContour")] public static extern void AddContour(void* tess,int size,void* pointer,int stride,int count);
    [DllImport("skrgui_native",CallingConvention=CallingConvention.Cdecl,EntryPoint="tessSetOption")] public static extern void SetOption(void* tess,int option,int value);
    [DllImport("skrgui_native",CallingConvention=CallingConvention.Cdecl,EntryPoint="tessTesselate")] public static extern int Tesselate(void* tess,int windingRule,int elementType,int polySize,int vertexSize,float* normal);
    [DllImport("skrgui_native",CallingConvention=CallingConvention.Cdecl,EntryPoint="tessGetVertexCount")] public static extern int GetVertexCount(void* tess);
    [DllImport("skrgui_native",CallingConvention=CallingConvention.Cdecl,EntryPoint="tessGetVertices")] public static extern float* GetVertices(void* tess);
    [DllImport("skrgui_native",CallingConvention=CallingConvention.Cdecl,EntryPoint="tessGetElementCount")] public static extern int GetElementCount(void* tess);
    [DllImport("skrgui_native",CallingConvention=CallingConvention.Cdecl,EntryPoint="tessGetElements")] public static extern int* GetElements(void* tess);
}
internal sealed class TessFloatBuffer(List<float> data)
{
    public readonly List<float> Data=data;
    public ulong Size() => (ulong)Data.Count;
    public bool IsEmpty() => Data.Count==0;
    public ref float this[ulong index] => ref CollectionsMarshal.AsSpan(Data)[checked((int)index)];
    public ref float this[int index] => ref CollectionsMarshal.AsSpan(Data)[index];
    public ref float this[uint index] => ref CollectionsMarshal.AsSpan(Data)[checked((int)index)];
    public void Add(float value) => Data.Add(value);
    public void Reserve(ulong count) => Data.EnsureCapacity(checked((int)count));
    public void Clear() => Data.Clear();
    public void RemoveAt(ulong index,ulong count) => Data.RemoveRange(checked((int)index),checked((int)count));
}
internal static unsafe partial class FillTessContext
{
    public const double KAreaEpsilon=.000000001;
    public const float KGeometryEpsilon=.000001f;
    public sealed class TessContour(List<float> values)
    {
        public readonly TessFloatBuffer Vertices=new(values);
        public ulong VertexCount() => Vertices.Size()/2;
    }
    public struct ContourAnalysis { public double SignedArea; public bool HasNonCollinearArea; }
    public struct FillReserveEstimate(ulong vertices,ulong triangles) { public ulong VertexCount=vertices,TriangleCount=triangles; }
    public struct ConvexFillSection(VGFillAANode forward,VGFillAANode backward) { public VGFillAANode Forward=forward,Backward=backward; }
    public readonly record struct TessAllocBuckets(int MeshEdgeBucketSize,int MeshVertexBucketSize,int MeshFaceBucketSize,int DictNodeBucketSize,int RegionBucketSize,int ExtraVertices);
    public sealed class TessHandle : IDisposable
    {
        public void* Tess;
        private GCHandle _allocator;
        public TessHandle(ulong expectedVertexCount,VGFillAllocator allocator)
        {
            _allocator=GCHandle.Alloc(allocator);var bucket=CalcAllocBuckets(expectedVertexCount);
            TessNative.Alloc descriptor=new(){Memalloc=&Memalloc,Memrealloc=&Memrealloc,Memfree=&Memfree,UserData=GCHandle.ToIntPtr(_allocator),MeshEdgeBucketSize=bucket.MeshEdgeBucketSize,MeshVertexBucketSize=bucket.MeshVertexBucketSize,MeshFaceBucketSize=bucket.MeshFaceBucketSize,DictNodeBucketSize=bucket.DictNodeBucketSize,RegionBucketSize=bucket.RegionBucketSize,ExtraVertices=bucket.ExtraVertices};
            Tess=TessNative.NewTess(&descriptor);
        }
        public bool IsValid() => Tess!=null;
        public void Dispose() { if(Tess!=null){TessNative.DeleteTess(Tess);Tess=null;}if(_allocator.IsAllocated)_allocator.Free(); }
        [UnmanagedCallersOnly(CallConvs=[typeof(CallConvCdecl)])] private static nint Memalloc(nint user,uint size) {var allocator=(VGFillAllocator)GCHandle.FromIntPtr(user).Target!;return allocator.Alloc!(allocator.UserData,size);}
        [UnmanagedCallersOnly(CallConvs=[typeof(CallConvCdecl)])] private static nint Memrealloc(nint user,nint ptr,uint size) {var allocator=(VGFillAllocator)GCHandle.FromIntPtr(user).Target!;return allocator.Realloc!(allocator.UserData,ptr,size);}
        [UnmanagedCallersOnly(CallConvs=[typeof(CallConvCdecl)])] private static void Memfree(nint user,nint ptr) {var allocator=(VGFillAllocator)GCHandle.FromIntPtr(user).Target!;allocator.Free!(allocator.UserData,ptr);}
    }
    public static VGFillAllocator ResolveAllocator(VGFillWorkspace workspace)
    {
        var value=workspace.Allocator;if(value is null)return VGFillAllocator.Default();
        if(value.Alloc is null||value.Realloc is null||value.Free is null){Debug.Assert(false,"Allocator requires all three callbacks");return VGFillAllocator.Default();}
        return new VGFillAllocator { UserData=value.UserData,Alloc=value.Alloc,Realloc=value.Realloc,Free=value.Free };
    }
    public static int ClampExtraVertices(ulong value) => (int)CppMath.Min(value,4096UL);
    public static TessAllocBuckets CalcAllocBuckets(ulong count)
    {
        if(count<=64)return new(128,128,64,128,64,8);
        if(count<=512)return new(512,512,256,512,256,ClampExtraVertices(CppMath.Max(8UL,count/8)));
        if(count<=2048)return new(1024,1024,512,1024,512,ClampExtraVertices(CppMath.Max(32UL,count/6)));
        if(count<=8192)return new(2048,2048,1024,2048,1024,ClampExtraVertices(CppMath.Max(128UL,count/4)));
        return new(4096,4096,2048,4096,2048,4096);
    }
    public static TessHandle MakeTess(ulong expectedVertexCount,VGFillAllocator allocator) => new(expectedVertexCount,allocator);
    public static void AddContour(void* tess,TessContour contour)
    {
        fixed(float* vertices=CollectionsMarshal.AsSpan(contour.Vertices.Data)) TessNative.AddContour(tess,2,vertices,sizeof(float)*2,(int)contour.VertexCount());
    }
    public static uint FindOrPushInnerVertex(VGMeshEmitter emitter,Dictionary<VGFillVertexKey,uint> vertexLookup,Offsetf point)
    {
        var key=VertexKey(point);if(vertexLookup.TryGetValue(key,out uint index))return index;
        index=emitter.PushVertex(point,1);vertexLookup.TryAdd(key,index);return index;
    }
}
