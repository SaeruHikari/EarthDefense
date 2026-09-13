namespace SkrGui;

// Source: visual/multi_child/visual_flex.hpp + src/visual/multi_child/visual_flex.cpp.
public enum EFlexFit : byte { Tight,Loose }
public enum EMainAxisSize : byte { Min,Max }
public enum EMainAxisAlignment : byte { Start,End,Center,SpaceBetween,SpaceAround,SpaceEvenly }
public enum ECrossAxisAlignment : byte { Start,End,Center,Stretch,Baseline }
public class VisualFlexSlot : VisualSlot
{
    private int _flex;private EFlexFit _fit=EFlexFit.Tight;internal Offsetf LayoutOffset=new();
    public int Flex()=>_flex;public EFlexFit Fit()=>_fit;public Offsetf Offset()=>LayoutOffset;
    public void SetFlex(int value){GuiAssert.Require(value>=0,"Flex must be nonnegative");if(_flex==value)return;_flex=value;Node()?.MarkNeedsLayoutForSizedByParentChange();}
    public void SetFit(EFlexFit value){if(_fit==value)return;_fit=value;Node()?.MarkNeedsLayoutForSizedByParentChange();}
}
public class VisualFlex : VisualMultiChild
{
    private EAxis _direction=EAxis.Horizontal;private EMainAxisSize _mainAxisSize=EMainAxisSize.Max;private EMainAxisAlignment _mainAxisAlignment=EMainAxisAlignment.Start;private ECrossAxisAlignment _crossAxisAlignment=ECrossAxisAlignment.Center;private ETextDirection _textDirection=ETextDirection.LTR;private EVerticalDirection _verticalDirection=EVerticalDirection.Down;private ETextBaseline? _textBaseline;private EClipBehavior _clipBehavior=EClipBehavior.None;private float _spacing,_overflow;
    public EAxis Direction()=>_direction;public EMainAxisSize MainAxisSize()=>_mainAxisSize;public EMainAxisAlignment MainAxisAlignment()=>_mainAxisAlignment;public ECrossAxisAlignment CrossAxisAlignment()=>_crossAxisAlignment;public ETextDirection TextDirection()=>_textDirection;public EVerticalDirection VerticalDirection()=>_verticalDirection;public ETextBaseline? TextBaseline()=>_textBaseline;public EClipBehavior ClipBehavior()=>_clipBehavior;public float Spacing()=>_spacing;public float Overflow()=>_overflow;public bool IsOverflowing()=>_overflow>GuiConstants.FlutterPrecisionErrorTolerance;
    public void SetDirection(EAxis value){if(_direction==value)return;_direction=value;MarkNeedsLayout();}
    public void SetMainAxisSize(EMainAxisSize value){if(_mainAxisSize==value)return;_mainAxisSize=value;MarkNeedsLayout();}
    public void SetMainAxisAlignment(EMainAxisAlignment value){if(_mainAxisAlignment==value)return;_mainAxisAlignment=value;MarkNeedsLayout();}
    public void SetCrossAxisAlignment(ECrossAxisAlignment value){if(_crossAxisAlignment==value)return;_crossAxisAlignment=value;MarkNeedsLayout();}
    public void SetTextDirection(ETextDirection value){if(_textDirection==value)return;_textDirection=value;MarkNeedsLayout();}
    public void SetVerticalDirection(EVerticalDirection value){if(_verticalDirection==value)return;_verticalDirection=value;MarkNeedsLayout();}
    public void SetTextBaseline(ETextBaseline? value){if(_textBaseline==value)return;_textBaseline=value;MarkNeedsLayout();}
    public void ClearTextBaseline()=>SetTextBaseline(null);
    public void SetClipBehavior(EClipBehavior value){if(_clipBehavior==value)return;_clipBehavior=value;}
    public void SetSpacing(float value){GuiAssert.Require(value>=0,"Flex spacing must be nonnegative");if(_spacing==value)return;_spacing=value;MarkNeedsLayout();}
    private struct LayoutSizes { public Sizef AxisSize;public float MainAxisFreeSpace;public float? BaselineOffset; }
    private sealed class DryChildLayout { public ulong Index;public required VisualNode Child;public BoxConstraints Constraints=new();public Sizef Size=new();public float MainPosition,CrossPosition; }
    private static VisualFlexSlot FlexSlot(VisualNode child){GuiAssert.Require(child.Slot()!=null,"Flex requires child slot");return (VisualFlexSlot)child.Slot()!;}
    protected override void ApplySlot(VisualNode child)=>SetChildSlot(child,new VisualFlexSlot());
    private bool IsHorizontal()=>_direction==EAxis.Horizontal;
    private bool IsCrossAxisStretched()=>_crossAxisAlignment==ECrossAxisAlignment.Stretch;
    private bool IsBaselineAligned()=>_crossAxisAlignment==ECrossAxisAlignment.Baseline&&IsHorizontal();
    private bool FlipMainAxis(){if(IsEmpty())return false;return IsHorizontal()?_textDirection==ETextDirection.RTL:_verticalDirection==EVerticalDirection.Up;}
    private bool FlipCrossAxis(){if(IsEmpty())return false;return IsHorizontal()?_verticalDirection==EVerticalDirection.Up:_textDirection==ETextDirection.RTL;}
    private float MainSize(Sizef size)=>IsHorizontal()?size.Width:size.Height;
    private float CrossSize(Sizef size)=>IsHorizontal()?size.Height:size.Width;
    private float MaxMainSize(BoxConstraints constraints)=>IsHorizontal()?constraints.MaxWidth:constraints.MaxHeight;
    private Sizef SizeFromAxis(float main,float cross)=>IsHorizontal()?new(main,cross):new(cross,main);
    private Sizef ConstrainAxisSize(float main,float cross,BoxConstraints constraints)=>constraints.Constrain(SizeFromAxis(main,cross));
    private BoxConstraints ConstraintsForNonFlexChild(BoxConstraints constraints)
    {bool fill=IsCrossAxisStretched();return IsHorizontal()?new(0,float.PositiveInfinity,fill?constraints.MaxHeight:0,constraints.MaxHeight):new(fill?constraints.MaxWidth:0,constraints.MaxWidth,0,float.PositiveInfinity);}
    private BoxConstraints ConstraintsForFlexChild(BoxConstraints constraints,float maxChildExtent,EFlexFit fit)
    {float minChildExtent=fit==EFlexFit.Tight?maxChildExtent:0;bool fill=IsCrossAxisStretched();return IsHorizontal()?new(minChildExtent,maxChildExtent,fill?constraints.MaxHeight:0,constraints.MaxHeight):new(fill?constraints.MaxWidth:0,constraints.MaxWidth,minChildExtent,maxChildExtent);}
    private LayoutSizes ComputeSizes(BoxConstraints constraints,Func<VisualNode,BoxConstraints,Sizef> layoutChild,Func<VisualNode,BoxConstraints,ETextBaseline,float?> getBaseline,Action<ulong,VisualNode,BoxConstraints,Sizef> recordChild)
    {
        ulong count=NumChildren();float gapSpace=0;if(count>0)gapSpace=Spacing()*(float)(count-1);
        float maxMain=MaxMainSize(constraints);bool canFlex=float.IsFinite(maxMain);var nonFlex=ConstraintsForNonFlexChild(constraints);
        int totalFlex=0;ulong firstFlex=ulong.MaxValue;float accumulatedMain=gapSpace,accumulatedCross=0;float? accumulatedAscent=null;float accumulatedDescent=0;bool baselineAligned=IsBaselineAligned();
        if(baselineAligned)GuiAssert.Require(TextBaseline().HasValue,"Flex baseline alignment requires text baseline");
        void AccumulateChild(ulong index,VisualNode child,BoxConstraints childConstraints,Sizef childSize)
        {
            recordChild(index,child,childConstraints,childSize);accumulatedMain+=MainSize(childSize);accumulatedCross=CppMath.Max(accumulatedCross,CrossSize(childSize));if(!baselineAligned)return;
            float? childBaseline=getBaseline(child,childConstraints,TextBaseline()!.Value);if(!childBaseline.HasValue)return;
            accumulatedAscent=accumulatedAscent.HasValue?CppMath.Max(accumulatedAscent.Value,childBaseline.Value):childBaseline.Value;accumulatedDescent=CppMath.Max(accumulatedDescent,CrossSize(childSize)-childBaseline.Value);
        }
        for(ulong i=0;i<NumChildren();i++)
        {
            var child=ChildAt(i);var slot=FlexSlot(child);int childFlex=slot.Flex();if(canFlex&&childFlex>0){totalFlex+=childFlex;if(firstFlex==ulong.MaxValue)firstFlex=i;continue;}
            var childSize=layoutChild(child,nonFlex);AccumulateChild(i,child,nonFlex,childSize);
        }
        float spacePerFlex=0;
        if(totalFlex>0)
        {
            float flexSpace=CppMath.Max(0,maxMain-accumulatedMain);spacePerFlex=flexSpace/(float)totalFlex;int remaining=totalFlex;
            for(ulong i=firstFlex;i<NumChildren()&&remaining>0;i++){var child=ChildAt(i);var slot=FlexSlot(child);int childFlex=slot.Flex();if(childFlex==0)continue;remaining-=childFlex;float maxChild=spacePerFlex*(float)childFlex;var childConstraints=ConstraintsForFlexChild(constraints,maxChild,slot.Fit());var childSize=layoutChild(child,childConstraints);AccumulateChild(i,child,childConstraints,childSize);}
        }
        if(accumulatedAscent.HasValue)accumulatedCross=CppMath.Max(accumulatedCross,accumulatedAscent.Value+accumulatedDescent);
        float idealMain=accumulatedMain;if(MainAxisSize()==EMainAxisSize.Max&&float.IsFinite(maxMain))idealMain=maxMain;
        var axisSize=ConstrainAxisSize(idealMain,accumulatedCross,constraints);return new(){AxisSize=axisSize,MainAxisFreeSpace=MainSize(axisSize)-accumulatedMain,BaselineOffset=accumulatedAscent};
    }
    protected override void PerformPaint(PaintContext context){bool clip=IsOverflowing()&&_clipBehavior!=EClipBehavior.None&&!Size().IsEmpty();if(clip)context.PushClipRect(Rectf.OffsetSize(Offsetf.Zero(),Size()));for(ulong i=0;i<NumChildren();i++){var child=ChildAt(i);PaintChild(child,context,FlexSlot(child).Offset());}if(clip)context.PopClip();}
    protected override bool HitTestChildren(VisualHitTestResult result,Offsetf position){ulong i=NumChildren();while(i>0){--i;var child=ChildAt(i);if(HitTestChild(result,child,position,FlexSlot(child).Offset()))return true;}return false;}
    protected override void PerformLayout(){CheckFlexConstraints(Constraints());var sizes=ComputeSizes(Constraints(),(child,c)=>{LayoutChild(child,c);return child.Size();},(child,c,b)=>child.GetDistanceToBaseline(b,true),(i,child,c,s)=>{});SetSize(sizes.AxisSize);_overflow=CppMath.Max(0,-sizes.MainAxisFreeSpace);PositionChildren(sizes);}
    protected override float ComputeMinIntrinsicWidth(float height)=>ComputeIntrinsicSize(EAxis.Horizontal,height,true);
    protected override float ComputeMaxIntrinsicWidth(float height)=>ComputeIntrinsicSize(EAxis.Horizontal,height,false);
    protected override float ComputeMinIntrinsicHeight(float width)=>ComputeIntrinsicSize(EAxis.Vertical,width,true);
    protected override float ComputeMaxIntrinsicHeight(float width)=>ComputeIntrinsicSize(EAxis.Vertical,width,false);
    protected override Sizef ComputeDryLayout(BoxConstraints constraints){CheckFlexConstraints(constraints);var sizes=ComputeSizes(constraints,(child,c)=>child.GetDryLayout(c),(child,c,b)=>child.GetDryBaseline(c,b),(i,child,c,s)=>{});return sizes.AxisSize;}
    protected override float? ComputeDryBaseline(BoxConstraints constraints,ETextBaseline baseline)
    {
        CheckFlexConstraints(constraints);var layouts=new List<DryChildLayout>();
        var sizes=ComputeSizes(constraints,(child,c)=>child.GetDryLayout(c),(child,c,b)=>child.GetDryBaseline(c,b),(i,child,c,s)=>layouts.Add(new(){Index=i,Child=child,Constraints=c,Size=s}));
        if(IsBaselineAligned())return sizes.BaselineOffset;if(layouts.Count==0)return null;
        float remaining=CppMath.Max(0,sizes.MainAxisFreeSpace);ulong count=NumChildren();bool mainFlipped=FlipMainAxis(),crossFlipped=FlipCrossAxis();DistributeSpace(_mainAxisAlignment,remaining,count,mainFlipped,_spacing,out float leading,out float between);
        float crossExtent=CrossSize(sizes.AxisSize),childMain=leading;
        void PositionChild(ulong index){for(int childIndex=0;childIndex<layouts.Count;childIndex++){var layout=layouts[childIndex];if(layout.Index!=index)continue;layout.MainPosition=childMain;layout.CrossPosition=ChildCrossAxisOffset(crossExtent-CrossSize(layout.Size),crossFlipped);childMain+=MainSize(layout.Size)+between;return;}}
        if(mainFlipped){for(ulong i=NumChildren();i>0;--i)PositionChild(i-1);}else{for(ulong i=0;i<NumChildren();i++)PositionChild(i);}
        if(IsHorizontal()){float? result=null;for(int i=0;i<layouts.Count;i++){var layout=layouts[i];float? childBaseline=layout.Child.GetDryBaseline(layout.Constraints,baseline);if(!childBaseline.HasValue)continue;float candidate=childBaseline.Value+layout.CrossPosition;result=result.HasValue?CppMath.Min(result.Value,candidate):candidate;}return result;}
        for(ulong i=0;i<NumChildren();i++){for(int childIndex=0;childIndex<layouts.Count;childIndex++){var layout=layouts[childIndex];if(layout.Index!=i)continue;float? childBaseline=layout.Child.GetDryBaseline(layout.Constraints,baseline);if(childBaseline.HasValue)return childBaseline.Value+layout.MainPosition;}}
        return null;
    }
    protected override float? ComputeDistanceToActualBaseline(ETextBaseline baseline)
    {
        if(IsHorizontal()){float? result=null;for(ulong i=0;i<NumChildren();i++){var child=ChildAt(i);float? childBaseline=child.GetDistanceToActualBaseline(baseline);if(!childBaseline.HasValue)continue;float candidate=childBaseline.Value+FlexSlot(child).Offset().Y;result=result.HasValue?CppMath.Min(result.Value,candidate):candidate;}return result;}
        for(ulong i=0;i<NumChildren();i++){var child=ChildAt(i);float? childBaseline=child.GetDistanceToActualBaseline(baseline);if(childBaseline.HasValue)return childBaseline.Value+FlexSlot(child).Offset().Y;}return base.ComputeDistanceToActualBaseline(baseline);
    }
    private float ChildCrossAxisOffset(float free,bool flipped)=>_crossAxisAlignment switch{ECrossAxisAlignment.Start=>flipped?free:0,ECrossAxisAlignment.End=>flipped?0:free,ECrossAxisAlignment.Center=>free/2,ECrossAxisAlignment.Stretch or ECrossAxisAlignment.Baseline=>0,_=>throw new InvalidOperationException("Unreachable flex cross alignment")};
    private float ChildIntrinsicSize(VisualNode child,EAxis direction,float extent,bool min)=>direction==EAxis.Horizontal?(min?child.GetMinIntrinsicWidth(extent):child.GetMaxIntrinsicWidth(extent)):(min?child.GetMinIntrinsicHeight(extent):child.GetMaxIntrinsicHeight(extent));
    private float ComputeIntrinsicSize(EAxis direction,float extent,bool min)
    {
        ulong count=NumChildren();if(_direction==direction)
        {
            int totalFlex=0;float inflexible=count>0?_spacing*(float)(count-1):0,maxFraction=0;
            for(ulong i=0;i<NumChildren();i++){var child=ChildAt(i);int flex=FlexSlot(child).Flex();float childSize=ChildIntrinsicSize(child,direction,extent,min);totalFlex+=flex;if(flex>0)maxFraction=CppMath.Max(maxFraction,childSize/(float)flex);else inflexible+=childSize;}
            return maxFraction*(float)totalFlex+inflexible;
        }
        BoxConstraints constraints=IsHorizontal()?new(0,extent,0,float.PositiveInfinity):new(0,float.PositiveInfinity,0,extent);
        var sizes=ComputeSizes(constraints,(child,c)=>{float mainConstraint=MainSize(c.MaxSize());float main=mainConstraint;if(!float.IsFinite(main))main=ChildIntrinsicSize(child,_direction,float.PositiveInfinity,false);float cross=ChildIntrinsicSize(child,FlipAxis(_direction),main,min);return SizeFromAxis(main,cross);},(child,c,b)=>child.GetDryBaseline(c,b),(i,child,c,s)=>{});
        return CrossSize(sizes.AxisSize);
    }
    private void CheckFlexConstraints(BoxConstraints constraints){if(float.IsFinite(MaxMainSize(constraints)))return;for(ulong i=0;i<NumChildren();i++){var slot=FlexSlot(ChildAt(i));GuiAssert.Require(!(slot.Flex()>0&&(_mainAxisSize==EMainAxisSize.Max||slot.Fit()==EFlexFit.Tight)),"Non-zero tight flex with unbounded main-axis constraints");}}
    private void PositionChildren(LayoutSizes sizes)
    {
        float remaining=CppMath.Max(0,sizes.MainAxisFreeSpace);ulong count=NumChildren();bool mainFlipped=FlipMainAxis(),crossFlipped=FlipCrossAxis(),baselineAligned=IsBaselineAligned();DistributeSpace(_mainAxisAlignment,remaining,count,mainFlipped,_spacing,out float leading,out float between);float crossExtent=CrossSize(sizes.AxisSize),childMain=leading;
        void PositionChild(ulong index)
        {
            var child=ChildAt(index);var slot=FlexSlot(child);var childSize=child.Size();float childCross=0;
            if(baselineAligned&&sizes.BaselineOffset.HasValue){float? childBaseline=child.GetDistanceToBaseline(_textBaseline!.Value,true);childCross=childBaseline.HasValue?sizes.BaselineOffset.Value-childBaseline.Value:0;}
            else childCross=ChildCrossAxisOffset(crossExtent-CrossSize(childSize),crossFlipped);
            slot.LayoutOffset=IsHorizontal()?new(childMain,childCross):new(childCross,childMain);childMain+=MainSize(childSize)+between;
        }
        if(mainFlipped){for(ulong i=NumChildren();i>0;--i)PositionChild(i-1);return;}for(ulong i=0;i<NumChildren();i++)PositionChild(i);
    }
    private static EAxis FlipAxis(EAxis axis)=>axis==EAxis.Horizontal?EAxis.Vertical:EAxis.Horizontal;
    private static void DistributeSpace(EMainAxisAlignment alignment,float free,ulong count,bool flipped,float spacing,out float leading,out float between)
    {
        if(alignment==EMainAxisAlignment.End){DistributeSpace(EMainAxisAlignment.Start,free,count,!flipped,spacing,out leading,out between);return;}
        if(alignment==EMainAxisAlignment.SpaceBetween&&count<2){DistributeSpace(EMainAxisAlignment.Start,free,count,flipped,spacing,out leading,out between);return;}
        if(alignment==EMainAxisAlignment.SpaceAround&&count==0){DistributeSpace(EMainAxisAlignment.Start,free,count,flipped,spacing,out leading,out between);return;}
        switch(alignment)
        {
            case EMainAxisAlignment.Start:leading=flipped?free:0;between=spacing;return;
            case EMainAxisAlignment.Center:leading=free/2;between=spacing;return;
            case EMainAxisAlignment.SpaceBetween:leading=0;between=free/(float)(count-1)+spacing;return;
            case EMainAxisAlignment.SpaceAround:leading=free/(float)count/2;between=free/(float)count+spacing;return;
            case EMainAxisAlignment.SpaceEvenly:leading=free/(float)(count+1);between=free/(float)(count+1)+spacing;return;
            default:throw new InvalidOperationException("Unreachable flex distribution");
        }
    }
}
