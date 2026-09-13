param([ValidateSet('Debug','Release')][string]$Configuration='Debug',[switch]$Verify,[string]$EngineRoot='D:/Code/ExtremeEngine-CppSLJIT')
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location $taskRoot
try {
    $nativeDll=Join-Path $taskRoot 'native/artifacts/win-x64/skrgui_native.dll'
    if(-not (Test-Path -LiteralPath $nativeDll)) {
        & (Join-Path $taskRoot 'native/build_native.ps1') -EngineRoot $EngineRoot
        if($LASTEXITCODE -ne 0){throw 'Pinned native dependency build failed.'}
    }
    if(-not (Test-Path -LiteralPath (Join-Path $taskRoot 'native/artifacts/win-x64/skrgui_gallery_native.dll'))) {
        & (Join-Path $taskRoot 'native/build_gallery_native.ps1') -EngineRoot $EngineRoot
        if($LASTEXITCODE -ne 0){throw 'Gallery native dependency build failed.'}
    }
    if($Verify -and -not (Test-Path -LiteralPath (Join-Path $taskRoot 'native/artifacts/win-x64/skrgui_nanovg_test.dll'))) {
        & (Join-Path $taskRoot 'native/build_nanovg_tests.ps1') -EngineRoot $EngineRoot
        if($LASTEXITCODE -ne 0){throw 'Original NanoVG test dependency build failed.'}
    }
    & dotnet build (Join-Path $taskRoot 'SkrGui.sln') --configuration $Configuration
    if($LASTEXITCODE -ne 0){throw 'SkrGui C# build failed.'}
    if($Verify) {
        & dotnet run --project (Join-Path $taskRoot 'tests/SkrGui.Core.Tests/SkrGui.Core.Tests.csproj') --configuration $Configuration --no-build -- --report=artifacts/core-contract-results.json
        if($LASTEXITCODE -ne 0){throw 'Source contract checks failed.'}
        & dotnet run --project (Join-Path $taskRoot 'tests/SkrGui.Godot.Tests/SkrGui.Godot.Tests.csproj') --configuration $Configuration --no-build -- --report=artifacts/gallery-contract-results.json
        if($LASTEXITCODE -ne 0){throw 'Gallery source contracts failed.'}
        & python (Join-Path $taskRoot 'tools/audit_test_coverage.py') --results artifacts/core-contract-results.json
        if($LASTEXITCODE -ne 0){throw 'Original test coverage audit failed.'}
        & python (Join-Path $taskRoot 'tools/verify_source_mapping.py') --engine $EngineRoot
        if($LASTEXITCODE -ne 0){throw 'Source mapping verification failed.'}
    }
} finally {Pop-Location}
