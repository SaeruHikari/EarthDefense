from pathlib import Path
p=Path('libraries/SkrGui.Core/Text/Font/FilesFontProvider.cs')
s=p.read_text()
s=s.replace('using System.Runtime.InteropServices;', 'using System.Collections;\nusing System.Runtime.InteropServices;')
s=s.replace('internal readonly record struct FilesFontProviderTable', '''// Source const FilesFontProviderFace borrow; no mutable catalog objects escape.
public sealed class ReadOnlyFilesFontProviderFace
{
    private readonly Func<FilesFontProviderFace> _read;
    private readonly LiveReadOnlyList<string> _families;
    internal ReadOnlyFilesFontProviderFace(Func<FilesFontProviderFace> read)
    { _read=read; _families=new(()=>_read().Families); }
    public FontProviderFaceSource Source=>_read().Source;
    public IReadOnlyList<string> Families=>_families;
    public EFontWeight Weight=>_read().Weight;
    public EFontStyle Style=>_read().Style;
    public EFontStretch Stretch=>_read().Stretch;
    public bool IsValid()=>_read().IsValid();
    public bool HasFamily(string family)=>_read().HasFamily(family);
    public FilesFontProviderFace Copy()
    {
        var f=_read(); var result=new FilesFontProviderFace{Source=f.Source,Weight=f.Weight,Style=f.Style,Stretch=f.Stretch};
        result.Families.AddRange(f.Families); return result;
    }
}
internal readonly record struct FilesFontProviderTable''')
s=s.replace('out ushort value','ref ushort value').replace('{value=0;if(!IsValidRange(data,offset,2))','{if(!IsValidRange(data,offset,2))')
s=s.replace('out uint value','ref uint value').replace('{value=0;if(!IsValidRange(data,offset,4))','{if(!IsValidRange(data,offset,4))')
s=s.replace('out FilesFontProviderTable table','ref FilesFontProviderTable table')
s=s.replace('table=default;if(!ReadU16(data,(ulong)faceOffset+4,out ushort count))', 'ushort count=0;if(!ReadU16(data,(ulong)faceOffset+4,ref count))')
s=s.replace('if(!ReadU32(data,record,out uint current)||current!=tag)', 'uint current=0;if(!ReadU32(data,record,ref current)||current!=tag)')
s=s.replace('if(!ReadU32(data,record+8,out uint offset)||!ReadU32(data,record+12,out uint length)', 'uint offset=0,length=0;\n            if(!ReadU32(data,record+8,ref offset)||!ReadU32(data,record+12,ref length)')
s=s.replace('ulong record,out string value)', 'ulong record,ref string value)')
s=s.replace('value="";if(!ReadU16(data,record,out ushort platform)||!ReadU16(data,record+8,out ushort length)||!ReadU16(data,record+10,out ushort offset))', 'ushort platform=0,length=0,offset=0;\n        if(!ReadU16(data,record,ref platform)||!ReadU16(data,record+8,ref length)||!ReadU16(data,record+10,ref offset))')
s=s.replace('if(!ReadU16(data,(ulong)table.Offset+2,out ushort count)||!ReadU16(data,(ulong)table.Offset+4,out ushort stringOffset))', 'ushort count=0,stringOffset=0;\n        if(!ReadU16(data,(ulong)table.Offset+2,ref count)||!ReadU16(data,(ulong)table.Offset+4,ref stringOffset))')
s=s.replace('ulong record=records+(ulong)i*12;if(!ReadU16(data,record+6,out ushort name)||name!=target)', 'ulong record=records+(ulong)i*12;ushort name=0;if(!ReadU16(data,record+6,ref name)||name!=target)')
s=s.replace('if(DecodeNameRecord(data,table.Offset,stringOffset,record,out string family))', 'string family="";if(DecodeNameRecord(data,table.Offset,stringOffset,record,ref family))')
s=s.replace("if(FindTable(data,offset,MakeTag('h','e','a','d'),out var head)&&IsValidRange(data,(ulong)head.Offset+44,2)&&ReadU16(data,(ulong)head.Offset+44,out ushort mac))", "FilesFontProviderTable head=default;ushort mac=0;\n        if(FindTable(data,offset,MakeTag('h','e','a','d'),ref head)&&IsValidRange(data,(ulong)head.Offset+44,2)&&ReadU16(data,(ulong)head.Offset+44,ref mac))")
s=s.replace("if(FindTable(data,offset,MakeTag('O','S','/','2'),out var os2)", "FilesFontProviderTable os2=default;\n        if(FindTable(data,offset,MakeTag('O','S','/','2'),ref os2)")
s=s.replace('if(ReadU16(data,(ulong)os2.Offset+4,out ushort weight))', 'ushort weight=0,stretch=0,selection=0;\n            if(ReadU16(data,(ulong)os2.Offset+4,ref weight))')
s=s.replace('out ushort stretch)', 'ref stretch)').replace('out ushort selection)', 'ref selection)')
s=s.replace('out FilesFontProviderFace face)', 'ref FilesFontProviderFace face)')
s=s.replace("face=new();if(!FindTable(data,offset,MakeTag('n','a','m','e'),out var name))return false;", "FilesFontProviderTable name=default;if(!FindTable(data,offset,MakeTag('n','a','m','e'),ref name))return false;\n        face=new();")
s=s.replace('faces.Clear();if(!ReadU32(data,0,out uint tag))', 'faces.Clear();uint tag=0;if(!ReadU32(data,0,ref tag))')
s=s.replace('if(!ReadU32(data,8,out uint count)', 'uint count=0;if(!ReadU32(data,8,ref count)')
s=s.replace('if(!ReadU32(data,12+(ulong)i*4,out uint offset))', 'uint offset=0;if(!ReadU32(data,12+(ulong)i*4,ref offset))')
s=s.replace('if(ParseFace(path,data,offset,i,out var face))faces.Add(face);', 'FilesFontProviderFace face=new();if(ParseFace(path,data,offset,i,ref face))faces.Add(face);')
s=s.replace('else if(ParseFace(path,data,0,0,out var face))faces.Add(face);', 'else {FilesFontProviderFace face=new();if(ParseFace(path,data,0,0,ref face))faces.Add(face);}')
s=s.replace('public IReadOnlyList<FilesFontProviderFace> FontFaces()=>_fontFaces;', '''private sealed class CatalogView(List<FilesFontProviderFace> faces):IReadOnlyList<ReadOnlyFilesFontProviderFace>
    {
        public int Count=>faces.Count;
        public ReadOnlyFilesFontProviderFace this[int index] {get {var face=faces[index];return new(()=>face);}}
        public IEnumerator<ReadOnlyFilesFontProviderFace> GetEnumerator()
        {foreach(var face in faces)yield return new(()=>face);}
        IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
    }
    public IReadOnlyList<ReadOnlyFilesFontProviderFace> FontFaces()=>new CatalogView(_fontFaces);''')
p.write_text(s)
