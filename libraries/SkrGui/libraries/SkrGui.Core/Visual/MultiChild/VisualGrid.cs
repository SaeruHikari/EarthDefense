namespace SkrGui;

// Source: visual/multi_child/visual_grid.hpp + src/visual/multi_child/visual_grid.cpp.
public enum EVisualGridTrackKind : byte { Fixed,Auto,Star,Percent }
public enum EVisualGridContentAlignment : byte { Start,Center,End,Stretch,SpaceBetween,SpaceAround,SpaceEvenly }
public enum EVisualGridItemAlignment : byte { Stretch,Start,Center,End }
public struct VisualGridTrackSize : IEquatable<VisualGridTrackSize>
{
    private EVisualGridTrackKind _kind;private float _value;
    public VisualGridTrackSize(){_kind=EVisualGridTrackKind.Star;_value=1;}
    public static VisualGridTrackSize Fixed(float value){GuiAssert.Require(value>=0,"Fixed track must be nonnegative");return new(){_kind=EVisualGridTrackKind.Fixed,_value=value};}
    public static VisualGridTrackSize Auto()=>new(){_kind=EVisualGridTrackKind.Auto,_value=0};
    public static VisualGridTrackSize Star(float weight=1){GuiAssert.Require(weight>=0,"Star weight must be nonnegative");return new(){_kind=EVisualGridTrackKind.Star,_value=weight};}
    public static VisualGridTrackSize Percent(float value){GuiAssert.Require(value>=0,"Percent track must be nonnegative");return new(){_kind=EVisualGridTrackKind.Percent,_value=value};}
    public EVisualGridTrackKind Kind()=>_kind;public float Value()=>_value;
    public static bool operator==(VisualGridTrackSize a,VisualGridTrackSize b)=>a._kind==b._kind&&a._value==b._value;
    public static bool operator!=(VisualGridTrackSize a,VisualGridTrackSize b)=>!(a==b);
    public bool Equals(VisualGridTrackSize b)=>this==b;public override bool Equals(object? obj)=>obj is VisualGridTrackSize b&&this==b;public override int GetHashCode()=>HashCode.Combine(_kind,_value);
}
public class VisualGridSlot : VisualSlot
{
    private uint _rowStart,_columnStart,_rowSpan=1,_columnSpan=1;private EVisualGridItemAlignment? _justifySelf,_alignSelf;internal Offsetf LayoutOffset=new();
    public uint RowStart()=>_rowStart;public uint ColumnStart()=>_columnStart;public uint RowSpan()=>_rowSpan;public uint ColumnSpan()=>_columnSpan;public EVisualGridItemAlignment? JustifySelf()=>_justifySelf;public EVisualGridItemAlignment? AlignSelf()=>_alignSelf;public Offsetf Offset()=>LayoutOffset;
    public void SetRowStart(uint value){if(_rowStart==value)return;_rowStart=value;Node()?.MarkNeedsLayoutForSizedByParentChange();}
    public void SetColumnStart(uint value){if(_columnStart==value)return;_columnStart=value;Node()?.MarkNeedsLayoutForSizedByParentChange();}
    public void SetRowSpan(uint value){GuiAssert.Require(value>0,"Row span must be positive");if(_rowSpan==value)return;_rowSpan=value;Node()?.MarkNeedsLayoutForSizedByParentChange();}
    public void SetColumnSpan(uint value){GuiAssert.Require(value>0,"Column span must be positive");if(_columnSpan==value)return;_columnSpan=value;Node()?.MarkNeedsLayoutForSizedByParentChange();}
    public void SetArea(uint rowStart,uint columnStart,uint rowSpan=1,uint columnSpan=1){SetRowStart(rowStart);SetColumnStart(columnStart);SetRowSpan(rowSpan);SetColumnSpan(columnSpan);}
    public void SetJustifySelf(EVisualGridItemAlignment? value){if(_justifySelf==value)return;_justifySelf=value;Node()?.MarkNeedsLayoutForSizedByParentChange();}
    public void SetAlignSelf(EVisualGridItemAlignment? value){if(_alignSelf==value)return;_alignSelf=value;Node()?.MarkNeedsLayoutForSizedByParentChange();}
    public void ClearJustifySelf()=>SetJustifySelf(null);public void ClearAlignSelf()=>SetAlignSelf(null);
}
public class VisualGrid : VisualMultiChild
{
    private readonly List<VisualGridTrackSize> _columns=new(),_rows=new();private float _columnGap,_rowGap;private EVisualGridContentAlignment _justifyContent=EVisualGridContentAlignment.Stretch,_alignContent=EVisualGridContentAlignment.Stretch;private EVisualGridItemAlignment _justifyItems=EVisualGridItemAlignment.Stretch,_alignItems=EVisualGridItemAlignment.Stretch;private ETextDirection _textDirection=ETextDirection.LTR;private bool _clipToBounds,_hasVisualOverflow;
    public VisualGrid(){_columns.Add(VisualGridTrackSize.Star());_rows.Add(VisualGridTrackSize.Star());}
    public ulong NumColumns()=>(ulong)_columns.Count;public ulong NumRows()=>(ulong)_rows.Count;
    public IReadOnlyList<VisualGridTrackSize> Columns()=>_columns.AsReadOnly();public IReadOnlyList<VisualGridTrackSize> Rows()=>_rows.AsReadOnly();
    public VisualGridTrackSize ColumnTrack(ulong index){GuiAssert.Require(index<(ulong)_columns.Count,"Column index out of range");return _columns[(int)index];}
    public VisualGridTrackSize RowTrack(ulong index){GuiAssert.Require(index<(ulong)_rows.Count,"Row index out of range");return _rows[(int)index];}
    // Source bulk setters do not mark layout dirty. Preserve that behavior.
    public void SetColumns(IReadOnlyList<VisualGridTrackSize> tracks){_columns.Clear();_columns.EnsureCapacity(tracks.Count);for(int i=0;i<tracks.Count;i++)_columns.Add(tracks[i]);}
    public void SetRows(IReadOnlyList<VisualGridTrackSize> tracks){_rows.Clear();_rows.EnsureCapacity(tracks.Count);for(int i=0;i<tracks.Count;i++)_rows.Add(tracks[i]);}
    public void SetColumnTrack(ulong index,VisualGridTrackSize track){GuiAssert.Require(index<(ulong)_columns.Count,"Column index out of range");if(_columns[(int)index]==track)return;_columns[(int)index]=track;MarkNeedsLayout();}
    public void SetRowTrack(ulong index,VisualGridTrackSize track){GuiAssert.Require(index<(ulong)_rows.Count,"Row index out of range");if(_rows[(int)index]==track)return;_rows[(int)index]=track;MarkNeedsLayout();}
    public void AddColumn(VisualGridTrackSize track){_columns.Add(track);MarkNeedsLayout();}public void AddRow(VisualGridTrackSize track){_rows.Add(track);MarkNeedsLayout();}public void ClearColumns(){_columns.Clear();MarkNeedsLayout();}public void ClearRows(){_rows.Clear();MarkNeedsLayout();}
    public float ColumnGap()=>_columnGap;public float RowGap()=>_rowGap;public EVisualGridContentAlignment JustifyContent()=>_justifyContent;public EVisualGridContentAlignment AlignContent()=>_alignContent;public EVisualGridItemAlignment JustifyItems()=>_justifyItems;public EVisualGridItemAlignment AlignItems()=>_alignItems;public ETextDirection TextDirection()=>_textDirection;public bool ClipToBounds()=>_clipToBounds;public bool IsOverflowing()=>_hasVisualOverflow;
    public void SetColumnGap(float value){GuiAssert.Require(value>=0,"Column gap must be nonnegative");if(_columnGap==value)return;_columnGap=value;MarkNeedsLayout();}
    public void SetRowGap(float value){GuiAssert.Require(value>=0,"Row gap must be nonnegative");if(_rowGap==value)return;_rowGap=value;MarkNeedsLayout();}
    public void SetGap(float columnGap,float rowGap){SetColumnGap(columnGap);SetRowGap(rowGap);}
    public void SetJustifyContent(EVisualGridContentAlignment value){if(_justifyContent==value)return;_justifyContent=value;MarkNeedsLayout();}
    public void SetAlignContent(EVisualGridContentAlignment value){if(_alignContent==value)return;_alignContent=value;MarkNeedsLayout();}
    public void SetJustifyItems(EVisualGridItemAlignment value){if(_justifyItems==value)return;_justifyItems=value;MarkNeedsLayout();}
    public void SetAlignItems(EVisualGridItemAlignment value){if(_alignItems==value)return;_alignItems=value;MarkNeedsLayout();}
    public void SetTextDirection(ETextDirection value){if(_textDirection==value)return;_textDirection=value;MarkNeedsLayout();}
    public void SetClipToBounds(bool value){if(_clipToBounds==value)return;_clipToBounds=value;}
    private sealed class ResolvedTrack { public VisualGridTrackSize Track=new();public float Size,Offset,StarWeight;public bool NeedsAutoMeasure; }
    private struct AxisResult { public float Size,ContentExtent; }
    private struct LayoutResult { public Sizef Size,ContentSize; }
    private static VisualGridSlot GridSlot(VisualNode child){GuiAssert.Require(child.Slot()!=null,"Grid requires child slot");return (VisualGridSlot)child.Slot()!;}
    private static float GapSpace(ulong count,float gap)=>count>0?gap*(float)(count-1):0;
    private static float AxisMaxExtent(BoxConstraints c,EAxis axis)=>axis==EAxis.Horizontal?c.MaxWidth:c.MaxHeight;
    private static float AxisConstrainExtent(BoxConstraints c,EAxis axis,float extent)=>axis==EAxis.Horizontal?c.ConstrainWidth(extent):c.ConstrainHeight(extent);
    private static bool AxisIsBounded(BoxConstraints c,EAxis axis)=>axis==EAxis.Horizontal?c.HasBoundedWidth():c.HasBoundedHeight();
    private static uint SlotAxisStart(VisualGridSlot slot,EAxis axis)=>axis==EAxis.Horizontal?slot.ColumnStart():slot.RowStart();
    private static uint SlotAxisSpan(VisualGridSlot slot,EAxis axis)=>axis==EAxis.Horizontal?slot.ColumnSpan():slot.RowSpan();
    private static void CheckSlotRange(VisualGridSlot slot,ulong rows,ulong columns){GuiAssert.Require(slot.RowSpan()>0&&slot.ColumnSpan()>0&&(ulong)slot.RowStart()+slot.RowSpan()<=rows&&(ulong)slot.ColumnStart()+slot.ColumnSpan()<=columns,"Grid slot area out of range");}
    private static float SpanExtent(List<ResolvedTrack> tracks,uint start,uint span,float gap){float extent=GapSpace(span,gap);for(uint i=0;i<span;i++)extent+=tracks[(int)(start+i)].Size;return extent;}
    private static float SpanPhysicalStart(List<ResolvedTrack> tracks,uint start,uint span){float result=tracks[(int)start].Offset;for(uint i=1;i<span;i++)result=CppMath.Min(result,tracks[(int)(start+i)].Offset);return result;}
    private static void DistributeContentSpace(EVisualGridContentAlignment alignment,float free,ulong count,float gap,bool horizontal,ETextDirection direction,out float leading,out float between)
    {
        leading=0;between=gap;if(free<=0)return;
        switch(alignment)
        {
            case EVisualGridContentAlignment.Start:leading=horizontal&&direction==ETextDirection.RTL?free:0;return;
            case EVisualGridContentAlignment.End:leading=horizontal&&direction==ETextDirection.RTL?0:free;return;
            case EVisualGridContentAlignment.Center:leading=free/2;return;
            case EVisualGridContentAlignment.SpaceBetween:if(count>1)between=gap+free/(float)(count-1);return;
            case EVisualGridContentAlignment.SpaceAround:if(count>0){float space=free/(float)count;leading=space/2;between=gap+space;}return;
            case EVisualGridContentAlignment.SpaceEvenly:if(count>0){float space=free/(float)(count+1);leading=space;between=gap+space;}return;
            case EVisualGridContentAlignment.Stretch:return;
            default:throw new InvalidOperationException("Unreachable grid content alignment");
        }
    }
    private static float ContentExtentOf(List<ResolvedTrack> tracks,float gap){float extent=GapSpace((ulong)tracks.Count,gap);for(int i=0;i<tracks.Count;i++)extent+=tracks[i].Size;return extent;}
    private static void PositionTracks(List<ResolvedTrack> tracks,float axisSize,float gap,EVisualGridContentAlignment alignment,bool horizontal,ETextDirection direction)
    {
        float content=ContentExtentOf(tracks,gap),free=axisSize-content;
        if(free>0&&alignment==EVisualGridContentAlignment.Stretch)
        {
            ulong stretchable=0;for(int i=0;i<tracks.Count;i++)if(tracks[i].NeedsAutoMeasure)++stretchable;
            if(stretchable>0){float extra=free/(float)stretchable;for(int i=0;i<tracks.Count;i++)if(tracks[i].NeedsAutoMeasure)tracks[i].Size+=extra;content=ContentExtentOf(tracks,gap);free=axisSize-content;}
        }
        DistributeContentSpace(alignment,free,(ulong)tracks.Count,gap,horizontal,direction,out float leading,out float between);
        if(horizontal&&direction==ETextDirection.RTL){float cursor=axisSize-leading;for(int i=0;i<tracks.Count;i++){cursor-=tracks[i].Size;tracks[i].Offset=cursor;cursor-=between;}return;}
        float forward=leading;for(int i=0;i<tracks.Count;i++){tracks[i].Offset=forward;forward+=tracks[i].Size+between;}
    }
    private static float ItemAxisOffset(EVisualGridItemAlignment alignment,float free,bool horizontal,ETextDirection direction)=>alignment switch{EVisualGridItemAlignment.Stretch or EVisualGridItemAlignment.Start=>horizontal&&direction==ETextDirection.RTL?free:0,EVisualGridItemAlignment.End=>horizontal&&direction==ETextDirection.RTL?0:free,EVisualGridItemAlignment.Center=>free/2,_=>throw new InvalidOperationException("Unreachable grid item alignment")};
    private static Rectf ChildAreaRect(VisualGridSlot slot,List<ResolvedTrack> columns,List<ResolvedTrack> rows,float columnGap,float rowGap){float left=SpanPhysicalStart(columns,slot.ColumnStart(),slot.ColumnSpan()),top=SpanPhysicalStart(rows,slot.RowStart(),slot.RowSpan());return Rectf.LTWH(left,top,SpanExtent(columns,slot.ColumnStart(),slot.ColumnSpan(),columnGap),SpanExtent(rows,slot.RowStart(),slot.RowSpan(),rowGap));}
    private static BoxConstraints ChildConstraintsFor(Rectf area,EVisualGridItemAlignment justify,EVisualGridItemAlignment align){float width=CppMath.Max(0,area.Width()),height=CppMath.Max(0,area.Height());return new(justify==EVisualGridItemAlignment.Stretch?width:0,width,align==EVisualGridItemAlignment.Stretch?height:0,height);}
    private static Offsetf ChildOffsetFor(Rectf area,Sizef childSize,EVisualGridItemAlignment justify,EVisualGridItemAlignment align,ETextDirection direction){float freeWidth=CppMath.Max(0,area.Width()-childSize.Width),freeHeight=CppMath.Max(0,area.Height()-childSize.Height);return new(area.Left+ItemAxisOffset(justify,freeWidth,true,direction),area.Top+ItemAxisOffset(align,freeHeight,false,direction));}
    private EVisualGridItemAlignment ResolveJustify(VisualGridSlot slot)=>slot.JustifySelf()??JustifyItems();
    private EVisualGridItemAlignment ResolveAlign(VisualGridSlot slot)=>slot.AlignSelf()??AlignItems();
    private AxisResult ResolveAxis(EAxis axis,BoxConstraints constraints,List<ResolvedTrack> tracks,Func<ulong,float> measureChild)
    {
        var source=axis==EAxis.Horizontal?Columns():Rows();float gap=axis==EAxis.Horizontal?ColumnGap():RowGap();bool bounded=AxisIsBounded(constraints,axis);float max=AxisMaxExtent(constraints,axis);float trackSpace=bounded?CppMath.Max(0,max-GapSpace((ulong)source.Count,gap)):0;
        tracks.Clear();for(int i=0;i<source.Count;i++)tracks.Add(new());float resolved=GapSpace((ulong)source.Count,gap),totalStar=0;
        for(int i=0;i<source.Count;i++)
        {
            var track=tracks[i];track.Track=source[i];
            switch(track.Track.Kind())
            {
                case EVisualGridTrackKind.Fixed:track.Size=track.Track.Value();break;
                case EVisualGridTrackKind.Auto:track.NeedsAutoMeasure=true;break;
                case EVisualGridTrackKind.Percent:if(bounded)track.Size=trackSpace*track.Track.Value();else track.NeedsAutoMeasure=true;break;
                case EVisualGridTrackKind.Star:if(bounded){track.StarWeight=track.Track.Value();totalStar+=track.StarWeight;}else track.NeedsAutoMeasure=true;break;
                default:throw new InvalidOperationException("Unreachable grid track kind");
            }
            resolved+=track.Size;
        }
        MeasureAutoTracks(axis,tracks,measureChild);resolved=ContentExtentOf(tracks,gap);
        if(bounded&&totalStar>0){float remaining=CppMath.Max(0,trackSpace-(resolved-GapSpace((ulong)tracks.Count,gap)));for(int i=0;i<tracks.Count;i++){var track=tracks[i];if(track.StarWeight>0)track.Size=remaining*track.StarWeight/totalStar;}resolved=ContentExtentOf(tracks,gap);}
        float size=totalStar>0&&bounded?AxisConstrainExtent(constraints,axis,max):AxisConstrainExtent(constraints,axis,resolved);
        PositionTracks(tracks,size,gap,axis==EAxis.Horizontal?JustifyContent():AlignContent(),axis==EAxis.Horizontal,TextDirection());return new(){Size=size,ContentExtent=ContentExtentOf(tracks,gap)};
    }
    private void MeasureAutoTracks(EAxis axis,List<ResolvedTrack> tracks,Func<ulong,float> measureChild)
    {
        float gap=axis==EAxis.Horizontal?ColumnGap():RowGap();
        for(ulong childIndex=0;childIndex<NumChildren();childIndex++)
        {
            var slot=GridSlot(ChildAt(childIndex));CheckSlotRange(slot,NumRows(),NumColumns());uint start=SlotAxisStart(slot,axis),span=SlotAxisSpan(slot,axis);ulong autoCount=0;
            for(uint i=0;i<span;i++)if(tracks[(int)(start+i)].NeedsAutoMeasure)++autoCount;if(autoCount==0)continue;
            float required=measureChild(childIndex),current=SpanExtent(tracks,start,span,gap),extra=CppMath.Max(0,required-current);if(extra==0)continue;float extraPer=extra/(float)autoCount;
            for(uint i=0;i<span;i++){var track=tracks[(int)(start+i)];if(track.NeedsAutoMeasure)track.Size+=extraPer;}
        }
    }
    private LayoutResult ComputeLayout(bool writeSlots,BoxConstraints constraints,Func<ulong,BoxConstraints,Sizef> layoutChild)
    {
        var columns=new List<ResolvedTrack>();var rows=new List<ResolvedTrack>();
        var columnResult=ResolveAxis(EAxis.Horizontal,constraints,columns,i=>ChildAt(i).GetMaxIntrinsicWidth(float.PositiveInfinity));
        var rowResult=ResolveAxis(EAxis.Vertical,constraints,rows,i=>{var child=ChildAt(i);var slot=GridSlot(child);float width=SpanExtent(columns,slot.ColumnStart(),slot.ColumnSpan(),ColumnGap());return child.GetDryLayout(new BoxConstraints(0,width,0,float.PositiveInfinity)).Height;});
        for(ulong i=0;i<NumChildren();i++){var slot=GridSlot(ChildAt(i));var area=ChildAreaRect(slot,columns,rows,ColumnGap(),RowGap());var justify=ResolveJustify(slot);var align=ResolveAlign(slot);var c=ChildConstraintsFor(area,justify,align);var childSize=layoutChild(i,c);if(writeSlots)GridSlot(ChildAt(i)).LayoutOffset=ChildOffsetFor(area,childSize,justify,align,TextDirection());}
        return new(){Size=new(columnResult.Size,rowResult.Size),ContentSize=new(columnResult.ContentExtent,rowResult.ContentExtent)};
    }
    private float? ComputeBaseline(BoxConstraints constraints,ETextBaseline baseline)
    {
        var columns=new List<ResolvedTrack>();var rows=new List<ResolvedTrack>();
        ResolveAxis(EAxis.Horizontal,constraints,columns,i=>ChildAt(i).GetMaxIntrinsicWidth(float.PositiveInfinity));
        ResolveAxis(EAxis.Vertical,constraints,rows,i=>{var child=ChildAt(i);var slot=GridSlot(child);float width=SpanExtent(columns,slot.ColumnStart(),slot.ColumnSpan(),ColumnGap());return child.GetDryLayout(new BoxConstraints(0,width,0,float.PositiveInfinity)).Height;});
        for(ulong i=0;i<NumChildren();i++){var child=ChildAt(i);var slot=GridSlot(child);var area=ChildAreaRect(slot,columns,rows,ColumnGap(),RowGap());var justify=ResolveJustify(slot);var align=ResolveAlign(slot);var c=ChildConstraintsFor(area,justify,align);float? childBaseline=child.GetDryBaseline(c,baseline);if(!childBaseline.HasValue)continue;var childSize=child.GetDryLayout(c);var offset=ChildOffsetFor(area,childSize,justify,align,TextDirection());return childBaseline.Value+offset.Y;}return null;
    }
    protected override void ApplySlot(VisualNode child)=>SetChildSlot(child,new VisualGridSlot());
    protected override void PerformPaint(PaintContext context){bool clip=_clipToBounds&&!Size().IsEmpty();if(clip)context.PushClipRect(Rectf.OffsetSize(Offsetf.Zero(),Size()));for(ulong i=0;i<NumChildren();i++){var child=ChildAt(i);PaintChild(child,context,GridSlot(child).Offset());}if(clip)context.PopClip();}
    protected override bool HitTestChildren(VisualHitTestResult result,Offsetf position){ulong i=NumChildren();while(i>0){--i;var child=ChildAt(i);if(HitTestChild(result,child,position,GridSlot(child).Offset()))return true;}return false;}
    protected override void PerformLayout(){_hasVisualOverflow=false;if(NumColumns()==0||NumRows()==0){SetSize(Constraints().Smallest());return;}var result=ComputeLayout(true,Constraints(),(i,c)=>{var child=ChildAt(i);LayoutChild(child,c);return child.Size();});SetSize(result.Size);_hasVisualOverflow=result.ContentSize.Width-Size().Width>GuiConstants.FlutterPrecisionErrorTolerance||result.ContentSize.Height-Size().Height>GuiConstants.FlutterPrecisionErrorTolerance;}
    protected override float ComputeMinIntrinsicWidth(float height)=>GetDryLayout(BoxConstraints.LooseHeight(height)).Width;
    protected override float ComputeMaxIntrinsicWidth(float height)=>GetDryLayout(BoxConstraints.LooseHeight(height)).Width;
    protected override float ComputeMinIntrinsicHeight(float width)=>GetDryLayout(BoxConstraints.LooseWidth(width)).Height;
    protected override float ComputeMaxIntrinsicHeight(float width)=>GetDryLayout(BoxConstraints.LooseWidth(width)).Height;
    protected override Sizef ComputeDryLayout(BoxConstraints constraints){if(NumColumns()==0||NumRows()==0)return constraints.Smallest();return ComputeLayout(false,constraints,(i,c)=>ChildAt(i).GetDryLayout(c)).Size;}
    protected override float? ComputeDryBaseline(BoxConstraints constraints,ETextBaseline baseline)=>NumColumns()==0||NumRows()==0?null:ComputeBaseline(constraints,baseline);
    protected override float? ComputeDistanceToActualBaseline(ETextBaseline baseline){for(ulong i=0;i<NumChildren();i++){var child=ChildAt(i);float? value=child.GetDistanceToActualBaseline(baseline);if(value.HasValue)return value.Value+GridSlot(child).Offset().Y;}return base.ComputeDistanceToActualBaseline(baseline);}
}
