import pathlib,json
root=pathlib.Path(__file__).resolve().parents[1]
data=json.loads((root/'migration/source-type-guids.json').read_text())
core=[r for r in data['types'] if '/core/' in r['path']]
code='''namespace SkrGui;
// Original SKR_RTTR type GUIDs, retained verbatim. CLR Type provides dynamic type/cast semantics.
public static class SourceTypeRegistry
{
 public const string SourceCommit = "611561f81534354c8c1618dc27c4782cd9277cbf";
 private static readonly IReadOnlyDictionary<Type,Guid> TypeGuids = new Dictionary<Type,Guid>
 {
'''
for r in core:code+=f'  [typeof({r["type"]})] = new("{r["guid"]}"),\n'
code+=''' };
 public static IReadOnlyDictionary<Type,Guid> Types => TypeGuids;
 public static bool TryGetGuid(Type type,out Guid id)=>TypeGuids.TryGetValue(type,out id);
 public static Guid GetGuid(Type type)=>TypeGuids.TryGetValue(type,out var id)?id:throw new ArgumentException("Type is not registered by the source GUI module.",nameof(type));
}
'''
(root/'libraries/SkrGui.Core/SourceTypeRegistry.cs').write_text(code)
sample=[r for r in data['types'] if '/samples/' in r['path']]
code='namespace SkrGui.Gallery;\npublic static class GallerySourceTypeRegistry\n{\n public static IReadOnlyDictionary<Type,Guid> Types { get; } = new Dictionary<Type,Guid>\n {\n'
for r in sample:code+=f'  [typeof({r["type"]})] = new("{r["guid"]}"),\n'
code+=' };\n}\n'
(root/'libraries/SkrGui.Godot/Gallery/GallerySourceTypeRegistry.cs').write_text(code)
print(len(core),'Core GUID types;',len(sample),'Gallery GUID types')
