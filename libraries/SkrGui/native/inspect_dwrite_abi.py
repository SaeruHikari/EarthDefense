import re,pathlib,json
base=pathlib.Path('C:/Program Files (x86)/Windows Kits/10/Include/10.0.26100.0/um')
interfaces={}
for name in ['dwrite.h','dwrite_1.h','dwrite_2.h']:
 s=(base/name).read_text(errors='replace')
 for m in re.finditer(r'interface DWRITE_DECLARE_INTERFACE\("([^"]+)"\) (\w+) : public (\w+)\s*\{(.*?)\n\};',s,re.S):
  guid,iface,parent,body=m.groups()
  methods=re.findall(r'STDMETHOD(?:_\([^,]+,\s*(\w+)\)|\(\s*(\w+)\))',body)
  interfaces[iface]=(guid,parent,[a or b for a,b in methods])
def get(name):
 if name=='IUnknown':return ['QueryInterface','AddRef','Release']
 return get(interfaces[name][1])+interfaces[name][2]
targets={'IDWriteFactory2':['GetSystemFontCollection','GetSystemFontFallback','CreateNumberSubstitution'],
'IDWriteFontCollection':['FindFamilyName','GetFontFamily'],'IDWriteFontFamily':['GetFirstMatchingFont'],'IDWriteFont':['CreateFontFace'],
'IDWriteFontFace':['GetFiles','GetIndex'],'IDWriteFontFile':['GetLoader','GetReferenceKey'],'IDWriteLocalFontFileLoader':['GetFilePathLengthFromKey','GetFilePathFromKey'],
'IDWriteFontFallback':['MapCharacters'],'IDWriteTextAnalysisSource':get('IDWriteTextAnalysisSource')}
out={}
for name,methods in targets.items():
 allmethods=get(name);out[name]={'guid':interfaces[name][0],'slots':{m:allmethods.index(m) for m in methods}}
print(json.dumps(out,indent=2))
pathlib.Path('native/dwrite-abi.json').write_text(json.dumps(out,indent=2))
