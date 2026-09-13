namespace SkrGui;
internal static class VgScalar
{
    public static bool NearlyEqual(float a,float b,float epsilon=1e-4f) => MathF.Abs(a-b)<=epsilon;
    public static bool IsFinite(float v) => float.IsFinite(v); public static bool IsFinite(double v) => double.IsFinite(v);
    public static bool IsNaN(float v) => float.IsNaN(v); public static bool IsNaN(double v) => double.IsNaN(v);
    public static float Abs(float v) => MathF.Abs(v); public static double Abs(double v) => Math.Abs(v);
    public static float Sqrt(float v) => MathF.Sqrt(v); public static double Sqrt(double v) => Math.Sqrt(v);
    public static float Sin(float v) => MathF.Sin(v); public static double Sin(double v) => Math.Sin(v);
    public static float Cos(float v) => MathF.Cos(v); public static double Cos(double v) => Math.Cos(v);
    public static float Tan(float v) => MathF.Tan(v); public static double Tan(double v) => Math.Tan(v);
    public static float Asin(float v) => MathF.Asin(v); public static double Asin(double v) => Math.Asin(v);
    public static float Acos(float v) => MathF.Acos(v); public static double Acos(double v) => Math.Acos(v);
    public static float Atan(float v) => MathF.Atan(v); public static double Atan(double v) => Math.Atan(v);
    public static float Atan2(float y,float x) => MathF.Atan2(y,x); public static double Atan2(double y,double x) => Math.Atan2(y,x);
    public static float Ceiling(float v) => MathF.Ceiling(v); public static double Ceiling(double v) => Math.Ceiling(v);
    public static float Floor(float v) => MathF.Floor(v); public static double Floor(double v) => Math.Floor(v);
    public static float Round(float v) => MathF.Round(v,MidpointRounding.AwayFromZero); public static double Round(double v) => Math.Round(v,MidpointRounding.AwayFromZero);
    public static float Pow(float v,float p) => MathF.Pow(v,p); public static double Pow(double v,double p) => Math.Pow(v,p);
    public static float Exp(float v) => MathF.Exp(v); public static double Exp(double v) => Math.Exp(v);
    public static float Log(float v) => MathF.Log(v); public static double Log(double v) => Math.Log(v);
    public static float CopySign(float v,float s) => MathF.CopySign(v,s); public static double CopySign(double v,double s) => Math.CopySign(v,s);
}
