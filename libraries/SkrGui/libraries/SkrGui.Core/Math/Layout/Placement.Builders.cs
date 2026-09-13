namespace SkrGui;
// Source: math/layout/placement.hpp nested builders; syntax-only function mapping.
public partial struct Placement
{
    public ref struct PinBuilder
    {
        private ref Placement _placement;
internal PinBuilder(ref Placement placement,Offsetf anchorPct,Offsetf pivotOffsetPx,Alignment pivot)
{ _placement=ref placement;_placement.Anchor=new(0,0,0,0);_placement.SizeDelta=Sizef.Zero();_placement.Pivot=Alignment.TopLeft();_placement.PivotOffset=Offsetf.Zero();bool valid=anchorPct.IsFinite()&&pivotOffsetPx.IsFinite()&&float.IsFinite(pivot.X)&&float.IsFinite(pivot.Y);if(!valid){GuiAssert.Verify(valid,"Pin anchor offset and pivot must be finite");return;}_placement.Anchor=new(anchorPct.X,anchorPct.Y,anchorPct.X,anchorPct.Y);_placement.Pivot=pivot;_placement.PivotOffset=pivotOffsetPx; }
        public PinBuilder SizePx(Sizef value_px)
        {

    bool is_input_valid = !float.IsNaN(value_px.Width) && value_px.Width >= 0.0f &&
        !float.IsNaN(value_px.Height) && value_px.Height >= 0.0f;
    if (!is_input_valid)
    {
        GuiAssert.Verify(is_input_valid ,"pin size px must be non-negative");
        return this;
    }

    _placement.SizeDelta = value_px;
    return this;

        }
        public PinBuilder Pivot(Alignment value)
        {

    bool is_input_valid = float.IsFinite(value.X) && float.IsFinite(value.Y);
    if (!is_input_valid)
    {
        GuiAssert.Verify(is_input_valid ,"pin pivot must be finite");
        return this;
    }

    _placement.Pivot = value;
    return this;

        }
        public PinBuilder OffsetPx(Offsetf value_px)
        {

    bool is_input_valid = value_px.IsFinite();
    if (!is_input_valid)
    {
        GuiAssert.Verify(is_input_valid ,"pin offset px must be finite");
        return this;
    }

    _placement.PivotOffset = value_px;
    return this;

        }
    }
    public ref struct AlignBuilder
    {
        private ref Placement _placement;
internal AlignBuilder(ref Placement placement,Alignment alignment)
{ _placement=ref placement;_placement.Anchor=new(0,0,0,0);_placement.SizeDelta=Sizef.Zero();_placement.Pivot=Alignment.TopLeft();_placement.PivotOffset=Offsetf.Zero();bool valid=float.IsFinite(alignment.X)&&float.IsFinite(alignment.Y);if(!valid){GuiAssert.Verify(valid,"Alignment must be finite");return;}var anchor=AlignmentToAnchorPct(alignment);_placement.Anchor=new(anchor.X,anchor.Y,anchor.X,anchor.Y);_placement.Pivot=alignment; }
        public AlignBuilder SizePx(Sizef value_px)
        {

    bool is_input_valid = !float.IsNaN(value_px.Width) && value_px.Width >= 0.0f &&
        !float.IsNaN(value_px.Height) && value_px.Height >= 0.0f;
    if (!is_input_valid)
    {
        GuiAssert.Verify(is_input_valid ,"align size px must be non-negative");
        return this;
    }

    _placement.SizeDelta = value_px;
    return this;

        }
        public AlignBuilder OffsetPx(Offsetf value_px)
        {

    bool is_input_valid = value_px.IsFinite();
    if (!is_input_valid)
    {
        GuiAssert.Verify(is_input_valid ,"align offset px must be finite");
        return this;
    }

    _placement.PivotOffset = value_px;
    return this;

        }
    }
    public ref struct InsetBuilder
    {
        private ref Placement _placement;
private EdgeInsets _value_px;
internal InsetBuilder(ref Placement placement,Alignment pivot)
{ _placement=ref placement;_value_px=EdgeInsets.Zero();_placement.ResetFill();bool valid=float.IsFinite(pivot.X)&&float.IsFinite(pivot.Y);if(!valid){GuiAssert.Verify(valid,"Inset pivot must be finite");return;}_placement.Pivot=pivot; }
        public InsetBuilder All(
    float value_pct,
    float value_px
)
        {

    float max_anchor_pct = 1.0f - value_pct;
    float size_delta_px = -2.0f * value_px;
    bool is_input_valid = float.IsFinite(value_pct) && float.IsFinite(value_px) &&
        float.IsFinite(max_anchor_pct) && float.IsFinite(size_delta_px);
    if (!is_input_valid)
    {
        GuiAssert.Verify(is_input_valid ,"inset pct and px must produce finite values");
        return this;
    }

    _value_px = EdgeInsets.All(value_px);
    _placement.Anchor = new Rectf(value_pct, value_pct, max_anchor_pct, max_anchor_pct);
    _placement.SizeDelta = new Sizef(size_delta_px, size_delta_px);
    Offsetf pivot_pct = AlignmentToAnchorPct(_placement.Pivot);
    _placement.PivotOffset = new Offsetf(
        value_px + size_delta_px * pivot_pct.X,
        value_px + size_delta_px * pivot_pct.Y
    );
    return this;

        }
        public InsetBuilder Horizontal(
    float value_pct,
    float value_px
)
        {

    float right_anchor_pct = 1.0f - value_pct;
    float width_delta_px = -2.0f * value_px;
    bool is_input_valid = float.IsFinite(value_pct) && float.IsFinite(value_px) &&
        float.IsFinite(right_anchor_pct) && float.IsFinite(width_delta_px);
    if (!is_input_valid)
    {
        GuiAssert.Verify(is_input_valid ,"horizontal inset pct and px must produce finite values");
        return this;
    }

    _value_px.Left = _value_px.Right = value_px;
    _placement.Anchor.Left = value_pct;
    _placement.Anchor.Right = right_anchor_pct;
    _placement.SizeDelta.Width = width_delta_px;
    float pivot_pct = AlignmentToAnchorPct(_placement.Pivot).X;
    _placement.PivotOffset.X = value_px + width_delta_px * pivot_pct;
    return this;

        }
        public InsetBuilder Vertical(
    float value_pct,
    float value_px
)
        {

    float bottom_anchor_pct = 1.0f - value_pct;
    float height_delta_px = -2.0f * value_px;
    bool is_input_valid = float.IsFinite(value_pct) && float.IsFinite(value_px) &&
        float.IsFinite(bottom_anchor_pct) && float.IsFinite(height_delta_px);
    if (!is_input_valid)
    {
        GuiAssert.Verify(is_input_valid ,"vertical inset pct and px must produce finite values");
        return this;
    }

    _value_px.Top = _value_px.Bottom = value_px;
    _placement.Anchor.Top = value_pct;
    _placement.Anchor.Bottom = bottom_anchor_pct;
    _placement.SizeDelta.Height = height_delta_px;
    float pivot_pct = AlignmentToAnchorPct(_placement.Pivot).Y;
    _placement.PivotOffset.Y = value_px + height_delta_px * pivot_pct;
    return this;

        }
        public InsetBuilder Left(
    float value_pct,
    float value_px
)
        {

    float width_delta_px = -(value_px + _value_px.Right);
    bool is_input_valid = float.IsFinite(value_pct) && float.IsFinite(value_px) &&
        float.IsFinite(width_delta_px);
    if (!is_input_valid)
    {
        GuiAssert.Verify(is_input_valid ,"left inset pct and px must produce finite values");
        return this;
    }

    _value_px.Left = value_px;
    _placement.Anchor.Left = value_pct;
    _placement.SizeDelta.Width = width_delta_px;
    float pivot_pct = AlignmentToAnchorPct(_placement.Pivot).X;
    _placement.PivotOffset.X = value_px + width_delta_px * pivot_pct;
    return this;

        }
        public InsetBuilder Top(
    float value_pct,
    float value_px
)
        {

    float height_delta_px = -(value_px + _value_px.Bottom);
    bool is_input_valid = float.IsFinite(value_pct) && float.IsFinite(value_px) &&
        float.IsFinite(height_delta_px);
    if (!is_input_valid)
    {
        GuiAssert.Verify(is_input_valid ,"top inset pct and px must produce finite values");
        return this;
    }

    _value_px.Top = value_px;
    _placement.Anchor.Top = value_pct;
    _placement.SizeDelta.Height = height_delta_px;
    float pivot_pct = AlignmentToAnchorPct(_placement.Pivot).Y;
    _placement.PivotOffset.Y = value_px + height_delta_px * pivot_pct;
    return this;

        }
        public InsetBuilder Right(
    float value_pct,
    float value_px
)
        {

    float right_anchor_pct = 1.0f - value_pct;
    float width_delta_px = -(_value_px.Left + value_px);
    bool is_input_valid = float.IsFinite(value_pct) && float.IsFinite(value_px) &&
        float.IsFinite(right_anchor_pct) && float.IsFinite(width_delta_px);
    if (!is_input_valid)
    {
        GuiAssert.Verify(is_input_valid ,"right inset pct and px must produce finite values");
        return this;
    }

    _value_px.Right = value_px;
    _placement.Anchor.Right = right_anchor_pct;
    _placement.SizeDelta.Width = width_delta_px;
    float pivot_pct = AlignmentToAnchorPct(_placement.Pivot).X;
    _placement.PivotOffset.X = _value_px.Left + width_delta_px * pivot_pct;
    return this;

        }
        public InsetBuilder Bottom(
    float value_pct,
    float value_px
)
        {

    float bottom_anchor_pct = 1.0f - value_pct;
    float height_delta_px = -(_value_px.Top + value_px);
    bool is_input_valid = float.IsFinite(value_pct) && float.IsFinite(value_px) &&
        float.IsFinite(bottom_anchor_pct) && float.IsFinite(height_delta_px);
    if (!is_input_valid)
    {
        GuiAssert.Verify(is_input_valid ,"bottom inset pct and px must produce finite values");
        return this;
    }

    _value_px.Bottom = value_px;
    _placement.Anchor.Bottom = bottom_anchor_pct;
    _placement.SizeDelta.Height = height_delta_px;
    float pivot_pct = AlignmentToAnchorPct(_placement.Pivot).Y;
    _placement.PivotOffset.Y = _value_px.Top + height_delta_px * pivot_pct;
    return this;

        }
        public InsetBuilder AllPct(float value_pct)
        {

    float max_anchor_pct = 1.0f - value_pct;
    bool is_input_valid = float.IsFinite(value_pct) && float.IsFinite(max_anchor_pct);
    if (!is_input_valid)
    {
        GuiAssert.Verify(is_input_valid ,"inset pct must produce finite anchors");
        return this;
    }

    _placement.Anchor = new Rectf(value_pct, value_pct, max_anchor_pct, max_anchor_pct);
    return this;

        }
        public InsetBuilder HorizontalPct(float value_pct)
        {

    float right_anchor_pct = 1.0f - value_pct;
    bool is_input_valid = float.IsFinite(value_pct) && float.IsFinite(right_anchor_pct);
    if (!is_input_valid)
    {
        GuiAssert.Verify(is_input_valid ,"horizontal inset pct must produce finite anchors");
        return this;
    }

    _placement.Anchor.Left = value_pct;
    _placement.Anchor.Right = right_anchor_pct;
    return this;

        }
        public InsetBuilder VerticalPct(float value_pct)
        {

    float bottom_anchor_pct = 1.0f - value_pct;
    bool is_input_valid = float.IsFinite(value_pct) && float.IsFinite(bottom_anchor_pct);
    if (!is_input_valid)
    {
        GuiAssert.Verify(is_input_valid ,"vertical inset pct must produce finite anchors");
        return this;
    }

    _placement.Anchor.Top = value_pct;
    _placement.Anchor.Bottom = bottom_anchor_pct;
    return this;

        }
        public InsetBuilder LeftPct(float value_pct)
        {

    bool is_input_valid = float.IsFinite(value_pct);
    if (!is_input_valid)
    {
        GuiAssert.Verify(is_input_valid ,"left inset pct must be finite");
        return this;
    }

    _placement.Anchor.Left = value_pct;
    return this;

        }
        public InsetBuilder TopPct(float value_pct)
        {

    bool is_input_valid = float.IsFinite(value_pct);
    if (!is_input_valid)
    {
        GuiAssert.Verify(is_input_valid ,"top inset pct must be finite");
        return this;
    }

    _placement.Anchor.Top = value_pct;
    return this;

        }
        public InsetBuilder RightPct(float value_pct)
        {

    float right_anchor_pct = 1.0f - value_pct;
    bool is_input_valid = float.IsFinite(value_pct) && float.IsFinite(right_anchor_pct);
    if (!is_input_valid)
    {
        GuiAssert.Verify(is_input_valid ,"right inset pct must produce a finite anchor");
        return this;
    }

    _placement.Anchor.Right = right_anchor_pct;
    return this;

        }
        public InsetBuilder BottomPct(float value_pct)
        {

    float bottom_anchor_pct = 1.0f - value_pct;
    bool is_input_valid = float.IsFinite(value_pct) && float.IsFinite(bottom_anchor_pct);
    if (!is_input_valid)
    {
        GuiAssert.Verify(is_input_valid ,"bottom inset pct must produce a finite anchor");
        return this;
    }

    _placement.Anchor.Bottom = bottom_anchor_pct;
    return this;

        }
        public InsetBuilder AllPx(float value_px)
        {

    float size_delta_px = -2.0f * value_px;
    bool is_input_valid = float.IsFinite(value_px) && float.IsFinite(size_delta_px);
    if (!is_input_valid)
    {
        GuiAssert.Verify(is_input_valid ,"inset px must produce finite size delta");
        return this;
    }

    _value_px = EdgeInsets.All(value_px);
    _placement.SizeDelta = new Sizef(size_delta_px, size_delta_px);
    Offsetf pivot_pct = AlignmentToAnchorPct(_placement.Pivot);
    _placement.PivotOffset = new Offsetf(
        value_px + size_delta_px * pivot_pct.X,
        value_px + size_delta_px * pivot_pct.Y
    );
    return this;

        }
        public InsetBuilder HorizontalPx(float value_px)
        {

    float width_delta_px = -2.0f * value_px;
    bool is_input_valid = float.IsFinite(value_px) && float.IsFinite(width_delta_px);
    if (!is_input_valid)
    {
        GuiAssert.Verify(is_input_valid ,"horizontal inset px must produce finite size delta");
        return this;
    }

    _value_px.Left = _value_px.Right = value_px;
    _placement.SizeDelta.Width = width_delta_px;
    float pivot_pct = AlignmentToAnchorPct(_placement.Pivot).X;
    _placement.PivotOffset.X = value_px + width_delta_px * pivot_pct;
    return this;

        }
        public InsetBuilder VerticalPx(float value_px)
        {

    float height_delta_px = -2.0f * value_px;
    bool is_input_valid = float.IsFinite(value_px) && float.IsFinite(height_delta_px);
    if (!is_input_valid)
    {
        GuiAssert.Verify(is_input_valid ,"vertical inset px must produce finite size delta");
        return this;
    }

    _value_px.Top = _value_px.Bottom = value_px;
    _placement.SizeDelta.Height = height_delta_px;
    float pivot_pct = AlignmentToAnchorPct(_placement.Pivot).Y;
    _placement.PivotOffset.Y = value_px + height_delta_px * pivot_pct;
    return this;

        }
        public InsetBuilder LeftPx(float value_px)
        {

    float width_delta_px = -(value_px + _value_px.Right);
    bool is_input_valid = float.IsFinite(value_px) && float.IsFinite(width_delta_px);
    if (!is_input_valid)
    {
        GuiAssert.Verify(is_input_valid ,"left inset px must produce finite size delta");
        return this;
    }

    _value_px.Left = value_px;
    _placement.SizeDelta.Width = width_delta_px;
    float pivot_pct = AlignmentToAnchorPct(_placement.Pivot).X;
    _placement.PivotOffset.X = value_px + width_delta_px * pivot_pct;
    return this;

        }
        public InsetBuilder TopPx(float value_px)
        {

    float height_delta_px = -(value_px + _value_px.Bottom);
    bool is_input_valid = float.IsFinite(value_px) && float.IsFinite(height_delta_px);
    if (!is_input_valid)
    {
        GuiAssert.Verify(is_input_valid ,"top inset px must produce finite size delta");
        return this;
    }

    _value_px.Top = value_px;
    _placement.SizeDelta.Height = height_delta_px;
    float pivot_pct = AlignmentToAnchorPct(_placement.Pivot).Y;
    _placement.PivotOffset.Y = value_px + height_delta_px * pivot_pct;
    return this;

        }
        public InsetBuilder RightPx(float value_px)
        {

    float width_delta_px = -(_value_px.Left + value_px);
    bool is_input_valid = float.IsFinite(value_px) && float.IsFinite(width_delta_px);
    if (!is_input_valid)
    {
        GuiAssert.Verify(is_input_valid ,"right inset px must produce finite size delta");
        return this;
    }

    _value_px.Right = value_px;
    _placement.SizeDelta.Width = width_delta_px;
    float pivot_pct = AlignmentToAnchorPct(_placement.Pivot).X;
    _placement.PivotOffset.X = _value_px.Left + width_delta_px * pivot_pct;
    return this;

        }
        public InsetBuilder BottomPx(float value_px)
        {

    float height_delta_px = -(_value_px.Top + value_px);
    bool is_input_valid = float.IsFinite(value_px) && float.IsFinite(height_delta_px);
    if (!is_input_valid)
    {
        GuiAssert.Verify(is_input_valid ,"bottom inset px must produce finite size delta");
        return this;
    }

    _value_px.Bottom = value_px;
    _placement.SizeDelta.Height = height_delta_px;
    float pivot_pct = AlignmentToAnchorPct(_placement.Pivot).Y;
    _placement.PivotOffset.Y = _value_px.Top + height_delta_px * pivot_pct;
    return this;

        }
    }
}
