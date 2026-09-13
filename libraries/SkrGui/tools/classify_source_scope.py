import pathlib,re,json,collections
root=pathlib.Path(__file__).resolve().parents[1]
inv=json.loads((root/'migration/source-inventory.json').read_text())
entries=[]
# Scope classification states what must map to managed GUI and what is the original SDK/tooling.
for f in inv['files']:
 p=f['path'];kind='reference-tooling'
 if '/core/include/' in p or '/core/src/' in p:
  kind='gui-runtime' if not p.endswith('LICENSE.embedded-fonts.txt') else 'license'
 elif '/core/tests/' in p:kind='source-tests'
 elif '/samples/gallery_common/' in p:kind='gallery-common-or-godot-host'
 elif '/samples/demo/' in p:kind='interactive-counter-example'
 elif '/core/build.cs' in p:kind='build-entry'
 elif '/core/bench/' in p:kind='original-performance-tool'
 elif '/samples/draw_gallery/' in p or '/samples/layout_gallery/' in p:kind='reference-gallery-pages'
 elif '/samples/' in p:kind='original-standalone-tool'
 entries.append(dict(f,scope=kind))
report={'source_commit':inv['commit'],'files':entries,'counts':dict(collections.Counter(e['scope'] for e in entries))}
(root/'migration/scope-classification.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps(report['counts'],indent=2))
