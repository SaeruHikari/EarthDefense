namespace SkrGui;

// Source: visual/proxy/visual_constrained.hpp + src/visual/proxy/visual_constrained.cpp.
public class VisualConstrained : VisualProxy
{
    private BoxConstraints _additionalConstraints=new();
    public static BoxConstraints Sized(float? width=null,float? height=null)=>BoxConstraints.TightFor(width,height);
    public static BoxConstraints Sized(Sizef size)=>BoxConstraints.Tight(size);
    public static BoxConstraints Width(float width)=>Sized(width,null);
    public static BoxConstraints Height(float height)=>Sized(null,height);
    public static BoxConstraints Square(float size)=>Sized(Sizef.Square(size));
    public static BoxConstraints Expand(float? width=null,float? height=null)=>new(width??float.PositiveInfinity,width??float.PositiveInfinity,height??float.PositiveInfinity,height??float.PositiveInfinity);
    public static BoxConstraints Shrink()=>Sized(Sizef.Zero());
    public BoxConstraints AdditionalConstraints()=>_additionalConstraints;
    public void SetAdditionalConstraints(BoxConstraints value){value.AssertValid();if(_additionalConstraints==value)return;_additionalConstraints=value;MarkNeedsLayout();}
    public void SetSized(float? width=null,float? height=null)=>SetAdditionalConstraints(Sized(width,height));
    public void SetSized(Sizef size)=>SetAdditionalConstraints(Sized(size));
    public void SetWidth(float width){var c=_additionalConstraints;c.MinWidth=width;c.MaxWidth=width;SetAdditionalConstraints(c);}
    public void SetHeight(float height){var c=_additionalConstraints;c.MinHeight=height;c.MaxHeight=height;SetAdditionalConstraints(c);}
    public void SetWidthExpand(){var c=_additionalConstraints;c.MinWidth=float.PositiveInfinity;c.MaxWidth=float.PositiveInfinity;SetAdditionalConstraints(c);}
    public void SetHeightExpand(){var c=_additionalConstraints;c.MinHeight=float.PositiveInfinity;c.MaxHeight=float.PositiveInfinity;SetAdditionalConstraints(c);}
    public void ClearWidth(){var unconstrained=new BoxConstraints();var c=_additionalConstraints;c.MinWidth=unconstrained.MinWidth;c.MaxWidth=unconstrained.MaxWidth;SetAdditionalConstraints(c);}
    public void ClearHeight(){var unconstrained=new BoxConstraints();var c=_additionalConstraints;c.MinHeight=unconstrained.MinHeight;c.MaxHeight=unconstrained.MaxHeight;SetAdditionalConstraints(c);}
    public void SetSquare(float size)=>SetAdditionalConstraints(Square(size));
    public void SetExpand(float? width=null,float? height=null)=>SetAdditionalConstraints(Expand(width,height));
    public void SetShrink()=>SetAdditionalConstraints(Shrink());
    protected override void PerformLayout(){var c=_additionalConstraints.Enforce(Constraints());if(Child() is {} child){LayoutChild(child,c);SetSize(child.Size());return;}SetSize(c.Constrain(Sizef.Zero()));}
    protected override float ComputeMinIntrinsicWidth(float height){if(_additionalConstraints.HasBoundedWidth()&&_additionalConstraints.HasTightWidth())return _additionalConstraints.MinWidth;float width=base.ComputeMinIntrinsicWidth(height);GuiAssert.Require(float.IsFinite(width),"Intrinsic width must be finite");return _additionalConstraints.HasInfiniteWidth()?width:_additionalConstraints.ConstrainWidth(width);}
    protected override float ComputeMaxIntrinsicWidth(float height){if(_additionalConstraints.HasBoundedWidth()&&_additionalConstraints.HasTightWidth())return _additionalConstraints.MinWidth;float width=base.ComputeMaxIntrinsicWidth(height);GuiAssert.Require(float.IsFinite(width),"Intrinsic width must be finite");return _additionalConstraints.HasInfiniteWidth()?width:_additionalConstraints.ConstrainWidth(width);}
    protected override float ComputeMinIntrinsicHeight(float width){if(_additionalConstraints.HasBoundedHeight()&&_additionalConstraints.HasTightHeight())return _additionalConstraints.MinHeight;float height=base.ComputeMinIntrinsicHeight(width);GuiAssert.Require(float.IsFinite(height),"Intrinsic height must be finite");return _additionalConstraints.HasInfiniteHeight()?height:_additionalConstraints.ConstrainHeight(height);}
    protected override float ComputeMaxIntrinsicHeight(float width){if(_additionalConstraints.HasBoundedHeight()&&_additionalConstraints.HasTightHeight())return _additionalConstraints.MinHeight;float height=base.ComputeMaxIntrinsicHeight(width);GuiAssert.Require(float.IsFinite(height),"Intrinsic height must be finite");return _additionalConstraints.HasInfiniteHeight()?height:_additionalConstraints.ConstrainHeight(height);}
    protected override Sizef ComputeDryLayout(BoxConstraints constraints){var c=_additionalConstraints.Enforce(constraints);return Child() is {} child?child.GetDryLayout(c):ComputeSizeForNoChild(c);}
    protected override Sizef ComputeSizeForNoChild(BoxConstraints constraints)=>constraints.Constrain(Sizef.Zero());
    protected override float? ComputeDryBaseline(BoxConstraints constraints,ETextBaseline baseline)=>Child()?.GetDryBaseline(_additionalConstraints.Enforce(constraints),baseline);
}
