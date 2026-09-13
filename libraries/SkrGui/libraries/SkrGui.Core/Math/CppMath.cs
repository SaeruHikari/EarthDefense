namespace SkrGui;

// C++ std::min/max/clamp compare in this order, including unordered float values.
internal static class CppMath
{
    public static float Min(float a,float b) => b<a ? b:a;
    public static float Max(float a,float b) => a<b ? b:a;
    public static float Clamp(float value,float min,float max) => value<min ? min : max<value ? max:value;
    public static float Fmod(float value,float divisor) => value%divisor;
    public static int Min(int a,int b) => b<a ? b:a;
    public static int Max(int a,int b) => a<b ? b:a;
    public static int Clamp(int value,int min,int max) => value<min ? min : max<value ? max:value;
    public static uint Min(uint a,uint b) => b<a ? b:a;
    public static uint Max(uint a,uint b) => a<b ? b:a;
    public static uint Clamp(uint value,uint min,uint max) => value<min ? min : max<value ? max:value;
    public static long Min(long a,long b) => b<a ? b:a;
    public static long Max(long a,long b) => a<b ? b:a;
    public static long Clamp(long value,long min,long max) => value<min ? min : max<value ? max:value;
    public static ulong Min(ulong a,ulong b) => b<a ? b:a;
    public static ulong Max(ulong a,ulong b) => a<b ? b:a;
    public static ulong Clamp(ulong value,ulong min,ulong max) => value<min ? min : max<value ? max:value;
    public static double Min(double a,double b) => b<a ? b:a;
    public static double Max(double a,double b) => a<b ? b:a;
    public static double Clamp(double value,double min,double max) => value<min ? min : max<value ? max:value;
    public static double Fmod(double value,double divisor) => value%divisor;
}
