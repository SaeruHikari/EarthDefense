param()
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $dotnet) {
    $candidate = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
    if (Test-Path -LiteralPath $candidate -PathType Leaf) { $dotnet = $candidate }
}
if (-not $dotnet) { throw '.NET SDK 未找到。请先安装项目要求的 .NET SDK。' }
$logDirectory = Join-Path $projectRoot 'artifacts'
New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null
$logPath = Join-Path $logDirectory 'validate-data.log'
$profileRoot = Join-Path $projectRoot '.runtime-tests\data-validation'
$previousAppData = $env:APPDATA
$previousLocalAppData = $env:LOCALAPPDATA
$env:APPDATA = Join-Path $profileRoot 'AppData'
$env:LOCALAPPDATA = Join-Path $profileRoot 'LocalAppData'
New-Item -ItemType Directory -Force -Path $env:APPDATA,$env:LOCALAPPDATA | Out-Null
Push-Location $projectRoot
try {
    Write-Host '正在检查真实 CSV：科技、特性、机型、经济、敌人、波次与数值引用……'
    Write-Host '此检查不启动游戏、不运行性能压力测试，也不读取或修改玩家存档。'
    $ErrorActionPreference = 'Continue'
    & $dotnet run --project (Join-Path $projectRoot 'tests\BalanceManaged\BalanceManaged.csproj') --configuration Release -- --validate-only 2>&1 | Tee-Object -FilePath $logPath
    $result = $LASTEXITCODE
    $ErrorActionPreference = 'Stop'
    if ($result -ne 0) { throw ('CSV 检查失败。请根据文件与行号修正后重试。日志：' + $logPath) }
    Write-Host ''
    Write-Host 'CSV 检查通过。重启游戏后会读取新表；已有存档的工程参数仍优先于表格默认值。' -ForegroundColor Green
    Write-Host ('日志：' + $logPath)
} finally {
    $ErrorActionPreference = 'Stop'
    $env:APPDATA = $previousAppData
    $env:LOCALAPPDATA = $previousLocalAppData
    Pop-Location
}
