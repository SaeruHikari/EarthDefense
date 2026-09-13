using System.Numerics;
namespace SkrGui;

// Source: SkrBase/math/manual/color/hct_color.hpp (611561f8).
public struct Double3
{
    public double X,Y,Z;
    public Double3(double value){X=Y=Z=value;}
    public Double3(double x,double y,double z){X=x;Y=y;Z=z;}
}
public partial struct HCTColor : IEquatable<HCTColor>
{
    public double Hue,Chroma,Tone;
    public HCTColor(double hue,double chroma,double tone)
    {
        Hue=hue;Chroma=chroma;Tone=tone;
        if(!AreFinite(hue,chroma,tone)){Hue=Chroma=Tone=0;GuiAssert.Verify(false,"HCTColor requires finite components");return;}
        Hue=NormalizeHue(hue);Chroma=ClampChroma(chroma);Tone=ClampTone(tone);
    }
    public HCTColor(Double3 hct):this(hct.X,hct.Y,hct.Z){}
    public HCTColor(Vector3 hct):this(hct.X,hct.Y,hct.Z){}
    public static explicit operator Double3(HCTColor color)=>new(color.Hue,color.Chroma,color.Tone);
    public static explicit operator Vector3(HCTColor color)=>new((float)color.Hue,(float)color.Chroma,(float)color.Tone);
    public HCTColor WithHue(double hue)=>new(hue,Chroma,Tone);
    public HCTColor WithChroma(double chroma)=>new(Hue,chroma,Tone);
    public HCTColor WithTone(double tone)=>new(Hue,Chroma,tone);
    public static bool operator==(HCTColor a,HCTColor b)=>a.Hue==b.Hue&&a.Chroma==b.Chroma&&a.Tone==b.Tone;
    public static bool operator!=(HCTColor a,HCTColor b)=>!(a==b);
    public bool Equals(HCTColor b)=>this==b;public override bool Equals(object? obj)=>obj is HCTColor b&&this==b;public override int GetHashCode()=>HashCode.Combine(Hue,Chroma,Tone);
    private static bool AreFinite(double hue,double chroma,double tone)=>double.IsFinite(hue)&&double.IsFinite(chroma)&&double.IsFinite(tone);
    private static double NormalizeHue(double hue){double normalized=hue%360;double result=normalized<0?normalized+360:normalized;return result>=360?0:result;}
    private static double ClampChroma(double chroma)=>chroma<0?0:chroma;
    private static double ClampTone(double tone)=>tone<0?0:tone>100?100:tone;
}
internal static class HctMath
{
    public static double Round(double value)=>Math.Round(value,MidpointRounding.AwayFromZero);
}
