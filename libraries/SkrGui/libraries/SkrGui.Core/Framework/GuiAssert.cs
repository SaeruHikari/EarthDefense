using System.Diagnostics.CodeAnalysis;
namespace SkrGui;

// Host-facing translation of Skr assertions. VERIFY keeps the source recovery path.
internal static class GuiAssert
{
    public static void Require([DoesNotReturnIf(false)] bool condition,string message) { if(!condition) throw new InvalidOperationException(message); }
    public static bool Verify(bool condition,string message) { if(!condition) System.Diagnostics.Trace.TraceError(message); return condition; }
}
