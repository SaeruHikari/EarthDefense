namespace SkrGui;

// Source: SkrGuiCore/math/layout/box_constraint.hpp (611561f8).
public struct BoxConstraints : IEquatable<BoxConstraints>
{
    public float MinWidth,MaxWidth,MinHeight,MaxHeight;
    public BoxConstraints() { MinWidth=MinHeight=0; MaxWidth=MaxHeight=float.PositiveInfinity; }
    public BoxConstraints(float minWidth,float maxWidth,float minHeight,float maxHeight) { MinWidth=minWidth; MaxWidth=maxWidth; MinHeight=minHeight; MaxHeight=maxHeight; }
    public static BoxConstraints Tight(Sizef size) => new(size.Width,size.Width,size.Height,size.Height);
    public static BoxConstraints TightFor(float? width=null,float? height=null) => new(width??0,width??float.PositiveInfinity,height??0,height??float.PositiveInfinity);
    public static BoxConstraints TightForFinite(float width=float.PositiveInfinity,float height=float.PositiveInfinity) => new(width!=float.PositiveInfinity?width:0,width!=float.PositiveInfinity?width:float.PositiveInfinity,height!=float.PositiveInfinity?height:0,height!=float.PositiveInfinity?height:float.PositiveInfinity);
    public static BoxConstraints TightWidth(float width) => new(width,width,0,float.PositiveInfinity);
    public static BoxConstraints TightHeight(float height) => new(0,float.PositiveInfinity,height,height);
    public static BoxConstraints Loose(Sizef size) => new(0,size.Width,0,size.Height);
    public static BoxConstraints LooseWidth(float width) => new(0,width,0,float.PositiveInfinity);
    public static BoxConstraints LooseHeight(float height) => new(0,float.PositiveInfinity,0,height);
    public static BoxConstraints Expand() => new(float.PositiveInfinity,float.PositiveInfinity,float.PositiveInfinity,float.PositiveInfinity);
    public static BoxConstraints ExpandWidth(float height) => new(float.PositiveInfinity,float.PositiveInfinity,height,height);
    public static BoxConstraints ExpandWidth(float minHeight,float maxHeight) => new(float.PositiveInfinity,float.PositiveInfinity,minHeight,maxHeight);
    public static BoxConstraints ExpandHeight(float width) => new(width,width,float.PositiveInfinity,float.PositiveInfinity);
    public static BoxConstraints ExpandHeight(float minWidth,float maxWidth) => new(minWidth,maxWidth,float.PositiveInfinity,float.PositiveInfinity);
    public static bool operator==(BoxConstraints a,BoxConstraints b) => a.MinWidth==b.MinWidth&&a.MaxWidth==b.MaxWidth&&a.MinHeight==b.MinHeight&&a.MaxHeight==b.MaxHeight;
    public static bool operator!=(BoxConstraints a,BoxConstraints b) => !(a==b);
    public bool Equals(BoxConstraints b) => this==b;
    public override bool Equals(object? obj) => obj is BoxConstraints b&&this==b;
    public override int GetHashCode() => HashCode.Combine(MinWidth,MaxWidth,MinHeight,MaxHeight);
    public BoxConstraints CopyWith(float? minWidth=null,float? maxWidth=null,float? minHeight=null,float? maxHeight=null) => new(minWidth??MinWidth,maxWidth??MaxWidth,minHeight??MinHeight,maxHeight??MaxHeight);
    public Sizef MinSize() => new(MinWidth,MinHeight);
    public Sizef MaxSize() => new(MaxWidth,MaxHeight);
    public void SetMinSize(Sizef size) { MinWidth=size.Width; MinHeight=size.Height; }
    public void SetMaxSize(Sizef size) { MaxWidth=size.Width; MaxHeight=size.Height; }
    public Sizef Smallest() => Constrain(new Sizef(0,0));
    public Sizef Biggest() => Constrain(new Sizef(float.PositiveInfinity,float.PositiveInfinity));
    public bool HasBoundedWidth() => MaxWidth<float.PositiveInfinity;
    public bool HasBoundedHeight() => MaxHeight<float.PositiveInfinity;
    public bool HasInfiniteWidth() => MinWidth>=float.PositiveInfinity;
    public bool HasInfiniteHeight() => MinHeight>=float.PositiveInfinity;
    public bool HasTightWidth() => MinWidth>=MaxWidth;
    public bool HasTightHeight() => MinHeight>=MaxHeight;
    public bool IsTight() => HasTightWidth()&&HasTightHeight();
    public bool IsNormalized() => MinWidth>=0&&MinWidth<=MaxWidth&&MinHeight>=0&&MinHeight<=MaxHeight;
    public bool IsSatisfiedBy(Sizef size) => MinWidth<=size.Width&&size.Width<=MaxWidth&&MinHeight<=size.Height&&size.Height<=MaxHeight;
    public void AssertValid() { if(float.IsNaN(MinWidth)||float.IsNaN(MaxWidth)||float.IsNaN(MinHeight)||float.IsNaN(MaxHeight)||MinWidth<0||MinHeight<0||MinWidth>MaxWidth||MinHeight>MaxHeight) throw new InvalidOperationException("Invalid source BoxConstraints"); }
    public float ConstrainWidth(float width=float.PositiveInfinity) => CppMath.Clamp(width,MinWidth,MaxWidth);
    public float ConstrainHeight(float height=float.PositiveInfinity) => CppMath.Clamp(height,MinHeight,MaxHeight);
    public Sizef Constrain(Sizef size) { size.Width=ConstrainWidth(size.Width); size.Height=ConstrainHeight(size.Height); return size; }
    public Sizef Constrain(float width,float height) => new(ConstrainWidth(width),ConstrainHeight(height));
    public Sizef ConstrainKeepAspect(Sizef size)
    {
        if(IsTight()) return Smallest();
        if(size.IsEmpty()) return Constrain(size);
        float width=size.Width,height=size.Height,aspectRatio=width/height;
        if(width>MaxWidth) { width=MaxWidth; height=width/aspectRatio; }
        if(height>MaxHeight) { height=MaxHeight; width=height*aspectRatio; }
        if(width<MinWidth) { width=MinWidth; height=width/aspectRatio; }
        if(height<MinHeight) { height=MinHeight; width=height*aspectRatio; }
        return Constrain(width,height);
    }
    public BoxConstraints Deflate(EdgeInsets insets) => Deflate(insets.Horizontal(),insets.Vertical());
    public BoxConstraints Deflate(float horizontal,float vertical)
    {
        float minWidth=CppMath.Max(0,MinWidth-horizontal),minHeight=CppMath.Max(0,MinHeight-vertical);
        return new(minWidth,CppMath.Max(minWidth,MaxWidth-horizontal),minHeight,CppMath.Max(minHeight,MaxHeight-vertical));
    }
    public BoxConstraints Deflate(float left,float top,float right,float bottom) => Deflate(left+right,top+bottom);
    public BoxConstraints Loosen() => new(0,MaxWidth,0,MaxHeight);
    public BoxConstraints Enforce(BoxConstraints constraints) => new(CppMath.Clamp(MinWidth,constraints.MinWidth,constraints.MaxWidth),CppMath.Clamp(MaxWidth,constraints.MinWidth,constraints.MaxWidth),CppMath.Clamp(MinHeight,constraints.MinHeight,constraints.MaxHeight),CppMath.Clamp(MaxHeight,constraints.MinHeight,constraints.MaxHeight));
    public BoxConstraints Tighten(float? width=null,float? height=null) => new(width.HasValue?CppMath.Clamp(width.Value,MinWidth,MaxWidth):MinWidth,width.HasValue?CppMath.Clamp(width.Value,MinWidth,MaxWidth):MaxWidth,height.HasValue?CppMath.Clamp(height.Value,MinHeight,MaxHeight):MinHeight,height.HasValue?CppMath.Clamp(height.Value,MinHeight,MaxHeight):MaxHeight);
    public BoxConstraints Flipped() => new(MinHeight,MaxHeight,MinWidth,MaxWidth);
    public BoxConstraints WidthConstraints() => new(MinWidth,MaxWidth,0,float.PositiveInfinity);
    public BoxConstraints HeightConstraints() => new(0,float.PositiveInfinity,MinHeight,MaxHeight);
    public static BoxConstraints Lerp(BoxConstraints a,BoxConstraints b,float t) => new(a.MinWidth+(b.MinWidth-a.MinWidth)*t,a.MaxWidth+(b.MaxWidth-a.MaxWidth)*t,a.MinHeight+(b.MinHeight-a.MinHeight)*t,a.MaxHeight+(b.MaxHeight-a.MaxHeight)*t);
}
