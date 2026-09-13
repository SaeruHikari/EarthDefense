namespace SkrGui;

// Source: visual/proxy/visual_fitted.hpp + src/visual/proxy/visual_fitted.cpp.
public class VisualFitted : VisualProxy
{
    private EBoxFit _fit=EBoxFit.Contain;
    private AlignmentMixed _alignment=SkrGui.Alignment.Center();
    private ETextDirection _textDirection=ETextDirection.LTR;
    private EClipBehavior _clipBehavior=EClipBehavior.None;
    public EBoxFit Fit()=>_fit;
    public AlignmentMixed Alignment()=>_alignment;
    public ETextDirection TextDirection()=>_textDirection;
    public Alignment ResolvedAlignment()=>_alignment.Resolve(_textDirection);
    public EClipBehavior ClipBehavior()=>_clipBehavior;
    public void SetFit(EBoxFit value){if(_fit==value)return;_fit=value;MarkNeedsLayout();}
    public void SetAlignment(AlignmentMixed value){if(_alignment==value)return;_alignment=value;MarkNeedsLayout();}
    public void SetTextDirection(ETextDirection value){if(_textDirection==value)return;_textDirection=value;MarkNeedsLayout();}
    public void SetClipBehavior(EClipBehavior value){if(_clipBehavior==value)return;_clipBehavior=value;}
    private struct PaintData { public Rectf SourceRect,DestinationRect;public Offsetf Scale;public bool HasVisualOverflow;public PaintData(){SourceRect=new();DestinationRect=new();Scale=new(1,1);HasVisualOverflow=false;} }
    protected override void PerformPaint(PaintContext context)
    {
        if(Child() is not {} child||Size().IsEmpty()||child.Size().IsEmpty())return;
        var data=ComputePaintData(child.Size(),Size());if(data.SourceRect.IsEmpty()||data.DestinationRect.IsEmpty())return;
        bool clip=data.HasVisualOverflow&&_clipBehavior!=EClipBehavior.None;if(clip)context.PushClipRect(Rectf.OffsetSize(Offsetf.Zero(),Size()));
        var transform=new PaintTransform();transform.ApplyOffset2D(data.DestinationRect.TopLeft());transform.ApplyScale2D(data.Scale);transform.ApplyOffset2D(-data.SourceRect.TopLeft());
        context.PushTransform(transform);PaintChild(child,context);context.PopTransform();if(clip)context.PopClip();
    }
    protected override bool HitTestChildren(VisualHitTestResult result,Offsetf position)
    {
        if(Child() is not {} child||Size().IsEmpty()||child.Size().IsEmpty())return false;
        var data=ComputePaintData(child.Size(),Size());if(data.SourceRect.IsEmpty()||data.DestinationRect.IsEmpty())return false;
        if(data.HasVisualOverflow&&_clipBehavior!=EClipBehavior.None&&!Rectf.OffsetSize(Offsetf.Zero(),Size()).Contains(position))return false;
        var transform=new PaintTransform();transform.ApplyOffset2D(data.DestinationRect.TopLeft());transform.ApplyScale2D(data.Scale);transform.ApplyOffset2D(-data.SourceRect.TopLeft());
        return result.AddWithPaintTransform(transform,position,(r,p)=>child.HitTest(r,p));
    }
    protected override void PerformLayout(){if(Child() is {} child){LayoutChild(child,new BoxConstraints());SetSize(ComputeFittedSize(Constraints(),child.Size()));return;}SetSize(Constraints().Smallest());}
    protected override Sizef ComputeDryLayout(BoxConstraints constraints)=>Child() is {} child?ComputeFittedSize(constraints,child.GetDryLayout(new BoxConstraints())):constraints.Smallest();
    protected override float? ComputeDryBaseline(BoxConstraints constraints,ETextBaseline baseline)=>Child()?.GetDryBaseline(new BoxConstraints(),baseline);
    private Sizef ComputeFittedSize(BoxConstraints constraints,Sizef childSize)
    {
        switch(_fit)
        {
            case EBoxFit.ScaleDown:var sizeConstraints=constraints.Loosen();var unconstrained=sizeConstraints.ConstrainKeepAspect(childSize);return constraints.Constrain(unconstrained);
            case EBoxFit.Fill:case EBoxFit.Contain:case EBoxFit.Cover:case EBoxFit.FitWidth:case EBoxFit.FitHeight:case EBoxFit.None:return constraints.ConstrainKeepAspect(childSize);
            default:throw new InvalidOperationException("Unreachable box fit");
        }
    }
    private PaintData ComputePaintData(Sizef childSize,Sizef outputSize)
    {
        var fitted=GuiMath.ApplyBoxFit(_fit,childSize,outputSize);if(fitted.Source.IsEmpty()||fitted.Destination.IsEmpty())return new();
        var alignment=ResolvedAlignment();var sourceRect=alignment.Inscribe(fitted.Source,Rectf.OffsetSize(Offsetf.Zero(),childSize));var destinationRect=alignment.Inscribe(fitted.Destination,Rectf.OffsetSize(Offsetf.Zero(),outputSize));
        return new(){SourceRect=sourceRect,DestinationRect=destinationRect,Scale=new(fitted.Destination.Width/fitted.Source.Width,fitted.Destination.Height/fitted.Source.Height),HasVisualOverflow=sourceRect.Width()<childSize.Width||sourceRect.Height()<childSize.Height};
    }
}
