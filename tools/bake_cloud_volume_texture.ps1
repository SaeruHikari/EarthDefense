param(
    [string]$Raw = 'res://artifacts/cloud_noise_3d.rgba8',
    [string]$Output = 'res://assets/earth/clouds/volume/cloud_noise_3d.res',
    [ValidateRange(1,256)][int]$Size = 96,
    [switch]$SkipBuild
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'managed_runtime.ps1')
$engine = Get-EarthwardManagedEngine $projectRoot
if (-not $SkipBuild) { Build-EarthwardManaged $projectRoot 'Debug' }
$priorAppData = $env:APPDATA
$priorLocalAppData = $env:LOCALAPPDATA
try {
    $profile = Join-Path $projectRoot '.runtime-tests/cloud-volume-bake'
    $env:APPDATA = Join-Path $profile 'AppData'
    $env:LOCALAPPDATA = Join-Path $profile 'LocalAppData'
    New-Item -ItemType Directory -Force -Path $env:APPDATA,$env:LOCALAPPDATA | Out-Null
    & $engine --path $projectRoot 'res://tools/bake_cloud_volume_texture.tscn' --position '-1700,100' --audio-driver Dummy -- --raw=$Raw --output=$Output --size=$Size
    if ($LASTEXITCODE -ne 0) { throw 'Cloud volume packing or byte validation failed.' }
} finally {
    $env:APPDATA = $priorAppData
    $env:LOCALAPPDATA = $priorLocalAppData
}
