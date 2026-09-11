param([switch]$Export,[ValidateSet('Debug','ExportRelease')][string]$Configuration='Debug')
$ErrorActionPreference='Stop'
$projectRoot=$PSScriptRoot
. (Join-Path $projectRoot 'tools\managed_runtime.ps1')
$engine=Get-EarthwardManagedEngine $projectRoot
Build-EarthwardManaged $projectRoot $Configuration
$buildProfile=Join-Path $projectRoot '.runtime-tests\managed-build'
$env:APPDATA=Join-Path $buildProfile 'AppData'
$env:LOCALAPPDATA=Join-Path $buildProfile 'LocalAppData'
New-Item -ItemType Directory -Force -Path $env:APPDATA,$env:LOCALAPPDATA | Out-Null
$importLog=Join-Path $projectRoot 'artifacts\managed-build-import.log'
& $engine --headless --editor --path $projectRoot --import --quit --log-file $importLog
if($LASTEXITCODE -ne 0 -or (Get-Content -LiteralPath $importLog -Raw -Encoding UTF8) -match 'SCRIPT ERROR|SHADER ERROR|Parse Error|ERROR: Failed to load') {throw 'Managed Godot import failed.'}
if($Export){
    $destination=Join-Path $projectRoot 'builds\Earthward-1.26-CSharp'
    New-Item -ItemType Directory -Force -Path $destination | Out-Null
    & $engine --headless --path $projectRoot --export-release 'Windows Desktop' (Join-Path $destination 'Earthward.exe') --log-file (Join-Path $projectRoot 'artifacts\managed-export.log')
    if($LASTEXITCODE -ne 0){throw 'Managed export failed. The .NET edition Windows export templates must be installed for Godot 4.6.1.'}
    Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination (Join-Path $destination 'README.md') -Force
    Write-Output ('Managed release: '+(Join-Path $destination 'Earthward.exe'))
}else{
    Write-Output 'C# assembly and resources verified. LaunchDebug.bat / LaunchNewCampaign.bat run this build. Use -Export for a packaged desktop release.'
}
