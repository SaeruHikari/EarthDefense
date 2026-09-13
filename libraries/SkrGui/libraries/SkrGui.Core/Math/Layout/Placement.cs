using System.Diagnostics.CodeAnalysis;
namespace SkrGui;

// Source: math/layout/placement.hpp (611561f8).
public enum EPlacementSizeMode : byte { Tight,Loose }
public partial struct Placement : IEquatable<Placement>
{
    public Rectf Anchor;public Sizef SizeDelta;public Alignment Pivot;public Offsetf PivotOffset;
    public Placement(){Anchor=new(0,0,1,1);SizeDelta=Sizef.Zero();Pivot=Alignment.TopLeft();PivotOffset=Offsetf.Zero();}
    public Placement(Rectf anchorPct,Sizef sizeDeltaPx,Alignment pivot,Offsetf pivotOffsetPx){Anchor=anchorPct;SizeDelta=sizeDeltaPx;Pivot=pivot;PivotOffset=pivotOffsetPx;if(!IsValid()){GuiAssert.Verify(IsValid(),"Invalid placement construction parameters");ResetFill();}}
    public static Placement Fill()=>new();
    public static Placement Pin(Offsetf anchorPct,Offsetf pivotOffsetPx=default,Sizef sizeDeltaPx=default,Alignment? pivot=null)
    {var result=new Placement();result.PinAt(anchorPct,pivotOffsetPx).SizePx(sizeDeltaPx).Pivot(pivot??Alignment.TopLeft());return result;}
    public static Placement Align(Alignment alignment,Sizef sizeDeltaPx=default,Offsetf pivotOffsetPx=default)
    {var result=new Placement();result.AlignTo(alignment).SizePx(sizeDeltaPx).OffsetPx(pivotOffsetPx);return result;}
    // C# cannot distinguish static Fill() from instance fill() by return type.
    [UnscopedRef] public ref Placement ResetFill(){Anchor=new(0,0,1,1);SizeDelta=Sizef.Zero();Pivot=Alignment.TopLeft();PivotOffset=Offsetf.Zero();return ref this;}
    [UnscopedRef] public PinBuilder PinAt(Offsetf anchorPct,Offsetf pivotOffsetPx=default)=>new(ref this,anchorPct,pivotOffsetPx,Alignment.TopLeft());
    [UnscopedRef] public PinBuilder PinLT(Offsetf insetPct,Offsetf insetPx=default)=>new(ref this,insetPct,insetPx,Alignment.TopLeft());
    [UnscopedRef] public PinBuilder PinRT(Offsetf insetPct,Offsetf insetPx=default)=>new(ref this,new Offsetf(1-insetPct.X,insetPct.Y),new Offsetf(-insetPx.X,insetPx.Y),Alignment.TopRight());
    [UnscopedRef] public PinBuilder PinLB(Offsetf insetPct,Offsetf insetPx=default)=>new(ref this,new Offsetf(insetPct.X,1-insetPct.Y),new Offsetf(insetPx.X,-insetPx.Y),Alignment.BottomLeft());
    [UnscopedRef] public PinBuilder PinRB(Offsetf insetPct,Offsetf insetPx=default)=>new(ref this,new Offsetf(1-insetPct.X,1-insetPct.Y),new Offsetf(-insetPx.X,-insetPx.Y),Alignment.BottomRight());
    [UnscopedRef] public PinBuilder PinLTPct(float leftPct,float topPct)=>PinLT(new Offsetf(leftPct,topPct));
    [UnscopedRef] public PinBuilder PinRTPct(float rightPct,float topPct)=>PinRT(new Offsetf(rightPct,topPct));
    [UnscopedRef] public PinBuilder PinLBPct(float leftPct,float bottomPct)=>PinLB(new Offsetf(leftPct,bottomPct));
    [UnscopedRef] public PinBuilder PinRBPct(float rightPct,float bottomPct)=>PinRB(new Offsetf(rightPct,bottomPct));
    [UnscopedRef] public PinBuilder PinLTPx(float leftPx,float topPx)=>PinLT(Offsetf.Zero(),new Offsetf(leftPx,topPx));
    [UnscopedRef] public PinBuilder PinRTPx(float rightPx,float topPx)=>PinRT(Offsetf.Zero(),new Offsetf(rightPx,topPx));
    [UnscopedRef] public PinBuilder PinLBPx(float leftPx,float bottomPx)=>PinLB(Offsetf.Zero(),new Offsetf(leftPx,bottomPx));
    [UnscopedRef] public PinBuilder PinRBPx(float rightPx,float bottomPx)=>PinRB(Offsetf.Zero(),new Offsetf(rightPx,bottomPx));
    [UnscopedRef] public AlignBuilder AlignTo(Alignment value)=>new(ref this,value);
    [UnscopedRef] public AlignBuilder AlignLeftTop()=>AlignTo(Alignment.TopLeft());
    [UnscopedRef] public AlignBuilder AlignCenterTop()=>AlignTo(Alignment.TopCenter());
    [UnscopedRef] public AlignBuilder AlignRightTop()=>AlignTo(Alignment.TopRight());
    [UnscopedRef] public AlignBuilder AlignLeftCenter()=>AlignTo(Alignment.CenterLeft());
    [UnscopedRef] public AlignBuilder AlignCenter()=>AlignTo(Alignment.Center());
    [UnscopedRef] public AlignBuilder AlignRightCenter()=>AlignTo(Alignment.CenterRight());
    [UnscopedRef] public AlignBuilder AlignLeftBottom()=>AlignTo(Alignment.BottomLeft());
    [UnscopedRef] public AlignBuilder AlignCenterBottom()=>AlignTo(Alignment.BottomCenter());
    [UnscopedRef] public AlignBuilder AlignRightBottom()=>AlignTo(Alignment.BottomRight());
    [UnscopedRef] public InsetBuilder Inset(Alignment? pivot=null)=>new(ref this,pivot??Alignment.TopLeft());
    public bool IsWidthPinned()=>Anchor.Left==Anchor.Right;
    public bool IsHeightPinned()=>Anchor.Top==Anchor.Bottom;
    public bool IsPinned()=>IsWidthPinned()&&IsHeightPinned();
    public bool IsWidthInset()=>Anchor.Left<Anchor.Right;
    public bool IsHeightInset()=>Anchor.Top<Anchor.Bottom;
    public bool IsInset()=>IsWidthInset()&&IsHeightInset();
    public bool IsWidthValid(){if(!float.IsFinite(Anchor.Left)||!float.IsFinite(Anchor.Right)||Anchor.Left>Anchor.Right||!float.IsFinite(Pivot.X)||!float.IsFinite(PivotOffset.X))return false;return IsWidthPinned()?!float.IsNaN(SizeDelta.Width)&&SizeDelta.Width>=0:float.IsFinite(SizeDelta.Width);}
    public bool IsHeightValid(){if(!float.IsFinite(Anchor.Top)||!float.IsFinite(Anchor.Bottom)||Anchor.Top>Anchor.Bottom||!float.IsFinite(Pivot.Y)||!float.IsFinite(PivotOffset.Y))return false;return IsHeightPinned()?!float.IsNaN(SizeDelta.Height)&&SizeDelta.Height>=0:float.IsFinite(SizeDelta.Height);}
    public bool IsValid()=>IsWidthValid()&&IsHeightValid();
    public static bool operator==(Placement a,Placement b)=>a.Anchor==b.Anchor&&a.SizeDelta==b.SizeDelta&&a.Pivot==b.Pivot&&a.PivotOffset==b.PivotOffset;
    public static bool operator!=(Placement a,Placement b)=>!(a==b);
    public bool Equals(Placement b)=>this==b;
    public override bool Equals(object? obj)=>obj is Placement b&&this==b;
    public override int GetHashCode()=>HashCode.Combine(Anchor,SizeDelta,Pivot,PivotOffset);
    public BoxConstraints? ResolveConstraints(BoxConstraints parentConstraints,EPlacementSizeMode sizeMode)
    {
        if(!IsValid()){GuiAssert.Verify(IsValid(),"Cannot resolve invalid placement");return null;}
        bool validMode=sizeMode==EPlacementSizeMode.Tight||sizeMode==EPlacementSizeMode.Loose;if(!validMode){GuiAssert.Verify(validMode,"Invalid placement size mode");return null;}
        if(!parentConstraints.IsNormalized()){GuiAssert.Verify(parentConstraints.IsNormalized(),"Parent constraints must be normalized");return null;}
        var result=new BoxConstraints();
        if(!ResolveAxisConstraints(parentConstraints.MinWidth,parentConstraints.MaxWidth,Anchor.Left,Anchor.Right,SizeDelta.Width,sizeMode,ref result.MinWidth,ref result.MaxWidth))return null;
        if(!ResolveAxisConstraints(parentConstraints.MinHeight,parentConstraints.MaxHeight,Anchor.Top,Anchor.Bottom,SizeDelta.Height,sizeMode,ref result.MinHeight,ref result.MaxHeight))return null;
        if(!result.IsNormalized()){GuiAssert.Verify(result.IsNormalized(),"Resolved constraints must be normalized");return null;}
        return result;
    }
    public Sizef? ResolveSize(Sizef parentSizePx)
    {bool valid=parentSizePx.IsFinite()&&parentSizePx.Width>=0&&parentSizePx.Height>=0;if(!valid){GuiAssert.Verify(valid,"Parent size must be finite and nonnegative");return null;}var result=ResolveConstraints(BoxConstraints.Tight(parentSizePx),EPlacementSizeMode.Tight);return result?.Biggest();}
    public Offsetf? PlaceOffset(Sizef parentSizePx,Sizef childSizePx)
    {
        if(!IsValid()){GuiAssert.Verify(IsValid(),"Cannot place invalid placement");return null;}
        bool parentValid=parentSizePx.IsFinite()&&parentSizePx.Width>=0&&parentSizePx.Height>=0;if(!parentValid){GuiAssert.Verify(parentValid,"Parent size must be finite and nonnegative");return null;}
        bool childValid=childSizePx.IsFinite()&&childSizePx.Width>=0&&childSizePx.Height>=0;if(!childValid){GuiAssert.Verify(childValid,"Child size must be finite and nonnegative");return null;}
        float pivotX=(Pivot.X+1)*0.5f,pivotY=(Pivot.Y+1)*0.5f;float spanX=Anchor.Right-Anchor.Left,spanY=Anchor.Bottom-Anchor.Top;
        float referenceX=parentSizePx.Width*(Anchor.Left+spanX*pivotX),referenceY=parentSizePx.Height*(Anchor.Top+spanY*pivotY);
        float left=referenceX+PivotOffset.X-childSizePx.Width*pivotX,top=referenceY+PivotOffset.Y-childSizePx.Height*pivotY;var result=new Offsetf(left,top);
        if(!result.IsFinite()){GuiAssert.Verify(result.IsFinite(),"Placed offset must be finite");return null;}return result;
    }
    public Rectf? PlaceRect(Rectf parentRectPx,Sizef childSizePx)
    {bool valid=parentRectPx.IsFinite()&&parentRectPx.Left<=parentRectPx.Right&&parentRectPx.Top<=parentRectPx.Bottom;if(!valid){GuiAssert.Verify(valid,"Parent rect must be finite and ordered");return null;}var offset=PlaceOffset(parentRectPx.Size(),childSizePx);if(!offset.HasValue)return null;var result=Rectf.OffsetSize(parentRectPx.TopLeft()+offset.Value,childSizePx);if(!result.IsFinite()){GuiAssert.Verify(result.IsFinite(),"Placed rect must be finite");return null;}return result;}
    private static Offsetf AlignmentToAnchorPct(Alignment value)=>new((value.X+1)*0.5f,(value.Y+1)*0.5f);
    private static bool ResolveAxisConstraints(float parentMin,float parentMax,float anchorMin,float anchorMax,float delta,EPlacementSizeMode mode,ref float resultMin,ref float resultMax)
    {
        if(anchorMin==anchorMax){if(delta<=0)return false;bool valid=mode!=EPlacementSizeMode.Tight||float.IsFinite(delta);if(!valid){GuiAssert.Verify(valid,"Tight pin size must be finite");return false;}resultMin=mode==EPlacementSizeMode.Tight&&float.IsFinite(delta)?delta:0;resultMax=delta;return true;}
        float span=anchorMax-anchorMin;if(!float.IsFinite(span)||span<=0){GuiAssert.Verify(float.IsFinite(span)&&span>0,"Inset anchor span must be finite and positive");return false;}
        resultMin=CppMath.Max(0,parentMin*span+delta);resultMax=float.IsInfinity(parentMax)?float.PositiveInfinity:CppMath.Max(resultMin,CppMath.Max(0,parentMax*span+delta));
        bool resultValid=!float.IsNaN(resultMin)&&!float.IsNaN(resultMax)&&resultMin>=0&&resultMin<=resultMax&&(!float.IsFinite(parentMin)||float.IsFinite(resultMin))&&(!float.IsFinite(parentMax)||float.IsFinite(resultMax));
        if(!resultValid){GuiAssert.Verify(resultValid,"Resolved axis constraints invalid");return false;}return resultMax>0;
    }
}
