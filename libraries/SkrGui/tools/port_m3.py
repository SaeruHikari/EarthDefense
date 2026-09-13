"""Literal control-flow port of fixed SPEC_2026 CMF/PHONE M3 sources."""
from pathlib import Path
import re,json
repo=Path(__file__).resolve().parents[1]
src=Path('D:/Code/ExtremeEngine-CppSLJIT/engine/modules/gui/core')
out=repo/'libraries/SkrGui.Core/Math/M3';out.mkdir(parents=True,exist_ok=True)
scheme=(src/'src/math/m3/color_scheme.cpp').read_text(encoding='utf8')
palette=(src/'src/math/m3/tonal_palette.cpp').read_text(encoding='utf8')
helper=(src/'src/math/m3/m3_color_private.hpp').read_text(encoding='utf8')
license=scheme[:scheme.index('#include')]
def pascal(s):return ''.join(x[:1].upper()+x[1:] for x in s.split('_'))
def funcs(text):
 pat=r'^\s*(?:(?:inline|constexpr|static)\s+)*(?P<ret>(?:const\s+)?(?:M3ColorScheme|M3TonalPalette|HCTColor|SRGBColor|bool|void|double|size_t|EPalette|ERole)\s*&?)\s+(?P<name>(?:\w+::)?\w+)\s*\((?P<args>[^;{}]*?)\)\s*(?:const\s+)?noexcept\s*\{'
 result=[]
 for m in re.finditer(pat,text,re.M):
  i=m.end();depth=1
  while depth:
   depth+=(text[i]=='{')-(text[i]=='}');i+=1
  result.append(dict(ret=m['ret'].replace('const ','').replace('&','').strip(),name=m['name'].split('::')[-1],args=m['args'],body=text[m.end():i-1],line=text.count('\n',0,m.start())+1))
 return result
hf,pf,sf=funcs(helper),funcs(palette),funcs(scheme)
names={f['name']:pascal(f['name']) for f in hf+pf+sf}
names.update({x:pascal(x) for x in ['hue','chroma','tone','red8','green8','blue8','to_srgb8','to_argb32']})
typemap={'uint8_t':'byte','uint16_t':'ushort','uint32_t':'uint','int32_t':'int','size_t':'int'}
def conv(s,ret=None):
 s=re.sub(r'//[^\n]*','',s)
 s=re.sub(r'SKR_VERIFY\((.*?)&&\s*"([^"]+)"\);',r'GuiAssert.Verify(\1,"\2");',s,flags=re.S)
 s=re.sub(r'\b(?:const|constexpr)\s+','',s)
 s=s.replace('HCTColor*','HCTColor?').replace('bool&','ref bool')
 s=re.sub(r'(?<=\w)&(?=\s)', '',s)
 s=s.replace('nullptr','null').replace('&tertiary','tertiary')
 s=re.sub(r'\bHCTColor\s+(\w+)\(([^;]+)\);',r'HCTColor \1 = new HCTColor(\2);',s)
 s=re.sub(r'\b(HCTColor|M3SchemeBuilder)\(',r'new \1(',s)
 s=s.replace('new new ','new ')
 s=re.sub(r'static_cast<([^>]+)>\(',lambda m:'('+typemap.get(m[1],m[1])+')(',s)
 s=re.sub(r'std::array<(\w+),\s*(\d+)>\s+(\w+)\{\};',r'\1[] \3 = new \1[\2];',s)
 s=re.sub(r'std::array<(\w+),\s*(\d+)>\s*&',r'\1[] ',s)
 for k,v in typemap.items():s=re.sub(r'\b'+k+r'\b',v,s)
 for name in ['min','max','clamp']:s=s.replace('std::'+name,'CppMath.'+pascal(name))
 for a,b in [('isfinite','double.IsFinite'),('pow','Math.Pow'),('abs','Math.Abs'),('round','HctMath.Round')]:s=s.replace('std::'+a,b)
 s=s.replace('::','.')
 for a,b in sorted(names.items(),key=lambda x:-len(x[0])):s=re.sub(r'\b'+a+r'(?=\s*\()',b,s)
 s=re.sub(r'\.(\w+)',lambda m:'.'+pascal(m[1]) if '_' in m[1] or m[1] in ['hue','chroma','tone','r','g','b','surface','outline','shadow','scrim','primary','secondary','tertiary','error','background'] else m[0],s)
 s=re.sub(r'(Background\(role,)\s*(\w+)',r'\1ref \2',s)
 s=re.sub(r'return\s*\{([^{}]*)\};',lambda m:'return new '+ret+'('+m[1].strip().rstrip(',')+');',s)
 return s
def emit(f,static=True,defaults=False):
 args=conv(f['args']).strip();ret=typemap.get(f['ret'],f['ret'])
 if defaults:args=args.replace('double contrast_level','double contrast_level = 0.0')
 return '    // source line '+str(f['line'])+'\n    public '+('static ' if static else '')+ret+' '+pascal(f['name'])+'('+args+')\n    {\n'+conv(f['body'],ret)+'\n    }\n'

(out/'M3ColorHelper.cs').write_text(license+'namespace SkrGui;\ninternal static class M3ColorHelper\n{\n'+''.join(emit(f) for f in hf)+'}\n',encoding='utf8')
palhead='''namespace SkrGui;
public struct M3TonalPalette
{
    private double _hue, _chroma;
    private HCTColor _key_color;
    private const double kMaximumChroma = 200.0;
    public M3TonalPalette() { }
    private M3TonalPalette(double hue, double chroma, HCTColor keyColor)
    { _hue=hue;_chroma=chroma;_key_color=keyColor; }
'''
(out/'M3TonalPalette.cs').write_text(license+palhead+''.join(emit(f,f['name'] in ['max_chroma_at','find_key_color'] or f['name'].startswith('From')) for f in pf)+'}\n',encoding='utf8')

enums='\n'.join('internal enum '+m[1]+' : byte\n{'+m[2]+'}' for m in re.finditer(r'enum class (\w+) : uint8_t\s*\{([^}]+)\}',scheme))
helpers=[f for f in sf if f['line']<350 or f['name']=='build_scheme']
(out/'M3SchemeFunctions.cs').write_text(license+'namespace SkrGui;\n'+enums+'\ninternal static class M3SchemeFunctions\n{\n'+''.join(emit(f) for f in helpers)+'}\n',encoding='utf8')
builderhead='''using static SkrGui.M3SchemeFunctions;
namespace SkrGui;
internal sealed class M3SchemeBuilder
{
    private M3ColorScheme _scheme = new();
    private bool _has_tertiary_source;
    private HCTColor _tertiary_source;
    private readonly double[] _minimum_tones=new double[6], _maximum_tones=new double[6];
    private readonly bool[] _has_minimum_tone=new bool[6], _has_maximum_tone=new bool[6];
    private readonly double[] _tones=new double[45];
    private readonly SRGBColor[] _colors=Enumerable.Repeat(new SRGBColor(),45).ToArray();
    private readonly bool[] _tone_resolved=new bool[45], _color_resolved=new bool[45];
    public M3SchemeBuilder(HCTColor primary_source,HCTColor? tertiary_source,bool is_dark,double contrast_level)
    {
        _has_tertiary_source=tertiary_source.HasValue;
        _tertiary_source=tertiary_source ?? new HCTColor();
        _scheme.PrimarySourceColor=primary_source;
        if(tertiary_source.HasValue) _scheme.TertiarySourceColor=tertiary_source.Value;
        _scheme.IsDark=is_dark;
        _scheme.ContrastLevel=contrast_level;
        InitializePalettes();
    }
'''
buildfuncs=[f for f in sf if 350<=f['line']<1000 and f['name']!='build_scheme']
(out/'M3SchemeBuilder.cs').write_text(license+builderhead+''.join(emit(f,f['name']=='_find_best_tone_for_chroma') for f in buildfuncs)+'}\n',encoding='utf8')
header=(src/'include/SkrGuiCore/math/m3/color_scheme.hpp').read_text(encoding='utf8')
fields=[]
for m in re.finditer(r'^    (HCTColor|container::Optional<HCTColor>|bool|double|M3TonalPalette|SRGBColor) (\w+)([^;]*);',header,re.M):
 t,n,v=m.groups();t=t.replace('container::Optional<HCTColor>','HCTColor?')
 fields.append('    public '+t+' '+pascal(n)+' = '+('null' if '?' in t else 'new()')+';')
publichead='using static SkrGui.M3SchemeFunctions;\nnamespace SkrGui;\npublic struct M3ColorScheme\n{\n    public const uint kSpecVersion=2026;\n    public M3ColorScheme() { }\n'+'\n'.join(fields)+'\n'
(out/'M3ColorScheme.cs').write_text(license+publichead+''.join(emit(f,True,True) for f in sf if f['name']=='FromCMF')+'}\n',encoding='utf8')
(repo/'.report/structure-m3-mapping.json').write_text(json.dumps([dict(source=source,name=f['name'],line=f['line'],status='translated-unverified') for source,fs in [('m3_color_private.hpp',hf),('tonal_palette.cpp',pf),('color_scheme.cpp',sf)] for f in fs],indent=2),encoding='utf8')
