using System.Runtime.InteropServices;
namespace SkrGui;

// Source: font/system_font_provider_mac.cpp. CFNumber ABI checked against Apple's CFNumber.h.
internal static unsafe class CoreTextNative
{
    private const string CF="/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const string CT="/System/Library/Frameworks/CoreText.framework/CoreText";
    private static nint _cf,_ct;
    internal static nint CfExport(string name)=>NativeLibrary.GetExport(_cf!=0?_cf:(_cf=NativeLibrary.Load(CF)),name);
    internal static nint CtConstant(string name)=>*(nint*)NativeLibrary.GetExport(_ct!=0?_ct:(_ct=NativeLibrary.Load(CT)),name);
    [StructLayout(LayoutKind.Sequential)] internal readonly struct CFRange(nint location,nint length){public readonly nint Location=location,Length=length;}
    [DllImport(CF)] internal static extern void CFRelease(nint value);
    [DllImport(CF)] internal static extern byte CFEqual(nint a,nint b);
    [DllImport(CF)] internal static extern nint CFStringCreateWithBytes(nint allocator,byte* bytes,nint count,uint encoding,byte external);
    [DllImport(CF)] internal static extern nint CFStringCreateWithCharacters(nint allocator,char* chars,nint count);
    [DllImport(CF)] internal static extern nint CFStringGetLength(nint value);
    [DllImport(CF)] internal static extern nint CFNumberCreate(nint allocator,nint type,void* value);
    [DllImport(CF)] internal static extern byte CFNumberGetValue(nint number,nint type,void* value);
    [DllImport(CF)] internal static extern nint CFArrayGetCount(nint value);
    [DllImport(CF)] internal static extern nint CFArrayGetValueAtIndex(nint value,nint index);
    [DllImport(CF)] internal static extern byte CFURLGetFileSystemRepresentation(nint url,byte resolve,byte* buffer,nint count);
    [DllImport(CF)] internal static extern nint CFDictionaryCreateMutable(nint allocator,nint capacity,nint keyCallbacks,nint valueCallbacks);
    [DllImport(CF)] internal static extern nint CFDictionaryGetValue(nint dictionary,nint key);
    [DllImport(CF)] internal static extern void CFDictionarySetValue(nint dictionary,nint key,nint value);
    [DllImport(CT)] internal static extern nint CTFontDescriptorCopyAttribute(nint descriptor,nint attribute);
    [DllImport(CT)] internal static extern nint CTFontManagerCreateFontDescriptorsFromURL(nint url);
    [DllImport(CT)] internal static extern nint CTFontDescriptorCreateWithAttributes(nint attributes);
    [DllImport(CT)] internal static extern nint CTFontDescriptorCreateMatchingFontDescriptor(nint descriptor,nint mandatoryAttributes);
    [DllImport(CT)] internal static extern nint CTFontCreateWithName(nint name,double size,nint matrix);
    [DllImport(CT)] internal static extern nint CTFontCreateUIFontForLanguage(uint type,double size,nint language);
    [DllImport(CT)] internal static extern nint CTFontCreateForString(nint font,nint text,CFRange range);
    [DllImport(CT)] internal static extern nint CTFontCopyFontDescriptor(nint font);
}
internal readonly struct FontProviderCFRef(nint value):IDisposable
{
    internal readonly nint Value=value;
    public void Dispose(){if(Value!=0)CoreTextNative.CFRelease(Value);}
    public static implicit operator nint(FontProviderCFRef value)=>value.Value;
    internal bool IsValid()=>Value!=0;
}
internal static unsafe class MacFontProviderHelper
{
    internal static bool IsValidQuery(Utf8StringView family)=>!family.IsEmpty();
    internal static double ToCoreTextWeight(EFontWeight weight)=>Math.Clamp(((int)weight-400.0)/500.0,-1,1);
    internal static double ToCoreTextWidth(EFontStretch stretch)=>Math.Clamp(((int)stretch-5.0)/4.0,-1,1);
    internal static uint ToCoreTextSymbolicTraits(EFontStyle style)=>style is EFontStyle.Italic or EFontStyle.Oblique?1u:0;
    internal static EFontWeight FromCoreTextWeight(double weight)=>(EFontWeight)Math.Clamp((int)Math.Round(weight*500+400,MidpointRounding.AwayFromZero),1,1000);
    internal static EFontStretch FromCoreTextWidth(double width)=>(EFontStretch)Math.Clamp((int)Math.Round(width*4+5,MidpointRounding.AwayFromZero),1,9);
    internal static EFontStyle FromCoreTextSymbolicTraits(uint traits)=>(traits&1)!=0?EFontStyle.Italic:EFontStyle.Normal;
    internal static nint CreateString(Utf8StringView text)
    {if(text.IsEmpty())return 0;fixed(byte* p=text.Bytes)return CoreTextNative.CFStringCreateWithBytes(0,p,(nint)text.Size(),0x08000100,0);}
    internal static nint CreateString(ReadOnlySpan<uint> codepoints)
    {
        List<char> chars=[];foreach(uint input in codepoints)
        {
            uint cp=input;if(cp>0x10ffff||(cp>=0xd800&&cp<=0xdfff))cp=0xfffd;
            if(cp<=0xffff)chars.Add((char)cp);else{cp-=0x10000;chars.Add((char)(0xd800+(cp>>10)));chars.Add((char)(0xdc00+(cp&0x3ff)));}
        }
        if(chars.Count==0)return 0;fixed(char* p=CollectionsMarshal.AsSpan(chars))return CoreTextNative.CFStringCreateWithCharacters(0,p,chars.Count);
    }
    internal static nint CreateNumber(double value)=>CoreTextNative.CFNumberCreate(0,13,&value);
    internal static nint CreateNumber(uint value)=>CoreTextNative.CFNumberCreate(0,3,&value);
    internal static uint GetFontFaceIndex(nint url,nint descriptor)
    {
        using var name=new FontProviderCFRef(CoreTextNative.CTFontDescriptorCopyAttribute(descriptor,CoreTextNative.CtConstant("kCTFontNameAttribute")));
        if(!name.IsValid())return 0;
        using var descriptors=new FontProviderCFRef(CoreTextNative.CTFontManagerCreateFontDescriptorsFromURL(url));if(!descriptors.IsValid())return 0;
        nint count=CoreTextNative.CFArrayGetCount(descriptors);
        for(nint i=0;i<count;i++)
        {
            nint candidate=CoreTextNative.CFArrayGetValueAtIndex(descriptors,i);if(candidate==0)continue;
            using var candidateName=new FontProviderCFRef(CoreTextNative.CTFontDescriptorCopyAttribute(candidate,CoreTextNative.CtConstant("kCTFontNameAttribute")));
            if(candidateName.IsValid()&&CoreTextNative.CFEqual(name,candidateName)!=0)return (uint)i;
        }
        return 0;
    }
    internal static bool GetFontSource(nint descriptor,ref FontProviderFaceSource source)
    {
        using var url=new FontProviderCFRef(CoreTextNative.CTFontDescriptorCopyAttribute(descriptor,CoreTextNative.CtConstant("kCTFontURLAttribute")));
        if(!url.IsValid())return false;
        byte* buffer=stackalloc byte[1024];new Span<byte>(buffer,1024).Clear();
        if(CoreTextNative.CFURLGetFileSystemRepresentation(url,1,buffer,1024)==0)return false;
        int pathLength=0;while(pathLength<1024&&buffer[pathLength]!=0)pathLength++;source.SourceKey=new Utf8StringView(new ReadOnlySpan<byte>(buffer,pathLength).ToArray());source.FaceIndex=GetFontFaceIndex(url,descriptor);return true;
    }
    internal static bool CopyTraitNumber(nint traits,nint name,ref double output)
    {if(traits==0)return false;nint value=CoreTextNative.CFDictionaryGetValue(traits,name);if(value==0)return false;fixed(double* p=&output)return CoreTextNative.CFNumberGetValue(value,13,p)!=0;}
    internal static bool CopyTraitNumber(nint traits,nint name,ref uint output)
    {if(traits==0)return false;nint value=CoreTextNative.CFDictionaryGetValue(traits,name);if(value==0)return false;fixed(uint* p=&output)return CoreTextNative.CFNumberGetValue(value,3,p)!=0;}
    internal static void GetFontTraits(nint descriptor,ref FontProviderFaceSource source)
    {
        using var traits=new FontProviderCFRef(CoreTextNative.CTFontDescriptorCopyAttribute(descriptor,CoreTextNative.CtConstant("kCTFontTraitsAttribute")));if(!traits.IsValid())return;
        double weight=0,width=0;uint symbolic=0;
        if(CopyTraitNumber(traits,CoreTextNative.CtConstant("kCTFontWeightTrait"),ref weight))source.Weight=FromCoreTextWeight(weight);
        if(CopyTraitNumber(traits,CoreTextNative.CtConstant("kCTFontWidthTrait"),ref width))source.Stretch=FromCoreTextWidth(width);
        if(CopyTraitNumber(traits,CoreTextNative.CtConstant("kCTFontSymbolicTrait"),ref symbolic))source.Style=FromCoreTextSymbolicTraits(symbolic);
    }
}
internal sealed class MacFontProvider:FontProvider
{
    public static bool IsSupported()=>OperatingSystem.IsMacOS()||OperatingSystem.IsIOS()||OperatingSystem.IsMacCatalyst();
    public override bool IsSystemFontSource()=>true;
    public override bool QueryFace(Utf8StringView family,EFontWeight weight,EFontStyle style,EFontStretch stretch,out FontProviderFaceSource source)
    {
        source=new();if(!IsSupported()||!MacFontProviderHelper.IsValidQuery(family))return false;
        using var familyName=new FontProviderCFRef(MacFontProviderHelper.CreateString(family));
        nint keys=CoreTextNative.CfExport("kCFTypeDictionaryKeyCallBacks"),values=CoreTextNative.CfExport("kCFTypeDictionaryValueCallBacks");
        using var attributes=new FontProviderCFRef(CoreTextNative.CFDictionaryCreateMutable(0,0,keys,values));
        using var traits=new FontProviderCFRef(CoreTextNative.CFDictionaryCreateMutable(0,0,keys,values));
        if(!familyName.IsValid()||!attributes.IsValid()||!traits.IsValid())return false;
        using var w=new FontProviderCFRef(MacFontProviderHelper.CreateNumber(MacFontProviderHelper.ToCoreTextWeight(weight)));
        using var width=new FontProviderCFRef(MacFontProviderHelper.CreateNumber(MacFontProviderHelper.ToCoreTextWidth(stretch)));
        using var symbolic=new FontProviderCFRef(MacFontProviderHelper.CreateNumber(MacFontProviderHelper.ToCoreTextSymbolicTraits(style)));
        if(!w.IsValid()||!width.IsValid()||!symbolic.IsValid())return false;
        CoreTextNative.CFDictionarySetValue(attributes,CoreTextNative.CtConstant("kCTFontFamilyNameAttribute"),familyName);
        CoreTextNative.CFDictionarySetValue(traits,CoreTextNative.CtConstant("kCTFontWeightTrait"),w);
        CoreTextNative.CFDictionarySetValue(traits,CoreTextNative.CtConstant("kCTFontWidthTrait"),width);
        CoreTextNative.CFDictionarySetValue(traits,CoreTextNative.CtConstant("kCTFontSymbolicTrait"),symbolic);
        CoreTextNative.CFDictionarySetValue(attributes,CoreTextNative.CtConstant("kCTFontTraitsAttribute"),traits);
        using var query=new FontProviderCFRef(CoreTextNative.CTFontDescriptorCreateWithAttributes(attributes));if(!query.IsValid())return false;
        using var matched=new FontProviderCFRef(CoreTextNative.CTFontDescriptorCreateMatchingFontDescriptor(query,0));if(!matched.IsValid())return false;
        if(!MacFontProviderHelper.GetFontSource(matched,ref source))return false;
        source.Weight=weight;source.Style=style;source.Stretch=stretch;return true;
    }
    internal bool QueryFallback(Utf8StringView baseFamily,ReadOnlySpan<uint> codepoints,Utf8StringView language,EFontWeight weight,EFontStyle style,EFontStretch stretch,out FontProviderFaceSource source)
    {
        source=new();if(!IsSupported())return false;
        using var text=new FontProviderCFRef(MacFontProviderHelper.CreateString(codepoints));if(!text.IsValid())return false;
        using var family=new FontProviderCFRef(MacFontProviderHelper.CreateString(baseFamily));
        using var locale=new FontProviderCFRef(MacFontProviderHelper.CreateString(language));
        using var baseFont=new FontProviderCFRef(family.IsValid()?CoreTextNative.CTFontCreateWithName(family,16,0):CoreTextNative.CTFontCreateUIFontForLanguage(2,16,locale));
        if(!baseFont.IsValid())return false;
        using var fallback=new FontProviderCFRef(CoreTextNative.CTFontCreateForString(baseFont,text,new(0,CoreTextNative.CFStringGetLength(text))));
        if(!fallback.IsValid())return false;
        using var descriptor=new FontProviderCFRef(CoreTextNative.CTFontCopyFontDescriptor(fallback));
        if(!descriptor.IsValid()||!MacFontProviderHelper.GetFontSource(descriptor,ref source))return false;
        source.Weight=weight;source.Style=style;source.Stretch=stretch;return source.IsValid();
    }
}
public abstract partial class FontProvider
{
    public static FontProvider? CreateSystem()=>WindowsFontProvider.IsSupported()?new WindowsFontProvider():MacFontProvider.IsSupported()?new MacFontProvider():null;
}
internal partial class TextServicesImpl
{
    internal static bool TryQuerySystemFallback(FontProvider provider,Utf8StringView family,ReadOnlySpan<uint> codepoints,Utf8StringView language,EFontWeight weight,EFontStyle style,EFontStretch stretch,out FontProviderFaceSource source)
    {
        if(provider is WindowsFontProvider windows)return windows.QueryFallback(family,codepoints,language,weight,style,stretch,out source);
        if(provider is MacFontProvider mac)return mac.QueryFallback(family,codepoints,language,weight,style,stretch,out source);
        source=new();return false;
    }
}
