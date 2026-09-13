import pathlib,subprocess,json,hashlib,re
root=pathlib.Path(__file__).resolve().parents[1]
engine=pathlib.Path('D:/Code/ExtremeEngine-CppSLJIT')
commit=subprocess.check_output(['git','-C',str(engine),'rev-parse','HEAD'],text=True).strip()
expected='611561f81534354c8c1618dc27c4782cd9277cbf'
if commit!=expected:raise SystemExit('Source commit changed; refusing to overwrite the pinned inventory.')
files=subprocess.check_output(['git','-C',str(engine),'ls-files','engine/modules/gui/core','engine/modules/gui/samples'],text=True).splitlines()
entries=[];cases=[];guids=[]
for relative in files:
 file=engine/relative
 if not file.is_file():continue
 data=file.read_bytes();text=data.decode('utf-8',errors='replace')
 item={'path':relative,'sha256':hashlib.sha256(data).hexdigest(),'bytes':len(data),'lines':len(text.splitlines())}
 entries.append(item)
 for m in re.finditer(r'SKR_TEST_CASE(?:_FIXTURE\([^,]+,\s*|\(\s*)"([^"]+)"',text):
  cases.append({'name':m.group(1),'path':relative,'line':text[:m.start()].count('\n')+1})
 for m in re.finditer(r'(?:struct|class)\s*\[\[sattr\(\s*guid\("([^"]+)"\)\s*\)\]\]\s*(?:SKR_\w+\s+)?(\w+)',text):
  guids.append({'type':m.group(2),'guid':m.group(1),'path':relative,'line':text[:m.start()].count('\n')+1})
out=root/'migration';out.mkdir(exist_ok=True)
for name,value in [('source-inventory.json',{'source_root':str(engine),'commit':commit,'files':entries}),('source-test-cases.json',{'commit':commit,'cases':cases}),('source-type-guids.json',{'commit':commit,'types':guids})]:
 (out/name).write_text(json.dumps(value,indent=2,ensure_ascii=False),encoding='utf-8')
print(json.dumps({'source_files':len(entries),'original_test_cases':len(cases),'source_guid_types':len(guids)}))
