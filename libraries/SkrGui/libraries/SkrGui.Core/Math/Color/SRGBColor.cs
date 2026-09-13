using System.Numerics;
namespace SkrGui;

// Source: SkrBase/math/manual/color/srgb_color.hpp (611561f8).
public struct SRGBColor : IEquatable<SRGBColor>
{
    public float R,G,B,A;
    public SRGBColor() { R=G=B=0; A=1; }
    public SRGBColor(float r,float g,float b,float a=1) { R=r; G=g; B=b; A=a; }
    public SRGBColor(Vector3 rgb,float alpha=1) : this(rgb.X,rgb.Y,rgb.Z,alpha) { }
    public SRGBColor(Vector4 rgba) : this(rgba.X,rgba.Y,rgba.Z,rgba.W) { }
    public static SRGBColor FromARGB32(uint v) => FromARGB((byte)(v>>24),(byte)(v>>16),(byte)(v>>8),(byte)v);
    public static SRGBColor FromRGBA32(uint v) => new(((v>>24)&255)/255f,((v>>16)&255)/255f,((v>>8)&255)/255f,(v&255)/255f);
    public static SRGBColor FromARGB(byte a,byte r,byte g,byte b) => new(r/255f,g/255f,b/255f,a/255f);
    public static SRGBColor FromRGBO(byte r,byte g,byte b,float opacity) => new(r/255f,g/255f,b/255f,opacity);
    public static SRGBColor FromRGBStr(string? value) => Parse(value,0,out var color) ? color : new SRGBColor();
    public static SRGBColor FromRGBAStr(string? value) => Parse(value,1,out var color) ? color : new SRGBColor();
    public static SRGBColor FromARGBStr(string? value) => Parse(value,2,out var color) ? color : new SRGBColor();
    // C++ consteval rejection is represented by a checked construction call in C#.
    public static SRGBColor FromRGBStrConsteval(string value) => ParseChecked(value,0);
    public static SRGBColor FromRGBAStrConsteval(string value) => ParseChecked(value,1);
    public static SRGBColor FromARGBStrConsteval(string value) => ParseChecked(value,2);
    public static SRGBColor FromRGBStrConsteval(string value,int length) => ParseChecked(value[..length],0);
    public static SRGBColor FromRGBAStrConsteval(string value,int length) => ParseChecked(value[..length],1);
    public static SRGBColor FromARGBStrConsteval(string value,int length) => ParseChecked(value[..length],2);
    private static SRGBColor ParseChecked(string value,int mode) => Parse(value,mode,out var c) ? c : throw new ArgumentException("Invalid source color literal",nameof(value));
    private static bool Parse(string? text,int mode,out SRGBColor color)
    {
        color=new SRGBColor();
        if(string.IsNullOrEmpty(text)||text[0]!='#') return false;
        int count=mode==0?3:4;
        bool shortForm=text.Length==count+1;
        if(!shortForm&&text.Length!=count*2+1) return false;
        Span<float> values=stackalloc float[4];
        bool valid=true;
        static int Hex(char c,ref bool valid) { if(c>='0'&&c<='9') return c-'0'; if(c>='a'&&c<='f') return 10+c-'a'; if(c>='A'&&c<='F') return 10+c-'A'; valid=false; return 0; }
        for(int i=0;i<count;i++) { int high=Hex(text[1+i*(shortForm?1:2)],ref valid); int low=shortForm?high:Hex(text[2+i*2],ref valid); values[i]=((high<<4)+low)/255f; }
        color=mode==0?new(values[0],values[1],values[2],1):mode==1?new(values[0],values[1],values[2],values[3]):new(values[1],values[2],values[3],values[0]);
        return valid;
    }
    private static float LinearToSrgb(float channel) => channel<=0.04045f/12.92f ? channel*12.92f : MathF.Pow(channel,1f/2.4f)*1.055f-0.055f;
    private static float SrgbToLinear(float channel) => channel<=0.04045f ? channel/12.92f : MathF.Pow((channel+0.055f)/1.055f,2.4f);
    private static byte FloatToByte(float value) { if(float.IsNaN(value)||value<=0) return 0; if(value>=1) return 255; return (byte)MathF.Round(value*255,MidpointRounding.AwayFromZero); }
    public static SRGBColor FromLinear(float r,float g,float b,float a=1) => new(LinearToSrgb(r),LinearToSrgb(g),LinearToSrgb(b),a);
    public static SRGBColor FromLinear(Vector3 rgb,float alpha=1) => FromLinear(rgb.X,rgb.Y,rgb.Z,alpha);
    public static SRGBColor FromLinear(Vector4 rgba) => FromLinear(rgba.X,rgba.Y,rgba.Z,rgba.W);
    public static SRGBColor Lerp(SRGBColor a,SRGBColor b,float t) => new(a.R+(b.R-a.R)*t,a.G+(b.G-a.G)*t,a.B+(b.B-a.B)*t,a.A+(b.A-a.A)*t);
    public static SRGBColor AlphaBlend(SRGBColor foreground,SRGBColor background)
    {
        float foregroundAlpha=foreground.A,backgroundAlpha=background.A;
        float outAlpha=foregroundAlpha+backgroundAlpha*(1-foregroundAlpha);
        if(outAlpha<=0) return new(0,0,0,0);
        float contribution=backgroundAlpha*(1-foregroundAlpha);
        float red=(foreground.R*foregroundAlpha+background.R*contribution)/outAlpha;
        float green=(foreground.G*foregroundAlpha+background.G*contribution)/outAlpha;
        float blue=(foreground.B*foregroundAlpha+background.B*contribution)/outAlpha;
        return new(red,green,blue,outAlpha);
    }
    public static float ContrastRatio(SRGBColor a,SRGBColor b) { float la=a.ComputeLuminance(),lb=b.ComputeLuminance(); return (Math.Max(la,lb)+0.05f)/(Math.Min(la,lb)+0.05f); }
    public static explicit operator Vector3(SRGBColor color) => new(color.R,color.G,color.B);
    public static explicit operator Vector4(SRGBColor color) => new(color.R,color.G,color.B,color.A);
    public Vector3 ToLinearRgb() => new(SrgbToLinear(R),SrgbToLinear(G),SrgbToLinear(B));
    public Vector4 ToLinear() => new(SrgbToLinear(R),SrgbToLinear(G),SrgbToLinear(B),A);
    public uint ToArgb32() => ((uint)Alpha8()<<24)|((uint)Red8()<<16)|((uint)Green8()<<8)|Blue8();
    public uint ToRgba32() => ((uint)Red8()<<24)|((uint)Green8()<<16)|((uint)Blue8()<<8)|Alpha8();
    public byte Alpha8() => FloatToByte(A);
    public byte Red8() => FloatToByte(R);
    public byte Green8() => FloatToByte(G);
    public byte Blue8() => FloatToByte(B);
    public bool IsOpaque() => A>=1;
    public bool IsTransparent() => A<=0;
    public bool IsFinite() => float.IsFinite(R)&&float.IsFinite(G)&&float.IsFinite(B)&&float.IsFinite(A);
    public bool HasNan() => float.IsNaN(R)||float.IsNaN(G)||float.IsNaN(B)||float.IsNaN(A);
    public float ComputeLuminance() => 0.2126f*SrgbToLinear(Math.Clamp(R,0,1))+0.7152f*SrgbToLinear(Math.Clamp(G,0,1))+0.0722f*SrgbToLinear(Math.Clamp(B,0,1));
    public bool IsLight() => ComputeLuminance()>0.5f;
    public bool IsDark() => !IsLight();
    public SRGBColor Clamped() => new(Math.Clamp(R,0,1),Math.Clamp(G,0,1),Math.Clamp(B,0,1),Math.Clamp(A,0,1));
    public SRGBColor WithAlpha(float alpha) => new(R,G,B,alpha);
    public SRGBColor WithRed(float red) => new(red,G,B,A);
    public SRGBColor WithGreen(float green) => new(R,green,B,A);
    public SRGBColor WithBlue(float blue) => new(R,G,blue,A);
    public SRGBColor WithValues(float? red=null,float? green=null,float? blue=null,float? alpha=null) => new(red??R,green??G,blue??B,alpha??A);
    public SRGBColor Premultiplied() => new(R*A,G*A,B*A,A);
    public SRGBColor Unpremultiplied() => A<=0 ? new(0,0,0,0) : new(R/A,G/A,B/A,A);
    public bool NearlyEqual(SRGBColor rhs,float epsilon=0.0001f) => MathF.Abs(R-rhs.R)<=epsilon&&MathF.Abs(G-rhs.G)<=epsilon&&MathF.Abs(B-rhs.B)<=epsilon&&MathF.Abs(A-rhs.A)<=epsilon;
    public static bool operator==(SRGBColor a,SRGBColor b) => a.R==b.R&&a.G==b.G&&a.B==b.B&&a.A==b.A;
    public static bool operator!=(SRGBColor a,SRGBColor b) => !(a==b);
    public bool Equals(SRGBColor b) => this==b;
    public override bool Equals(object? obj) => obj is SRGBColor b&&this==b;
    public override int GetHashCode() => HashCode.Combine(R,G,B,A);
}
