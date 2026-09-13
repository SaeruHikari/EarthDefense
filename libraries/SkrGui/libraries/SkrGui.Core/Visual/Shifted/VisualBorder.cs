namespace SkrGui;

// Source: visual/shifted/visual_border.hpp + src/visual/shifted/visual_border.cpp.
public struct VisualBorderRadius : IEquatable<VisualBorderRadius>
{
    public Radius TopLeft,TopRight,BottomRight,BottomLeft;
    public VisualBorderRadius(Radius topLeft=default,Radius topRight=default,Radius bottomRight=default,Radius bottomLeft=default){TopLeft=topLeft;TopRight=topRight;BottomRight=bottomRight;BottomLeft=bottomLeft;}
    public static VisualBorderRadius Zero()=>new();
    public static VisualBorderRadius All(Radius radius)=>new(radius,radius,radius,radius);
    public static VisualBorderRadius Only(Radius topLeft=default,Radius topRight=default,Radius bottomRight=default,Radius bottomLeft=default)=>new(topLeft,topRight,bottomRight,bottomLeft);
    public bool IsZero()=>TopLeft.IsZero()&&TopRight.IsZero()&&BottomRight.IsZero()&&BottomLeft.IsZero();
    public bool IsNonNegative()=>TopLeft.X>=0&&TopLeft.Y>=0&&TopRight.X>=0&&TopRight.Y>=0&&BottomRight.X>=0&&BottomRight.Y>=0&&BottomLeft.X>=0&&BottomLeft.Y>=0;
    public RRect ToRrect(Rectf rect)=>new RRect(rect.Left,rect.Top,rect.Right,rect.Bottom,TopLeft,TopRight,BottomRight,BottomLeft).ScaleRadii();
    public static bool operator==(VisualBorderRadius a,VisualBorderRadius b)=>a.TopLeft==b.TopLeft&&a.TopRight==b.TopRight&&a.BottomRight==b.BottomRight&&a.BottomLeft==b.BottomLeft;
    public static bool operator!=(VisualBorderRadius a,VisualBorderRadius b)=>!(a==b);
    public bool Equals(VisualBorderRadius b)=>this==b;
    public override bool Equals(object? obj)=>obj is VisualBorderRadius b&&this==b;
    public override int GetHashCode()=>HashCode.Combine(TopLeft,TopRight,BottomRight,BottomLeft);
}
public class VisualBorder : VisualShifted
{
    private VisualBorderRadius _radius=new();private EdgeInsets _borderThickness=new();private SRGBColor _borderColor=new(0,0,0,0),_backgroundColor=new(0,0,0,0);
    public VisualBorderRadius Radius()=>_radius;
    public EdgeInsets BorderThickness()=>_borderThickness;
    public SRGBColor BorderColor()=>_borderColor;
    public SRGBColor BackgroundColor()=>_backgroundColor;
    public void SetRadius(VisualBorderRadius value){GuiAssert.Require(value.IsNonNegative(),"Border radius must be nonnegative");if(_radius==value)return;_radius=value;}
    public void SetRadius(Radius value)=>SetRadius(VisualBorderRadius.All(value));
    public void SetBorderThickness(EdgeInsets value){GuiAssert.Require(value.IsNonNegative(),"Border thickness must be nonnegative");if(_borderThickness==value)return;_borderThickness=value;MarkNeedsLayout();}
    public void SetBorderThickness(float value)=>SetBorderThickness(EdgeInsets.All(value));
    public void SetBorderColor(SRGBColor value){GuiAssert.Require(value.IsFinite(),"Border color must be finite");if(_borderColor==value)return;_borderColor=value;}
    public void SetBackgroundColor(SRGBColor value){GuiAssert.Require(value.IsFinite(),"Background color must be finite");if(_backgroundColor==value)return;_backgroundColor=value;}
    protected override void PerformPaint(PaintContext context)
    {
        var outer=_radius.ToRrect(Rectf.OffsetSize(Offsetf.Zero(),Size()));
        var background=_backgroundColor.WithAlpha(_backgroundColor.A*context.Opacity());
        var options=new BasicMeshOptions{PixelRatio=context.PixelRatio(),AaRadius=context.AaRadius()};
        var bound=outer.Rect().Inflate(context.AaRadius()/context.PixelRatio());
        if(background.IsFinite()&&!background.IsTransparent()&&!outer.IsEmpty()){var mesh=new Mesh();if(BasicMeshes.RRectFill(mesh,outer,new ColorGradientSolid(background),options))context.DrawMesh(mesh,null,null,bound);}
        var border=_borderColor.WithAlpha(_borderColor.A*context.Opacity());
        if(border.IsFinite()&&!border.IsTransparent()&&!_borderThickness.IsZero()&&!outer.IsEmpty()){var mesh=new Mesh();if(BasicMeshes.RRectBorder(mesh,outer,_borderThickness,new ColorGradientSolid(border),options))context.DrawMesh(mesh,null,null,bound);}
        PaintChild(Child(),context,ChildOffset());
    }
    protected override void PerformLayout(){if(Child() is {} child){LayoutChild(child,Constraints().Deflate(_borderThickness));_childOffset=_borderThickness.TopLeft();SetSize(Constraints().Constrain(_borderThickness.InflateSize(child.Size())));return;}_childOffset=Offsetf.Zero();SetSize(Constraints().Constrain(_borderThickness.CollapsedSize()));}
    protected override float ComputeMinIntrinsicWidth(float height)=>Child() is {} child?child.GetMinIntrinsicWidth(CppMath.Max(0,height-_borderThickness.Vertical()))+_borderThickness.Horizontal():_borderThickness.Horizontal();
    protected override float ComputeMaxIntrinsicWidth(float height)=>Child() is {} child?child.GetMaxIntrinsicWidth(CppMath.Max(0,height-_borderThickness.Vertical()))+_borderThickness.Horizontal():_borderThickness.Horizontal();
    protected override float ComputeMinIntrinsicHeight(float width)=>Child() is {} child?child.GetMinIntrinsicHeight(CppMath.Max(0,width-_borderThickness.Horizontal()))+_borderThickness.Vertical():_borderThickness.Vertical();
    protected override float ComputeMaxIntrinsicHeight(float width)=>Child() is {} child?child.GetMaxIntrinsicHeight(CppMath.Max(0,width-_borderThickness.Horizontal()))+_borderThickness.Vertical():_borderThickness.Vertical();
    protected override Sizef ComputeDryLayout(BoxConstraints constraints){if(Child() is not {} child)return constraints.Constrain(_borderThickness.CollapsedSize());var childSize=child.GetDryLayout(constraints.Deflate(_borderThickness));return constraints.Constrain(_borderThickness.InflateSize(childSize));}
    protected override float? ComputeDryBaseline(BoxConstraints constraints,ETextBaseline baseline){if(Child() is not {} child)return null;float? result=child.GetDryBaseline(constraints.Deflate(_borderThickness),baseline);return result.HasValue?result.Value+_borderThickness.Top:result;}
}
