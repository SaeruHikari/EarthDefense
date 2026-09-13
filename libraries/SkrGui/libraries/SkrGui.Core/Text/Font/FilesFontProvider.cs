using System.Collections;
using System.Runtime.InteropServices;
using System.Text;
namespace SkrGui;

// Source: text/files_font_provider.hpp and font/files_font_provider.cpp.
public sealed class FilesFontProviderFace
{
    public FontProviderFaceSource Source=new();
    public readonly List<Utf8StringView> Families=[];
    public EFontWeight Weight=EFontWeight.Regular;
    public EFontStyle Style=EFontStyle.Normal;
    public EFontStretch Stretch=EFontStretch.Normal;
    public bool IsValid()=>Source.IsValid()&&Families.Count!=0;
    public bool HasFamily(Utf8StringView family){foreach(var f in Families)if(f==family)return true;return false;}
}
// Source const FilesFontProviderFace borrow; no mutable catalog objects escape.
public sealed class ReadOnlyFilesFontProviderFace
{
    private readonly Func<FilesFontProviderFace> _read;
    private readonly LiveReadOnlyList<Utf8StringView> _families;
    internal ReadOnlyFilesFontProviderFace(Func<FilesFontProviderFace> read)
    { _read=read; _families=new(()=>_read().Families); }
    public FontProviderFaceSource Source=>_read().Source;
    public IReadOnlyList<Utf8StringView> Families=>_families;
    public EFontWeight Weight=>_read().Weight;
    public EFontStyle Style=>_read().Style;
    public EFontStretch Stretch=>_read().Stretch;
    public bool IsValid()=>_read().IsValid();
    public bool HasFamily(Utf8StringView family)=>_read().HasFamily(family);
    public FilesFontProviderFace Copy()
    {
        var f=_read(); var result=new FilesFontProviderFace{Source=f.Source,Weight=f.Weight,Style=f.Style,Stretch=f.Stretch};
        foreach(var family in f.Families)result.Families.Add(new Utf8StringView(family.Bytes.ToArray())); return result;
    }
}
internal readonly record struct FilesFontProviderTable(uint Offset,uint Length);
internal static class FilesFontProviderParseHelper
{
    internal static uint MakeTag(char a,char b,char c,char d)=>((uint)(byte)a<<24)|((uint)(byte)b<<16)|((uint)(byte)c<<8)|(byte)d;
    internal static bool IsValidRange(ReadOnlySpan<byte> data,ulong offset,ulong size)=>offset<=(ulong)data.Length&&size<=(ulong)data.Length-offset;
    internal static bool ReadU16(ReadOnlySpan<byte> data,ulong offset,ref ushort value)
    {if(!IsValidRange(data,offset,2))return false;value=(ushort)((data[(int)offset]<<8)|data[(int)offset+1]);return true;}
    internal static bool ReadU32(ReadOnlySpan<byte> data,ulong offset,ref uint value)
    {if(!IsValidRange(data,offset,4))return false;int i=(int)offset;value=((uint)data[i]<<24)|((uint)data[i+1]<<16)|((uint)data[i+2]<<8)|data[i+3];return true;}
    internal static bool FindTable(ReadOnlySpan<byte> data,uint faceOffset,uint tag,ref FilesFontProviderTable table)
    {
        ushort count=0;if(!ReadU16(data,(ulong)faceOffset+4,ref count))return false;
        ulong records=(ulong)faceOffset+12;if(!IsValidRange(data,records,(ulong)count*16))return false;
        for(ushort i=0;i<count;i++)
        {
            ulong record=records+(ulong)i*16;
            uint current=0;if(!ReadU32(data,record,ref current)||current!=tag)continue;
            uint offset=0,length=0;
            if(!ReadU32(data,record+8,ref offset)||!ReadU32(data,record+12,ref length)||!IsValidRange(data,offset,length))return false;
            table=new(offset,length);return true;
        }
        return false;
    }
    internal static bool DecodeUtf16BeName(ReadOnlySpan<byte> data,ulong offset,ushort size,out Utf8StringView value)
    {
        value="";if(size%2!=0||!IsValidRange(data,offset,size))return false;
        char[] chars=new char[size/2];for(int i=0;i<chars.Length;i++){int p=(int)offset+i*2;chars[i]=(char)((data[p]<<8)|data[p+1]);}
        value=new string(chars);return !value.IsEmpty();
    }
    internal static bool DecodeAsciiName(ReadOnlySpan<byte> data,ulong offset,ushort size,out Utf8StringView value)
    {
        value="";if(!IsValidRange(data,offset,size))return false;
        for(int i=0;i<size;i++){byte c=data[(int)offset+i];if(c<0x20||c>0x7e)return false;}
        value=new Utf8StringView(data.Slice((int)offset,size).ToArray());return !value.IsEmpty();
    }
    internal static bool DecodeNameRecord(ReadOnlySpan<byte> data,uint tableOffset,ushort stringOffset,ulong record,ref Utf8StringView value)
    {
        ushort platform=0,length=0,offset=0;
        if(!ReadU16(data,record,ref platform)||!ReadU16(data,record+8,ref length)||!ReadU16(data,record+10,ref offset))return false;
        ulong start=(ulong)tableOffset+stringOffset+offset;
        if(platform is 0 or 3)return DecodeUtf16BeName(data,start,length,out value);
        return platform==1&&DecodeAsciiName(data,start,length,out value);
    }
    internal static void AddUniqueFamily(List<Utf8StringView> families,Utf8StringView family)
    {if(family.IsEmpty())return;foreach(var f in families)if(f==family)return;families.Add(new Utf8StringView(family.Bytes.ToArray()));}
    internal static bool ParseNameTable(ReadOnlySpan<byte> data,FilesFontProviderTable table,List<Utf8StringView> families)
    {
        ushort count=0,stringOffset=0;
        if(!ReadU16(data,(ulong)table.Offset+2,ref count)||!ReadU16(data,(ulong)table.Offset+4,ref stringOffset))return false;
        ulong records=(ulong)table.Offset+6;if(!IsValidRange(data,records,(ulong)count*12))return false;
        for(ushort pass=0;pass<2;pass++)
        {
            ushort target=(ushort)(pass==0?16:1);
            for(ushort i=0;i<count;i++)
            {
                ulong record=records+(ulong)i*12;ushort name=0;if(!ReadU16(data,record+6,ref name)||name!=target)continue;
                Utf8StringView family="";if(DecodeNameRecord(data,table.Offset,stringOffset,record,ref family))AddUniqueFamily(families,family);
            }
        }
        return families.Count!=0;
    }
    internal static EFontWeight NormalizeWeight(ushort value)=>value<1?EFontWeight.Regular:value>1000?(EFontWeight)1000:(EFontWeight)value;
    internal static EFontStretch NormalizeStretch(ushort value)=>value<1||value>9?EFontStretch.Normal:(EFontStretch)value;
    internal static EFontStyle StyleFromSelection(ushort value)=>(value&1)!=0?EFontStyle.Italic:(value&(1<<9))!=0?EFontStyle.Oblique:EFontStyle.Normal;
    internal static EFontStyle StyleFromMacStyle(ushort value)=>(value&2)!=0?EFontStyle.Italic:EFontStyle.Normal;
    internal static EFontWeight WeightFromMacStyle(ushort value)=>(value&1)!=0?EFontWeight.Bold:EFontWeight.Regular;
    internal static void ParseMetrics(ReadOnlySpan<byte> data,uint offset,FilesFontProviderFace face)
    {
        FilesFontProviderTable head=default;ushort mac=0;
        if(FindTable(data,offset,MakeTag('h','e','a','d'),ref head)&&IsValidRange(data,(ulong)head.Offset+44,2)&&ReadU16(data,(ulong)head.Offset+44,ref mac))
        {face.Weight=WeightFromMacStyle(mac);face.Style=StyleFromMacStyle(mac);}
        FilesFontProviderTable os2=default;
        if(FindTable(data,offset,MakeTag('O','S','/','2'),ref os2)&&IsValidRange(data,os2.Offset,64))
        {
            ushort weight=0,stretch=0,selection=0;
            if(ReadU16(data,(ulong)os2.Offset+4,ref weight))face.Weight=NormalizeWeight(weight);
            if(ReadU16(data,(ulong)os2.Offset+6,ref stretch))face.Stretch=NormalizeStretch(stretch);
            if(ReadU16(data,(ulong)os2.Offset+62,ref selection))face.Style=StyleFromSelection(selection);
        }
    }
    internal static bool ParseFace(Utf8StringView path,ReadOnlySpan<byte> data,uint offset,uint index,ref FilesFontProviderFace face)
    {
        FilesFontProviderTable name=default;if(!FindTable(data,offset,MakeTag('n','a','m','e'),ref name))return false;
        face=new();
        face.Source=new(){SourceKey=new Utf8StringView(path.Bytes.ToArray()),FaceIndex=index};ParseMetrics(data,offset,face);
        face.Source.Weight=face.Weight;face.Source.Style=face.Style;face.Source.Stretch=face.Stretch;
        return ParseNameTable(data,name,face.Families)&&face.IsValid();
    }
    internal static bool ParseFile(Utf8StringView path,ReadOnlySpan<byte> data,List<FilesFontProviderFace> faces)
    {
        faces.Clear();uint tag=0;if(!ReadU32(data,0,ref tag))return false;
        if(tag==MakeTag('t','t','c','f'))
        {
            uint count=0;if(!ReadU32(data,8,ref count)||count==0||!IsValidRange(data,12,(ulong)count*4))return false;
            for(uint i=0;i<count;i++)
            {
                uint offset=0;if(!ReadU32(data,12+(ulong)i*4,ref offset))continue;
                FilesFontProviderFace face=new();if(ParseFace(path,data,offset,i,ref face))faces.Add(face);
            }
        }
        else {FilesFontProviderFace face=new();if(ParseFace(path,data,0,0,ref face))faces.Add(face);}
        return faces.Count!=0;
    }
    internal static int FontWeightValue(EFontWeight v)=>(int)v;
    internal static int FontStretchValue(EFontStretch v)=>(int)v;
    internal static int StyleDistance(EFontStyle a,EFontStyle b)=>a==b?0:100000;
    internal static int MatchScore(FilesFontProviderFace face,EFontWeight weight,EFontStyle style,EFontStretch stretch)=>
        StyleDistance(face.Style,style)+Math.Abs(FontStretchValue(face.Stretch)-FontStretchValue(stretch))*1000+Math.Abs(FontWeightValue(face.Weight)-FontWeightValue(weight));
    internal static bool IsFallback(FilesFontProviderFace face,EFontWeight weight,EFontStyle style,EFontStretch stretch)=>face.Weight!=weight||face.Style!=style||face.Stretch!=stretch;
}
public sealed class FilesFontProvider:FontProvider
{
    private readonly List<FilesFontProviderFace> _fontFaces=[];
    private readonly List<Utf8StringView> _fontPaths=[];
    internal static Utf8StringView FilesFontSourceKey(Utf8StringView path)
    {
        try
        {
            var f=new FileInfo(path.ToString());if(!f.Exists||(f.Attributes&FileAttributes.Directory)!=0)return "";
            // SkrOS Windows FileTime is the raw FILETIME count (100 ns since 1601).
            long stamp=f.LastWriteTimeUtc.ToFileTimeUtc();
            byte[] suffix=Encoding.ASCII.GetBytes(FormattableString.Invariant($"#skr-font:{stamp}:{f.Length}"));
            byte[] key=new byte[path.Bytes.Length+suffix.Length];path.Bytes.CopyTo(key);suffix.CopyTo(key,path.Bytes.Length);
            return new Utf8StringView(key);
        }
        catch(IOException){return "";}catch(UnauthorizedAccessException){return "";}catch(ArgumentException){return "";}
    }
    public bool AddFontFile(Utf8StringView path)
    {
        List<byte> bytes=[];if(!LoadFaceData(new(){SourceKey=path},bytes)||bytes.Count==0)return false;
        List<FilesFontProviderFace> parsed=[];if(!FilesFontProviderParseHelper.ParseFile(path,CollectionsMarshal.AsSpan(bytes),parsed))return false;
        foreach(var face in parsed){_fontFaces.Add(face);_fontPaths.Add(new Utf8StringView(path.Bytes.ToArray()));}return true;
    }
    public ulong AddFontFiles(ReadOnlySpan<Utf8StringView> paths)
    {ulong count=0;foreach(var path in paths)if(AddFontFile(path))count++;return count;}
    public void ClearFontFiles(){_fontFaces.Clear();_fontPaths.Clear();}
    private sealed class CatalogView(List<FilesFontProviderFace> faces):IReadOnlyList<ReadOnlyFilesFontProviderFace>
    {
        public int Count=>faces.Count;
        public ReadOnlyFilesFontProviderFace this[int index] {get {var face=faces[index];return new(()=>face);}}
        public IEnumerator<ReadOnlyFilesFontProviderFace> GetEnumerator()
        {foreach(var face in faces)yield return new(()=>face);}
        IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
    }
    public IReadOnlyList<ReadOnlyFilesFontProviderFace> FontFaces()=>new CatalogView(_fontFaces);
    public override bool QueryFace(Utf8StringView family,EFontWeight weight,EFontStyle style,EFontStretch stretch,out FontProviderFaceSource source)
    {
        source=new();if(family.IsEmpty())return false;FilesFontProviderFace? best=null;int score=int.MaxValue,index=-1;
        for(int i=0;i<_fontFaces.Count;i++)
        {
            var f=_fontFaces[i];if(!f.HasFamily(family))continue;int candidate=FilesFontProviderParseHelper.MatchScore(f,weight,style,stretch);
            if(best==null||candidate<score){best=f;score=candidate;index=i;}
        }
        if(best==null)return false;source=best.Source;source.SourceKey=FilesFontSourceKey(_fontPaths[index]);source.Weight=weight;source.Style=style;source.Stretch=stretch;
        return source.IsValid();
    }
    public override bool LoadFaceData(FontProviderFaceSource source,List<byte> bytes)
    {
        bytes.Clear();
        foreach(Utf8StringView path in _fontPaths)if(path==source.SourceKey||FilesFontSourceKey(path)==source.SourceKey)return ReadAll(path,bytes);
        return ReadAll(source.SourceKey,bytes);
    }
    private static bool ReadAll(Utf8StringView path,List<byte> bytes)
    {try{bytes.AddRange(File.ReadAllBytes(path.ToString()));return true;}catch(IOException){return false;}catch(UnauthorizedAccessException){return false;}catch(ArgumentException){return false;}}
}
