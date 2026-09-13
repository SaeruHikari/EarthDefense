param([switch]$NoBuild,[string]$GodotPath='D:/MyGame/Godot/4.6.1-net/Godot_v4.6.1-stable_mono_win64_console.exe')
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if(-not $NoBuild){& (Join-Path $PSScriptRoot 'BuildSkrGui.ps1') -Verify}
if(-not(Test-Path -LiteralPath $GodotPath)){throw "Godot .NET is missing: $GodotPath"}
$taskProject=Join-Path $taskRoot 'godot/SkrGuiHost'
$taskLogs=Join-Path $taskRoot 'artifacts/godot-verification-logs'
New-Item -ItemType Directory -Path $taskLogs -Force | Out-Null
function Invoke-SkrGuiGpuCheck([string]$Name,[string[]]$Extra) {
    $argsForGodot=@('--path',('"' + $taskProject + '"'),'--position','-20000,-20000','--rendering-method','mobile','--rendering-driver','vulkan')+$Extra
    $stdout=Join-Path $taskLogs ($Name+'.log')
    $stderr=Join-Path $taskLogs ($Name+'.err.log')
    $taskProcess=Start-Process -FilePath $GodotPath -ArgumentList $argsForGodot -WindowStyle Hidden -RedirectStandardOutput $stdout -RedirectStandardError $stderr -PassThru
    if(-not $taskProcess.WaitForExit(60000)){$taskProcess.Kill();throw "Godot verification timeout: $Name"}
    $taskProcess.Refresh()
    $log=[IO.File]::ReadAllText($stdout)
    $errors=[IO.File]::ReadAllText($stderr)
    if($log -notmatch '(SKRGUI_.*CHECKS: passed|SKRGUI_GPU_PROBE_PASS=12)' -or $errors.Length -gt 0) {throw ("Godot verification failed: "+$Name+[Environment]::NewLine+$log+[Environment]::NewLine+$errors)}
    Write-Output "$Name passed"
}
Invoke-SkrGuiGpuCheck 'backend' @('res://backend_gpu_probe.tscn')
foreach($mode in @('sdf','gray','gray-lcd','sdf-lcd')){
    $taskOutput=Join-Path $taskRoot ('artifacts/godot-modes/'+$mode)
    Invoke-SkrGuiGpuCheck $mode @('--','--self-test',('--text-aa='+$mode),('--output="'+$taskOutput+'"'))
}
