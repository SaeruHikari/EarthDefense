param([Parameter(Mandatory=$true)][string[]]$Scenes)
$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$player=Join-Path $projectRoot 'EarthwardPlayer.exe'
$runStamp=Get-Date -Format 'yyyyMMdd-HHmmss-fff'
$failed=@()
foreach($scene in $Scenes) {
    $profile=Join-Path $projectRoot ('.runtime-tests\native127-'+$runStamp+'-'+$scene)
    $env:APPDATA=Join-Path $profile 'AppData'
    $env:LOCALAPPDATA=Join-Path $profile 'LocalAppData'
    New-Item -ItemType Directory -Force -Path $env:APPDATA,$env:LOCALAPPDATA | Out-Null
    $log=Join-Path $projectRoot ('artifacts\native127-'+$scene+'.log')
    $stdout=Join-Path $projectRoot ('artifacts\native127-'+$scene+'.stdout.log')
    $stderr=Join-Path $projectRoot ('artifacts\native127-'+$scene+'.stderr.log')
    $arguments=@('--audio-driver','Dummy','--position','-1700,100','--log-file',$log,'--',('--native-scene=res://tests/managed_'+$scene+'.tscn'))
    $process=Start-Process -FilePath $player -ArgumentList $arguments -WorkingDirectory $projectRoot -WindowStyle Hidden -RedirectStandardOutput $stdout -RedirectStandardError $stderr -PassThru
    $null=$process.Handle
    if(-not $process.WaitForExit(180000)) {
        Stop-Process -Id $process.Id
        $failed+=$scene
        Write-Output ('NATIVE_SCENE_TIMEOUT: '+$scene)
        continue
    }
    $process.WaitForExit()
    $output=(Get-Content -LiteralPath $stdout -Raw -Encoding UTF8)+(Get-Content -LiteralPath $stderr -Raw -Encoding UTF8)
    $output | Write-Output
    if($process.ExitCode -ne 0 -or $output -match 'SCRIPT ERROR|SHADER ERROR|Parse Error|_FAIL:|EXCEPTION:|ERROR: Failed to load' -or $output -notmatch '0 failures') {
        $failed+=$scene
        Write-Output ('NATIVE_SCENE_FAILED: '+$scene)
    } else { Write-Output ('NATIVE_SCENE_PASSED: '+$scene) }
}
if($failed.Count -gt 0) {throw ('Failed native scenes: '+($failed -join ', '))}
