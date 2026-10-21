<#
.SYNOPSIS
    生成 ModSorterAgent 的发布包：编译 → 清洗产物 → 内附说明与许可 → 打包 zip。

.DESCRIPTION
    输出目录为 artifacts\publish\ModSorterAgent-<版本>[-win-x64]。脚本会剔除
    开发机残留与运行非必需文件，并对发布出的可执行文件跑一次自检作为出厂门禁。

.PARAMETER Configuration
    构建配置，默认 Release。

.PARAMETER RuntimeIdentifier
    目标运行时标识，默认 win-x64。

.PARAMETER SelfContained
    打包为自包含发布（用户无需预装 .NET 9 桌面运行时，包体明显增大）。
    默认关闭，即框架依赖发布，用户需自行安装 .NET 9 桌面运行时。

.PARAMETER Zip
    额外生成 zip 压缩包并输出其 SHA256。

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File publish.ps1 -Zip
#>
[CmdletBinding()]
param(
    [string] $Configuration = 'Release',
    [string] $RuntimeIdentifier = 'win-x64',
    [switch] $SelfContained,
    [switch] $Zip
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$project = Join-Path $root 'ModSorterAgent.csproj'

if (-not (Test-Path $project)) {
    throw "未找到项目文件：$project"
}

$version = ([xml](Get-Content -LiteralPath $project -Raw)).SelectSingleNode('//Version').InnerText
if ([string]::IsNullOrWhiteSpace($version)) {
    throw 'ModSorterAgent.csproj 中缺少 Version 属性，无法生成带版本号的发布包。'
}

$suffix = if ($SelfContained) { "-$RuntimeIdentifier-selfcontained" } else { "-$RuntimeIdentifier" }
$packageName = "ModSorterAgent-$version$suffix"
$publishRoot = Join-Path $root 'artifacts\publish'
$stageDir = Join-Path $publishRoot $packageName
$zipPath = Join-Path $publishRoot "$packageName.zip"

Write-Host "[1/5] 清理旧产物：$stageDir"
if (Test-Path $stageDir) { Remove-Item -LiteralPath $stageDir -Recurse -Force }
New-Item -ItemType Directory -Path $stageDir -Force | Out-Null

$flavor = 'framework-dependent'
if ($SelfContained) { $flavor = 'self-contained' }

Write-Host "[2/5] 发布 $version（$flavor）…"
$dotnetCmd = 'dotnet'
$userDotnet = Join-Path $env:USERPROFILE '.dotnet\dotnet.exe'
if (Test-Path $userDotnet) {
    $sdkCheck = & $userDotnet --list-sdks 2>$null | Select-String '^10\.'
    if ($sdkCheck) {
        $dotnetCmd = $userDotnet
        $dotnetRoot = Split-Path $userDotnet -Parent
        $env:DOTNET_ROOT = $dotnetRoot
        $env:PATH = "$dotnetRoot;$env:PATH"
    }
}

$publishArgs = @(
    'publish', $project,
    '-c', $Configuration,
    '-r', $RuntimeIdentifier,
    '-o', $stageDir,
    '--nologo'
)
if ($SelfContained) {
    $publishArgs += @('--self-contained', 'true')
} else {
    $publishArgs += @('--self-contained', 'false')
}

& $dotnetCmd @publishArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish 失败，退出码 $LASTEXITCODE。"
}

Write-Host '[3/5] 清洗产物（剔除开发机残留、个人数据与运行非必需文件）'
# 个人数据与凭据：设置里的 API Key 为 DPAPI 密文，排序档案属个人数据，一律不进发布包。
$personalFiles = @('AppSettings.json', 'SortProfiles.json')
# 调试符号：发布包不带 pdb。
$debugFiles = Get-ChildItem -LiteralPath $stageDir -Filter '*.pdb' -File
# DecompilerServer 以类库方式被进程内引用，其 MCP 宿主可执行文件与宿主配置不参与运行。
$hostFiles = @('DecompilerServer.exe', 'DecompilerServer.deps.json', 'DecompilerServer.runtimeconfig.json')

foreach ($name in ($personalFiles + $hostFiles)) {
    $path = Join-Path $stageDir $name
    if (Test-Path $path) {
        Remove-Item -LiteralPath $path -Force
        Write-Host "  - 移除 $name"
    }
}
foreach ($file in $debugFiles) {
    Remove-Item -LiteralPath $file.FullName -Force
    Write-Host "  - 移除 $($file.Name)"
}

foreach ($doc in @('README.md', 'THIRD-PARTY-NOTICES.md', 'LICENSE')) {
    $source = Join-Path $root $doc
    if (-not (Test-Path $source)) {
        throw "发布包缺少 $doc，许可与运行前提必须随包分发。"
    }
    Copy-Item -LiteralPath $source -Destination $stageDir -Force
}

foreach ($forbidden in $personalFiles) {
    if (Test-Path (Join-Path $stageDir $forbidden)) {
        throw "发布包中仍存在个人数据文件：$forbidden"
    }
}

Write-Host '[4/5] 出厂自检'
$exePath = Join-Path $stageDir 'ModSorterAgent.exe'
& $exePath --selftest
if ($LASTEXITCODE -ne 0) {
    throw "发布包自检失败，退出码 $LASTEXITCODE。"
}

Write-Host '[5/5] 汇总'
$files = Get-ChildItem -LiteralPath $stageDir -Recurse -File
$sizeMb = [math]::Round((($files | Measure-Object -Property Length -Sum).Sum / 1MB), 1)
Write-Host "  目录：$stageDir"
Write-Host "  文件：$($files.Count) 个，合计 $sizeMb MB"
Write-Host "  版本：ModSorterAgent $version（$RuntimeIdentifier）"

if ($Zip) {
    if (Test-Path $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
    Compress-Archive -Path (Join-Path $stageDir '*') -DestinationPath $zipPath -CompressionLevel Optimal
    $hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
    $zipMb = [math]::Round((Get-Item -LiteralPath $zipPath).Length / 1MB, 1)
    Write-Host "  压缩包：$zipPath（$zipMb MB）"
    Write-Host "  SHA256：$hash"
}
