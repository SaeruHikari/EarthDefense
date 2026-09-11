param([string[]]$Layouts=@('world','cluster'),[int]$Warmup=300,[int]$Samples=1200,[string]$Tag='release-final',[ValidateSet('combat','patrol')][string]$Mode='combat',[int]$Enemies=1000,[switch]$ProfileStages)
$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$player=Join-Path $projectRoot 'EarthwardPlayer.exe'
foreach($layout in $Layouts) {
    if($layout -notin @('world','cluster')) {throw 'Unknown layout'}
    $profile=Join-Path $projectRoot ('.runtime-tests\fleet127-'+$Tag+'-'+$layout+'-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
    $env:APPDATA=Join-Path $profile 'AppData'
    $env:LOCALAPPDATA=Join-Path $profile 'LocalAppData'
    New-Item -ItemType Directory -Force -Path $env:APPDATA,$env:LOCALAPPDATA | Out-Null
    $stdout=Join-Path $projectRoot ('artifacts\fleet127-'+$Tag+'-'+$layout+'.stdout.log')
    $stderr=Join-Path $projectRoot ('artifacts\fleet127-'+$Tag+'-'+$layout+'.stderr.log')
    $arguments=@('--audio-driver','Dummy','--position','-1700,100','--','--native-scene=res://tests/managed_fleet_scale.tscn',('--bench-tag='+$Tag),('--layout='+$layout),('--bench-mode='+$Mode),('--enemies='+$Enemies),('--warmup='+$Warmup),('--samples='+$Samples))
    if($ProfileStages) {$arguments+='--profile-stages=true'}
    $process=Start-Process -FilePath $player -ArgumentList $arguments -WorkingDirectory $projectRoot -WindowStyle Hidden -RedirectStandardOutput $stdout -RedirectStandardError $stderr -PassThru
    $null=$process.Handle
    if(-not $process.WaitForExit(300000)) {Stop-Process -Id $process.Id;throw 'Benchmark timed out'}
    $process.WaitForExit()
    if($process.ExitCode -ne 0) {Get-Content $stderr -Encoding UTF8;throw ('Benchmark failed: '+$layout)}
    $suffix=if($layout -eq 'world') {'-world'} else {''}
    $report=Join-Path $projectRoot ('artifacts\fleet127-native-'+$Tag+$suffix+'-'+$Mode+'-'+$Enemies+'.json')
    $result=Get-Content -LiteralPath $report -Raw -Encoding UTF8 | ConvertFrom-Json
    [pscustomobject]@{Layout=$layout;FrameP50=$result.timings.frame_ms.p50;FrameP95=$result.timings.frame_ms.p95;CombatP50=$result.timings.combat_ms.p50;SyncP50=$result.timings.sync_ms.p50;GpuP50=$result.timings.world_gpu_ms.p50;SimulationSeconds=$result.simulated_seconds;MinimumFriendly=$result.minimum_friendly_after_maintenance;Damage=$result.damage_events;EngagedFactories=$result.factories_observed_engaged;Report=$report} | Format-List
}
