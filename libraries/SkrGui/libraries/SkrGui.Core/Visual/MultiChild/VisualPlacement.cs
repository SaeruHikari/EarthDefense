namespace SkrGui;

// Source: visual/multi_child/visual_placement.hpp + src/visual/multi_child/visual_placement.cpp.
public enum EVisualPlacementAxisSizeKind : byte { Expand,Shrink,Sized }
public struct VisualPlacementAxisSize : IEquatable<VisualPlacementAxisSize>
{
    private EVisualPlacementAxisSizeKind _kind;private float _valuePx;private float? _minValuePx,_maxValuePx;
    public static VisualPlacementAxisSize Expand()=>new(){_kind=EVisualPlacementAxisSizeKind.Expand};
    public static VisualPlacementAxisSize Shrink()=>new(){_kind=EVisualPlacementAxisSizeKind.Shrink};
    public static VisualPlacementAxisSize Sized(float valuePx){var result=new VisualPlacementAxisSize{_kind=EVisualPlacementAxisSizeKind.Sized,_valuePx=valuePx};GuiAssert.Verify(result.IsValid(),"Sized axis must be finite and nonnegative");return result;}
    public EVisualPlacementAxisSizeKind Kind()=>_kind;public float ValuePx()=>_valuePx;public float? MinValuePx()=>_minValuePx;public float? MaxValuePx()=>_maxValuePx;
    public void SetMinValuePx(float? value){bool valid=!value.HasValue||(float.IsFinite(value.Value)&&value.Value>=0&&(!_maxValuePx.HasValue||value.Value<=_maxValuePx.Value));if(!valid){GuiAssert.Verify(valid,"Minimum axis bound invalid");return;}_minValuePx=value;}
    public void SetMaxValuePx(float? value){bool valid=!value.HasValue||(float.IsFinite(value.Value)&&value.Value>=0&&(!_minValuePx.HasValue||value.Value>=_minValuePx.Value));if(!valid){GuiAssert.Verify(valid,"Maximum axis bound invalid");return;}_maxValuePx=value;}
    public void SetBoundsPx(float? min,float? max){bool minValid=!min.HasValue||(float.IsFinite(min.Value)&&min.Value>=0);bool maxValid=!max.HasValue||(float.IsFinite(max.Value)&&max.Value>=0);bool orderValid=!min.HasValue||!max.HasValue||min.Value<=max.Value;bool valid=minValid&&maxValid&&orderValid;if(!valid){GuiAssert.Verify(valid,"Axis bounds must be finite nonnegative ordered");return;}_minValuePx=min;_maxValuePx=max;}
    public void ClearMinValuePx()=>_minValuePx=null;public void ClearMaxValuePx()=>_maxValuePx=null;public void ClearBounds(){_minValuePx=null;_maxValuePx=null;}
    public bool IsValid(){bool kindValid=_kind==EVisualPlacementAxisSizeKind.Expand||_kind==EVisualPlacementAxisSizeKind.Shrink||_kind==EVisualPlacementAxisSizeKind.Sized;bool valueValid=_kind!=EVisualPlacementAxisSizeKind.Sized||(float.IsFinite(_valuePx)&&_valuePx>=0);bool minValid=!_minValuePx.HasValue||(float.IsFinite(_minValuePx.Value)&&_minValuePx.Value>=0);bool maxValid=!_maxValuePx.HasValue||(float.IsFinite(_maxValuePx.Value)&&_maxValuePx.Value>=0);bool orderValid=!_minValuePx.HasValue||!_maxValuePx.HasValue||_minValuePx.Value<=_maxValuePx.Value;return kindValid&&valueValid&&minValid&&maxValid&&orderValid;}
    public static bool operator==(VisualPlacementAxisSize a,VisualPlacementAxisSize b)=>a._kind==b._kind&&a._valuePx==b._valuePx&&a._minValuePx==b._minValuePx&&a._maxValuePx==b._maxValuePx;
    public static bool operator!=(VisualPlacementAxisSize a,VisualPlacementAxisSize b)=>!(a==b);
    public bool Equals(VisualPlacementAxisSize b)=>this==b;public override bool Equals(object? obj)=>obj is VisualPlacementAxisSize b&&this==b;public override int GetHashCode()=>HashCode.Combine(_kind,_valuePx,_minValuePx,_maxValuePx);
}
public class VisualPlacementSlot : VisualSlot
{
    private Placement _placement=SkrGui.Placement.Fill();private EPlacementSizeMode _sizeMode=EPlacementSizeMode.Tight;internal Offsetf LayoutOffset=new();
    public ref readonly Placement Placement()=>ref _placement;public EPlacementSizeMode SizeMode()=>_sizeMode;public Offsetf Offset()=>LayoutOffset;
    public void SetPlacement(Placement value){if(!value.IsValid()){GuiAssert.Verify(value.IsValid(),"Placement slot requires valid placement");return;}if(_placement==value)return;_placement=value;Node()?.MarkNeedsLayoutForSizedByParentChange();}
    public void SetSizeMode(EPlacementSizeMode value){bool valid=value==EPlacementSizeMode.Tight||value==EPlacementSizeMode.Loose;if(!valid){GuiAssert.Verify(valid,"Placement slot size mode invalid");return;}_sizeMode=value;Node()?.MarkNeedsLayoutForSizedByParentChange();}
    public void SetFill()=>SetPlacement(SkrGui.Placement.Fill());public void ClearPlacement()=>SetFill();
}
public class VisualPlacement : VisualMultiChild
{
    private VisualPlacementAxisSize _widthRule=VisualPlacementAxisSize.Expand(),_heightRule=VisualPlacementAxisSize.Expand();private EClipBehavior _clipBehavior=EClipBehavior.None;private bool _hasVisualOverflow;
    public VisualPlacementAxisSize WidthRule()=>_widthRule;public VisualPlacementAxisSize HeightRule()=>_heightRule;public EClipBehavior ClipBehavior()=>_clipBehavior;public bool IsOverflowing()=>_hasVisualOverflow;
    public void SetWidthRule(VisualPlacementAxisSize value){if(!value.IsValid()){GuiAssert.Verify(value.IsValid(),"Placement width rule invalid");return;}if(_widthRule==value)return;_widthRule=value;MarkNeedsLayoutForSizedByParentChange();}
    public void SetHeightRule(VisualPlacementAxisSize value){if(!value.IsValid()){GuiAssert.Verify(value.IsValid(),"Placement height rule invalid");return;}if(_heightRule==value)return;_heightRule=value;MarkNeedsLayoutForSizedByParentChange();}
    public void SetWidth(float value)=>SetWidthRule(VisualPlacementAxisSize.Sized(value));public void SetHeight(float value)=>SetHeightRule(VisualPlacementAxisSize.Sized(value));
    public new void SetSize(Sizef value){var widthRule=VisualPlacementAxisSize.Sized(value.Width);var heightRule=VisualPlacementAxisSize.Sized(value.Height);if(!widthRule.IsValid()||!heightRule.IsValid())return;SetWidthRule(widthRule);SetHeightRule(heightRule);}
    public void SetWidthExpand()=>SetWidthRule(VisualPlacementAxisSize.Expand());public void SetHeightExpand()=>SetHeightRule(VisualPlacementAxisSize.Expand());public void SetExpand(){SetWidthExpand();SetHeightExpand();}
    public void SetWidthShrink()=>SetWidthRule(VisualPlacementAxisSize.Shrink());public void SetHeightShrink()=>SetHeightRule(VisualPlacementAxisSize.Shrink());public void SetShrink(){SetWidthShrink();SetHeightShrink();}
    public void SetWidthBounds(float? min,float? max){var rule=_widthRule;rule.SetBoundsPx(min,max);SetWidthRule(rule);}public void SetHeightBounds(float? min,float? max){var rule=_heightRule;rule.SetBoundsPx(min,max);SetHeightRule(rule);}
    public void ClearWidthBounds()=>SetWidthBounds(null,null);public void ClearHeightBounds()=>SetHeightBounds(null,null);
    public void SetClipBehavior(EClipBehavior value){if(_clipBehavior==value)return;_clipBehavior=value;}
    protected override bool SizedByParent()=>true;
    private static VisualPlacementSlot PlacementSlot(VisualNode child){GuiAssert.Require(child.Slot()!=null,"Placement requires child slot");return (VisualPlacementSlot)child.Slot()!;}
    private static bool ChildOverflows(Sizef parent,Offsetf offset,Sizef child)=>offset.X<0||offset.Y<0||offset.X+child.Width>parent.Width||offset.Y+child.Height>parent.Height;
    private static float ResolveAxisSize(VisualPlacementAxisSize rule,BoxConstraints constraints,bool isWidth)
    {
        GuiAssert.Require(rule.IsValid(),"Placement axis rule invalid");bool bounded=isWidth?constraints.HasBoundedWidth():constraints.HasBoundedHeight();float min=isWidth?constraints.MinWidth:constraints.MinHeight,max=isWidth?constraints.MaxWidth:constraints.MaxHeight;float target=min;
        switch(rule.Kind()){case EVisualPlacementAxisSizeKind.Expand:target=bounded?max:min;break;case EVisualPlacementAxisSizeKind.Shrink:target=min;break;case EVisualPlacementAxisSizeKind.Sized:target=rule.ValuePx();break;default:throw new InvalidOperationException("Unreachable placement size rule");}
        if(rule.MinValuePx().HasValue)target=CppMath.Max(target,rule.MinValuePx()!.Value);if(rule.MaxValuePx().HasValue)target=CppMath.Min(target,rule.MaxValuePx()!.Value);
        float result=isWidth?constraints.ConstrainWidth(target):constraints.ConstrainHeight(target);GuiAssert.Require(float.IsFinite(result),"Placement axis size must be finite");return result;
    }
    private static float ResolveIntrinsicAxisSize(VisualPlacementAxisSize rule)
    {
        GuiAssert.Require(rule.IsValid(),"Placement axis rule invalid");float target=0;
        switch(rule.Kind()){case EVisualPlacementAxisSizeKind.Expand:case EVisualPlacementAxisSizeKind.Shrink:target=0;break;case EVisualPlacementAxisSizeKind.Sized:target=rule.ValuePx();break;default:throw new InvalidOperationException("Unreachable placement size rule");}
        if(rule.MinValuePx().HasValue)target=CppMath.Max(target,rule.MinValuePx()!.Value);if(rule.MaxValuePx().HasValue)target=CppMath.Min(target,rule.MaxValuePx()!.Value);return CppMath.Max(0,target);
    }
    private Sizef ComputeSize(BoxConstraints constraints)=>new(ResolveAxisSize(WidthRule(),constraints,true),ResolveAxisSize(HeightRule(),constraints,false));
    protected override void ApplySlot(VisualNode child)=>SetChildSlot(child,new VisualPlacementSlot());
    protected override void PerformPaint(PaintContext context){bool clip=_clipBehavior!=EClipBehavior.None&&_hasVisualOverflow&&!Size().IsEmpty();if(clip)context.PushClipRect(Rectf.OffsetSize(Offsetf.Zero(),Size()));for(ulong i=0;i<NumChildren();i++){var child=ChildAt(i);PaintChild(child,context,PlacementSlot(child).Offset());}if(clip)context.PopClip();}
    protected override bool HitTestChildren(VisualHitTestResult result,Offsetf position){ulong i=NumChildren();while(i>0){--i;var child=ChildAt(i);if(HitTestChild(result,child,position,PlacementSlot(child).Offset()))return true;}return false;}
    protected override void PerformLayout()
    {
        _hasVisualOverflow=false;var computed=ComputeSize(Constraints());base.SetSize(computed);
        for(ulong i=0;i<NumChildren();i++)
        {
            var child=ChildAt(i);var slot=PlacementSlot(child);var parentConstraints=slot.SizeMode()==EPlacementSizeMode.Tight?BoxConstraints.Tight(Size()):BoxConstraints.Loose(Size());var c=slot.Placement().ResolveConstraints(parentConstraints,slot.SizeMode());
            if(!c.HasValue){LayoutChild(child,BoxConstraints.Tight(Sizef.Zero()));slot.LayoutOffset=Offsetf.Zero();continue;}
            LayoutChild(child,c.Value);var offset=slot.Placement().PlaceOffset(Size(),child.Size());if(!offset.HasValue){slot.LayoutOffset=Offsetf.Zero();continue;}slot.LayoutOffset=offset.Value;_hasVisualOverflow=ChildOverflows(Size(),slot.Offset(),child.Size())||_hasVisualOverflow;
        }
    }
    protected override float ComputeMinIntrinsicWidth(float height)=>ResolveIntrinsicAxisSize(_widthRule);protected override float ComputeMaxIntrinsicWidth(float height)=>ResolveIntrinsicAxisSize(_widthRule);protected override float ComputeMinIntrinsicHeight(float width)=>ResolveIntrinsicAxisSize(_heightRule);protected override float ComputeMaxIntrinsicHeight(float width)=>ResolveIntrinsicAxisSize(_heightRule);
    protected override Sizef ComputeDryLayout(BoxConstraints constraints)=>ComputeSize(constraints);
    protected override float? ComputeDryBaseline(BoxConstraints constraints,ETextBaseline baseline)
    {
        var size=GetDryLayout(constraints);for(ulong i=0;i<NumChildren();i++){var child=ChildAt(i);var slot=PlacementSlot(child);var parentConstraints=slot.SizeMode()==EPlacementSizeMode.Tight?BoxConstraints.Tight(size):BoxConstraints.Loose(size);var c=slot.Placement().ResolveConstraints(parentConstraints,slot.SizeMode());if(!c.HasValue)continue;float? childBaseline=child.GetDryBaseline(c.Value,baseline);if(!childBaseline.HasValue)continue;var childSize=child.GetDryLayout(c.Value);var offset=slot.Placement().PlaceOffset(size,childSize);if(offset.HasValue)return childBaseline.Value+offset.Value.Y;}return null;
    }
    protected override float? ComputeDistanceToActualBaseline(ETextBaseline baseline){for(ulong i=0;i<NumChildren();i++){var child=ChildAt(i);float? value=child.GetDistanceToActualBaseline(baseline);if(!value.HasValue)continue;return value.Value+PlacementSlot(child).Offset().Y;}return null;}
}
