import pathlib,re,json,collections,argparse,sys
root=pathlib.Path(__file__).resolve().parents[1]
parser=argparse.ArgumentParser();parser.add_argument('--results',type=pathlib.Path)
args=parser.parse_args()
source=json.loads((root/'migration/source-test-cases.json').read_text())['cases']
def normalize(name):
 if '::' in name:
  source_file,suffix=name.split('::',1)
  candidates=[c['name'] for c in source if c['path'].endswith(source_file) and (c['name']==suffix or c['name'].endswith('/'+suffix))]
  if len(candidates)==1:return candidates[0]
 return name
registered=collections.defaultdict(list)
for file in (root/'tests').rglob('*.cs'):
 if any(x in file.parts for x in ('bin','obj')):continue
 text=file.read_text(encoding='utf-8-sig')
 for match in re.finditer(r'\[GuiTest\("((?:\\.|[^"\\])*)"\)\]',text):
  raw=json.loads('"'+match[1]+'"');name=normalize(raw)
  registered[name].append({'path':str(file.relative_to(root)).replace('\\','/'),'line':text[:match.start()].count('\n')+1,'attribute':raw})
missing=[c for c in source if c['name'] not in registered]
source_names={c['name'] for c in source}
extra={name:loc for name,loc in registered.items() if name not in source_names}
report={'source_commit':'611561f81534354c8c1618dc27c4782cd9277cbf','original_case_count':len(source),'mapped_original_cases':len(source)-len(missing),'missing_cases':missing,'additional_checks':extra,'cases':[dict(c,targets=registered.get(c['name'],[])) for c in source],'duplicate_case_names':{name:loc for name,loc in registered.items() if len(loc)>1}}
failed=bool(missing or report['duplicate_case_names'])
if args.results:
 results=json.loads(args.results.read_text(encoding='utf-8-sig'))
 actual={normalize(t['source']):t for t in results['tests']}
 not_passed=[name for name in sorted(source_names) if name not in actual or actual[name]['result']!='passed']
 report['execution']={'report':str(args.results),'passed_original_cases':len(source)-len(not_passed),'missing_or_failed_original_cases':not_passed,'all_result_count':len(results['tests']),'all_failures':results['failed']}
 failed|=bool(not_passed or results['failed'])
 print(f'Executed and passed {len(source)-len(not_passed)} / {len(source)} original cases')
(root/'migration/test-coverage.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(f'Mapped {len(source)-len(missing)} / {len(source)} original cases; {len(extra)} additional checks')
for c in missing:print('MISSING',c['path'],c['name'])
if report['duplicate_case_names']:print('DUPLICATES',list(report['duplicate_case_names']))
sys.exit(1 if failed else 0)
