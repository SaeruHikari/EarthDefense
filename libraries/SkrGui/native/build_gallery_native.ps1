param([string]$EngineRoot='D:/Code/ExtremeEngine-CppSLJIT')
$ErrorActionPreference='Stop'
& python (Join-Path $PSScriptRoot 'configure_gallery_native.py') --engine $EngineRoot
if($LASTEXITCODE -ne 0){throw 'Gallery dependency configuration failed.'}
$taskNativeRoot=Join-Path $PSScriptRoot 'gallery'
$vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$vsRoot=& $vswhere -latest -property installationPath
$vcvars=Join-Path $vsRoot 'VC/Auxiliary/Build/vcvars64.bat'
$commands=@('@echo off',('call "'+$vcvars+'" >nul'),'if errorlevel 1 exit /b 1',('cmake -S "'+$taskNativeRoot+'" -B "'+(Join-Path $taskNativeRoot 'build')+'" -G Ninja -DCMAKE_BUILD_TYPE=Release -DCMAKE_C_COMPILER=clang-cl'),'if errorlevel 1 exit /b 1',('cmake --build "'+(Join-Path $taskNativeRoot 'build')+'"'),'exit /b %errorlevel%')
[IO.File]::WriteAllLines((Join-Path $taskNativeRoot 'build_current.cmd'),$commands,[Text.Encoding]::ASCII)
& (Join-Path $taskNativeRoot 'build_current.cmd')
if($LASTEXITCODE -ne 0){throw 'Gallery native dependency build failed.'}
