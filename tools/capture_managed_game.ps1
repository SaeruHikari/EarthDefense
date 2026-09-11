param([string]$Name='main',[switch]$Continue,[switch]$Research)
$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'managed_runtime.ps1')
$engine=Get-EarthwardManagedEngine $projectRoot
$testRoot=Join-Path $projectRoot ('.runtime-tests\managed-'+$Name)
$env:APPDATA=Join-Path $testRoot 'AppData'
$env:LOCALAPPDATA=Join-Path $testRoot 'LocalAppData'
New-Item -ItemType Directory -Force -Path $env:APPDATA,$env:LOCALAPPDATA | Out-Null
$arguments=@('--path',$projectRoot,'res://main.tscn','--position','-1700,100','--audio-driver','Dummy','--log-file',(Join-Path $projectRoot ('artifacts\managed-'+$Name+'.log')),'--',('--capture='+ (Join-Path $projectRoot ('artifacts\managed-'+$Name+'.png'))),'--capture-after=2.5','--quit-after-capture')
if($Continue){$arguments+='--continue'}else{$arguments+='--demo'}
if($Research){$arguments+='--research-view'}
& $engine @arguments
exit $LASTEXITCODE
