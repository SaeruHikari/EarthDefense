from pathlib import Path
p=Path('libraries/SkrGui.Core/Framework/Key.cs');s=p.read_text().replace('namespace SkrGui;','using System.Diagnostics.CodeAnalysis;\nnamespace SkrGui;')
s=s.replace('private string? _string;', 'private Utf8StringView _string;')
s=s.replace('public Key(string value):this(){_type=EKeyType.String;_string=value;}', 'public Key(Utf8StringView value):this(){_type=EKeyType.String;_string=new Utf8StringView(value.Bytes.ToArray());}')
s=s.replace('FromString(string v)','FromString(Utf8StringView v)').replace('SetString(string v)','SetString(Utf8StringView v)')
s=s.replace('public string GetString(){GuiAssert.Require(IsString(),"Key is not String");return _string!;}', '''[UnscopedRef] public ref readonly Utf8StringView GetString(){GuiAssert.Require(IsString(),"Key is not String");return ref _string;}
    // C# names distinguish the source const and mutable reference overloads.
    [UnscopedRef] public ref Utf8StringView GetStringMutable(){GuiAssert.Require(IsString(),"Key is not String");return ref _string;}''')
s=s.replace('_string?.GetHashCode(StringComparison.Ordinal)??0','_string.GetHashCode()')
p.write_text(s)
