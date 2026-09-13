using System.Runtime.InteropServices;
namespace SkrGui;
internal static unsafe partial class TextNative
{
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "u_ispunct_72")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool u_ispunct(int cp);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "u_isalpha_72")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool u_isalpha(int cp);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "u_isupper_72")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool u_isupper(int cp);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "u_islower_72")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool u_islower(int cp);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "u_isdigit_72")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool u_isdigit(int cp);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "u_toupper_72")]
    internal static extern int u_toupper(int cp);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "u_tolower_72")]
    internal static extern int u_tolower(int cp);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "u_charType_72")]
    internal static extern byte u_charType(int cp);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "u_getCombiningClass_72")]
    internal static extern byte u_getCombiningClass(int cp);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "u_hasBinaryProperty_72")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool u_hasBinaryProperty(int cp, int property);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "u_getIntPropertyValue_72")]
    internal static extern int u_getIntPropertyValue(int cp, int property);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uscript_getScript_72")]
    internal static extern int uscript_getScript(int cp, ref int status);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uscript_getUsage_72")]
    internal static extern int uscript_getUsage(int script);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uloc_isRightToLeft_72")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool uloc_isRightToLeft(byte* locale);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ubrk_open_72")]
    internal static extern nint ubrk_open(int type, byte* locale, char* text, int length, ref int status);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ubrk_close_72")]
    internal static extern void ubrk_close(nint iterator);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ubrk_setUText_72")]
    internal static extern void ubrk_setUText(nint iterator, nint text, ref int status);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ubrk_first_72")]
    internal static extern int ubrk_first(nint iterator);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ubrk_next_72")]
    internal static extern int ubrk_next(nint iterator);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "utext_openUTF8_72")]
    internal static extern nint utext_openUTF8(nint text, byte* data, long length, ref int status);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "utext_close_72")]
    internal static extern nint utext_close(nint text);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "u_strFromUTF8_72")]
    internal static extern char* u_strFromUTF8(char* destination, int capacity, int* length, byte* source, int sourceLength, ref int status);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "u_strToUTF8_72")]
    internal static extern byte* u_strToUTF8(byte* destination, int capacity, int* length, char* source, int sourceLength, ref int status);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "unorm2_getNFKDInstance_72")]
    internal static extern nint unorm2_getNFKDInstance(ref int status);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "unorm2_getNFCInstance_72")]
    internal static extern nint unorm2_getNFCInstance(ref int status);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "unorm2_normalize_72")]
    internal static extern int unorm2_normalize(nint normalizer, char* source, int length, char* destination, int capacity, ref int status);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "unorm2_isNormalized_72")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool unorm2_isNormalized(nint normalizer, char* source, int length, ref int status);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ucasemap_open_72")]
    internal static extern nint ucasemap_open(byte* locale, uint options, ref int status);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ucasemap_close_72")]
    internal static extern void ucasemap_close(nint map);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ucasemap_utf8ToUpper_72")]
    internal static extern int ucasemap_utf8ToUpper(nint map, byte* destination, int capacity, byte* source, int length, ref int status);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ucasemap_utf8ToLower_72")]
    internal static extern int ucasemap_utf8ToLower(nint map, byte* destination, int capacity, byte* source, int length, ref int status);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ucasemap_utf8ToTitle_72")]
    internal static extern int ucasemap_utf8ToTitle(nint map, byte* destination, int capacity, byte* source, int length, ref int status);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uspoof_open_72")]
    internal static extern nint uspoof_open(ref int status);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uspoof_close_72")]
    internal static extern void uspoof_close(nint checker);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uspoof_setChecks_72")]
    internal static extern void uspoof_setChecks(nint checker, int checks, ref int status);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uspoof_getSkeletonUTF8_72")]
    internal static extern int uspoof_getSkeletonUTF8(nint checker, uint type, byte* text, int length, byte* destination, int capacity, ref int status);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uset_openEmpty_72")]
    internal static extern nint uset_openEmpty();
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uset_close_72")]
    internal static extern void uset_close(nint set);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uset_addAll_72")]
    internal static extern void uset_addAll(nint set, nint additional);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uspoof_getRecommendedSet_72")]
    internal static extern nint uspoof_getRecommendedSet(ref int status);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uspoof_getInclusionSet_72")]
    internal static extern nint uspoof_getInclusionSet(ref int status);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uspoof_setAllowedChars_72")]
    internal static extern void uspoof_setAllowedChars(nint checker, nint chars, ref int status);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uspoof_setRestrictionLevel_72")]
    internal static extern void uspoof_setRestrictionLevel(nint checker, int level);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uspoof_check2UTF8_72")]
    internal static extern int uspoof_check2UTF8(nint checker, byte* text, int length, nint result, ref int status);
}
