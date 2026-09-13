using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SkrGui;

internal static class NativeDependencies
{
    private static nint _library;
    [ModuleInitializer]
    internal static void Initialize() => NativeLibrary.SetDllImportResolver(typeof(NativeDependencies).Assembly, Resolve);
    private static nint Resolve(string name, Assembly assembly, DllImportSearchPath? path)
    {
        if (name != "skrgui_native") return 0;
        if (_library != 0) return _library;
        string filename = OperatingSystem.IsWindows() ? "skrgui_native.dll" : OperatingSystem.IsMacOS() ? "libskrgui_native.dylib" : "libskrgui_native.so";
        string? explicitPath = Environment.GetEnvironmentVariable("SKRGUI_NATIVE_LIBRARY");
        if (explicitPath != null && NativeLibrary.TryLoad(explicitPath, out _library)) return _library;
        foreach (var start in new[] { AppContext.BaseDirectory, Path.GetDirectoryName(assembly.Location)!, Directory.GetCurrentDirectory() })
            for (DirectoryInfo? dir = new(start); dir != null; dir = dir.Parent)
                foreach (var candidate in new[] { Path.Combine(dir.FullName, filename), Path.Combine(dir.FullName, "native", "artifacts", "win-x64", filename) })
                    if (File.Exists(candidate) && NativeLibrary.TryLoad(candidate, out _library)) return _library;
        throw new DllNotFoundException("The source-pinned SkrGui third-party library is missing. Run native/build_native.ps1.");
    }
}
