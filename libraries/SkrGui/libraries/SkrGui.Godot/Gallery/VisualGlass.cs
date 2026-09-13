namespace SkrGui.Gallery;
// Source: gallery_common/visual_glass.hpp and src/visual_glass.cpp.
public sealed class BatchCmdBackdropGlass:BatchCmdBackdrop
{
    public float BlurRadius;
    public Offsetf LightDirection=new(-.70710677f,-.70710677f);
    public SRGBColor LightColor=new(0,0,0,1),GlassColor=new(1,1,1,1);
    public float GlassTransmittance=1,GlassGrain,GlassRefraction;
    public Sizef ShapeSize;public Radius TopLeft,TopRight,BottomRight,BottomLeft;
}
public class VisualGlass:VisualProxy
{
    private float _blurRadius=16,_transmittance=1,_grain,_refraction;
    private VisualBorderRadius _radius;
    private Offsetf _lightDirection=new(-.70710677f,-.70710677f);
    private SRGBColor _lightColor=new(0,0,0,1),_glassColor=new(1,1,1,1);
    public float BlurRadius()=>_blurRadius;
    public VisualBorderRadius Radius()=>_radius;
    public Offsetf LightDirection()=>_lightDirection;
    public SRGBColor LightColor()=>_lightColor;
    public SRGBColor GlassColor()=>_glassColor;
    public float GlassTransmittance()=>_transmittance;
    public float GlassGrain()=>_grain;
    public float GlassRefraction()=>_refraction;
    private static void Require(bool v,string message){if(!v)throw new InvalidOperationException(message);}
    public void SetBlurRadius(float v){Require(float.IsFinite(v)&&v>=0,"VisualGlass blur radius must be finite and non-negative");v=float.IsFinite(v)?MathF.Max(0,v):0;_blurRadius=v>.0001f?v:0;}
    public void SetRadius(VisualBorderRadius v){Require(v.IsNonNegative(),"VisualGlass radius must be non-negative");if(_radius!=v)_radius=v;}
    public void SetRadius(Radius v)=>SetRadius(VisualBorderRadius.All(v));
    public void SetRadius(Radius tl,Radius tr,Radius br,Radius bl)=>SetRadius(VisualBorderRadius.Only(tl,tr,br,bl));
    public void SetLightDirection(Offsetf v){Require(v.IsFinite(),"VisualGlass light direction must be finite");_lightDirection=v.Normalize(new(-.70710677f,-.70710677f));}
    public void SetLightColor(SRGBColor v){Require(v.IsFinite(),"VisualGlass light color must be finite");_lightColor=v.IsFinite()?v.Clamped():new(0,0,0,1);}
    public void SetGlassColor(SRGBColor v){Require(v.IsFinite(),"VisualGlass glass color must be finite");_glassColor=v.IsFinite()?v.Clamped():new(1,1,1,1);}
    public void SetGlassTransmittance(float v){Require(float.IsFinite(v),"VisualGlass glass transmittance must be finite");_transmittance=float.IsFinite(v)?Math.Clamp(v,0,1):1;}
    public void SetGlassGrain(float v){Require(float.IsFinite(v),"VisualGlass glass grain must be finite");_grain=float.IsFinite(v)?Math.Clamp(v,0,1):0;}
    public void SetGlassRefraction(float v){Require(float.IsFinite(v),"VisualGlass glass refraction must be finite");_refraction=float.IsFinite(v)?Math.Clamp(v,0,1):0;}
    protected override void PerformPaint(PaintContext context)
    {
        var bound=Rectf.OffsetSize(Offsetf.Zero(),Size());
        if(!bound.IsEmpty())
        {
            float pixelRatio=context.PixelRatio();var rrect=_radius.ToRrect(bound);
            var mesh=new Mesh();var gradient=new ColorGradientSolid(new SRGBColor(1,1,1,context.Opacity()));
            var options=new BasicMeshOptions(){PixelRatio=pixelRatio,AaRadius=context.AaRadius(),UvRect=Rectf.LTWH(0,0,1,1)};
            bool built=_radius.IsZero()?BasicMeshes.RectFill(mesh,bound,gradient,options):BasicMeshes.RRectFill(mesh,rrect,gradient,options);
            if(built)
            {
                float blur=_blurRadius>.0001f?_blurRadius:0,refraction=_refraction>.0001f?_refraction*26:0,aa=context.AaRadius()/pixelRatio;
                var effect=new BatchCmdBackdropGlass(){BlurRadius=_blurRadius,LightDirection=_lightDirection,LightColor=_lightColor,GlassColor=_glassColor,
                    GlassTransmittance=_transmittance,GlassGrain=_grain,GlassRefraction=_refraction,ShapeSize=bound.Size(),
                    TopLeft=rrect.TlRadius,TopRight=rrect.TrRadius,BottomRight=rrect.BrRadius,BottomLeft=rrect.BlRadius};
                context.Backdrop(effect,mesh,bound.Inflate(blur+refraction+aa));
            }
        }
        base.PerformPaint(context);
    }
}
