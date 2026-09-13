// Source: text/font_provider.hpp + font/font_provider.cpp @ 611561f8.
namespace SkrGui;

public record struct FontProviderFaceSource
{
    private Utf8StringView _sourceKey;
    public Utf8StringView SourceKey { readonly get => _sourceKey; set => _sourceKey = new Utf8StringView(value.Bytes.ToArray()); }
    public uint FaceIndex;
    public EFontWeight Weight = EFontWeight.Regular;
    public EFontStyle Style = EFontStyle.Normal;
    public EFontStretch Stretch = EFontStretch.Normal;
    public FontProviderFaceSource() { }
    public bool IsValid() => !SourceKey.IsEmpty();
}
public readonly record struct FontFaceQuery(FontProvider? Provider, FontProviderFaceSource Source, bool IsFallback = false)
{
    public bool IsValid() => Provider is not null && Source.IsValid();
}
public abstract partial class FontProvider
{
    public virtual bool IsSystemFontSource() => false;
    public abstract bool QueryFace(Utf8StringView family, EFontWeight weight, EFontStyle style, EFontStretch stretch, out FontProviderFaceSource out_source);
    public virtual bool LoadFaceData(FontProviderFaceSource source, List<byte> out_data)
    {
        out_data.Clear();
        if (!source.IsValid()) return false;
        try { out_data.AddRange(File.ReadAllBytes(source.SourceKey.ToString())); return true; }
        catch (IOException) { out_data.Clear(); return false; }
        catch (UnauthorizedAccessException) { out_data.Clear(); return false; }
    }
}
public sealed class TextServicesDesc
{
    public bool AddSystemFontProvider = true;
    public TextFontRasterConfig DefaultFontRasterConfig = new();
    public byte[]? IcuData;
}
