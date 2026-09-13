import pathlib,re
root=pathlib.Path(__file__).resolve().parents[1]
p=root/'libraries/SkrGui.Core/Text/FontProvider.cs';s=p.read_text().replace('public string SourceKey = "";','public Utf8StringView SourceKey;').replace('!string.IsNullOrEmpty(SourceKey)','!SourceKey.IsEmpty()').replace('QueryFace(string family,','QueryFace(Utf8StringView family,').replace('File.ReadAllBytes(source.SourceKey)','File.ReadAllBytes(source.SourceKey.ToString())');p.write_text(s)
for name in ['WindowsFontProvider','MacFontProvider']:
 p=root/f'libraries/SkrGui.Core/Text/Font/{name}.cs';s=p.read_text().replace('IsValidQuery(string family)=>family.Length!=0','IsValidQuery(Utf8StringView family)=>!family.IsEmpty()').replace('QueryFace(string family,','QueryFace(Utf8StringView family,')
 if name=='WindowsFontProvider':
  s=s.replace('out string result','out Utf8StringView result').replace('result="";if(p==null','result=default;if(p==null').replace('result=Encoding.UTF8.GetString(utf8,0,count)','result=new Utf8StringView(utf8.AsMemory(0,count))').replace('FromWide(p,length,out string path)','FromWide(p,length,out Utf8StringView path)')
 else:
  s=s.replace('source.SourceKey=Marshal.PtrToStringUTF8((nint)buffer)??"";','int pathLength=0;while(pathLength<1024&&buffer[pathLength]!=0)pathLength++;source.SourceKey=new Utf8StringView(new ReadOnlySpan<byte>(buffer,pathLength).ToArray());')
 p.write_text(s)
# Test/sample providers share this signature. Do not modify unrelated text runtime files.
for base in ['tests','libraries/SkrGui.Godot/Gallery']:
 for p in (root/base).rglob('*.cs'):
  if any(x in p.parts for x in ['bin','obj']):continue
  s=p.read_text(encoding='utf-8-sig')
  updated=s.replace('QueryFace(string family,','QueryFace(Utf8StringView family,')
  if updated!=s:p.write_text(updated,encoding='utf-8')
p=root/'tests/SkrGui.Core.Tests/Text/TestFontAssets.cs';s=p.read_text();s=s.replace('public string Family="",SourceKey="";','public Utf8StringView Family,SourceKey;');p.write_text(s)
p=root/'tests/SkrGui.Core.Tests/Text/TextContractFixture.cs';s=p.read_text().replace('MakeStyle(string family,','MakeStyle(Utf8StringView family,');p.write_text(s)
