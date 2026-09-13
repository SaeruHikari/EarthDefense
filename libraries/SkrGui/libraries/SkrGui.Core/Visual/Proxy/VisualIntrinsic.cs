namespace SkrGui;

// Source: visual_intrinsic_width/height.hpp and their matching cpp files.
public class VisualIntrinsicWidth : VisualProxy
{
    private float? _stepWidth,_stepHeight;
    public float? StepWidth()=>_stepWidth;
    public float? StepHeight()=>_stepHeight;
    public void SetStepWidth(float? value){GuiAssert.Require(!value.HasValue||value.Value>0,"Step width must be null or positive");if(_stepWidth==value)return;_stepWidth=value;MarkNeedsLayout();}
    public void SetStepHeight(float? value){GuiAssert.Require(!value.HasValue||value.Value>0,"Step height must be null or positive");if(_stepHeight==value)return;_stepHeight=value;MarkNeedsLayout();}
    public void ClearStepWidth()=>SetStepWidth(null);
    public void ClearStepHeight()=>SetStepHeight(null);
    protected override void PerformLayout(){if(Child() is {} child){LayoutChild(child,ChildConstraints(child,Constraints()));SetSize(child.Size());return;}SetSize(Constraints().Smallest());}
    protected override float ComputeMinIntrinsicWidth(float height)=>GetMaxIntrinsicWidth(height);
    protected override float ComputeMaxIntrinsicWidth(float height)=>Child() is {} child?ApplyStep(child.GetMaxIntrinsicWidth(height),_stepWidth):0;
    protected override float ComputeMinIntrinsicHeight(float width){if(Child() is not {} child)return 0;if(!float.IsFinite(width))width=GetMaxIntrinsicWidth(float.PositiveInfinity);GuiAssert.Require(float.IsFinite(width),"Intrinsic width must be finite");return ApplyStep(child.GetMinIntrinsicHeight(width),_stepHeight);}
    protected override float ComputeMaxIntrinsicHeight(float width){if(Child() is not {} child)return 0;if(!float.IsFinite(width))width=GetMaxIntrinsicWidth(float.PositiveInfinity);GuiAssert.Require(float.IsFinite(width),"Intrinsic width must be finite");return ApplyStep(child.GetMaxIntrinsicHeight(width),_stepHeight);}
    protected override Sizef ComputeDryLayout(BoxConstraints constraints)=>Child() is {} child?child.GetDryLayout(ChildConstraints(child,constraints)):constraints.Smallest();
    protected override float? ComputeDryBaseline(BoxConstraints constraints,ETextBaseline baseline)=>Child() is {} child?child.GetDryBaseline(ChildConstraints(child,constraints),baseline):null;
    private static float ApplyStep(float input,float? step){GuiAssert.Require(float.IsFinite(input),"Step input must be finite");if(!step.HasValue)return input;return MathF.Ceiling(input/step.Value)*step.Value;}
    private BoxConstraints ChildConstraints(VisualNode child,BoxConstraints constraints){float? width=null;if(!constraints.HasTightWidth())width=ApplyStep(child.GetMaxIntrinsicWidth(constraints.MaxHeight),_stepWidth);float? height=null;if(_stepHeight.HasValue)height=ApplyStep(child.GetMaxIntrinsicHeight(constraints.MaxWidth),_stepHeight);return constraints.Tighten(width,height);}
}
public class VisualIntrinsicHeight : VisualProxy
{
    protected override void PerformLayout(){if(Child() is {} child){LayoutChild(child,ChildConstraints(child,Constraints()));SetSize(child.Size());return;}SetSize(Constraints().Smallest());}
    protected override float ComputeMinIntrinsicWidth(float height){if(Child() is not {} child)return 0;if(!float.IsFinite(height))height=child.GetMaxIntrinsicHeight(float.PositiveInfinity);GuiAssert.Require(float.IsFinite(height),"Intrinsic height must be finite");return child.GetMinIntrinsicWidth(height);}
    protected override float ComputeMaxIntrinsicWidth(float height){if(Child() is not {} child)return 0;if(!float.IsFinite(height))height=child.GetMaxIntrinsicHeight(float.PositiveInfinity);GuiAssert.Require(float.IsFinite(height),"Intrinsic height must be finite");return child.GetMaxIntrinsicWidth(height);}
    protected override float ComputeMinIntrinsicHeight(float width)=>GetMaxIntrinsicHeight(width);
    protected override Sizef ComputeDryLayout(BoxConstraints constraints)=>Child() is {} child?child.GetDryLayout(ChildConstraints(child,constraints)):constraints.Smallest();
    protected override float? ComputeDryBaseline(BoxConstraints constraints,ETextBaseline baseline)=>Child() is {} child?child.GetDryBaseline(ChildConstraints(child,constraints),baseline):null;
    private BoxConstraints ChildConstraints(VisualNode child,BoxConstraints constraints)=>constraints.HasTightHeight()?constraints:constraints.Tighten(null,child.GetMaxIntrinsicHeight(constraints.MaxWidth));
}
