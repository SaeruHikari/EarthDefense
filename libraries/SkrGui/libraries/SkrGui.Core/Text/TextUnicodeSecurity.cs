using System.Runtime.InteropServices;
using System.Text;
namespace SkrGui;

// Source: text/service/text_unicode_service.cpp:743-994 @ 611561f8.
internal sealed partial class TextServicesImpl
{
    public override unsafe long IsConfusable(Utf8StringView text, ReadOnlySpan<Utf8StringView> dictionary)
    {
        if (BackendValue == ETextServicesBackend.Fallback) return -1;
        int status = 0; nint checker = TextNative.uspoof_open(ref status);
        if (status > 0 || checker == 0) return -1;
        TextNative.uspoof_setChecks(checker, 7, ref status);
        if (status > 0) { TextNative.uspoof_close(checker); return -1; }
        bool Skeleton(Utf8StringView value, out byte[] output)
        {
            output = []; int localStatus = 0;
            fixed (byte* bytes = value.Bytes)
            {
                int length = TextNative.uspoof_getSkeletonUTF8(checker, 0, bytes, value.Bytes.Length, null, 0, ref localStatus);
                if (localStatus != 15 && localStatus > 0) return false;
                localStatus = 0; output = new byte[length];
                fixed (byte* target = output) TextNative.uspoof_getSkeletonUTF8(checker, 0, bytes, value.Bytes.Length, target, length, ref localStatus);
                return localStatus <= 0;
            }
        }
        if (!Skeleton(text, out var skeleton)) { TextNative.uspoof_close(checker); return -1; }
        for (int i = 0; i < dictionary.Length; i++)
            if (Skeleton(dictionary[i], out var candidate) && candidate.Length == skeleton.Length && skeleton.AsSpan().SequenceEqual(candidate))
            { TextNative.uspoof_close(checker); return i; }
        TextNative.uspoof_close(checker); return -1;
    }
    public override unsafe bool SpoofCheck(Utf8StringView text)
    {
        if (BackendValue == ETextServicesBackend.Fallback) return false;
        int status = 0; nint checker = TextNative.uspoof_open(ref status);
        if (status > 0 || checker == 0) return false;
        nint allowed = TextNative.uset_openEmpty(); if (allowed == 0) { TextNative.uspoof_close(checker); return false; }
        nint recommended = TextNative.uspoof_getRecommendedSet(ref status), inclusion = TextNative.uspoof_getInclusionSet(ref status);
        if (status > 0 || recommended == 0 || inclusion == 0) { TextNative.uset_close(allowed); TextNative.uspoof_close(checker); return false; }
        TextNative.uset_addAll(allowed, recommended); TextNative.uset_addAll(allowed, inclusion); TextNative.uspoof_setAllowedChars(checker, allowed, ref status);
        TextNative.uspoof_setRestrictionLevel(checker, 0x40000000);
        int result; fixed (byte* source = text.Bytes) result = TextNative.uspoof_check2UTF8(checker, source, text.Bytes.Length, 0, ref status);
        TextNative.uset_close(allowed); TextNative.uspoof_close(checker); return status <= 0 && result != 0;
    }
    public override unsafe Utf8StringView StripDiacritics(Utf8StringView text)
    {
        string Fallback() => TextAlgorithms.SimpleStripDiacritics(text);
        if (BackendValue == ETextServicesBackend.Fallback) return Fallback();
        if (!TextAlgorithms.Utf8ToUtf16(text, out var utf16)) return Fallback();
        int status = 0; nint normalizer = TextNative.unorm2_getNFKDInstance(ref status); if (status > 0 || normalizer == 0) return Fallback();
        char[] normalized;
        fixed (char* input = utf16)
        {
            int count = TextNative.unorm2_normalize(normalizer, input, utf16.Length, null, 0, ref status);
            if (status != 15 && status > 0) return Fallback(); status = 0; normalized = new char[count];
            fixed (char* output = normalized) TextNative.unorm2_normalize(normalizer, input, utf16.Length, output, count, ref status);
            if (status > 0) return Fallback();
        }
        List<char> filtered = [];
        for (int i = 0; i < normalized.Length;)
        {
            int begin = i, cp = normalized[i++];
            if (cp >= 0xd800 && cp <= 0xdbff && i < normalized.Length && normalized[i] >= 0xdc00 && normalized[i] <= 0xdfff)
                cp = ((cp - 0xd800) << 10) + normalized[i++] - 0xdc00 + 0x10000;
            if (TextNative.u_getCombiningClass(cp) == 0) for (int j = begin; j < i; j++) filtered.Add(normalized[j]);
        }
        status = 0; int bytesLength = 0;
        fixed (char* input = CollectionsMarshal.AsSpan(filtered))
        {
            TextNative.u_strToUTF8(null, 0, &bytesLength, input, filtered.Count, ref status);
            if (status != 15 && status > 0) return Fallback(); status = 0; byte[] bytes = new byte[bytesLength];
            fixed (byte* output = bytes) TextNative.u_strToUTF8(output, bytesLength, null, input, filtered.Count, ref status);
            return status <= 0 ? Encoding.UTF8.GetString(bytes) : Fallback();
        }
    }
}
