namespace SkrGui.Gallery;

// Source: gallery_common/gallery_shared.hpp and gallery_shared.cpp. Only GPU ownership is supplied by the Godot backend.
public enum EGalleryShaderKind:byte{Solid,Textured,TextGrayLcd,TextSdf,TextSdfLcd,BackdropBlur}
public enum EGalleryTextAAMode:byte{Auto,Gray,GrayLcd,Sdf,SdfLcd}
public struct GalleryBackdropShapeParams:IEquatable<GalleryBackdropShapeParams>
{
    public Sizef Size;public Radius TopLeft,TopRight,BottomRight,BottomLeft;
    public static bool operator==(GalleryBackdropShapeParams a,GalleryBackdropShapeParams b)=>a.Size==b.Size&&a.TopLeft==b.TopLeft&&a.TopRight==b.TopRight&&a.BottomRight==b.BottomRight&&a.BottomLeft==b.BottomLeft;
    public static bool operator!=(GalleryBackdropShapeParams a,GalleryBackdropShapeParams b)=>!(a==b);
    public bool Equals(GalleryBackdropShapeParams b)=>this==b;
    public override bool Equals(object? b)=>b is GalleryBackdropShapeParams p&&this==p;
    public override int GetHashCode()=>HashCode.Combine(Size,TopLeft,TopRight,BottomRight,BottomLeft);
}
public sealed class GalleryTextureData
{
    public byte[] Pixels=[];public uint Width,Height,Stride;public ulong Generation;
    public ETextAtlasFormat Format=ETextAtlasFormat.RGBA8;
    public GalleryTextureData(){}
    public GalleryTextureData(GalleryTextureData source){Pixels=(byte[])source.Pixels.Clone();Width=source.Width;Height=source.Height;Stride=source.Stride;Generation=source.Generation;Format=source.Format;}
}
public sealed class GalleryBackendTexture:Texture
{
    public GalleryTextureData Data=new();public TextServices? AtlasServices;public uint AtlasIndex=uint.MaxValue;public Sizei Size;
    internal Action? ReleaseGpu;
    public override Sizei PixelSize()=>Size;
    public bool IsValid()=>Size.Width>0&&Size.Height>0&&Data.Pixels.Length!=0;
    public void ResetData(GalleryTextureData value){var snapshot=new GalleryTextureData(value);DestroyGpu();Data=snapshot;Size=new((int)snapshot.Width,(int)snapshot.Height);}
    public void DestroyGpu(){var release=ReleaseGpu;ReleaseGpu=null;release?.Invoke();}
    public override void Dispose()=>DestroyGpu();
}
public sealed class GalleryBackendShader:Shader
{
    public EGalleryShaderKind Kind;
    public float BlurRadius,BlurDownScale=4;
    public Offsetf LightDirection=new(-.70710677f,-.70710677f);
    public SRGBColor LightColor=new(0,0,0,1),GlassColor=new(1,1,1,1);
    public float GlassTransmittance=1,GlassGrain,GlassRefraction;
    public GalleryBackdropShapeParams BackdropShape;
    internal Action? ReleaseGpu;
    internal Func<bool>? GpuValidity;
    public bool IsValid()=>GpuValidity?.Invoke()??false;
    public bool IsTextured()=>Kind!=EGalleryShaderKind.Solid;
    public void DestroyGpu(){var release=ReleaseGpu;ReleaseGpu=null;GpuValidity=null;release?.Invoke();}
    public override void Dispose()=>DestroyGpu();
}
public sealed class GalleryBackendResources
{
    private readonly List<GalleryBackendTexture> _textures=[];
    private GalleryBackendShader? _solidShader,_texturedShader,_grayLcdShader,_sdfShader,_sdfLcdShader;
    private readonly List<GalleryBackendShader> _backdropBlurShaders=[];
    public Shader SolidColorShader()=>Shader(EGalleryShaderKind.Solid);
    public Shader TextPixelModeShader(ETextPixelMode mode)=>Shader(mode switch{
        ETextPixelMode.SdfLcd or ETextPixelMode.MsdfLcd=>EGalleryShaderKind.TextSdfLcd,
        ETextPixelMode.Sdf or ETextPixelMode.Msdf=>EGalleryShaderKind.TextSdf,
        ETextPixelMode.GrayLcd=>EGalleryShaderKind.TextGrayLcd,_=>EGalleryShaderKind.Textured});
    public GalleryBackendShader Shader(EGalleryShaderKind kind)
    {
        switch(kind)
        {
            case EGalleryShaderKind.Textured:return _texturedShader??=new(){Kind=kind};
            case EGalleryShaderKind.TextGrayLcd:return _grayLcdShader??=new(){Kind=kind};
            case EGalleryShaderKind.TextSdf:return _sdfShader??=new(){Kind=kind};
            case EGalleryShaderKind.TextSdfLcd:return _sdfLcdShader??=new(){Kind=kind};
            case EGalleryShaderKind.BackdropBlur:return BackdropBlurShader(0);
            default:return _solidShader??=new(){Kind=EGalleryShaderKind.Solid};
        }
    }
    public GalleryBackendShader BackdropBlurShader(float blur)=>BackdropBlurShader(blur,new(-.70710677f,-.70710677f),new(0,0,0,1),new(1,1,1,1),1,0,0,new());
    public GalleryBackendShader BackdropBlurShader(float blur,Offsetf lightDirection,SRGBColor lightColor,SRGBColor glassColor,float transmittance,float grain,float refraction,GalleryBackdropShapeParams shape)
    {
        const float epsilon=.0001f;
        blur=float.IsFinite(blur)?MathF.Max(0,blur):0;blur=blur>epsilon?blur:0;
        lightDirection=lightDirection.Normalize(new(-.70710677f,-.70710677f));
        lightColor=lightColor.IsFinite()?lightColor.Clamped():new(0,0,0,1);
        if(lightColor.A<=epsilon||GalleryShared.Luminance(lightColor)<=epsilon){lightDirection=new(-.70710677f,-.70710677f);lightColor=new(0,0,0,0);}
        glassColor=glassColor.IsFinite()?glassColor.Clamped():new(1,1,1,1);
        transmittance=float.IsFinite(transmittance)?Math.Clamp(transmittance,0,1):1;
        transmittance=transmittance<1-epsilon?transmittance:1;
        float tintDelta=MathF.Max(MathF.Max(MathF.Abs(glassColor.R-1),MathF.Abs(glassColor.G-1)),MathF.Abs(glassColor.B-1));
        if(transmittance==1&&tintDelta<=epsilon)glassColor=new(1,1,1,1);
        grain=float.IsFinite(grain)?Math.Clamp(grain,0,1):0;grain=grain>epsilon?grain:0;
        refraction=float.IsFinite(refraction)?Math.Clamp(refraction,0,1):0;refraction=refraction>epsilon?refraction:0;
        foreach(var s in _backdropBlurShaders)
            if(s.BlurRadius==blur&&s.LightDirection==lightDirection&&s.LightColor==lightColor&&s.GlassColor==glassColor&&s.GlassTransmittance==transmittance&&s.GlassGrain==grain&&s.GlassRefraction==refraction&&s.BackdropShape==shape)return s;
        var shader=new GalleryBackendShader(){Kind=EGalleryShaderKind.BackdropBlur,BlurRadius=blur,BlurDownScale=4,LightDirection=lightDirection,LightColor=lightColor,
            GlassColor=glassColor,GlassTransmittance=transmittance,GlassGrain=grain,GlassRefraction=refraction,BackdropShape=shape};
        _backdropBlurShaders.Add(shader);return shader;
    }
    public Shader TexturedShader()=>Shader(EGalleryShaderKind.Textured);
    public GalleryBackendTexture CreateTexture(){var texture=new GalleryBackendTexture();AddTexture(texture);return texture;}
    public GalleryBackendTexture FontAtlasBackendTexture(TextServices services,uint index)
    {
        foreach(var t in _textures)if(ReferenceEquals(t.AtlasServices,services)&&t.AtlasIndex==index)return t;
        var texture=new GalleryBackendTexture(){AtlasServices=services,AtlasIndex=index};AddTexture(texture);return texture;
    }
    public void AddTexture(GalleryBackendTexture? texture){if(texture==null)return;foreach(var t in _textures)if(ReferenceEquals(t,texture))return;_textures.Add(texture);}
    public void Clear()
    {
        foreach(var t in _textures)t.DestroyGpu();_textures.Clear();
        _solidShader?.DestroyGpu();_texturedShader?.DestroyGpu();_grayLcdShader?.DestroyGpu();_sdfShader?.DestroyGpu();_sdfLcdShader?.DestroyGpu();
        foreach(var s in _backdropBlurShaders)s.DestroyGpu();
        _solidShader=_texturedShader=_grayLcdShader=_sdfShader=_sdfLcdShader=null;_backdropBlurShaders.Clear();
    }
}
