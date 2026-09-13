import pathlib,re,hashlib,json,shutil,struct
root=pathlib.Path(__file__).resolve().parents[1]
src=pathlib.Path('D:/Code/ExtremeEngine-CppSLJIT/engine/modules/gui/core/tests/text')
out=root/'tests/SkrGui.Core.Tests/Text/Assets';out.mkdir(parents=True,exist_ok=True)
text=(src/'embedded_text_test_fonts.cpp').read_text();result={}
for name,body in re.findall(r'const uint8_t k(\w+)\[\]\s*=\s*\{(.*?)\};',text,re.S):
 data=bytes(int(x,16) for x in re.findall(r'0x([0-9A-Fa-f]{2})',body));sha=hashlib.sha256(data).hexdigest()
 (out/(name+'.font')).write_bytes(data);result[name]={'sha256':sha,'bytes':len(data)}
shutil.copyfile(src/'LICENSE.embedded-fonts.txt',out/'LICENSE.embedded-fonts.txt')
# Materialize the exact deterministic sbix builder (only a test input, no runtime algorithm replacement).
source=(src/'embedded_text_bitmap_font.cpp').read_text()
pngs={}
for name,body in re.findall(r'constexpr uint8_t k(Png\d+)\[\]\s*\{(.*?)\};',source,re.S):
 pngs[name]=bytes(int(x,16) for x in re.findall(r'0x([0-9A-Fa-f]{2})',body))
font=bytearray((out/'Color.font').read_bytes())
u16=lambda p:struct.unpack_from('>H',font,p)[0]
u32=lambda p:struct.unpack_from('>I',font,p)[0]
def entry(tag):
 for i in range(u16(4)):
  p=12+i*16
  if bytes(font[p:p+4])==tag:return p
 raise AssertionError(tag)
maxp,head,replace=(entry(tag) for tag in [b'maxp',b'head',b'GPOS'])
maxoff,headoff,reploff,replsize=u32(maxp+8),u32(head+8),u32(replace+8),u32(replace+12)
count=u16(maxoff+4)
def strike(ppem,x,y,png):
 size=4+(count+1)*4;data=bytearray(size+8+len(png));struct.pack_into('>HH',data,0,ppem,72)
 for i in range(count+1):struct.pack_into('>I',data,4+i*4,size if i<=34 else len(data))
 struct.pack_into('>hh4s',data,size,x,y,b'png ');data[size+8:]=png;return data
a,b=strike(16,2,-1,pngs['Png16']),strike(8,0,0,pngs['Png8'])
sbix=struct.pack('>HHIII',1,1,2,16,16+len(a))+a+b;assert len(sbix)<=replsize
font[reploff:reploff+replsize]=bytes(replsize);font[reploff:reploff+len(sbix)]=sbix
def checksum(data):return sum(int.from_bytes(data[i:i+4].ljust(4,b'\0'),'big') for i in range(0,len(data),4))&0xffffffff
font[replace:replace+4]=b'sbix';struct.pack_into('>I',font,replace+4,checksum(sbix));struct.pack_into('>I',font,replace+12,len(sbix))
struct.pack_into('>I',font,headoff+8,0);struct.pack_into('>I',font,headoff+8,(0xb1b0afba-checksum(font))&0xffffffff)
(out/'ColorBitmap.font').write_bytes(font);result['ColorBitmap']={'sha256':hashlib.sha256(font).hexdigest(),'bytes':len(font),'source_builder':'embedded_text_bitmap_font.cpp'}
(root/'native/test-font-manifest.json').write_text(json.dumps(result,indent=2))
# Original Gallery derives from the same licensed Inter input.
shutil.copyfile(src/'LICENSE.embedded-fonts.txt',root/'libraries/SkrGui.Godot/Assets/LICENSE.embedded-fonts.txt')
print(json.dumps(result,indent=2))
