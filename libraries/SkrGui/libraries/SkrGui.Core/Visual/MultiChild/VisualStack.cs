namespace SkrGui;

// Source: visual/multi_child/visual_stack.hpp + src/visual/multi_child/visual_stack.cpp.
public enum EStackFit : byte { Loose,Expand,Passthrough }
public class VisualStackSlot : VisualSlot
{
    private float? _left,_top,_right,_bottom,_width,_height;
    internal Offsetf LayoutOffset=new();
    public float? Left()=>_left;public float? Top()=>_top;public float? Right()=>_right;public float? Bottom()=>_bottom;public float? Width()=>_width;public float? Height()=>_height;
    public Offsetf Offset()=>LayoutOffset;
    public bool IsPositioned()=>_left.HasValue||_top.HasValue||_right.HasValue||_bottom.HasValue||_width.HasValue||_height.HasValue;
    public void SetLeft(float? value){if(_left==value)return;_left=value;Node()?.MarkNeedsLayoutForSizedByParentChange();}
    public void SetTop(float? value){if(_top==value)return;_top=value;Node()?.MarkNeedsLayoutForSizedByParentChange();}
    public void SetRight(float? value){if(_right==value)return;_right=value;Node()?.MarkNeedsLayoutForSizedByParentChange();}
    public void SetBottom(float? value){if(_bottom==value)return;_bottom=value;Node()?.MarkNeedsLayoutForSizedByParentChange();}
    public void SetWidth(float? value){GuiAssert.Require(!value.HasValue||value.Value>=0,"Stack slot width must be null or nonnegative");if(_width==value)return;_width=value;Node()?.MarkNeedsLayoutForSizedByParentChange();}
    public void SetHeight(float? value){GuiAssert.Require(!value.HasValue||value.Value>=0,"Stack slot height must be null or nonnegative");if(_height==value)return;_height=value;Node()?.MarkNeedsLayoutForSizedByParentChange();}
    public void SetPosition(float? left=null,float? top=null,float? right=null,float? bottom=null,float? width=null,float? height=null){SetLeft(left);SetTop(top);SetRight(right);SetBottom(bottom);SetWidth(width);SetHeight(height);}
    public void ClearLeft()=>SetLeft(null);public void ClearTop()=>SetTop(null);public void ClearRight()=>SetRight(null);public void ClearBottom()=>SetBottom(null);public void ClearWidth()=>SetWidth(null);public void ClearHeight()=>SetHeight(null);public void ClearPosition()=>SetPosition();
    internal BoxConstraints PositionedChildConstraints(Sizef stackSize)
    {float? width=_left.HasValue&&_right.HasValue?CppMath.Max(0,stackSize.Width-_right.Value-_left.Value):_width;float? height=_top.HasValue&&_bottom.HasValue?CppMath.Max(0,stackSize.Height-_bottom.Value-_top.Value):_height;return BoxConstraints.TightFor(width,height);}
}
public class VisualStack : VisualMultiChild
{
    private AlignmentMixed _alignment=AlignmentDirectional.TopStart();private ETextDirection _textDirection=ETextDirection.LTR;private EStackFit _fit=EStackFit.Loose;private EClipBehavior _clipBehavior=EClipBehavior.HardEdge;private bool _hasVisualOverflow;
    public AlignmentMixed Alignment()=>_alignment;public ETextDirection TextDirection()=>_textDirection;public Alignment ResolvedAlignment()=>_alignment.Resolve(_textDirection);public EStackFit Fit()=>_fit;public EClipBehavior ClipBehavior()=>_clipBehavior;public bool IsOverflowing()=>_hasVisualOverflow;
    public void SetAlignment(AlignmentMixed value){if(_alignment==value)return;_alignment=value;MarkNeedsLayout();}
    public void SetTextDirection(ETextDirection value){if(_textDirection==value)return;_textDirection=value;MarkNeedsLayout();}
    public void SetFit(EStackFit value){if(_fit==value)return;_fit=value;MarkNeedsLayout();}
    public void SetClipBehavior(EClipBehavior value){if(_clipBehavior==value)return;_clipBehavior=value;}
    protected static VisualStackSlot StackSlot(VisualNode child){GuiAssert.Require(child.Slot()!=null,"Stack requires child slot");return (VisualStackSlot)child.Slot()!;}
    protected override void ApplySlot(VisualNode child)=>SetChildSlot(child,new VisualStackSlot());
    protected BoxConstraints NonPositionedChildConstraints(BoxConstraints constraints)=>_fit switch{EStackFit.Loose=>constraints.Loosen(),EStackFit.Expand=>BoxConstraints.Tight(constraints.Biggest()),EStackFit.Passthrough=>constraints,_=>throw new InvalidOperationException("Unreachable stack fit")};
    private Sizef ComputeSize(BoxConstraints constraints,Func<VisualNode,BoxConstraints,Sizef> layoutChild)
    {
        bool hasChildren=!IsEmpty(),hasNonPositioned=false;float width=constraints.MinWidth,height=constraints.MinHeight;var nonPositioned=NonPositionedChildConstraints(constraints);
        for(ulong i=0;i<NumChildren();i++){var child=ChildAt(i);var slot=StackSlot(child);if(slot.IsPositioned())continue;hasNonPositioned=true;var childSize=layoutChild(child,nonPositioned);width=CppMath.Max(width,childSize.Width);height=CppMath.Max(height,childSize.Height);}
        if(!hasChildren){var biggest=constraints.Biggest();return biggest.IsFinite()?biggest:constraints.Smallest();}
        Sizef size=new();if(hasNonPositioned){size=new(width,height);GuiAssert.Require(size.Width==constraints.ConstrainWidth(width)&&size.Height==constraints.ConstrainHeight(height),"Stack size must satisfy constraints");}else size=constraints.Biggest();
        GuiAssert.Require(size.IsFinite(),"Only-positioned Stack needs bounded constraints");return size;
    }
    protected override void PerformPaint(PaintContext context)
    {bool clip=_clipBehavior!=EClipBehavior.None&&_hasVisualOverflow&&!Size().IsEmpty();if(clip)context.PushClipRect(Rectf.OffsetSize(Offsetf.Zero(),Size()));PerformPaintStack(context);if(clip)context.PopClip();}
    protected override bool HitTestChildren(VisualHitTestResult result,Offsetf position)=>HitTestStack(result,position);
    protected override void PerformLayout()
    {
        _hasVisualOverflow=false;var size=ComputeSize(Constraints(),(child,c)=>{LayoutChild(child,c);return child.Size();});SetSize(size);var alignment=ResolvedAlignment();
        for(ulong i=0;i<NumChildren();i++){var child=ChildAt(i);var slot=StackSlot(child);slot.LayoutOffset=Offsetf.Zero();if(!slot.IsPositioned()){var delta=Size()-child.Size();slot.LayoutOffset=alignment.AlongOffset(new Offsetf(delta.Width,delta.Height));continue;}_hasVisualOverflow=LayoutPositionedChild(child,slot,Size(),alignment)||_hasVisualOverflow;}
    }
    protected override float ComputeMinIntrinsicWidth(float height){float extent=0;for(ulong i=0;i<NumChildren();i++){var child=ChildAt(i);if(!StackSlot(child).IsPositioned())extent=CppMath.Max(extent,child.GetMinIntrinsicWidth(height));}return extent;}
    protected override float ComputeMaxIntrinsicWidth(float height){float extent=0;for(ulong i=0;i<NumChildren();i++){var child=ChildAt(i);if(!StackSlot(child).IsPositioned())extent=CppMath.Max(extent,child.GetMaxIntrinsicWidth(height));}return extent;}
    protected override float ComputeMinIntrinsicHeight(float width){float extent=0;for(ulong i=0;i<NumChildren();i++){var child=ChildAt(i);if(!StackSlot(child).IsPositioned())extent=CppMath.Max(extent,child.GetMinIntrinsicHeight(width));}return extent;}
    protected override float ComputeMaxIntrinsicHeight(float width){float extent=0;for(ulong i=0;i<NumChildren();i++){var child=ChildAt(i);if(!StackSlot(child).IsPositioned())extent=CppMath.Max(extent,child.GetMaxIntrinsicHeight(width));}return extent;}
    protected override Sizef ComputeDryLayout(BoxConstraints constraints)=>ComputeSize(constraints,(child,c)=>child.GetDryLayout(c));
    protected override float? ComputeDryBaseline(BoxConstraints constraints,ETextBaseline baseline)
    {var nonPositioned=NonPositionedChildConstraints(constraints);var alignment=ResolvedAlignment();var size=GetDryLayout(constraints);float? result=null;for(ulong i=0;i<NumChildren();i++){var child=ChildAt(i);float? childBaseline=BaselineForChild(child,StackSlot(child),size,nonPositioned,alignment,baseline);if(!childBaseline.HasValue)continue;result=result.HasValue?CppMath.Min(result.Value,childBaseline.Value):childBaseline;}return result;}
    protected override float? ComputeDistanceToActualBaseline(ETextBaseline baseline)
    {float? result=null;for(ulong i=0;i<NumChildren();i++){var child=ChildAt(i);float? childBaseline=child.GetDistanceToActualBaseline(baseline);if(!childBaseline.HasValue)continue;float candidate=childBaseline.Value+StackSlot(child).Offset().Y;result=result.HasValue?CppMath.Min(result.Value,candidate):candidate;}return result;}
    protected virtual void PerformPaintStack(PaintContext context){for(ulong i=0;i<NumChildren();i++){var child=ChildAt(i);PaintChild(child,context,StackSlot(child).Offset());}}
    protected virtual bool HitTestStack(VisualHitTestResult result,Offsetf position){ulong i=NumChildren();while(i>0){--i;var child=ChildAt(i);if(HitTestChild(result,child,position,StackSlot(child).Offset()))return true;}return false;}
    protected float? BaselineForChild(VisualNode child,VisualStackSlot slot,Sizef stackSize,BoxConstraints nonPositioned,Alignment alignment,ETextBaseline baseline)
    {
        var childConstraints=slot.IsPositioned()?slot.PositionedChildConstraints(stackSize):nonPositioned;float? offset=child.GetDryBaseline(childConstraints,baseline);if(!offset.HasValue)return null;float y=0;
        if(slot.Top().HasValue)y=slot.Top()!.Value;else{var childSize=child.GetDryLayout(childConstraints);if(slot.Bottom().HasValue)y=stackSize.Height-slot.Bottom()!.Value-childSize.Height;else{var delta=stackSize-childSize;y=alignment.AlongOffset(new Offsetf(delta.Width,delta.Height)).Y;}}
        return offset.Value+y;
    }
    private static bool LayoutPositionedChild(VisualNode child,VisualStackSlot slot,Sizef size,Alignment alignment)
    {
        GuiAssert.Require(slot.IsPositioned(),"Positioned layout requires positioned slot");LayoutChild(child,slot.PositionedChildConstraints(size));float x=0;
        if(slot.Left().HasValue)x=slot.Left()!.Value;else if(slot.Right().HasValue)x=size.Width-slot.Right()!.Value-child.Size().Width;else{var delta=size-child.Size();x=alignment.AlongOffset(new Offsetf(delta.Width,delta.Height)).X;}
        float y=0;if(slot.Top().HasValue)y=slot.Top()!.Value;else if(slot.Bottom().HasValue)y=size.Height-slot.Bottom()!.Value-child.Size().Height;else{var delta=size-child.Size();y=alignment.AlongOffset(new Offsetf(delta.Width,delta.Height)).Y;}
        slot.LayoutOffset=new(x,y);return x<0||x+child.Size().Width>size.Width||y<0||y+child.Size().Height>size.Height;
    }
}
public class VisualIndexedStack : VisualStack
{
    private ulong? _index=0;
    public ulong? Index()=>_index;
    public VisualNode? DisplayedChild(){if(!_index.HasValue||IsEmpty())return null;GuiAssert.Require(_index.Value<NumChildren(),"IndexedStack index out of range");return ChildAt(_index.Value);}
    public void SetIndex(ulong? value){if(_index==value)return;_index=value;MarkNeedsLayout();}
    public void ClearIndex()=>SetIndex(null);
    protected override float? ComputeDryBaseline(BoxConstraints constraints,ETextBaseline baseline){if(!_index.HasValue||IsEmpty())return null;var child=DisplayedChild()!;return BaselineForChild(child,StackSlot(child),GetDryLayout(constraints),NonPositionedChildConstraints(constraints),ResolvedAlignment(),baseline);}
    protected override float? ComputeDistanceToActualBaseline(ETextBaseline baseline){if(!_index.HasValue||IsEmpty())return null;var child=DisplayedChild()!;float? value=child.GetDistanceToActualBaseline(baseline);return value.HasValue?value.Value+StackSlot(child).Offset().Y:null;}
    protected override void PerformPaintStack(PaintContext context){if(!_index.HasValue||IsEmpty())return;var child=DisplayedChild()!;PaintChild(child,context,StackSlot(child).Offset());}
    protected override bool HitTestStack(VisualHitTestResult result,Offsetf position){if(!_index.HasValue||IsEmpty())return false;var child=DisplayedChild()!;return HitTestChild(result,child,position,StackSlot(child).Offset());}
}
