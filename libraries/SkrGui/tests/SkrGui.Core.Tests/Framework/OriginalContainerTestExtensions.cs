namespace SkrGui.Tests;
// Test-only adapters for source RC casts and Vector/String queries.
internal static class OriginalContainerTestExtensions
{
    public static T Front<T>(this IReadOnlyList<T> value)=>value[0];
    public static T Back<T>(this IReadOnlyList<T> value)=>value[value.Count-1];
    public static bool IsEmpty<T>(this ReadOnlySpan<T> value)=>value.Length==0;
    public static int Size<T>(this ReadOnlySpan<T> value)=>value.Length;
    public static T? RttrCast<T>(this object value)where T:class=>value as T;
    public static int Size<T>(this IReadOnlyCollection<T> value)=>value.Count;
    public static bool IsEmpty<T>(this IReadOnlyCollection<T> value)=>value.Count==0;
    public static bool IsEmpty(this string value)=>value.Length==0;
    public static T[] Data<T>(this IEnumerable<T> value)=>value.ToArray();
}
