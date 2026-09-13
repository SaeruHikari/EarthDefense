import json,pathlib,re
root=pathlib.Path(__file__).resolve().parents[1]
source=(root/'libraries/SkrGui.Core/Text/Font/TextNative.Font.cs').read_text()
structs={}
for name,body in re.findall(r'\[StructLayout\(LayoutKind.Sequential\)\] internal struct (\w+)\s*\{(.*?)\n?\}',source,re.S):
    fields=[]
    for typ,names in re.findall(r'public (nint|int|uint|ushort|short|byte|FT_\w+) (\w+(?:\s*,\s*\w+)*);',body):
        for field in names.split(','):fields.append(field.strip())
    structs[name]=fields
special={'FT_Generic':{'Data':'data','Finalizer':'finalizer'},'FT_Vector':{'X':'x','Y':'y'},'FT_BBox':{'XMin':'xMin','YMin':'yMin','XMax':'xMax','YMax':'yMax'},
'FT_Matrix':{'Xx':'xx','Xy':'xy','Yx':'yx','Yy':'yy'},'FT_Bitmap_Size':{'XPpem':'x_ppem','YPpem':'y_ppem'},
'FT_Size_Metrics':{'XPpem':'x_ppem','YPpem':'y_ppem','XScale':'x_scale','YScale':'y_scale'},
'FT_SizeRec':{'Internal':'internal'},'FT_FaceRec':{'UnitsPerEm':'units_per_EM','Bbox':'bbox'},
'FT_Glyph_Metrics':{'HoriBearingX':'horiBearingX','HoriBearingY':'horiBearingY','HoriAdvance':'horiAdvance','VertBearingX':'vertBearingX','VertBearingY':'vertBearingY','VertAdvance':'vertAdvance'},
'FT_Var_Axis':{'Default':'def'},'FT_MM_Var':{'NumDesigns':'num_designs','NumNamedStyles':'num_namedstyles','NamedStyle':'namedstyle'},
'FT_Outline':{'NContours':'n_contours','NPoints':'n_points'}}
special['FT_GlyphSlotRec']={'LinearHoriAdvance':'linearHoriAdvance','LinearVertAdvance':'linearVertAdvance'}
special['FT_Size_Request']={'HoriResolution':'horiResolution','VertResolution':'vertResolution'}
special['FT_SfntName']={'StringLength':'string_len'}
def native_field(name,f):
    return special.get(name,{}).get(f,re.sub(r'(?<!^)(?=[A-Z])','_',f).lower())
headers=['#include <stdio.h>','#include <stddef.h>','#include <ft2build.h>','#include FT_FREETYPE_H','#include FT_SIZES_H','#include FT_MULTIPLE_MASTERS_H','#include FT_SFNT_NAMES_H','#include FT_COLOR_H','#include FT_OUTLINE_H','int main(){','printf("{");']
first=True
mapping={}
for name,fields in structs.items():
    ctype='FT_Size_RequestRec' if name=='FT_Size_Request' else name
    mapping[name]={}
    for field in fields:
        native=native_field(name,field);mapping[name][field]=native
        headers.append('printf("'+('' if first else ',')+'\\\"'+name+'.'+field+'\\\":%zu",offsetof('+ctype+','+native+'));')
        first=False
    size='offsetof(FT_FaceRec,driver)' if name=='FT_FaceRec' else 'sizeof('+ctype+')'
    headers.append('printf("'+('' if first else ',')+'\\\"'+name+'.$size\\\":%zu",'+size+');')
headers.extend(['printf("}");return 0;}'])
out=root/'native/build/abi_probe.cpp';out.parent.mkdir(exist_ok=True,parents=True);out.write_text('\n'.join(headers))
(root/'native/abi-field-map.json').write_text(json.dumps(mapping,indent=2))
