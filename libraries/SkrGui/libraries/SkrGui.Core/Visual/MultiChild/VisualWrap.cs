namespace SkrGui;

// Source: visual/multi_child/visual_wrap.hpp + src/visual/multi_child/visual_wrap.cpp.
public enum EWrapAlignment : byte { Start,End,Center,SpaceBetween,SpaceAround,SpaceEvenly }
public enum EWrapCrossAlignment : byte { Start,End,Center }
public class VisualWrapSlot : VisualSlot { internal Offsetf LayoutOffset=new();public Offsetf Offset()=>LayoutOffset; }
public class VisualWrap : VisualMultiChild
{
    private EAxis _direction=EAxis.Horizontal;private EWrapAlignment _alignment=EWrapAlignment.Start,_runAlignment=EWrapAlignment.Start;private float _spacing,_runSpacing;private EWrapCrossAlignment _crossAxisAlignment=EWrapCrossAlignment.Start;private ETextDirection _textDirection=ETextDirection.LTR;private EVerticalDirection _verticalDirection=EVerticalDirection.Down;private EClipBehavior _clipBehavior=EClipBehavior.None;private Sizef _overflow=new();
    public EAxis Direction()=>_direction;public EWrapAlignment Alignment()=>_alignment;public float Spacing()=>_spacing;public EWrapAlignment RunAlignment()=>_runAlignment;public float RunSpacing()=>_runSpacing;public EWrapCrossAlignment CrossAxisAlignment()=>_crossAxisAlignment;public ETextDirection TextDirection()=>_textDirection;public EVerticalDirection VerticalDirection()=>_verticalDirection;public EClipBehavior ClipBehavior()=>_clipBehavior;public Sizef Overflow()=>_overflow;public bool IsOverflowing()=>_overflow.Width>0||_overflow.Height>0;
    public void SetDirection(EAxis value){if(_direction==value)return;_direction=value;MarkNeedsLayout();}
    public void SetAlignment(EWrapAlignment value){if(_alignment==value)return;_alignment=value;MarkNeedsLayout();}
    public void SetSpacing(float value){GuiAssert.Require(value>=0,"Wrap spacing must be nonnegative");if(_spacing==value)return;_spacing=value;MarkNeedsLayout();}
    public void SetRunAlignment(EWrapAlignment value){if(_runAlignment==value)return;_runAlignment=value;MarkNeedsLayout();}
    public void SetRunSpacing(float value){GuiAssert.Require(value>=0,"Wrap run spacing must be nonnegative");if(_runSpacing==value)return;_runSpacing=value;MarkNeedsLayout();}
    public void SetCrossAxisAlignment(EWrapCrossAlignment value){if(_crossAxisAlignment==value)return;_crossAxisAlignment=value;MarkNeedsLayout();}
    public void SetTextDirection(ETextDirection value){if(_textDirection==value)return;_textDirection=value;MarkNeedsLayout();}
    public void SetVerticalDirection(EVerticalDirection value){if(_verticalDirection==value)return;_verticalDirection=value;MarkNeedsLayout();}
    public void SetClipBehavior(EClipBehavior value){if(_clipBehavior==value)return;_clipBehavior=value;}
    private sealed class RunMetrics { public Sizef AxisSize=new();public ulong LeadingChildIndex,ChildCount; }
    private sealed class LayoutSizes { public Sizef ChildrenAxisSize=new();public readonly List<RunMetrics> Runs=new(); }
    private static VisualWrapSlot WrapSlot(VisualNode child){GuiAssert.Require(child.Slot()!=null,"Wrap requires child slot");return (VisualWrapSlot)child.Slot()!;}
    protected override void ApplySlot(VisualNode child)=>SetChildSlot(child,new VisualWrapSlot());
    private bool IsHorizontal()=>_direction==EAxis.Horizontal;
    private bool FlipMainAxis(){if(IsEmpty())return false;return IsHorizontal()?_textDirection==ETextDirection.RTL:_verticalDirection==EVerticalDirection.Up;}
    private bool FlipCrossAxis(){if(IsEmpty())return false;return IsHorizontal()?_verticalDirection==EVerticalDirection.Up:_textDirection==ETextDirection.RTL;}
    private float MainSize(Sizef size)=>IsHorizontal()?size.Width:size.Height;
    private Sizef AxisSizeFromSize(Sizef size)=>IsHorizontal()?size:size.Flipped();
    private Sizef SizeFromAxis(Sizef size)=>IsHorizontal()?size:size.Flipped();
    private Offsetf OffsetFromAxis(float main,float cross)=>IsHorizontal()?new(main,cross):new(cross,main);
    private Sizef ConstrainAxisSize(Sizef size,BoxConstraints constraints)=>IsHorizontal()?constraints.Constrain(size):constraints.Flipped().Constrain(size);
    private BoxConstraints ConstraintsForChild(BoxConstraints constraints)=>IsHorizontal()?new(0,constraints.MaxWidth,0,float.PositiveInfinity):new(0,float.PositiveInfinity,0,constraints.MaxHeight);
    private bool NextChildIndex(ulong current,bool reverse,ref ulong index){if(reverse){if(current==0)return false;index=current-1;return true;}if(current+1>=NumChildren())return false;index=current+1;return true;}
    private LayoutSizes ComputeRuns(BoxConstraints constraints,Func<VisualNode,BoxConstraints,Sizef> layoutChild,Action<ulong,VisualNode,BoxConstraints,Sizef> recordChild)
    {
        var sizes=new LayoutSizes();var childConstraints=ConstraintsForChild(constraints);float maxMain=MainSize(constraints.Biggest());bool mainFlipped=FlipMainAxis();
        for(ulong index=0;index<NumChildren();index++)
        {
            var child=ChildAt(index);var childSize=layoutChild(child,childConstraints);recordChild(index,child,childConstraints,childSize);var childAxis=AxisSizeFromSize(childSize);
            if(sizes.Runs.Count!=0)
            {
                var current=sizes.Runs[^1];float nextMain=current.AxisSize.Width+childAxis.Width+Spacing();
                if(nextMain-maxMain<=GuiConstants.FlutterPrecisionErrorTolerance){current.AxisSize.Width=nextMain;current.AxisSize.Height=CppMath.Max(current.AxisSize.Height,childAxis.Height);current.ChildCount++;if(mainFlipped)current.LeadingChildIndex=index;continue;}
            }
            var run=new RunMetrics{AxisSize=childAxis,LeadingChildIndex=index,ChildCount=1};sizes.Runs.Add(run);
        }
        float main=0,cross=0;for(int i=0;i<sizes.Runs.Count;i++){var run=sizes.Runs[i];main=CppMath.Max(main,run.AxisSize.Width);cross+=run.AxisSize.Height;}
        if(sizes.Runs.Count>1)cross+=RunSpacing()*(sizes.Runs.Count-1);sizes.ChildrenAxisSize=new(main,cross);return sizes;
    }
    private void PositionChildren(LayoutSizes sizes,Sizef freeAxis,Sizef containerAxis,Action<ulong,Offsetf> positionChild,Func<ulong,Sizef> getChildSize)
    {
        if(sizes.Runs.Count==0)return;
        float crossFree=CppMath.Max(0,freeAxis.Height);bool mainFlipped=FlipMainAxis(),crossFlipped=FlipCrossAxis();var effectiveCross=crossFlipped?FlipCrossAlignment(CrossAxisAlignment()):CrossAxisAlignment();
        DistributeSpace(RunAlignment(),crossFree,(ulong)sizes.Runs.Count,crossFlipped,RunSpacing(),out float runLeading,out float runBetween);float runCross=runLeading;
        void PositionRun(RunMetrics run)
        {
            ulong childCount=run.ChildCount;if(childCount==0)return;
            float mainFree=CppMath.Max(0,containerAxis.Width-run.AxisSize.Width);DistributeSpace(Alignment(),mainFree,childCount,mainFlipped,Spacing(),out float childLeading,out float childBetween);float childMain=childLeading;
            void PositionChildIndex(ulong childIndex){var childSize=getChildSize(childIndex);var childAxis=AxisSizeFromSize(childSize);float childCross=ChildCrossAxisOffset(effectiveCross,run.AxisSize.Height-childAxis.Height);positionChild(childIndex,OffsetFromAxis(childMain,runCross+childCross));childMain+=childAxis.Width+childBetween;}
            ulong index=run.LeadingChildIndex;for(ulong remaining=childCount;remaining>0;--remaining){PositionChildIndex(index);if(remaining==1)break;bool found=NextChildIndex(index,mainFlipped,ref index);GuiAssert.Require(found,"Wrap run count inconsistent with child indices");}
        }
        if(crossFlipped){for(int i=sizes.Runs.Count;i>0;--i){var run=sizes.Runs[i-1];PositionRun(run);runCross+=run.AxisSize.Height+runBetween;}return;}
        for(int i=0;i<sizes.Runs.Count;i++){var run=sizes.Runs[i];PositionRun(run);runCross+=run.AxisSize.Height+runBetween;}
    }
    protected override void PerformPaint(PaintContext context){bool clip=IsOverflowing()&&_clipBehavior!=EClipBehavior.None&&!Size().IsEmpty();if(clip)context.PushClipRect(Rectf.OffsetSize(Offsetf.Zero(),Size()));for(ulong i=0;i<NumChildren();i++){var child=ChildAt(i);PaintChild(child,context,WrapSlot(child).Offset());}if(clip)context.PopClip();}
    protected override bool HitTestChildren(VisualHitTestResult result,Offsetf position){ulong i=NumChildren();while(i>0){--i;var child=ChildAt(i);if(HitTestChild(result,child,position,WrapSlot(child).Offset()))return true;}return false;}
    protected override void PerformLayout()
    {
        ResetChildOffsets();if(IsEmpty()){SetSize(Constraints().Smallest());_overflow=Sizef.Zero();return;}
        var sizes=ComputeRuns(Constraints(),(child,c)=>{LayoutChild(child,c);return child.Size();},(i,child,c,size)=>{});
        var container=ConstrainAxisSize(sizes.ChildrenAxisSize,Constraints());SetSize(SizeFromAxis(container));var free=container-sizes.ChildrenAxisSize;_overflow=SizeFromAxis(new Sizef(CppMath.Max(0,-free.Width),CppMath.Max(0,-free.Height)));
        PositionChildren(sizes,free,container);
    }
    protected override float ComputeMinIntrinsicWidth(float height){if(IsHorizontal()){float width=0;for(ulong i=0;i<NumChildren();i++)width=CppMath.Max(width,ChildAt(i).GetMinIntrinsicWidth(float.PositiveInfinity));return width;}return GetDryLayout(BoxConstraints.LooseHeight(height)).Width;}
    protected override float ComputeMaxIntrinsicWidth(float height){if(IsHorizontal()){float width=0;for(ulong i=0;i<NumChildren();i++)width+=ChildAt(i).GetMaxIntrinsicWidth(float.PositiveInfinity);return width;}return GetDryLayout(BoxConstraints.LooseHeight(height)).Width;}
    protected override float ComputeMinIntrinsicHeight(float width){if(IsHorizontal())return GetDryLayout(BoxConstraints.LooseWidth(width)).Height;float height=0;for(ulong i=0;i<NumChildren();i++)height=CppMath.Max(height,ChildAt(i).GetMinIntrinsicHeight(float.PositiveInfinity));return height;}
    protected override float ComputeMaxIntrinsicHeight(float width){if(IsHorizontal())return GetDryLayout(BoxConstraints.LooseWidth(width)).Height;float height=0;for(ulong i=0;i<NumChildren();i++)height+=ChildAt(i).GetMaxIntrinsicHeight(float.PositiveInfinity);return height;}
    protected override Sizef ComputeDryLayout(BoxConstraints constraints)
    {
        var childConstraints=ConstraintsForChild(constraints);float mainLimit=MainSize(constraints.Biggest());float main=0,cross=0,runMain=0,runCross=0;ulong childCount=0;
        for(ulong i=0;i<NumChildren();i++){var child=ChildAt(i);var childSize=child.GetDryLayout(childConstraints);var childAxis=AxisSizeFromSize(childSize);if(childCount>0&&runMain+childAxis.Width+_spacing>mainLimit){main=CppMath.Max(main,runMain);cross+=runCross+_runSpacing;runMain=0;runCross=0;childCount=0;}runMain+=childAxis.Width;runCross=CppMath.Max(runCross,childAxis.Height);if(childCount>0)runMain+=_spacing;childCount++;}
        cross+=runCross;main=CppMath.Max(main,runMain);return constraints.Constrain(SizeFromAxis(new Sizef(main,cross)));
    }
    protected override float? ComputeDryBaseline(BoxConstraints constraints,ETextBaseline baseline)
    {
        if(IsEmpty())return null;var childConstraints=ConstraintsForChild(constraints);var sizes=ComputeRuns(constraints,(child,c)=>child.GetDryLayout(c),(i,child,c,size)=>{});var container=ConstrainAxisSize(sizes.ChildrenAxisSize,constraints);float? result=null;
        // Source passes ChildrenAxisSize here (not container-minus-children).
        PositionChildren(sizes,sizes.ChildrenAxisSize,container,(i,offset)=>{float? childBaseline=ChildAt(i).GetDryBaseline(childConstraints,baseline);if(!childBaseline.HasValue)return;float candidate=childBaseline.Value+offset.Y;result=result.HasValue?CppMath.Min(result.Value,candidate):candidate;},i=>ChildAt(i).GetDryLayout(childConstraints));return result;
    }
    protected override float? ComputeDistanceToActualBaseline(ETextBaseline baseline){float? result=null;for(ulong i=0;i<NumChildren();i++){var child=ChildAt(i);float? childBaseline=child.GetDistanceToActualBaseline(baseline);if(!childBaseline.HasValue)continue;float candidate=childBaseline.Value+WrapSlot(child).Offset().Y;result=result.HasValue?CppMath.Min(result.Value,candidate):candidate;}return result;}
    private float ChildCrossAxisOffset(EWrapCrossAlignment alignment,float free)=>alignment switch{EWrapCrossAlignment.Start=>0,EWrapCrossAlignment.End=>free,EWrapCrossAlignment.Center=>free/2,_=>throw new InvalidOperationException("Unreachable wrap alignment")};
    private void PositionChildren(LayoutSizes sizes,Sizef free,Sizef container){ResetChildOffsets();PositionChildren(sizes,free,container,(i,offset)=>WrapSlot(ChildAt(i)).LayoutOffset=offset,i=>ChildAt(i).Size());}
    private void ResetChildOffsets(){for(ulong i=0;i<NumChildren();i++)WrapSlot(ChildAt(i)).LayoutOffset=Offsetf.Zero();}
    private static EWrapCrossAlignment FlipCrossAlignment(EWrapCrossAlignment alignment)=>alignment switch{EWrapCrossAlignment.Start=>EWrapCrossAlignment.End,EWrapCrossAlignment.End=>EWrapCrossAlignment.Start,EWrapCrossAlignment.Center=>EWrapCrossAlignment.Center,_=>throw new InvalidOperationException("Unreachable wrap cross alignment")};
    private static void DistributeSpace(EWrapAlignment alignment,float free,ulong count,bool flipped,float spacing,out float leading,out float between)
    {
        GuiAssert.Require(count>0,"Wrap distribution needs at least one item");
        if(alignment==EWrapAlignment.End){DistributeSpace(EWrapAlignment.Start,free,count,!flipped,spacing,out leading,out between);return;}
        if(alignment==EWrapAlignment.SpaceBetween&&count<2){DistributeSpace(EWrapAlignment.Start,free,count,flipped,spacing,out leading,out between);return;}
        switch(alignment)
        {
            case EWrapAlignment.Start:leading=flipped?free:0;between=spacing;return;
            case EWrapAlignment.Center:leading=free/2;between=spacing;return;
            case EWrapAlignment.SpaceBetween:leading=0;between=free/(float)(count-1)+spacing;return;
            case EWrapAlignment.SpaceAround:leading=free/(float)count/2;between=free/(float)count+spacing;return;
            case EWrapAlignment.SpaceEvenly:leading=free/(float)(count+1);between=free/(float)(count+1)+spacing;return;
            default:throw new InvalidOperationException("Unreachable wrap distribution");
        }
    }
}
