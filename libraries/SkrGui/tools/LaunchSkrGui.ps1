param([switch]$NoBuild,[switch]$SelfTest,[ValidateSet('auto','gray','gray-lcd','sdf','sdf-lcd')][string]$TextAa='sdf',[string]$GodotPath='D:/MyGame/Godot/4.6.1-net/Godot_v4.6.1-stable_mono_win64_console.exe')
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if(-not $NoBuild) {& (Join-Path $PSScriptRoot 'BuildSkrGui.ps1')}
if(-not (Test-Path -LiteralPath $GodotPath)){throw "Godot .NET is missing: $GodotPath"}
$projectRoot=Join-Path $taskRoot 'godot/SkrGuiHost'
$godotArguments=@('--path',$projectRoot,'--rendering-method','mobile','--rendering-driver','vulkan','--',('--text-aa='+$TextAa))
if($SelfTest){$godotArguments+=@('--self-test',('--output='+(Join-Path $taskRoot 'artifacts/godot')))}
& $GodotPath @godotArguments
exit $LASTEXITCODE
