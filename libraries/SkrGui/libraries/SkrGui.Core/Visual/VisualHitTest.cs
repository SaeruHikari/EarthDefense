using System.Numerics;
namespace SkrGui;

// Source: SkrGuiCore/visual/visual_hit_test.hpp (611561f8).
public struct VisualHitTestEntry
{
    public VisualNode? Target;
    public Offsetf LocalPosition;
    public Matrix4x4 Transform;
    public VisualHitTestEntry() { Target=null; LocalPosition=new(); Transform=Matrix4x4.Identity; }
}
public sealed class VisualHitTestResult
{
    private readonly List<VisualHitTestEntry> _path=new();
    private readonly List<Matrix4x4> _transforms=new();
    public VisualHitTestResult()=>ResetTransformStack();
    public IReadOnlyList<VisualHitTestEntry> Path()=>_path.AsReadOnly();
    public bool IsEmpty()=>_path.Count==0;
    public ulong NumEntries()=>(ulong)_path.Count;
    public void Add(VisualNode target,Offsetf localPosition) { GuiAssert.Require(target!=null,"Hit test requires a target"); _path.Add(new(){Target=target,LocalPosition=localPosition,Transform=LastTransform()}); }
    public void Clear() { _path.Clear(); ResetTransformStack(); }
    public void PushOffset(Offsetf offset)=>PushTransform(Matrix4x4.CreateTranslation(offset.X,offset.Y,0));
    public void PushTransform(Matrix4x4 transform)=>_transforms.Add(LastTransform()*transform);
    public void PopTransform() { GuiAssert.Require(_transforms.Count>1,"Hit test transform stack underflow"); _transforms.RemoveAt(_transforms.Count-1); }
    public bool AddWithPaintOffset(Offsetf? offset,Offsetf position,Func<VisualHitTestResult,Offsetf,bool> hitTest)
    {
        Offsetf transformedPosition=offset.HasValue?position-offset.Value:position;
        if(offset.HasValue) PushOffset(-offset.Value);
        bool result=hitTest(this,transformedPosition);
        if(offset.HasValue) PopTransform();
        return result;
    }
    public bool AddWithPaintTransform(PaintTransform? transform,Offsetf position,Func<VisualHitTestResult,Offsetf,bool> hitTest)=>AddWithPaintTransform(transform?.ResolvedTransform(),position,hitTest);
    public bool AddWithPaintTransform(Matrix4x4? transform,Offsetf position,Func<VisualHitTestResult,Offsetf,bool> hitTest)
    {
        if(!transform.HasValue) return AddWithRawTransform(null,position,hitTest);
        Matrix4x4 raw=new();
        if(!TryInvert(RemovePerspectiveTransform(transform.Value),ref raw)) return false;
        return AddWithRawTransform(raw,position,hitTest);
    }
    public bool AddWithRawTransform(Matrix4x4? transform,Offsetf position,Func<VisualHitTestResult,Offsetf,bool> hitTest)
    {
        if(!transform.HasValue) return hitTest(this,position);
        Offsetf transformedPosition=new();
        if(!ProjectPosition(transform.Value,position,ref transformedPosition)) return false;
        PushTransform(transform.Value); bool result=hitTest(this,transformedPosition); PopTransform(); return result;
    }
    private static bool TryInvert(Matrix4x4 transform,ref Matrix4x4 inverse)
    {
        float determinant=transform.GetDeterminant();
        if(!float.IsFinite(determinant)||MathF.Abs(determinant)<=GuiConstants.FlutterPrecisionErrorTolerance) return false;
        Matrix4x4.Invert(transform,out inverse); return true;
    }
    private static Matrix4x4 RemovePerspectiveTransform(Matrix4x4 transform)
    { transform.M13=0;transform.M23=0;transform.M31=0;transform.M32=0;transform.M33=1;transform.M34=0;transform.M43=0;return transform; }
    private static bool ProjectPosition(Matrix4x4 transform,Offsetf position,ref Offsetf result)
    {
        var point=Vector4.Transform(new Vector4(position.X,position.Y,0,1),transform);
        if(point.W!=1) { point.X/=point.W;point.Y/=point.W; }
        if(!float.IsFinite(point.X)||!float.IsFinite(point.Y)) return false;
        result=new(point.X,point.Y);return true;
    }
    private void ResetTransformStack() { _transforms.Clear();_transforms.Add(Matrix4x4.Identity); }
    private Matrix4x4 LastTransform() { GuiAssert.Require(_transforms.Count!=0,"Hit test requires a root transform");return _transforms[^1]; }
}
