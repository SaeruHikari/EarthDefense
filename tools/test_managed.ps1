param([switch]$HeadlessOnly, [switch]$SkipBuild)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'managed_runtime.ps1')
$engine = Get-EarthwardManagedEngine $projectRoot
$originalAppData = $env:APPDATA
$originalLocalAppData = $env:LOCALAPPDATA
Push-Location $projectRoot
try {
    if (-not $SkipBuild) { Build-EarthwardManaged $projectRoot 'Debug' }
    # Native UI checks edit their own settings (for example the coverage
    # multiplier).  Give every suite invocation a fresh profile so a prior
    # test run can never leak state into the next one.
    $runStamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
    foreach ($suite in @('DomainManaged', 'CombatManaged')) {
        $modes = if ($suite -eq 'CombatManaged') { @('--local-shields', '--coverage', '--bombardment') } else { @('') }
        foreach ($mode in $modes) {
            $suffix = if ($mode) { '-' + $mode.TrimStart('-').Replace('-', '_') } else { '' }
            $log = Join-Path $projectRoot ('artifacts\managed-suite-' + $suite + $suffix + '.log')
            $suiteArgs = @('run', '--project', ('tests\' + $suite + '\' + $suite + '.csproj'), '--configuration', 'Release')
            if ($mode) { $suiteArgs += @('--', $mode) }
            & dotnet @suiteArgs 2>&1 | Tee-Object -FilePath $log
            if ($LASTEXITCODE -ne 0) { throw ($suite + $mode + ' regression failed. See ' + $log) }
        }
    }
    $scenes = @('rendering')
    if (-not $HeadlessOnly) { $scenes += @('presentation', 'local_shield_render', 'research_topology', 'research_extensions_ui', 'factory_coverage', 'satellite_launch', 'satellite_launcher_ui') }
    foreach ($scene in $scenes) {
        $profile = Join-Path $projectRoot ('.runtime-tests\managed-suite-' + $runStamp + '-' + $scene)
        $env:APPDATA = Join-Path $profile 'AppData'
        $env:LOCALAPPDATA = Join-Path $profile 'LocalAppData'
        New-Item -ItemType Directory -Force -Path $env:APPDATA, $env:LOCALAPPDATA | Out-Null
        $log = Join-Path $projectRoot ('artifacts\managed-suite-' + $scene + '.log')
        $arguments = @('--path', $projectRoot, ('res://tests/managed_' + $scene + '.tscn'), '--audio-driver', 'Dummy', '--log-file', $log)
        if ($HeadlessOnly) { $arguments += '--headless' }
        else { $arguments += @('--position', '-1700,100') }
        & $engine @arguments
        $output = Get-Content -LiteralPath $log -Raw -Encoding UTF8
        if ($LASTEXITCODE -ne 0 -or $output -match 'SCRIPT ERROR|SHADER ERROR|Parse Error|_FAIL:|EXCEPTION:|ERROR: Failed to load') {
            throw ($scene + ' native regression failed. See ' + $log)
        }
        if ($output -notmatch '0 failures') { throw ($scene + ' did not report successful completion.') }
    }
    Write-Output 'MANAGED_SUITE_PASSED: managed rules, combat and native application checks passed in isolated profiles.'
} finally {
    $env:APPDATA = $originalAppData
    $env:LOCALAPPDATA = $originalLocalAppData
    Pop-Location
}
