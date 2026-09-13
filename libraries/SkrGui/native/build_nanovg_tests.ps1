param([string]$EngineRoot='D:/Code/ExtremeEngine-CppSLJIT')
$ErrorActionPreference='Stop'
$taskNativeRoot=Join-Path $PSScriptRoot 'nanovg-test'
$taskSource=[IO.Path]::GetFullPath((Join-Path $EngineRoot 'engine/packages/nanovg/nanovg/nanovg.cpp')).Replace('\','/')
if(-not (Test-Path -LiteralPath $taskSource)){throw "Pinned NanoVG source is missing: $taskSource"}
$taskCmake=@('cmake_minimum_required(VERSION 3.20)','project(SkrGuiNanoVGTest LANGUAGES CXX)',('add_library(skrgui_nanovg_test SHARED "'+$taskSource+'")'),'set_target_properties(skrgui_nanovg_test PROPERTIES CXX_STANDARD 20 WINDOWS_EXPORT_ALL_SYMBOLS ON RUNTIME_OUTPUT_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}/../artifacts/win-x64")')
[IO.File]::WriteAllLines((Join-Path $taskNativeRoot 'CMakeLists.txt'),$taskCmake,[Text.Encoding]::ASCII)
$vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$vsRoot=& $vswhere -latest -property installationPath
$vcvars=Join-Path $vsRoot 'VC/Auxiliary/Build/vcvars64.bat'
$commands=@('@echo off',('call "'+$vcvars+'" >nul'),'if errorlevel 1 exit /b 1',('cmake -S "'+$taskNativeRoot+'" -B "'+(Join-Path $taskNativeRoot 'build')+'" -G Ninja -DCMAKE_BUILD_TYPE=Release -DCMAKE_CXX_COMPILER=clang-cl'),'if errorlevel 1 exit /b 1',('cmake --build "'+(Join-Path $taskNativeRoot 'build')+'"'),'exit /b %errorlevel%')
[IO.File]::WriteAllLines((Join-Path $taskNativeRoot 'build_current.cmd'),$commands,[Text.Encoding]::ASCII)
& (Join-Path $taskNativeRoot 'build_current.cmd')
if($LASTEXITCODE -ne 0){throw 'NanoVG reference dependency build failed.'}
