import pathlib
root=pathlib.Path(__file__).resolve().parents[1]
p=root/'native/configure_gallery_native.py';s=p.read_text();s=s.replace('import pathlib,re,json,hashlib','import pathlib,re,json,hashlib,argparse').replace("engine=pathlib.Path('D:/Code/ExtremeEngine-CppSLJIT')","args=argparse.ArgumentParser();args.add_argument('--engine',default='D:/Code/ExtremeEngine-CppSLJIT');engine=pathlib.Path(args.parse_args().engine).resolve()");p.write_text(s)
p=root/'native/build_gallery_native.ps1';s=p.read_text().replace("(Join-Path $PSScriptRoot 'configure_gallery_native.py')","(Join-Path $PSScriptRoot 'configure_gallery_native.py') --engine $EngineRoot");p.write_text(s)
p=root/'libraries/SkrGui.Godot/SkrGui.Godot.csproj';s=p.read_text().replace('</Project>','<ItemGroup><None Include="../../native/artifacts/win-x64/skrgui_gallery_native.dll" Link="skrgui_gallery_native.dll" CopyToOutputDirectory="PreserveNewest" Condition="Exists(\'../../native/artifacts/win-x64/skrgui_gallery_native.dll\')" /></ItemGroup></Project>');p.write_text(s)
p=root/'tools/BuildSkrGui.ps1';s=p.read_text()
s=s.replace("    & dotnet build",'''    if(-not (Test-Path -LiteralPath (Join-Path $taskRoot 'native/artifacts/win-x64/skrgui_gallery_native.dll'))) {
        & (Join-Path $taskRoot 'native/build_gallery_native.ps1') -EngineRoot $EngineRoot
        if($LASTEXITCODE -ne 0){throw 'Gallery native dependency build failed.'}
    }
    if($Verify -and -not (Test-Path -LiteralPath (Join-Path $taskRoot 'native/artifacts/win-x64/skrgui_nanovg_test.dll'))) {
        & (Join-Path $taskRoot 'native/build_nanovg_tests.ps1')
        if($LASTEXITCODE -ne 0){throw 'Original NanoVG test dependency build failed.'}
    }
    & dotnet build''',1)
s=s.replace("        if($LASTEXITCODE -ne 0){throw 'Source contract checks failed.'}","""        if($LASTEXITCODE -ne 0){throw 'Source contract checks failed.'}
        & dotnet run --project (Join-Path $taskRoot 'tests/SkrGui.Godot.Tests/SkrGui.Godot.Tests.csproj') --configuration $Configuration --no-build -- --report=artifacts/gallery-contract-results.json
        if($LASTEXITCODE -ne 0){throw 'Gallery source contracts failed.'}
        & python (Join-Path $taskRoot 'tools/audit_test_coverage.py')
        if($LASTEXITCODE -ne 0){throw 'Original test coverage audit failed.'}""")
p.write_text(s)
