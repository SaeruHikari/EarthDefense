param([string[]]$Scenes=@('factory_coverage','factory_coverage_render','frontier_warning_ui','resource_cheat_ui','research_extensions_ui','local_shield_render'))
$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'managed_runtime.ps1')
$engine=Get-EarthwardManagedEngine $projectRoot
$runStamp=Get-Date -Format 'yyyyMMdd-HHmmss-fff'
foreach($scene in $Scenes) {
    $profile=Join-Path $projectRoot ('.runtime-tests\features126-'+$runStamp+'-'+$scene)
    $env:APPDATA=Join-Path $profile 'AppData'
    $env:LOCALAPPDATA=Join-Path $profile 'LocalAppData'
    New-Item -ItemType Directory -Force -Path $env:APPDATA,$env:LOCALAPPDATA | Out-Null
    $log=Join-Path $projectRoot ('artifacts\features126-'+$scene+'.log')
    & $engine --path $projectRoot ('res://tests/managed_'+$scene+'.tscn') --audio-driver Dummy --position '-1700,100' --log-file $log
    $resultCode=$LASTEXITCODE
    $output=Get-Content -LiteralPath $log -Raw -Encoding UTF8
    if($resultCode -ne 0 -or $output -match 'SCRIPT ERROR|SHADER ERROR|Parse Error|_FAIL:|EXCEPTION:|ERROR: Failed to load' -or $output -notmatch '0 failures') {throw ('Native scene failed: '+$scene)}
    Write-Output ('FEATURE_SCENE_PASSED: '+$scene)
}
