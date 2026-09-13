import pathlib,json,hashlib
root=pathlib.Path(__file__).resolve().parents[1]
core=json.loads((root/'artifacts/core-contract-results.json').read_text())
gallery=json.loads((root/'artifacts/gallery-contract-results.json').read_text())
gpu=json.loads((root/'artifacts/gpu-probe/results.json').read_text())
host=json.loads((root/'artifacts/godot/host-verification.json').read_text())
source=json.loads((root/'migration/source-verification.json').read_text())
coverage=json.loads((root/'migration/test-coverage.json').read_text())
native={}
for p in (root/'native/artifacts/win-x64').glob('*'):
 if p.suffix in ('.dll','.dat'):native[p.name]={'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()}
result={'source_commit':source['source_commit'],'runtime_file_mapping':{'mapped':source['runtime_mapped_files'],'unmapped':len(source['unmapped']),'original_headers':128,'original_sources_and_private_headers':111},'reference_source_hashes':{'checked':source['reference_files'],'changed':source['changed_reference_files']},'original_test_cases':coverage.get('execution',{}),'core_tests':{'passed':core['passed'],'failed':core['failed']},'gallery_tests':{'passed':gallery['passed'],'failed':gallery['failed']},'gpu_checks':gpu,'counter_host':host,'native_dependencies':native,'language_and_host_boundaries':['C# GC final collection timing differs from last-RC synchronous destruction; explicit GUI lifecycle and non-owning references are preserved.','Godot RenderingDevice submissions report asynchronous device failures through LastRenderError.','macOS platform provider has not been run on the Windows validation host.'],'additional_source_tools':'Standalone draw/layout gallery pages, HTML reports and benchmark/estimator programs remain separate reference tools, not linked into the migrated GUI runtime.'}
(root/'migration/verification-summary.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print(f'Runtime {source["runtime_mapped_files"]}; original tests {coverage["execution"]["passed_original_cases"]}; Core {core["passed"]}/{core["failed"]}; Gallery {gallery["passed"]}/{gallery["failed"]}; GPU {len(gpu["passed"])}')
