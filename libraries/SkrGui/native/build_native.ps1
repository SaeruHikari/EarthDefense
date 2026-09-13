param([string]$EngineRoot = 'D:/Code/ExtremeEngine-CppSLJIT')
$ErrorActionPreference = 'Stop'
$nativeRoot = [IO.Path]::GetFullPath($PSScriptRoot)
& python (Join-Path $nativeRoot 'configure_native.py') --engine $EngineRoot
if ($LASTEXITCODE -ne 0) { throw 'Native source configuration failed.' }
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$vsRoot = & $vswhere -latest -property installationPath
$vcvars = Join-Path $vsRoot 'VC/Auxiliary/Build/vcvars64.bat'
if (-not (Test-Path -LiteralPath $vcvars)) { throw 'MSVC x64 development environment is unavailable.' }
$commands = @(
    '@echo off',
    ('call "' + $vcvars + '" >nul'),
    'if errorlevel 1 exit /b 1',
    ('cmake -S "' + $nativeRoot + '" -B "' + (Join-Path $nativeRoot 'build') + '" -G Ninja -DCMAKE_BUILD_TYPE=Release -DCMAKE_C_COMPILER=clang-cl -DCMAKE_CXX_COMPILER=clang-cl'),
    'if errorlevel 1 exit /b 1',
    ('cmake --build "' + (Join-Path $nativeRoot 'build') + '" --parallel 12'),
    'exit /b %errorlevel%'
)
$script = Join-Path $nativeRoot 'build_current.cmd'
[IO.File]::WriteAllLines($script, $commands, [Text.Encoding]::ASCII)
& $script
if ($LASTEXITCODE -ne 0) { throw 'Pinned third-party library build failed.' }
