namespace SkrGui;

// Source: visual/proxy/visual_transform.hpp + src/visual/proxy/visual_transform.cpp.
public class VisualTransform : VisualProxy
{
    private PaintTransform _transform=new();private Offsetf _origin=new();
    private AlignmentMixed _alignment=SkrGui.Alignment.TopLeft();private ETextDirection _textDirection=ETextDirection.LTR;private bool _transformHitTests=true;
    public ref readonly PaintTransform Transform()=>ref _transform;
    public Offsetf Origin()=>_origin;
    public AlignmentMixed Alignment()=>_alignment;
    public ETextDirection TextDirection()=>_textDirection;
    public bool TransformHitTests()=>_transformHitTests;
    public void SetTransform(PaintTransform value){_transform=value;}
    public void SetOrigin(Offsetf value){_origin=value;}
    public void SetAlignment(AlignmentMixed value){_alignment=value;}
    public void SetTextDirection(ETextDirection value){_textDirection=value;}
    public void SetTransformHitTests(bool value){_transformHitTests=value;}
    public PaintTransform EffectiveTransform()
    {var result=new PaintTransform();var offset=Origin()+Alignment().Resolve(TextDirection()).AlongSize(Size());if(offset!=Offsetf.Zero())result.ApplyOffset2D(offset);result.Apply(_transform);if(offset!=Offsetf.Zero())result.ApplyOffset2D(-offset);return result;}
    public override bool HitTest(VisualHitTestResult result,Offsetf position)=>HitTestChildren(result,position);
    protected override bool HitTestChildren(VisualHitTestResult result,Offsetf position)
    {if(Child() is not {} child)return false;PaintTransform? transform=null;if(_transformHitTests)transform=EffectiveTransform();return result.AddWithPaintTransform(transform,position,(r,p)=>child.HitTest(r,p));}
    protected override void PerformPaint(PaintContext context)
    {if(Child() is not {} child)return;var transform=EffectiveTransform();float determinant=transform.ResolvedTransform().GetDeterminant();if(!float.IsFinite(determinant)||MathF.Abs(determinant)<=GuiConstants.FlutterPrecisionErrorTolerance)return;context.PushTransform(transform);PaintChild(child,context);context.PopTransform();}
}
