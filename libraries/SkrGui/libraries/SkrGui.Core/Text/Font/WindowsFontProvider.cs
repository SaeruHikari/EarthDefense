using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Text;
namespace SkrGui;

// Source: font/system_font_provider_windows.cpp. COM slots verified from Windows SDK; native/dwrite-abi.json.
internal static unsafe class DWriteNative
{
    internal static nint Slot(nint p,int slot)=>(*(nint**)p)[slot];
    internal static uint AddRef(nint p)=>p!=0?((delegate* unmanaged[Stdcall]<nint,uint>)Slot(p,1))(p):0;
    internal static uint Release(nint p)=>p!=0?((delegate* unmanaged[Stdcall]<nint,uint>)Slot(p,2))(p):0;
    internal static int QueryInterface(nint p,in Guid id,out nint result)
    {fixed(Guid* i=&id)return ((delegate* unmanaged[Stdcall]<nint,Guid*,out nint,int>)Slot(p,0))(p,i,out result);}
    internal static readonly Guid Factory2Id=new("0439fc60-ca44-4994-8dee-3a9af7b732ec");
    internal static readonly Guid LocalLoaderId=new("b2d9f3ec-c9fe-4a11-a2ec-d86208f7c0a2");
    [DllImport("dwrite.dll",ExactSpelling=true)] internal static extern int DWriteCreateFactory(int type,in Guid iid,out nint factory);
    [DllImport("kernel32.dll",ExactSpelling=true)] internal static extern int MultiByteToWideChar(uint page,uint flags,byte* text,int textSize,char* output,int count);
    [DllImport("kernel32.dll",ExactSpelling=true)] internal static extern int WideCharToMultiByte(uint page,uint flags,char* text,int textSize,byte* output,int count,nint defaultChar,nint usedDefault);
    [DllImport("kernel32.dll",ExactSpelling=true)] internal static extern int GetLocaleInfoEx(char* locale,uint type,char* output,int count);
}
internal static unsafe class WindowsFontProviderHelper
{
    internal static bool IsValidQuery(Utf8StringView family)=>!family.IsEmpty();
    internal static int ToDwriteWeight(EFontWeight v)=>(int)v;
    internal static int ToDwriteStyle(EFontStyle v)=>v switch{EFontStyle.Italic=>2,EFontStyle.Oblique=>1,_=>0};
    internal static int ToDwriteStretch(EFontStretch v)=>(int)v;
    internal static EFontWeight FromDwriteWeight(int v)=>(EFontWeight)v;
    internal static EFontStyle FromDwriteStyle(int v)=>v switch{2=>EFontStyle.Italic,1=>EFontStyle.Oblique,_=>EFontStyle.Normal};
    internal static EFontStretch FromDwriteStretch(int v)=>(EFontStretch)v;
    internal static bool ToWide(Utf8StringView text,out char[] output)
    {
        output=[];if(text.IsEmpty()||text.Size()>int.MaxValue)return false;
        fixed(byte* p=text.Bytes)
        {
            int count=DWriteNative.MultiByteToWideChar(65001,0,p,(int)text.Size(),null,0);if(count<=0)return false;
            output=new char[count+1];fixed(char* result=output)return DWriteNative.MultiByteToWideChar(65001,0,p,(int)text.Size(),result,count)==count;
        }
    }
    internal static bool ToWide(ReadOnlySpan<uint> codepoints,out char[] output)
    {
        List<char> text=[];
        foreach(uint input in codepoints)
        {
            uint cp=input;if(cp>0x10ffff||(cp>=0xd800&&cp<=0xdfff))cp=0xfffd;
            if(cp<=0xffff)text.Add((char)cp);else{cp-=0x10000;text.Add((char)(0xd800+(cp>>10)));text.Add((char)(0xdc00+(cp&0x3ff)));}
        }
        text.Add('\0');output=text.ToArray();return output.Length>1;
    }
    internal static bool FromWide(char* p,ulong size,out Utf8StringView result)
    {
        result=default;if(p==null||size>int.MaxValue)return false;
        int count=DWriteNative.WideCharToMultiByte(65001,0,p,(int)size,null,0,0,0);if(count<=0)return false;
        byte[] utf8=new byte[count+1];fixed(byte* bytes=utf8)if(DWriteNative.WideCharToMultiByte(65001,0,p,(int)size,bytes,count,0,0)!=count)return false;
        result=new Utf8StringView(utf8.AsMemory(0,count));return true;
    }
    internal static bool GetFilePath(nint face,ref FontProviderFaceSource source)
    {
        uint count=0;
        var getFiles=(delegate* unmanaged[Stdcall]<nint,ref uint,nint*,int>)DWriteNative.Slot(face,4);
        if(getFiles(face,ref count,null)<0||count==0)return false;
        var files=new nint[count];fixed(nint* p=files)if(getFiles(face,ref count,p)<0)return false;
        bool found=false;
        foreach(nint file in files)
        {
            if(file==0)continue;nint loader=0,local=0;
            try
            {
                if(((delegate* unmanaged[Stdcall]<nint,out nint,int>)DWriteNative.Slot(file,4))(file,out loader)>=0&&
                   DWriteNative.QueryInterface(loader,DWriteNative.LocalLoaderId,out local)>=0&&
                   ((delegate* unmanaged[Stdcall]<nint,out nint,out uint,int>)DWriteNative.Slot(file,3))(file,out nint key,out uint keySize)>=0&&
                   ((delegate* unmanaged[Stdcall]<nint,nint,uint,out uint,int>)DWriteNative.Slot(local,4))(local,key,keySize,out uint length)>=0)
                {
                    var buffer=new char[length+1];
                    fixed(char* p=buffer)
                    {
                        if(((delegate* unmanaged[Stdcall]<nint,nint,uint,char*,uint,int>)DWriteNative.Slot(local,5))(local,key,keySize,p,length+1)>=0&&FromWide(p,length,out Utf8StringView path))
                        {source.SourceKey=path;source.FaceIndex=((delegate* unmanaged[Stdcall]<nint,uint>)DWriteNative.Slot(face,5))(face);found=true;}
                    }
                }
            }
            finally{DWriteNative.Release(file);DWriteNative.Release(local);DWriteNative.Release(loader);}
            if(found)break; // Retains the source's first local-file selection and release order.
        }
        return found;
    }
}
internal sealed unsafe class WindowsFallbackAnalysisSource
{
    [StructLayout(LayoutKind.Sequential)] private struct NativeSource {public nint Vtable,Handle;public int References;}
    private static readonly Guid Iunknown=new("00000000-0000-0000-c000-000000000046"),AnalysisId=new("688e1a58-5094-47c8-adc8-fbcea60ae92b");
    private static readonly nint Vtable=CreateVtable();
    private GCHandle _textPin,_localePin;
    internal readonly uint Length;
    internal readonly bool IsRtl;
    internal readonly nint NumberSubstitution;
    internal readonly nint Pointer;
    internal WindowsFallbackAnalysisSource(char[] text,char[] locale,bool rtl,nint number)
    {
        Length=(uint)text.Length-1;IsRtl=rtl;NumberSubstitution=number;DWriteNative.AddRef(number);
        _textPin=GCHandle.Alloc(text,GCHandleType.Pinned);_localePin=GCHandle.Alloc(locale.Length==0?"en-US\0".ToCharArray():locale,GCHandleType.Pinned);
        var source=(NativeSource*)NativeMemory.AllocZeroed((nuint)sizeof(NativeSource));
        source->Vtable=Vtable;source->Handle=GCHandle.ToIntPtr(GCHandle.Alloc(this));source->References=1;Pointer=(nint)source;
    }
    private static WindowsFallbackAnalysisSource Get(nint p)=>(WindowsFallbackAnalysisSource)GCHandle.FromIntPtr(((NativeSource*)p)->Handle).Target!;
    private static nint CreateVtable()
    {
        var table=(nint*)NativeMemory.Alloc((nuint)(8*sizeof(nint)));
        table[0]=(nint)(delegate* unmanaged[Stdcall]<nint,Guid*,nint*,int>)&Query;
        table[1]=(nint)(delegate* unmanaged[Stdcall]<nint,uint>)&Add;
        table[2]=(nint)(delegate* unmanaged[Stdcall]<nint,uint>)&Release;
        table[3]=(nint)(delegate* unmanaged[Stdcall]<nint,uint,char**,uint*,int>)&TextAt;
        table[4]=(nint)(delegate* unmanaged[Stdcall]<nint,uint,char**,uint*,int>)&TextBefore;
        table[5]=(nint)(delegate* unmanaged[Stdcall]<nint,int>)&Direction;
        table[6]=(nint)(delegate* unmanaged[Stdcall]<nint,uint,uint*,char**,int>)&Locale;
        table[7]=(nint)(delegate* unmanaged[Stdcall]<nint,uint,uint*,nint*,int>)&Number;
        return (nint)table;
    }
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvStdcall)])]
    private static int Query(nint p,Guid* id,nint* output)
    {
        if(output==null)return unchecked((int)0x80070057);
        if(*id==Iunknown||*id==AnalysisId){*output=p;Interlocked.Increment(ref ((NativeSource*)p)->References);return 0;}
        *output=0;return unchecked((int)0x80004002);
    }
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvStdcall)])]
    private static uint Add(nint p)=>(uint)Interlocked.Increment(ref ((NativeSource*)p)->References);
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvStdcall)])]
    private static uint Release(nint p)
    {
        uint count=(uint)Interlocked.Decrement(ref ((NativeSource*)p)->References);
        if(count==0){var c=Get(p);DWriteNative.Release(c.NumberSubstitution);c._textPin.Free();c._localePin.Free();GCHandle.FromIntPtr(((NativeSource*)p)->Handle).Free();NativeMemory.Free((void*)p);}return count;
    }
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvStdcall)])]
    private static int TextAt(nint p,uint position,char** text,uint* count)
    {
        if(text==null||count==null)return unchecked((int)0x80070057);var c=Get(p);
        if(position>=c.Length){*text=null;*count=0;return 0;}
        *text=(char*)c._textPin.AddrOfPinnedObject()+position;*count=c.Length-position;return 0;
    }
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvStdcall)])]
    private static int TextBefore(nint p,uint position,char** text,uint* count)
    {
        if(text==null||count==null)return unchecked((int)0x80070057);var c=Get(p);position=Math.Min(position,c.Length);
        if(position==0){*text=null;*count=0;return 0;}*text=(char*)c._textPin.AddrOfPinnedObject();*count=position;return 0;
    }
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvStdcall)])]
    private static int Direction(nint p)=>Get(p).IsRtl?1:0;
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvStdcall)])]
    private static int Locale(nint p,uint position,uint* count,char** locale)
    {
        if(count==null||locale==null)return unchecked((int)0x80070057);var c=Get(p);position=Math.Min(position,c.Length);
        *count=c.Length-position;*locale=(char*)c._localePin.AddrOfPinnedObject();return 0;
    }
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvStdcall)])]
    private static int Number(nint p,uint position,uint* count,nint* value)
    {
        if(count==null||value==null)return unchecked((int)0x80070057);var c=Get(p);position=Math.Min(position,c.Length);
        *count=c.Length-position;*value=c.NumberSubstitution;DWriteNative.AddRef(*value);return 0;
    }
}
internal sealed unsafe class WindowsFontProviderState:IDisposable
{
    internal nint Factory,Collection,Fallback;
    internal bool EnsureFonts()
    {
        if(Factory!=0)return true;
        if(DWriteNative.DWriteCreateFactory(0,DWriteNative.Factory2Id,out Factory)<0||
            ((delegate* unmanaged[Stdcall]<nint,out nint,int,int>)DWriteNative.Slot(Factory,3))(Factory,out Collection,0)<0)
        {DWriteNative.Release(Factory);DWriteNative.Release(Collection);Factory=Collection=0;return false;}
        return true;
    }
    internal bool EnsureFallback()
    {
        if(Fallback!=0)return true;return EnsureFonts()&&((delegate* unmanaged[Stdcall]<nint,out nint,int>)DWriteNative.Slot(Factory,26))(Factory,out Fallback)>=0;
    }
    public void Dispose(){DWriteNative.Release(Fallback);DWriteNative.Release(Collection);DWriteNative.Release(Factory);Fallback=Collection=Factory=0;}
}
internal sealed unsafe class WindowsFontProvider:FontProvider,IDisposable
{
    private WindowsFontProviderState? _state=new();
    public static bool IsSupported()=>OperatingSystem.IsWindows();
    public override bool IsSystemFontSource()=>true;
    public void Dispose(){_state?.Dispose();_state=null;GC.SuppressFinalize(this);}
    ~WindowsFontProvider(){_state?.Dispose();}
    public override bool QueryFace(Utf8StringView family,EFontWeight weight,EFontStyle style,EFontStretch stretch,out FontProviderFaceSource source)
    {
        source=new();if(!IsSupported()||_state==null||!WindowsFontProviderHelper.IsValidQuery(family)||!_state.EnsureFonts()||!WindowsFontProviderHelper.ToWide(family,out var name))return false;
        uint index;int exists;fixed(char* p=name)
        {if(((delegate* unmanaged[Stdcall]<nint,char*,out uint,out int,int>)DWriteNative.Slot(_state.Collection,5))(_state.Collection,p,out index,out exists)<0||exists==0)return false;}
        nint f=0,font=0,face=0;
        try
        {
            if(((delegate* unmanaged[Stdcall]<nint,uint,out nint,int>)DWriteNative.Slot(_state.Collection,4))(_state.Collection,index,out f)<0||
               ((delegate* unmanaged[Stdcall]<nint,int,int,int,out nint,int>)DWriteNative.Slot(f,7))(f,(int)weight,(int)stretch,WindowsFontProviderHelper.ToDwriteStyle(style),out font)<0||
               ((delegate* unmanaged[Stdcall]<nint,out nint,int>)DWriteNative.Slot(font,13))(font,out face)<0)return false;
            if(!WindowsFontProviderHelper.GetFilePath(face,ref source))return false;
            source.Weight=weight;source.Style=style;source.Stretch=stretch;return true;
        }
        finally{DWriteNative.Release(face);DWriteNative.Release(font);DWriteNative.Release(f);}
    }
    internal bool QueryFallback(Utf8StringView baseFamily,ReadOnlySpan<uint> codepoints,Utf8StringView language,EFontWeight weight,EFontStyle style,EFontStretch stretch,out FontProviderFaceSource output)
    {
        output=new();if(!IsSupported()||_state==null||!_state.EnsureFallback()||!WindowsFontProviderHelper.ToWide(codepoints,out var text))return false;
        char[] locale=[],family=[];if(!language.IsEmpty())WindowsFontProviderHelper.ToWide(language,out locale);
        if(!baseFamily.IsEmpty())WindowsFontProviderHelper.ToWide(baseFamily,out family);
        nint number=0,mapped=0,face=0;
        try
        {
            fixed(char* l=locale.Length==0?"en-US\0".ToCharArray():locale)
            {
                uint layout=0;bool rtl=DWriteNative.GetLocaleInfoEx(l,0x20000070,(char*)&layout,2)!=0&&layout==1;
                if(((delegate* unmanaged[Stdcall]<nint,int,char*,int,out nint,int>)DWriteNative.Slot(_state.Factory,22))(_state.Factory,2,l,1,out number)<0)return false;
                var source=new WindowsFallbackAnalysisSource(text,locale,rtl,number);
                uint mappedLength=0;float scale=1;int result;
                fixed(char* f=family)
                {
                    result=((delegate* unmanaged[Stdcall]<nint,nint,uint,uint,nint,char*,int,int,int,out uint,out nint,out float,int>)DWriteNative.Slot(_state.Fallback,3))
                        (_state.Fallback,source.Pointer,0,source.Length,_state.Collection,f,(int)weight,WindowsFontProviderHelper.ToDwriteStyle(style),(int)stretch,out mappedLength,out mapped,out scale);
                }
                DWriteNative.Release(source.Pointer);
                if(result<0||mapped==0||mappedLength<source.Length)return false;
                if(((delegate* unmanaged[Stdcall]<nint,out nint,int>)DWriteNative.Slot(mapped,13))(mapped,out face)<0||!WindowsFontProviderHelper.GetFilePath(face,ref output))return false;
                output.Weight=weight;output.Style=style;output.Stretch=stretch;return output.IsValid();
            }
        }
        finally{DWriteNative.Release(face);DWriteNative.Release(mapped);DWriteNative.Release(number);}
    }
}
