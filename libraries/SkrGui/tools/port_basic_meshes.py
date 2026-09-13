import pathlib,re,json
root=pathlib.Path(__file__).resolve().parents[1]
path=pathlib.Path('D:/Code/ExtremeEngine-CppSLJIT/engine/modules/gui/core/src/batch/mesh.cpp')
source=path.read_text()
clean=re.sub(r'//[^\n]*','',source)
def block_at(pos):
 depth=0
 for end in range(pos,len(clean)):
  if clean[end]=='{':depth+=1
  elif clean[end]=='}':
   depth-=1
   if depth==0:return clean[pos:end+1],end+1
 raise ValueError('unbalanced body')
def pascal(name):
 special={'rrect':'RRect','rrect_fill':'RRectFill','rrect_stroke':'RRectStroke','rrect_border':'RRectBorder','diff_rrect':'DiffRRect','to_rgba32':'ToRgba32','make_vg_backend':'MakeVgBackend','add_rrect':'AddRrect','deflate_rrect':'DeflateRrect','tl_radius':'TlRadius','tr_radius':'TrRadius','br_radius':'BrRadius','bl_radius':'BlRadius','aa_radius':'AaRadius','control_1':'Control1','control_2':'Control2'}
 return special.get(name,''.join(x[:1].upper()+x[1:] for x in name.strip('_').split('_')))
methods=[]
for m in re.finditer(r'bool BasicMeshes::(\w+)\s*\((.*?)\)\s*\{',clean,re.S):
 body,end=block_at(m.end()-1);methods.append((m.group(1),m.group(2),body,source[:source.index('bool BasicMeshes::'+m.group(1))].count('\n')+1))
assert len(methods)==20
hstart=clean.index('struct RRectMeshHelper')
helper,_=block_at(clean.index('{',hstart))
helper_names=re.findall(r'static (?:bool|void|RRect|Offsetf|uint32_t) (\w+)\s*\(',helper)
method_names=[name for name,_,_,_ in methods]+['_collect_curve_split_lines','_flatten_options','_fill_options','_single_convex_fill_options','_stroke_options']+helper_names
def translate(text,helper=False):
 text=re.sub(r'\bconst\s+','',text).replace(' noexcept','').replace('private:','')
 text=re.sub(r'\buint32_t\b','uint',text);text=re.sub(r'\buint64_t\b','ulong',text);text=re.sub(r'\bMeshIndex\b','uint',text)
 text=re.sub(r'\bout\b','output',text).replace('nullptr','null')
 text=text.replace('Optional<Offsetf>','Offsetf?')
 text=text.replace('!cursor ||','!cursor.HasValue ||').replace('cursor->','cursor.Value.')
 text=text.replace('InlineVector<VGPathFlattenLine, 8>','List<VGPathFlattenLine>')
 text=text.replace('std::isfinite','float.IsFinite').replace('std::fabs','MathF.Abs').replace('std::cos','MathF.Cos').replace('std::sin','MathF.Sin').replace('std::max','CppMath.Max')
 text=text.replace('skr::math::kPi2','(MathF.PI * 2)').replace('skr::math::kHalfPi','(MathF.PI * .5f)').replace('skr::math::kPi','MathF.PI')
 text=re.sub(r'\[(?:&|&path)\]\(CubicBezier& cubic\)',r'(CubicBezier cubic) =>',text)
 text=text.replace('auto positive_radius = [](Radius radius)', 'Func<Radius,Radius> positive_radius = (Radius radius) =>')
 text=re.sub(r'\b(VGPath|VGPathFlatten|Mesh|RRect|ColorGradient|BasicMeshOptions)\s*&\s*',r'\1 ',text)
 text=text.replace('*options.', 'options.').replace('&options.', 'options.').replace('&gradient','gradient')
 text=text.replace('::','.')
 text=re.sub(r'uint (\w+)\[(\d+)\];',r'uint[] \1 = new uint[\2];',text)
 text=text.replace('bool sides[4]','bool[] sides')
 text=re.sub(r'bool sides\[\]\s*=\s*\{(.*?)\};',r'bool[] sides = [\1];',text,flags=re.S)
 text=re.sub(r'\b(MeshBuilder) (\w+)\(',r'\1 \2 = new(',text)
 text=re.sub(r'\b(VGPath|VGPathFlatten|List<VGPathFlattenLine>) (\w+)\s*;',r'\1 \2 = new();',text)
 for name in method_names:text=re.sub(r'\b'+re.escape(name)+r'\b',pascal(name),text)
 text=re.sub(r'(?<![\w.])(Offsetf|RRect|Radius)\(',r'new \1(',text)
 text=re.sub(r'\b(VGBasicPrimitiveOptions)\s*\{',r'new \1(){',text)
 # Two-coordinate aggregate arguments (ninepatch position/UV).
 text=re.sub(r'\{([^{};\n]+,[^{};\n]+)\}',r'new Offsetf(\1)',text)
 # Object-initializer member designators are the only dots following whitespace directly.
 text=re.sub(r'(?<=[{,])\s*\.(\w+)\s*=',lambda m:' '+pascal(m.group(1))+' =',text)
 text=re.sub(r'\.(\w+)',lambda m:'.'+pascal(m.group(1)) if m.group(1)[0].islower() else m.group(0),text)
 text=text.replace('.Span()', '.ToArray()')
 text=text.replace('output.Vertices.ResizeUnsafe(vertex_begin);','output.Vertices.RemoveRange((int)vertex_begin, output.Vertices.Count-(int)vertex_begin);')
 text=text.replace('output.Indices.ResizeUnsafe(index_begin);','output.Indices.RemoveRange((int)index_begin, output.Indices.Count-(int)index_begin);')
 text=text.replace('static constexpr float','private const float')
 # C# permits empty for(;;); all original branch order is retained.
 return text
helper_code=translate(helper,True)
helper_code=re.sub(r'(?m)^(\s*)static (?!constexpr)',r'\1internal static ',helper_code)
pieces=['using System.Runtime.InteropServices;\nnamespace SkrGui;\n// Source: src/batch/mesh.cpp, complete RRectMeshHelper and all BasicMeshes methods.\ninternal static class RRectMeshHelper\n'+helper_code+'\npublic static partial class BasicMeshes\n{']
mapping=[]
for name,params,body,line in methods:
 if name=='text':continue
 pars=translate(params).strip()
 translated=translate(body)
 pieces.append('    // Original mesh.cpp:'+str(line)+' '+name+'\n    public static bool '+pascal(name)+'('+pars+')\n'+translated)
 mapping.append({'source':'src/batch/mesh.cpp','line':line,'symbol':'BasicMeshes::'+name,'target_symbol':'BasicMeshes.'+pascal(name),'status':'translated-unverified'})
pieces.append('}')
target=root/'libraries/SkrGui.Core/Batch/BasicMeshes.Generated.cs';target.write_text('\n'.join(pieces))
(root/'migration/basic-meshes-map.json').write_text(json.dumps(mapping,indent=2))
print('Generated RRect helper +',len(mapping),'mesh methods. Text + four option helpers are hand mapped separately.')
