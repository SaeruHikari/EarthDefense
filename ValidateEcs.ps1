[CmdletBinding()]
param(
    [switch]$NativeAot,
    [switch]$Stress,
    [switch]$Benchmarks,
    [switch]$GameBuild,
    [string]$RuntimeIdentifier = 'win-x64',
    [string]$AotFrameworkVersion = '8.0.22'
)

$ErrorActionPreference = 'Stop'

function Invoke-DotNet {
    param([string[]]$CommandArguments)
    & dotnet @CommandArguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet failed with exit code $LASTEXITCODE : $($CommandArguments -join ' ')"
    }
}

Push-Location -LiteralPath $PSScriptRoot
try {
    Invoke-DotNet @('build', 'Sugoi.sln', '-c', 'Release', '--nologo')
    $checks = @('run', '--project', 'tests/SugoiChecks/SugoiChecks.csproj', '-c', 'Release', '--no-build')
    if ($Stress) { $checks += @('--', '--stress') }
    Invoke-DotNet $checks
    Invoke-DotNet @('run', '--project', 'tests/SugoiGeneratorChecks/SugoiGeneratorChecks.csproj', '-c', 'Release', '--no-build')
    $scales = if ($Benchmarks) { @(5000, 10000, 50000) } else { @(5000) }
    foreach ($count in $scales) {
        Invoke-DotNet @('run', '--project', 'samples/Sugoi.DefenseSimulation/Sugoi.DefenseSimulation.csproj', '-c', 'Release', '--no-build', '--', '--entities', "$count", '--frames', '120', '--workers', '4')
    }
    if ($NativeAot) {
        $aotDirectory = Join-Path $PSScriptRoot "artifacts/sugoi/aot-$RuntimeIdentifier"
        Invoke-DotNet @('publish', 'tests/SugoiChecks/SugoiChecks.csproj', '-c', 'Release', '-r', $RuntimeIdentifier,
            '--self-contained', 'true', '-p:PublishAot=true', "-p:RuntimeFrameworkVersion=$AotFrameworkVersion", '-o', $aotDirectory, '--nologo')
        $executable = Join-Path $aotDirectory 'SugoiChecks.exe'
        if (-not (Test-Path -LiteralPath $executable)) {
            throw 'Publish completed, but this Windows runner cannot execute the requested target. Run its native binary on that target.'
        }
        if ($Stress) { & $executable --stress } else { & $executable }
        if ($LASTEXITCODE -ne 0) { throw "Native ECS checks failed with exit code $LASTEXITCODE" }
    }
    if ($GameBuild) { Invoke-DotNet @('build', 'Earthward.csproj', '-c', 'Debug', '--nologo') }
    Write-Host 'All requested Sugoi validations passed.'
}
finally { Pop-Location }
