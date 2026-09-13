namespace SkrGui;

// Sources: visual/shifted/visual_shifted.*, visual_aligning_shifted.*, visual_positioned.*.
public abstract class VisualShifted : VisualSingleChild
{
    protected Offsetf _childOffset=new();
    public Offsetf ChildOffset()=>_childOffset;
    protected override void PerformPaint(PaintContext context)=>PaintChild(Child(),context,ChildOffset());
    protected override bool HitTestChildren(VisualHitTestResult result,Offsetf position)=>HitTestChild(result,Child(),position,ChildOffset());
    protected override float ComputeMinIntrinsicWidth(float height)=>Child()?.GetMinIntrinsicWidth(height)??0;
    protected override float ComputeMaxIntrinsicWidth(float height)=>Child()?.GetMaxIntrinsicWidth(height)??0;
    protected override float ComputeMinIntrinsicHeight(float width)=>Child()?.GetMinIntrinsicHeight(width)??0;
    protected override float ComputeMaxIntrinsicHeight(float width)=>Child()?.GetMaxIntrinsicHeight(width)??0;
    protected override Sizef ComputeDryLayout(BoxConstraints constraints)=>Child()?.GetDryLayout(constraints)??ComputeSizeForNoChild(constraints);
    protected virtual Sizef ComputeSizeForNoChild(BoxConstraints constraints)=>constraints.Smallest();
    protected override float? ComputeDryBaseline(BoxConstraints constraints,ETextBaseline baseline)=>Child() is {} child?child.GetDryBaseline(constraints,baseline):base.ComputeDryBaseline(constraints,baseline);
    protected override float? ComputeDistanceToActualBaseline(ETextBaseline baseline)
    {if(Child() is not {} child)return base.ComputeDistanceToActualBaseline(baseline);float? result=child.GetDistanceToActualBaseline(baseline);return result.HasValue?result.Value+_childOffset.Y:base.ComputeDistanceToActualBaseline(baseline);}
}
public abstract class VisualAligningShifted : VisualShifted
{
    private AlignmentMixed _alignment=SkrGui.Alignment.Center();private ETextDirection _textDirection=ETextDirection.LTR;
    public AlignmentMixed Alignment()=>_alignment;
    public ETextDirection TextDirection()=>_textDirection;
    public Alignment ResolvedAlignment()=>_alignment.Resolve(_textDirection);
    public void SetAlignment(AlignmentMixed value){if(_alignment==value)return;_alignment=value;MarkNeedsLayout();}
    public void SetTextDirection(ETextDirection value){if(_textDirection==value)return;_textDirection=value;MarkNeedsLayout();}
    protected void AlignChild(){GuiAssert.Require(Child()!=null,"AlignChild requires child");var delta=Size()-Child()!.Size();_childOffset=ResolvedAlignment().AlongOffset(new Offsetf(delta.Width,delta.Height));}
}
public class VisualPositioned : VisualAligningShifted
{
    private float? _widthFactor,_heightFactor;
    public float? WidthFactor()=>_widthFactor;
    public float? HeightFactor()=>_heightFactor;
    public void SetWidthFactor(float? value){GuiAssert.Require(!value.HasValue||value.Value>=0,"Width factor must be null or nonnegative");if(_widthFactor==value)return;_widthFactor=value;MarkNeedsLayout();}
    public void SetHeightFactor(float? value){GuiAssert.Require(!value.HasValue||value.Value>=0,"Height factor must be null or nonnegative");if(_heightFactor==value)return;_heightFactor=value;MarkNeedsLayout();}
    public void ClearWidthFactor()=>SetWidthFactor(null);
    public void ClearHeightFactor()=>SetHeightFactor(null);
    private bool ShrinkWrapWidth(BoxConstraints constraints)=>_widthFactor.HasValue||!constraints.HasBoundedWidth();
    private bool ShrinkWrapHeight(BoxConstraints constraints)=>_heightFactor.HasValue||!constraints.HasBoundedHeight();
    private Sizef ComputeSize(BoxConstraints constraints,Sizef childSize)=>constraints.Constrain(ShrinkWrapWidth(constraints)?childSize.Width*(_widthFactor??1):float.PositiveInfinity,ShrinkWrapHeight(constraints)?childSize.Height*(_heightFactor??1):float.PositiveInfinity);
    protected override void PerformLayout(){if(Child() is {} child){LayoutChild(child,Constraints().Loosen());SetSize(ComputeSize(Constraints(),child.Size()));AlignChild();return;}_childOffset=Offsetf.Zero();SetSize(ComputeSizeForNoChild(Constraints()));}
    protected override float ComputeMinIntrinsicWidth(float height)=>base.ComputeMinIntrinsicWidth(height)*(_widthFactor??1);
    protected override float ComputeMaxIntrinsicWidth(float height)=>base.ComputeMaxIntrinsicWidth(height)*(_widthFactor??1);
    protected override float ComputeMinIntrinsicHeight(float width)=>base.ComputeMinIntrinsicHeight(width)*(_heightFactor??1);
    protected override float ComputeMaxIntrinsicHeight(float width)=>base.ComputeMaxIntrinsicHeight(width)*(_heightFactor??1);
    protected override Sizef ComputeDryLayout(BoxConstraints constraints)=>Child() is {} child?ComputeSize(constraints,child.GetDryLayout(constraints.Loosen())):ComputeSizeForNoChild(constraints);
    protected override Sizef ComputeSizeForNoChild(BoxConstraints constraints)=>constraints.Constrain(ShrinkWrapWidth(constraints)?0:float.PositiveInfinity,ShrinkWrapHeight(constraints)?0:float.PositiveInfinity);
    protected override float? ComputeDryBaseline(BoxConstraints constraints,ETextBaseline baseline)
    {if(Child() is not {} child)return null;var childConstraints=constraints.Loosen();float? result=child.GetDryBaseline(childConstraints,baseline);if(!result.HasValue)return null;var childSize=child.GetDryLayout(childConstraints);var boxSize=ComputeSize(constraints,childSize);var delta=boxSize-childSize;var childOffset=ResolvedAlignment().AlongOffset(new Offsetf(delta.Width,delta.Height));return result.Value+childOffset.Y;}
}
public class VisualPadding : VisualShifted
{
    private EdgeInsetsMixed _padding=new();private ETextDirection _textDirection=ETextDirection.LTR;
    public EdgeInsetsMixed Padding()=>_padding;
    public ETextDirection TextDirection()=>_textDirection;
    public EdgeInsets ResolvedPadding()=>_padding.Resolve(_textDirection);
    public void SetPadding(EdgeInsetsMixed value){GuiAssert.Require(value.IsNonNegative(),"Padding must be nonnegative");if(_padding==value)return;_padding=value;MarkNeedsLayout();}
    public void SetTextDirection(ETextDirection value){if(_textDirection==value)return;_textDirection=value;MarkNeedsLayout();}
    protected override void PerformLayout(){var padding=ResolvedPadding();if(Child() is {} child){LayoutChild(child,Constraints().Deflate(padding));_childOffset=padding.TopLeft();SetSize(Constraints().Constrain(padding.InflateSize(child.Size())));return;}_childOffset=Offsetf.Zero();SetSize(Constraints().Constrain(padding.CollapsedSize()));}
    protected override float ComputeMinIntrinsicWidth(float height){var padding=ResolvedPadding();return Child() is {} child?child.GetMinIntrinsicWidth(CppMath.Max(0,height-padding.Vertical()))+padding.Horizontal():padding.Horizontal();}
    protected override float ComputeMaxIntrinsicWidth(float height){var padding=ResolvedPadding();return Child() is {} child?child.GetMaxIntrinsicWidth(CppMath.Max(0,height-padding.Vertical()))+padding.Horizontal():padding.Horizontal();}
    protected override float ComputeMinIntrinsicHeight(float width){var padding=ResolvedPadding();return Child() is {} child?child.GetMinIntrinsicHeight(CppMath.Max(0,width-padding.Horizontal()))+padding.Vertical():padding.Vertical();}
    protected override float ComputeMaxIntrinsicHeight(float width){var padding=ResolvedPadding();return Child() is {} child?child.GetMaxIntrinsicHeight(CppMath.Max(0,width-padding.Horizontal()))+padding.Vertical():padding.Vertical();}
    protected override Sizef ComputeDryLayout(BoxConstraints constraints){var padding=ResolvedPadding();if(Child() is not {} child)return constraints.Constrain(padding.CollapsedSize());var childSize=child.GetDryLayout(constraints.Deflate(padding));return constraints.Constrain(padding.InflateSize(childSize));}
    protected override float? ComputeDryBaseline(BoxConstraints constraints,ETextBaseline baseline){if(Child() is not {} child)return null;var padding=ResolvedPadding();float? result=child.GetDryBaseline(constraints.Deflate(padding),baseline);return result.HasValue?result.Value+padding.Top:result;}
}
