namespace SkrGui;

// Source: visual/proxy/visual_proxy.hpp and src/visual/proxy/visual_proxy.cpp.
public class VisualProxy : VisualSingleChild
{
    protected override void PerformPaint(PaintContext context)=>PaintChild(Child(),context);
    protected override bool HitTestChildren(VisualHitTestResult result,Offsetf position)=>HitTestChild(result,Child(),position);
    protected override void PerformLayout(){if(Child() is {} child){LayoutChild(child,Constraints());SetSize(child.Size());return;}SetSize(ComputeSizeForNoChild(Constraints()));}
    protected override float ComputeMinIntrinsicWidth(float height)=>Child()?.GetMinIntrinsicWidth(height)??0;
    protected override float ComputeMaxIntrinsicWidth(float height)=>Child()?.GetMaxIntrinsicWidth(height)??0;
    protected override float ComputeMinIntrinsicHeight(float width)=>Child()?.GetMinIntrinsicHeight(width)??0;
    protected override float ComputeMaxIntrinsicHeight(float width)=>Child()?.GetMaxIntrinsicHeight(width)??0;
    protected override Sizef ComputeDryLayout(BoxConstraints constraints)=>Child()?.GetDryLayout(constraints)??ComputeSizeForNoChild(constraints);
    protected virtual Sizef ComputeSizeForNoChild(BoxConstraints constraints)=>constraints.Smallest();
    protected override float? ComputeDryBaseline(BoxConstraints constraints,ETextBaseline baseline)=>Child() is {} child?child.GetDryBaseline(constraints,baseline):base.ComputeDryBaseline(constraints,baseline);
    protected override float? ComputeDistanceToActualBaseline(ETextBaseline baseline)=>Child() is {} child?child.GetDistanceToActualBaseline(baseline):base.ComputeDistanceToActualBaseline(baseline);
}
public enum EVisualOpacityMode : byte { Multiply,Override }
public class VisualOpacity : VisualProxy
{
    private float _opacity=1;private EVisualOpacityMode _opacityMode;
    public float Opacity()=>_opacity;
    public EVisualOpacityMode OpacityMode()=>_opacityMode;
    public bool IsOpacityOverride()=>_opacityMode==EVisualOpacityMode.Override;
    public void SetOpacity(float value){GuiAssert.Require(value>=0&&value<=1&&float.IsFinite(value),"Opacity must be finite in [0,1]");if(_opacity==value)return;_opacity=value;}
    public void SetOpacityMode(EVisualOpacityMode value){if(_opacityMode==value)return;_opacityMode=value;}
    public void SetOpacityOverride(bool value)=>SetOpacityMode(value?EVisualOpacityMode.Override:EVisualOpacityMode.Multiply);
    protected override void PerformPaint(PaintContext context){if(Child()==null)return;if(_opacityMode==EVisualOpacityMode.Override)context.PushOpacityOverride(_opacity);else context.PushOpacity(_opacity);if(context.Opacity()>0)base.PerformPaint(context);context.PopOpacity();}
}
public class VisualVisibility : VisualProxy
{
    private bool _visibility=true,_maintainSize;
    public bool Visibility()=>_visibility;
    public bool MaintainSize()=>_maintainSize;
    public void SetVisibility(bool value){if(_visibility==value)return;_visibility=value;MarkNeedsLayoutForSizedByParentChange();}
    public void SetMaintain(bool value)=>SetMaintainSize(value);
    public void SetMaintainSize(bool value){if(_maintainSize==value)return;_maintainSize=value;MarkNeedsLayoutForSizedByParentChange();}
    private bool ShouldMaintainSize()=>_visibility||_maintainSize;
    protected override bool SizedByParent()=>!ShouldMaintainSize();
    protected override void PerformPaint(PaintContext context){if(!_visibility)return;base.PerformPaint(context);}
    public override bool HitTest(VisualHitTestResult result,Offsetf position)=>_visibility&&base.HitTest(result,position);
    protected override void PerformLayout(){if(ShouldMaintainSize())base.PerformLayout();else{LayoutChild(Child(),Constraints(),false);SetSize(Constraints().Smallest());}}
    protected override float ComputeMinIntrinsicWidth(float height)=>ShouldMaintainSize()?base.ComputeMinIntrinsicWidth(height):0;
    protected override float ComputeMaxIntrinsicWidth(float height)=>ShouldMaintainSize()?base.ComputeMaxIntrinsicWidth(height):0;
    protected override float ComputeMinIntrinsicHeight(float width)=>ShouldMaintainSize()?base.ComputeMinIntrinsicHeight(width):0;
    protected override float ComputeMaxIntrinsicHeight(float width)=>ShouldMaintainSize()?base.ComputeMaxIntrinsicHeight(width):0;
    protected override Sizef ComputeDryLayout(BoxConstraints constraints)=>ShouldMaintainSize()?base.ComputeDryLayout(constraints):constraints.Smallest();
    protected override float? ComputeDryBaseline(BoxConstraints constraints,ETextBaseline baseline)=>ShouldMaintainSize()?base.ComputeDryBaseline(constraints,baseline):null;
    protected override float? ComputeDistanceToActualBaseline(ETextBaseline baseline)=>ShouldMaintainSize()?base.ComputeDistanceToActualBaseline(baseline):null;
}
public class VisualClipRect : VisualProxy
{
    private EClipBehavior _clipBehavior=EClipBehavior.HardEdge;private Rectf? _customClipRect;
    public EClipBehavior ClipBehavior()=>_clipBehavior;
    public Rectf? CustomClipRect()=>_customClipRect;
    public Rectf ResolvedClipRect()=>_customClipRect??Rectf.OffsetSize(Offsetf.Zero(),Size());
    public void SetClipBehavior(EClipBehavior value){if(_clipBehavior==value)return;_clipBehavior=value;}
    public void SetCustomClipRect(Rectf? value){if(_customClipRect==value)return;_customClipRect=value;}
    public void ClearCustomClipRect()=>SetCustomClipRect(null);
    protected override void PerformPaint(PaintContext context){if(Child() is not {} child)return;if(_clipBehavior==EClipBehavior.None){PaintChild(child,context);return;}context.PushClipRect(ResolvedClipRect());PaintChild(child,context);context.PopClip();}
    protected override bool HitTestChildren(VisualHitTestResult result,Offsetf position){if(_clipBehavior!=EClipBehavior.None&&!ResolvedClipRect().Contains(position))return false;return base.HitTestChildren(result,position);}
}
public class VisualLimited : VisualProxy
{
    private float _maxWidth=float.PositiveInfinity,_maxHeight=float.PositiveInfinity;
    public float MaxWidth()=>_maxWidth;
    public float MaxHeight()=>_maxHeight;
    public void SetMaxWidth(float value){GuiAssert.Require(value>=0,"Max width must be non-negative");if(_maxWidth==value)return;_maxWidth=value;MarkNeedsLayout();}
    public void SetMaxHeight(float value){GuiAssert.Require(value>=0,"Max height must be non-negative");if(_maxHeight==value)return;_maxHeight=value;MarkNeedsLayout();}
    private BoxConstraints LimitConstraints(BoxConstraints constraints)=>new(constraints.MinWidth,constraints.HasBoundedWidth()?constraints.MaxWidth:constraints.ConstrainWidth(_maxWidth),constraints.MinHeight,constraints.HasBoundedHeight()?constraints.MaxHeight:constraints.ConstrainHeight(_maxHeight));
    protected override void PerformLayout(){var incoming=Constraints();var limited=LimitConstraints(incoming);if(Child() is {} child){LayoutChild(child,limited);SetSize(incoming.Constrain(child.Size()));return;}SetSize(ComputeSizeForNoChild(incoming));}
    protected override Sizef ComputeDryLayout(BoxConstraints constraints){var limited=LimitConstraints(constraints);return Child() is {} child?constraints.Constrain(child.GetDryLayout(limited)):ComputeSizeForNoChild(constraints);}
    protected override Sizef ComputeSizeForNoChild(BoxConstraints constraints)=>LimitConstraints(constraints).Constrain(Sizef.Zero());
}
public class VisualAspectRatio : VisualProxy
{
    private float _aspectRatio=1;
    public float AspectRatio()=>_aspectRatio;
    public void SetAspectRatio(float value){GuiAssert.Require(value>0&&float.IsFinite(value),"Aspect ratio must be finite and positive");if(_aspectRatio==value)return;_aspectRatio=value;MarkNeedsLayoutForSizedByParentChange();}
    protected override bool SizedByParent()=>true;
    protected override void PerformLayout(){var size=GetDryLayout(Constraints());SetSize(size);LayoutChild(Child(),BoxConstraints.Tight(size));}
    protected override float ComputeMinIntrinsicWidth(float height)=>float.IsFinite(height)?height*_aspectRatio:base.ComputeMinIntrinsicWidth(height);
    protected override float ComputeMaxIntrinsicWidth(float height)=>float.IsFinite(height)?height*_aspectRatio:base.ComputeMaxIntrinsicWidth(height);
    protected override float ComputeMinIntrinsicHeight(float width)=>float.IsFinite(width)?width/_aspectRatio:base.ComputeMinIntrinsicHeight(width);
    protected override float ComputeMaxIntrinsicHeight(float width)=>float.IsFinite(width)?width/_aspectRatio:base.ComputeMaxIntrinsicHeight(width);
    protected override Sizef ComputeDryLayout(BoxConstraints constraints)=>ApplyAspectRatio(constraints);
    protected override float? ComputeDryBaseline(BoxConstraints constraints,ETextBaseline baseline)=>Child()?.GetDryBaseline(BoxConstraints.Tight(GetDryLayout(constraints)),baseline);
    private Sizef ApplyAspectRatio(BoxConstraints constraints)
    {
        constraints.AssertValid();GuiAssert.Require(constraints.HasBoundedWidth()||constraints.HasBoundedHeight(),"Aspect ratio cannot have both axes unbounded");if(constraints.IsTight())return constraints.Smallest();
        float width=constraints.MaxWidth,height=0;if(float.IsFinite(width))height=width/_aspectRatio;else{height=constraints.MaxHeight;width=height*_aspectRatio;}
        if(width>constraints.MaxWidth){width=constraints.MaxWidth;height=width/_aspectRatio;}
        if(height>constraints.MaxHeight){height=constraints.MaxHeight;width=height*_aspectRatio;}
        if(width<constraints.MinWidth){width=constraints.MinWidth;height=width/_aspectRatio;}
        if(height<constraints.MinHeight){height=constraints.MinHeight;width=height*_aspectRatio;}
        return constraints.Constrain(width,height);
    }
}
