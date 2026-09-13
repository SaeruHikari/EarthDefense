import pathlib,re,hashlib,json
root=pathlib.Path(__file__).resolve().parents[1]
source=pathlib.Path('D:/Code/ExtremeEngine-CppSLJIT/engine/modules/gui/samples/gallery_common/src/embedded_gallery_font.hpp')
text=source.read_text()
body=re.search(r'kSkrMiniSans\[\]\s*=\s*\{(.*?)\};',text,re.S).group(1)
data=bytes(int(v,16) for v in re.findall(r'0x([0-9a-fA-F]{2})',body))
sha=hashlib.sha256(data).hexdigest();assert sha=='cfcb9c61a9a36bf812dcddceef282e7b032544cd3eaec3f39022ccb3d60638df'
output=root/'libraries/SkrGui.Godot/Assets/SkrMiniSans.ttf';output.parent.mkdir(parents=True,exist_ok=True);output.write_bytes(data)
(root/'native/gallery-font-manifest.json').write_text(json.dumps({'source':str(source),'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'font_sha256':sha,'bytes':len(data)},indent=2))
print('Original Gallery font:',len(data),'bytes;',sha)
