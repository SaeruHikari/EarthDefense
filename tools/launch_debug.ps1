param([switch]$VerifyOnly, [switch]$NewCampaign, [string]$TestProfileRoot = '')
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$debugRoot = Join-Path $projectRoot $(if ($NewCampaign) { '.runtime\new-campaign' } else { '.runtime\debug-cleared' })
if ($TestProfileRoot) {
    if (-not $VerifyOnly) { throw 'TestProfileRoot is reserved for VerifyOnly validation.' }
    $testRoot = [IO.Path]::GetFullPath($TestProfileRoot)
    $allowedTestRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot '.runtime-tests')) + '\'
    if (-not $testRoot.StartsWith($allowedTestRoot,[StringComparison]::OrdinalIgnoreCase)) { throw 'Validation profiles must stay inside .runtime-tests.' }
    $debugRoot = $testRoot
}
New-Item -ItemType Directory -Force -Path $debugRoot | Out-Null
$launcherLog = Join-Path $debugRoot 'launcher.log'
function Quote-Argument([string]$Value) { return '"' + $Value.Replace('"','\"') + '"' }
try {
    . (Join-Path $PSScriptRoot 'managed_runtime.ps1')
    . (Join-Path $PSScriptRoot 'managed_player.ps1')
    $consoleEngine = Get-EarthwardManagedEngine $projectRoot
    $env:APPDATA = Join-Path $debugRoot 'AppData'
    $env:LOCALAPPDATA = Join-Path $debugRoot 'LocalAppData'
    $relativeProfile = 'Godot\app_userdata\EARTHWARD · 地球守望'
    $targetProfile = Join-Path $env:APPDATA $relativeProfile
    New-Item -ItemType Directory -Force -Path $targetProfile,$env:LOCALAPPDATA | Out-Null
    $checkpoint = Join-Path $targetProfile 'earthward_checkpoint.json'
    if (Test-Path -LiteralPath $checkpoint -PathType Leaf) {
        # Save compatibility is intentionally out of scope for this development
        # branch. A profile written by the retired surface-lab schema is
        # discarded in place so --continue always opens a valid current run.
        $current = $false
        try {
            $saved = Get-Content -LiteralPath $checkpoint -Raw -Encoding UTF8 | ConvertFrom-Json
            $buildingNames = @($saved.game.buildings.PSObject.Properties.Name)
            $current = $saved.version -eq 3 -and $saved.game.version -eq 1 -and
                ($buildingNames -notcontains 'lab') -and $saved.game.expedition.version -eq 4
        } catch { $current = $false }
        if (-not $current) {
            Remove-Item -LiteralPath $targetProfile -Recurse -Force
            New-Item -ItemType Directory -Force -Path $targetProfile | Out-Null
            ('Discarded an obsolete development profile; current schema starts clean: ' + $checkpoint) | Set-Content -LiteralPath (Join-Path $debugRoot 'profile-origin.txt') -Encoding UTF8
        }
    }
    # Publish current C# with the standard Release API. Debug/editor test binaries remain independent.
    $engine = Publish-EarthwardManagedPlayer -ProjectRoot $projectRoot
    $importLog = Join-Path $debugRoot 'import.log'
    $importArgs = @('--headless','--editor','--path',$projectRoot,'--import','--quit','--log-file',$importLog)
    & $consoleEngine @importArgs
    $importExitCode = $LASTEXITCODE
    $importText = if (Test-Path -LiteralPath $importLog) { Get-Content -LiteralPath $importLog -Raw -Encoding UTF8 } else { '' }
    if ($importExitCode -ne 0 -or $importText -match 'SCRIPT ERROR|SHADER ERROR|Parse Error|ERROR: Failed to load') { throw ('Current source did not import cleanly. Inspect ' + $importLog) }
    if ($VerifyOnly) {
        ('Verified official Release source player: ' + $engine + ' (project: ' + $projectRoot + ')') | Set-Content -LiteralPath $launcherLog -Encoding UTF8
        Write-Output 'LAUNCH_DEBUG_VERIFIED: current C# ExportRelease publish, editor resource import and isolated profile are ready.'
        exit 0
    }
    $gameLog = Join-Path $debugRoot 'game.log'
    $arguments = @('--log-file',(Quote-Argument $gameLog),'--','--continue')
    # This is the interactive game the user requested, so its window is visible.
    $game = Start-Process -FilePath $engine -ArgumentList $arguments -WorkingDirectory $projectRoot -PassThru
    $game.Id | Set-Content -LiteralPath (Join-Path $debugRoot 'game.pid') -Encoding ASCII
    ('Started source game PID ' + $game.Id + ' from ' + $projectRoot) | Set-Content -LiteralPath $launcherLog -Encoding UTF8
    Write-Output ('Development game started. PID ' + $game.Id)
} catch {
    $_.Exception.Message | Set-Content -LiteralPath $launcherLog -Encoding UTF8
    Write-Error $_.Exception.Message
    exit 1
}
