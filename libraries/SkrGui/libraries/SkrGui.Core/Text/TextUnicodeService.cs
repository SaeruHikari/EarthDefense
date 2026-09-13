using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
namespace SkrGui;

// Source: text/service/text_unicode_service.cpp @ 611561f8.
internal static partial class TextAlgorithms
{
    internal static bool IsControl(uint cp) => cp <= 0x1f || cp >= 0x7f && cp <= 0x9f;
    internal static bool IsLinebreak(uint cp) => cp >= 0xa && cp <= 0xd || cp == 0x85 || cp == 0x2028 || cp == 0x2029;
    internal static bool IsHardBreakAt(IReadOnlyList<uint> codepoints, ulong source)
    { if (source >= (ulong)codepoints.Count || !IsLinebreak(codepoints[(int)source])) return false; return codepoints[(int)source] != '\r' || source + 1 >= (ulong)codepoints.Count || codepoints[(int)source + 1] != '\n'; }
    internal static bool IsWhitespace(uint cp) => cp == 0x20 || cp == 0xa0 || cp == 0x1680 || cp >= 0x2000 && cp <= 0x200b || cp == 0x202f || cp == 0x205f || cp == 0x3000 || cp == 0x2028 || cp == 0x2029 || cp >= 9 && cp <= 0xd || cp == 0x85;
    internal static bool IsZeroWidth(uint cp, bool preserveControl) => preserveControl ? cp == 0x200b || cp == 0xfeff : cp >= 0x200b && cp <= 0x200d || cp == 0x2060 || cp == 0xfeff;
    internal static bool DecodeCodepoints(Utf8StringView text, List<uint> output)
    {
        output.Clear(); int index = 0; var bytes = text.Bytes;
        while (index < bytes.Length)
        { if (Rune.DecodeFromUtf8(bytes[index..], out var cp, out int count) != OperationStatus.Done) { output.Clear(); return false; } output.Add((uint)cp.Value); index += count; }
        return true;
    }
    internal static string EncodeCodepoints(IReadOnlyList<uint> codepoints)
    { var result = new StringBuilder(); foreach (var cp in codepoints) result.Append(char.ConvertFromUtf32((int)cp)); return result.ToString(); }
    internal static bool IsWordPunctuation(uint cp, bool advanced)
    {
        bool punctuation = advanced ? TextNative.u_ispunct((int)cp) : cp >= 0x20 && cp <= 0x2f || cp >= 0x3a && cp <= 0x40 || cp >= 0x5b && cp <= 0x5e || cp == 0x60 || cp >= 0x7b && cp <= 0x7e || cp >= 0x2000 && cp <= 0x206f || cp >= 0x3000 && cp <= 0x303f;
        return punctuation && cp != 0x5f || cp == 0x5f || cp == 9 || cp == 0xfffc;
    }
    internal static byte[] NullTerminated(Utf8StringView value)
    { var bytes = new byte[value.Bytes.Length + 1]; value.Bytes.CopyTo(bytes); return bytes; }
    internal static unsafe bool Utf8ToUtf16(Utf8StringView text, out char[] output)
    {
        output = []; int status = 0, length = 0;
        fixed (byte* source = text.Bytes)
        {
            TextNative.u_strFromUTF8(null, 0, &length, source, text.Bytes.Length, ref status);
            if (status != 15 && status > 0) return false; status = 0; output = new char[length];
            fixed (char* result = output) TextNative.u_strFromUTF8(result, length, null, source, text.Bytes.Length, ref status);
        }
        return status <= 0;
    }
    internal static unsafe bool IsNormalized(nint normalizer, char[] utf16, ref int status)
    { fixed (char* text = utf16) return TextNative.unorm2_isNormalized(normalizer, text, utf16.Length, ref status); }
    internal static string SimpleCaseMap(Utf8StringView text, bool upper)
    {
        List<uint> codepoints = []; if (!DecodeCodepoints(text, codepoints)) return "";
        for (int i = 0; i < codepoints.Count; i++) codepoints[i] = (uint)(upper ? TextNative.u_toupper((int)codepoints[i]) : TextNative.u_tolower((int)codepoints[i]));
        return EncodeCodepoints(codepoints);
    }
    internal static string SimpleTitle(Utf8StringView text)
    {
        List<uint> source = []; if (!DecodeCodepoints(text, source) || source.Count == 0) return "";
        List<uint> separated = [source[0]];
        bool pu = TextNative.u_isupper((int)source[0]), pl = TextNative.u_islower((int)source[0]), pd = TextNative.u_isdigit((int)source[0]);
        for (int i = 1; i < source.Count; i++)
        {
            bool cu = TextNative.u_isupper((int)source[i]), cl = TextNative.u_islower((int)source[i]), cd = TextNative.u_isdigit((int)source[i]);
            bool nl = i + 1 < source.Count && TextNative.u_islower((int)source[i + 1]);
            if (pl && cu || (pu || pd) && cu && nl || pd && cl && nl || (pu || pl) && cd) separated.Add(0x20);
            separated.Add(source[i]); pu = cu; pl = cl; pd = cd;
        }
        List<uint> output = []; bool wordStart = true;
        foreach (var original in separated)
        {
            uint cp = original; bool delimiter = IsWhitespace(cp) || cp == 0x5f || cp == 0x2d || cp == 0x2010 || cp == 0x2011;
            if (delimiter) { if (output.Count != 0 && output[^1] != 0x20) output.Add(0x20); wordStart = true; continue; }
            cp = (uint)(wordStart ? TextNative.u_toupper((int)cp) : TextNative.u_tolower((int)cp)); output.Add(cp); wordStart = false;
        }
        if (output.Count != 0 && output[^1] == 0x20) output.RemoveAt(output.Count - 1);
        return EncodeCodepoints(output);
    }
    internal static string SimpleStripDiacritics(Utf8StringView text)
    {
        List<uint> source = []; if (!DecodeCodepoints(text, source)) return ""; List<uint> result = [];
        foreach (uint cp in source) { if (cp >= 0x2b0 && cp <= 0x36f) continue; result.Add(FallbackStripDiacritic(cp)); }
        return EncodeCodepoints(result);
    }
    internal static bool IsLetterCategory(byte category) => category is >= 1 and <= 5;
}
internal sealed partial class TextServicesImpl
{
    public override unsafe bool IsLocaleRightToLeft(Utf8StringView locale)
    { if (BackendValue == ETextServicesBackend.Fallback) return false; fixed (byte* bytes = TextAlgorithms.NullTerminated(locale)) return TextNative.uloc_isRightToLeft(bytes); }
    public override unsafe void StringWordBreaks(Utf8StringView text, Utf8StringView language, ulong characters_per_line, List<TextRange> out_breaks)
    {
        out_breaks.Clear(); List<uint> cps = []; if (!TextAlgorithms.DecodeCodepoints(text, cps)) return;
        byte[] advanced = new byte[cps.Count + 1];
        if (BackendValue == ETextServicesBackend.Advanced)
        {
            int status = 0; nint iterator;
            fixed (byte* locale = TextAlgorithms.NullTerminated(language)) iterator = TextNative.ubrk_open(1, locale, null, 0, ref status);
            fixed (byte* bytes = text.Bytes)
            {
                nint utext = TextNative.utext_openUTF8(0, bytes, text.Bytes.Length, ref status);
                if (status <= 0 && iterator != 0)
                {
                    TextNative.ubrk_setUText(iterator, utext, ref status); ulong b = 0, source = 0;
                    for (int boundary = TextNative.ubrk_first(iterator); status <= 0 && boundary != -1; boundary = TextNative.ubrk_next(iterator))
                    {
                        while (b < (ulong)boundary) { b += TextAlgorithms.DecodeNext(text, b).Bytes; source++; }
                        if (source != unchecked((ulong)cps.Count - 1)) advanced[source] = 1;
                    }
                }
                if (iterator != 0) TextNative.ubrk_close(iterator); TextNative.utext_close(utext);
            }
        }
        if (characters_per_line > 0)
        {
            ulong lineStart = 0, lineLength = 0; long lastBreak = -1;
            for (ulong i = 0; i < (ulong)cps.Count; i++)
            {
                uint cp = cps[(int)i]; bool linebreak = TextAlgorithms.IsLinebreak(cp), white = TextAlgorithms.IsWhitespace(cp), punctuation = TextAlgorithms.IsWordPunctuation(cp, BackendValue == ETextServicesBackend.Advanced);
                if (linebreak) { if (lineLength > 0) out_breaks.Add(new(lineStart, i)); lineStart = i; lineLength = 0; lastBreak = -1; continue; }
                if (advanced[i] != 0 || white || punctuation) lastBreak = (long)i;
                if (lineLength == characters_per_line)
                {
                    if (lastBreak >= 0)
                    {
                        ulong p = (ulong)lastBreak, withSpaces = p;
                        while (p > lineStart && TextAlgorithms.IsWhitespace(cps[(int)p - 1])) p--;
                        if (lineStart != p) out_breaks.Add(new(lineStart, p));
                        while (withSpaces < (ulong)cps.Count && TextAlgorithms.IsWhitespace(cps[(int)withSpaces])) withSpaces++;
                        lineStart = withSpaces;
                        if (withSpaces < i) lineLength = i - withSpaces; else { i = withSpaces; lineLength = 0; }
                    }
                    else { out_breaks.Add(new(lineStart, i)); lineStart = i; lineLength = 0; }
                    lastBreak = -1;
                }
                lineLength++;
            }
            if (lineLength > 0) out_breaks.Add(new(lineStart, (ulong)cps.Count)); return;
        }
        long wordStart = 0; ulong wordLength = 0;
        for (int i = 0; i < cps.Count; i++)
        {
            uint cp = cps[i]; bool linebreak = TextAlgorithms.IsLinebreak(cp), white = TextAlgorithms.IsWhitespace(cp), punctuation = TextAlgorithms.IsWordPunctuation(cp, BackendValue == ETextServicesBackend.Advanced);
            if (wordStart < 0) { if (!linebreak && !white && !punctuation) wordStart = i; continue; }
            if (linebreak) { if (wordLength > 0) out_breaks.Add(new((ulong)wordStart, (ulong)i)); wordStart = -1; wordLength = 0; }
            else if (advanced[i] != 0 || white || punctuation) { if (wordLength > 0) out_breaks.Add(new((ulong)wordStart, (ulong)i)); wordStart = white || punctuation ? -1 : i; wordLength = 0; }
            wordLength++;
        }
        if (wordStart >= 0 && wordLength > 0) out_breaks.Add(new((ulong)wordStart, (ulong)cps.Count));
    }
    public override unsafe void StringCharacterBreaks(Utf8StringView text, Utf8StringView language, List<ulong> out_breaks)
    {
        out_breaks.Clear(); List<uint> cps = []; if (!TextAlgorithms.DecodeCodepoints(text, cps)) return;
        void Fallback() { for (ulong i = 1; i < (ulong)cps.Count; i++) out_breaks.Add(i); }
        if (BackendValue == ETextServicesBackend.Fallback) { Fallback(); return; }
        int status = 0; nint iterator;
        fixed (byte* locale = TextAlgorithms.NullTerminated(language)) iterator = TextNative.ubrk_open(0, locale, null, 0, ref status);
        fixed (byte* bytes = text.Bytes)
        {
            nint utext = TextNative.utext_openUTF8(0, bytes, bytes == null ? 0 : text.Bytes.Length, ref status);
            if (status > 0 || iterator == 0) { if (iterator != 0) TextNative.ubrk_close(iterator); TextNative.utext_close(utext); Fallback(); return; }
            TextNative.ubrk_setUText(iterator, utext, ref status); ulong b = 0, source = 0;
            for (int boundary = TextNative.ubrk_next(iterator); status <= 0 && boundary != -1; boundary = TextNative.ubrk_next(iterator))
            { while (b < (ulong)boundary) { b += TextAlgorithms.DecodeNext(text, b).Bytes; source++; } out_breaks.Add(source); }
            TextNative.ubrk_close(iterator); TextNative.utext_close(utext);
        }
        if (out_breaks.Count == 0 && cps.Count != 0) Fallback();
    }
    public override bool IsValidLetter(uint codepoint) => TextNative.u_isalpha((int)codepoint);
    private unsafe delegate int CaseTransform(nint map, byte* destination, int capacity, byte* source, int length, ref int status);
    private static unsafe string CaseMap(Utf8StringView text, Utf8StringView language, CaseTransform transform)
    {
        int status = 0; nint map;
        fixed (byte* locale = TextAlgorithms.NullTerminated(language)) map = TextNative.ucasemap_open(locale, 0, ref status);
        if (status > 0 || map == 0) return "";
        fixed (byte* input = text.Bytes)
        {
            int size = transform(map, null, 0, input, text.Bytes.Length, ref status);
            if (status != 15 && status > 0) { TextNative.ucasemap_close(map); return ""; }
            status = 0; byte[] output = new byte[size];
            fixed (byte* target = output) transform(map, target, size, input, text.Bytes.Length, ref status);
            TextNative.ucasemap_close(map); return status <= 0 ? Encoding.UTF8.GetString(output) : "";
        }
    }
    public override unsafe Utf8StringView StringToUpper(Utf8StringView text, Utf8StringView language = default)
    { if (BackendValue == ETextServicesBackend.Fallback) return TextAlgorithms.SimpleCaseMap(text, true); string result = CaseMap(text, language, TextNative.ucasemap_utf8ToUpper); return result.Length == 0 && !text.IsEmpty() ? TextAlgorithms.SimpleCaseMap(text, true) : result; }
    public override unsafe Utf8StringView StringToLower(Utf8StringView text, Utf8StringView language = default)
    { if (BackendValue == ETextServicesBackend.Fallback) return TextAlgorithms.SimpleCaseMap(text, false); string result = CaseMap(text, language, TextNative.ucasemap_utf8ToLower); return result.Length == 0 && !text.IsEmpty() ? TextAlgorithms.SimpleCaseMap(text, false) : result; }
    public override unsafe Utf8StringView StringToTitle(Utf8StringView text, Utf8StringView language = default)
    { if (BackendValue == ETextServicesBackend.Fallback) return TextAlgorithms.SimpleTitle(text); string result = CaseMap(text, language, TextNative.ucasemap_utf8ToTitle); return result.Length == 0 && !text.IsEmpty() ? TextAlgorithms.SimpleTitle(text) : result; }
    [Conditional("DEBUG")] private static void SourceUnimplemented() => Debug.Fail("Unimplemented in SkrGui source at 611561f8.");
    public override bool LoadSupportData(Utf8StringView path) { SourceUnimplemented(); return false; }
    public override Utf8StringView SupportDataFilename() { SourceUnimplemented(); return default; }
    public override Utf8StringView SupportDataInfo() { SourceUnimplemented(); return ""; }
    public override bool SaveSupportData(Utf8StringView path) { SourceUnimplemented(); return false; }
    public override ReadOnlySpan<byte> SupportData() { SourceUnimplemented(); return []; }
    public override bool IsLocaleUsingSupportData(Utf8StringView locale) { SourceUnimplemented(); return false; }
    public override void ParseStructuredText(ETextStructuredTextParser parser, ReadOnlySpan<Utf8StringView> arguments, Utf8StringView text, List<TextBidiOverride> out_overrides) { SourceUnimplemented(); out_overrides.Clear(); }
}
