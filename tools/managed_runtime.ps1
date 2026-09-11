$ErrorActionPreference='Stop'
function Get-EarthwardManagedEngine {
    param([string]$ProjectRoot)
    $candidateList=@()
    if($env:GODOT_DOTNET_EXE){$candidateList+=$env:GODOT_DOTNET_EXE}
    if($env:GODOT_EXE -and $env:GODOT_EXE -match 'mono|dotnet|net'){ $candidateList+=$env:GODOT_EXE }
    $candidateList+=Join-Path (Split-Path -Parent $ProjectRoot) 'Godot\4.6.1-net\Godot_v4.6.1-stable_mono_win64_console.exe'
    $chosen=$candidateList | Where-Object {Test-Path -LiteralPath $_ -PathType Leaf} | Select-Object -First 1
    if(-not $chosen){throw 'Godot 4.6.1 .NET was not found. Set GODOT_DOTNET_EXE to the mono/.NET engine.'}
    $consoleCandidate=$chosen -replace '_win64\.exe$', '_win64_console.exe'
    if(Test-Path -LiteralPath $consoleCandidate -PathType Leaf){$chosen=$consoleCandidate}
    $version=& $chosen --headless --version
    if($LASTEXITCODE -ne 0 -or $version -notmatch '4\.6\.1.*mono'){throw ('This project requires Godot 4.6.1 .NET; detected: '+$version)}
    return $chosen
}
function Build-EarthwardManaged {
    param([string]$ProjectRoot,[string]$Configuration='Debug')
    if(-not(Get-Command dotnet -ErrorAction SilentlyContinue)){throw '.NET SDK is missing.'}
    $buildLock=New-Object System.Threading.Mutex($false,'Local\EarthwardManagedBuild')
    $acquired=$false
    try {
        $acquired=$buildLock.WaitOne(60000)
        if(-not $acquired){throw 'Another managed build is still running.'}
        & dotnet build (Join-Path $ProjectRoot 'Earthward.csproj') --configuration $Configuration --nologo -p:ValidationSlice=
        if($LASTEXITCODE -ne 0){throw 'C# build failed. The game was not started.'}
    } finally {if($acquired){$buildLock.ReleaseMutex()};$buildLock.Dispose()}
}
