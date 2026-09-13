using System.Runtime.InteropServices;
namespace SkrGui;

internal static unsafe partial class TextNative
{
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int FT_Init_FreeType(out nint library);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int FT_Done_FreeType(nint library);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int FT_Property_Set(nint library, byte* module, byte* property, void* value);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int FT_New_Memory_Face(nint library, byte* data, int length, int faceIndex, out nint face);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "udata_setFileAccess_72")] internal static extern void udata_setFileAccess(int access, ref int status);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "udata_setCommonData_72")] internal static extern void udata_setCommonData(void* data, ref int status);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "u_init_72")] internal static extern void u_init(ref int status);
}
internal enum ETextServicesBackend : byte { Fallback, Advanced }
internal enum ETextShapedProjection : byte { FullRange, LineBreak }
public abstract partial class TextServices : IDisposable
{
    public static TextServices CreateFallback(TextServicesDesc? desc = null) => new TextServicesImpl(desc ?? new(), ETextServicesBackend.Fallback);
    public static TextServices CreateAdvanced(TextServicesDesc? desc = null) => new TextServicesImpl(desc ?? new(), ETextServicesBackend.Advanced);
    public abstract void Dispose();
}
internal sealed partial class TextServicesImpl : TextServices
{
    internal nint Library;
    internal ETextServicesBackend BackendValue;
    internal ulong NextFontFaceId = 1, FontTopologyRevisionValue = 1, FontStateRevisionValue = 1;
    internal bool IsIcuDataReady;
    internal bool IsDisposed;
    internal TextFontRasterConfig DefaultFontRasterConfigValue = new();
    internal readonly TextAtlasManager AtlasManager = new();
    internal readonly List<FontProvider> FontProviders = [];
    internal readonly List<TextFontData> FontData = [];
    internal readonly List<TextFontAsset> FontAssets = [];
    internal readonly List<TextFontFaceImpl> FontFaces = [];
    internal readonly Dictionary<ulong, TextFontFaceImpl> FontFacesById = [];
    private static readonly object IcuLock = new();
    private static byte[]? _activeIcuSource;
    private static GCHandle _activeIcuPin;
    private static nint _activeIcuAlignedStorage;
    private static bool _fileAccessConfigured;
    internal unsafe TextServicesImpl(TextServicesDesc desc, ETextServicesBackend backend)
    {
        BackendValue = backend; IsIcuDataReady = InitializeIcuData(desc.IcuData);
        if (TextNative.FT_Init_FreeType(out Library) != 0) Library = 0;
        else
        {
            int spread = 4;
            fixed (byte* property = "spread\0"u8)
            { fixed (byte* module = "sdf\0"u8) TextNative.FT_Property_Set(Library, module, property, &spread); fixed (byte* module = "bsdf\0"u8) TextNative.FT_Property_Set(Library, module, property, &spread); }
        }
        SetDefaultFontRasterConfig(desc.DefaultFontRasterConfig);
        if (desc.AddSystemFontProvider && FontProvider.CreateSystem() is { } provider) FontProviders.Add(provider);
    }
    private static unsafe bool InitializeIcuData(byte[]? source)
    {
        lock (IcuLock)
        {
            int status = 0;
            if (!_fileAccessConfigured) { TextNative.udata_setFileAccess(3, ref status); _fileAccessConfigured = true; }
            if (source is null || source.Length < 32) return false;
            if (_activeIcuSource is not null)
            { if (!ReferenceEquals(_activeIcuSource, source)) return false; status = 0; TextNative.u_init(ref status); return status <= 0; }
            var pin = GCHandle.Alloc(source, GCHandleType.Pinned);
            byte* storage = (byte*)pin.AddrOfPinnedObject(); nint aligned = 0;
            if (((nuint)storage & 15) != 0)
            { aligned = (nint)NativeMemory.AlignedAlloc((nuint)source.Length, 16); if (aligned == 0) { pin.Free(); return false; } source.CopyTo(new Span<byte>((void*)aligned, source.Length)); storage = (byte*)aligned; }
            status = 0; TextNative.udata_setCommonData(storage, ref status);
            if (status > 0) { pin.Free(); if (aligned != 0) NativeMemory.AlignedFree((void*)aligned); return false; }
            _activeIcuSource = source; _activeIcuPin = pin; _activeIcuAlignedStorage = aligned;
            status = 0; TextNative.u_init(ref status); return status <= 0;
        }
    }
    public override void Dispose()
    {
        if (IsDisposed) return;
        UnloadAllFontFaces(); if (Library != 0) { TextNative.FT_Done_FreeType(Library); Library = 0; }
        IsDisposed = true; GC.SuppressFinalize(this);
    }
    ~TextServicesImpl() { Dispose(); }
    public override Utf8StringView Name() => BackendValue == ETextServicesBackend.Advanced ? "ICU / HarfBuzz" : "Fallback (Built-in)";
    public override Utf8StringView ShortName() => BackendValue == ETextServicesBackend.Advanced ? "advanced" : "fallback";
    public override ETextFeature Features()
    {
        var result = ETextFeature.SimpleLayout | ETextFeature.FontBitmap | ETextFeature.FontDynamic;
        if (BackendValue == ETextServicesBackend.Advanced)
        { result |= ETextFeature.Shaping | ETextFeature.FontVariable; if (IsIcuDataReady) result |= ETextFeature.BidiLayout | ETextFeature.BreakIterators | ETextFeature.ContextSensitiveCaseConversion | ETextFeature.UnicodeIdentifiers | ETextFeature.UnicodeSecurity; }
        foreach (var provider in FontProviders) if (provider.IsSystemFontSource()) { result |= ETextFeature.FontSystem; break; }
        return result;
    }
    public override bool HasFeature(ETextFeature feature) => (Features() & feature) == feature;
    public override TextParagraph CreateParagraph() => new TextParagraphImpl(this);
    public override TextLine CreateLine() => new TextLineImpl(this);
    internal bool IsValid() => Library != 0;
    internal ulong FontTopologyRevision() => FontTopologyRevisionValue;
    internal ulong FontStateRevision() => FontStateRevisionValue;
    internal ETextServicesBackend Backend() => BackendValue;
}
