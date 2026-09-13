import pathlib,json,hashlib,argparse,sys
root=pathlib.Path(__file__).resolve().parents[1]
args=argparse.ArgumentParser();args.add_argument('--engine',default='D:/Code/ExtremeEngine-CppSLJIT');engine=pathlib.Path(args.parse_args().engine)
inventory=json.loads((root/'migration/source-inventory.json').read_text())
bad=[]
for f in inventory['files']:
 p=engine/f['path']
 if not p.is_file() or hashlib.sha256(p.read_bytes()).hexdigest()!=f['sha256']:bad.append(f['path'])
audit=json.loads((root/'migration/core-semantics-audit.json').read_text())
missing_targets=[(f['source'],t) for f in audit['files'] for t in f['targets'] if not(root/t).is_file()]
unmapped=[f['source'] for f in audit['files'] if not f['targets']]
output={'source_commit':inventory['commit'],'reference_files':len(inventory['files']),'changed_reference_files':bad,'runtime_mapped_files':len(audit['files']),'missing_targets':missing_targets,'unmapped':unmapped,'target_sha256':{t:hashlib.sha256((root/t).read_bytes()).hexdigest() for t in sorted({t for f in audit['files'] for t in f['targets']}) if(root/t).is_file()}}
(root/'migration/source-verification.json').write_text(json.dumps(output,indent=2))
print(f'Reference hashes: {len(inventory["files"])-len(bad)}/{len(inventory["files"])}; GUI runtime mappings: {len(audit["files"])-len(unmapped)}/{len(audit["files"])}')
if bad:print('SOURCE DRIFT',bad)
if missing_targets:print('MISSING TARGETS',missing_targets)
sys.exit(1 if bad or missing_targets or unmapped else 0)
