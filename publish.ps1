<#
.SYNOPSIS
    生成 ModSorterAgent 的发布包：编译 → 清洗产物 → 内附依赖、桥接模组与许可 → 离线 Smoke 验证 → 出厂自检 → 打包 zip。

.DESCRIPTION
    输出目录为 artifacts\publish\ModSorterAgent-<版本>-win-x64[-selfcontained]。
    脚本剔除开发机残留与运行非必需文件，完整内置运行所需的 .NET 10 SDK 工具链、
    Harmony、RimBridgeServer 与 RuntimePatchProbe 桥接模组，并对发布产物执行离线编译 smoke
    与出厂 selftest 作为发布门禁。

.PARAMETER Configuration
    构建配置，默认 Release。

.PARAMETER RuntimeIdentifier
    目标运行时标识，默认 win-x64。

.PARAMETER SelfContained
    打包为自包含发布（用户无需预装 .NET 10 运行时）。
    默认开启自包含发布。

.PARAMETER Zip
    额外生成 zip 压缩包并输出其 SHA256。

.PARAMETER DotnetRoot
    .NET 10 SDK 所在根目录。为空时自动检测用户目录或系统安装目录中的 10.x SDK。

.PARAMETER BridgeModsRoot
    实机桥接模组输入来源根目录，默认 D:\Steam\steamapps\common\RimWorld\Mods。
#>
[CmdletBinding()]
param(
    [string] $Configuration = 'Release',
    [string] $RuntimeIdentifier = 'win-x64',
    [switch] $SelfContained = $true,
    [switch] $Zip,
    [string] $DotnetRoot = '',
    [string] $BridgeModsRoot = 'D:\Steam\steamapps\common\RimWorld\Mods'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-HighestVersionDirectory {
    param(
        [string] $parentPath,
        [string] $filterPattern = '^\d+\.'
    )
    if (-not (Test-Path -LiteralPath $parentPath)) { return $null }
    $subdirs = Get-ChildItem -LiteralPath $parentPath -Directory | Where-Object { $_.Name -match $filterPattern }
    if (-not $subdirs) { return $null }
    $sorted = $subdirs | Sort-Object {
        if ($_.Name -match '^(\d+)(\.(\d+))?(\.(\d+))?(\.(\d+))?') {
            try { return [version]$matches[0] } catch { }
        }
        return [version]'0.0.0.0'
    } -Descending
    return $sorted[0].Name
}

function Copy-FilteredTree {
    param(
        [string] $sourceRoot,
        [string] $destRoot,
        [string[]] $relativePaths
    )
    if (-not (Test-Path -LiteralPath $sourceRoot)) {
        throw "未找到模组来源目录：$sourceRoot"
    }
    if (-not (Test-Path -LiteralPath $destRoot)) {
        New-Item -ItemType Directory -Path $destRoot -Force | Out-Null
    }

    $excludedExts = @('.cs', '.csproj', '.sln', '.user', '.suo', '.dotsettings', '.pdb')
    $excludedDirs = @('source', 'src', 'bin', 'obj', '.git', '.vs', '.idea')

    foreach ($rel in $relativePaths) {
        $sourceItem = Join-Path $sourceRoot $rel
        if (-not (Test-Path -LiteralPath $sourceItem)) {
            throw "模组来源缺少必需项：$sourceItem"
        }
        $targetItem = Join-Path $destRoot $rel

        if ((Get-Item -LiteralPath $sourceItem) -is [System.IO.DirectoryInfo]) {
            New-Item -ItemType Directory -Path $targetItem -Force | Out-Null
            $allFiles = Get-ChildItem -LiteralPath $sourceItem -Recurse -File
            foreach ($file in $allFiles) {
                $fileRel = $file.FullName.Substring($sourceItem.Length).TrimStart('\', '/')
                $parts = $fileRel.Split([char[]]@('\', '/'))
                $skip = $false
                foreach ($p in $parts) {
                    if ($excludedDirs -contains $p.ToLowerInvariant()) {
                        $skip = $true
                        break
                    }
                }
                if ($skip) { continue }
                if ($excludedExts -contains $file.Extension.ToLowerInvariant()) { continue }
                if ($file.Name -like 'readme*') { continue }

                $destFile = Join-Path $targetItem $fileRel
                $destParent = Split-Path -Path $destFile -Parent
                if (-not (Test-Path -LiteralPath $destParent)) {
                    New-Item -ItemType Directory -Path $destParent -Force | Out-Null
                }
                Copy-Item -LiteralPath $file.FullName -Destination $destFile -Force
            }
        } else {
            $destParent = Split-Path -Path $targetItem -Parent
            if (-not (Test-Path -LiteralPath $destParent)) {
                New-Item -ItemType Directory -Path $destParent -Force | Out-Null
            }
            Copy-Item -LiteralPath $sourceItem -Destination $targetItem -Force
        }
    }
}

$root = $PSScriptRoot
$project = Join-Path $root 'ModSorterAgent.csproj'

if (-not (Test-Path -LiteralPath $project)) {
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
$personalFiles = @('AppSettings.json', 'SortProfiles.json')

# 自动解析或校验 .NET 10 SDK 根路径
if ([string]::IsNullOrWhiteSpace($DotnetRoot)) {
    $candidates = @(
        (Join-Path $env:USERPROFILE '.dotnet'),
        (Join-Path $env:ProgramFiles 'dotnet')
    )
    foreach ($cand in $candidates) {
        $candExe = Join-Path $cand 'dotnet.exe'
        if (Test-Path -LiteralPath $candExe) {
            $sdkCheck = & $candExe --list-sdks 2>$null | Select-String '^10\.'
            if ($sdkCheck) {
                $DotnetRoot = $cand
                break
            }
        }
    }
}

if ([string]::IsNullOrWhiteSpace($DotnetRoot) -or (-not (Test-Path -LiteralPath (Join-Path $DotnetRoot 'dotnet.exe')))) {
    throw "未找到包含 .NET 10 SDK 的 DotnetRoot。请通过 -DotnetRoot 参数显式指定。"
}

$dotnetCmd = Join-Path $DotnetRoot 'dotnet.exe'
$env:DOTNET_ROOT = $DotnetRoot
$env:PATH = "$DotnetRoot;$env:PATH"

Write-Host "[1/6] 清理旧产物：$stageDir"
$backupUserFiles = @{}
foreach ($name in $personalFiles) {
    $existing = Join-Path $stageDir $name
    if (Test-Path -LiteralPath $existing) {
        $backupUserFiles[$name] = Get-Content -LiteralPath $existing -Raw
    }
}
if (-not $backupUserFiles.ContainsKey('AppSettings.json')) {
    $binReleaseSettings = Join-Path $root 'bin\Release\net10.0-windows\AppSettings.json'
    $localAppDataSettings = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'ModSorterAgent\AppSettings.json'
    if (Test-Path -LiteralPath $binReleaseSettings) {
        $backupUserFiles['AppSettings.json'] = Get-Content -LiteralPath $binReleaseSettings -Raw
    } elseif (Test-Path -LiteralPath $localAppDataSettings) {
        $backupUserFiles['AppSettings.json'] = Get-Content -LiteralPath $localAppDataSettings -Raw
    }
}
if (Test-Path -LiteralPath $stageDir) { Remove-Item -LiteralPath $stageDir -Recurse -Force }
New-Item -ItemType Directory -Path $stageDir -Force | Out-Null

$flavor = if ($SelfContained) { 'self-contained' } else { 'framework-dependent' }

Write-Host "[2/6] 发布 $version（$flavor）…"
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

Write-Host '[3/6] 清洗产物（剔除开发机残留、个人数据与运行非必需文件）'
# 个人数据与凭据：设置里的 API Key 为 DPAPI 密文，排序档案属个人数据，一律不进发布包。
$debugFiles = Get-ChildItem -LiteralPath $stageDir -Filter '*.pdb' -File
# DecompilerServer 以类库方式被进程内引用，其 MCP 宿主可执行文件与宿主配置不参与运行。
$hostFiles = @('DecompilerServer.exe', 'DecompilerServer.deps.json', 'DecompilerServer.runtimeconfig.json')

foreach ($name in ($personalFiles + $hostFiles)) {
    $path = Join-Path $stageDir $name
    if (Test-Path -LiteralPath $path) {
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
    if (-not (Test-Path -LiteralPath $source)) {
        throw "发布包缺少 $doc，许可与运行前提必须随包分发。"
    }
    Copy-Item -LiteralPath $source -Destination $stageDir -Force
}

foreach ($forbidden in $personalFiles) {
    if (Test-Path -LiteralPath (Join-Path $stageDir $forbidden)) {
        throw "发布包中仍存在个人数据文件：$forbidden"
    }
}

# 内置依赖：GABS 二进制与其许可原件必须随包，缺一即视为发布失败。
$gabsExe = Join-Path $stageDir 'GABS\gabs.exe'
foreach ($required in @($gabsExe,
                        (Join-Path $stageDir 'Licenses\GABS-LICENSE.txt'),
                        (Join-Path $stageDir 'Licenses\DecompilerServer-LICENSE.txt'))) {
    if (-not (Test-Path -LiteralPath $required)) {
        throw "发布包缺少内置依赖或许可原件：$required"
    }
}

Write-Host '[4/6] 部署内置 .NET 10 SDK 工具链与实机桥接模组…'
# 1. 内置 .NET 10 SDK 工具链至 Tools\dotnet
$stageToolsDotnet = Join-Path $stageDir 'Tools\dotnet'
if (-not (Test-Path -LiteralPath $stageToolsDotnet)) {
    New-Item -ItemType Directory -Path $stageToolsDotnet -Force | Out-Null
}

Copy-Item -LiteralPath (Join-Path $DotnetRoot 'dotnet.exe') -Destination $stageToolsDotnet -Force

foreach ($licName in @('LICENSE.txt', 'LICENSE', 'ThirdPartyNotices.txt')) {
    $licSrc = Join-Path $DotnetRoot $licName
    if (Test-Path -LiteralPath $licSrc) {
        Copy-Item -LiteralPath $licSrc -Destination $stageToolsDotnet -Force
    }
}

$highestFxr = Get-HighestVersionDirectory (Join-Path $DotnetRoot 'host\fxr') '^\d+\.'
if (-not $highestFxr) { throw "未在 $DotnetRoot\host\fxr 找到 hostfxr 目录。" }
$srcFxrDir = Join-Path $DotnetRoot "host\fxr\$highestFxr"
$stageFxrDir = Join-Path $stageToolsDotnet "host\fxr\$highestFxr"
New-Item -ItemType Directory -Path $stageFxrDir -Force | Out-Null
Get-ChildItem -LiteralPath $srcFxrDir | Copy-Item -Destination $stageFxrDir -Recurse -Force

$highestRuntime = Get-HighestVersionDirectory (Join-Path $DotnetRoot 'shared\Microsoft.NETCore.App') '^10\.'
if (-not $highestRuntime) { throw "未在 $DotnetRoot\shared\Microsoft.NETCore.App 找到 10.x 运行时。" }
$srcRuntimeDir = Join-Path $DotnetRoot "shared\Microsoft.NETCore.App\$highestRuntime"
$stageRuntimeDir = Join-Path $stageToolsDotnet "shared\Microsoft.NETCore.App\$highestRuntime"
New-Item -ItemType Directory -Path $stageRuntimeDir -Force | Out-Null
Get-ChildItem -LiteralPath $srcRuntimeDir | Copy-Item -Destination $stageRuntimeDir -Recurse -Force

$highestSdk = Get-HighestVersionDirectory (Join-Path $DotnetRoot 'sdk') '^10\.'
if (-not $highestSdk) { throw "未在 $DotnetRoot\sdk 找到 10.x SDK 目录。" }
$srcSdkDir = Join-Path $DotnetRoot "sdk\$highestSdk"
$stageSdkDir = Join-Path $stageToolsDotnet "sdk\$highestSdk"
New-Item -ItemType Directory -Path $stageSdkDir -Force | Out-Null
Get-ChildItem -LiteralPath $srcSdkDir | Copy-Item -Destination $stageSdkDir -Recurse -Force

$highestPack = Get-HighestVersionDirectory (Join-Path $DotnetRoot 'packs\Microsoft.NETCore.App.Ref') '^10\.'
if (-not $highestPack) { throw "未在 $DotnetRoot\packs\Microsoft.NETCore.App.Ref 找到 10.x 目标包。" }
$srcPackDir = Join-Path $DotnetRoot "packs\Microsoft.NETCore.App.Ref\$highestPack"
$stagePackDir = Join-Path $stageToolsDotnet "packs\Microsoft.NETCore.App.Ref\$highestPack"
New-Item -ItemType Directory -Path $stagePackDir -Force | Out-Null
Get-ChildItem -LiteralPath $srcPackDir | Copy-Item -Destination $stagePackDir -Recurse -Force

$srcManifests = Join-Path $DotnetRoot 'sdk-manifests'
if (Test-Path -LiteralPath $srcManifests) {
    Copy-Item -LiteralPath $srcManifests -Destination $stageToolsDotnet -Recurse -Force
}

# 2. 内置实机桥接模组至 BundledMods
$stageBundledMods = Join-Path $stageDir 'BundledMods'
$srcHarmony = Join-Path $BridgeModsRoot 'Harmony'
$srcBridgeServer = Join-Path $BridgeModsRoot 'RimBridgeServer'
$srcRuntimeProbe = Join-Path $BridgeModsRoot 'RuntimePatchProbe'

Copy-FilteredTree -sourceRoot $srcHarmony `
                  -destRoot (Join-Path $stageBundledMods 'Harmony') `
                  -relativePaths @('About', 'LoadFolders.xml', '1.4', '1.5', '1.6', '1.7', 'LICENSE')

Copy-FilteredTree -sourceRoot $srcBridgeServer `
                  -destRoot (Join-Path $stageBundledMods 'RimBridgeServer') `
                  -relativePaths @('About', 'LoadFolders.xml', '1.6', 'LICENSE')

Copy-FilteredTree -sourceRoot $srcRuntimeProbe `
                  -destRoot (Join-Path $stageBundledMods 'RuntimePatchProbe') `
                  -relativePaths @('About', 'LoadFolders.xml', 'BridgeTools')

# 3. 强制校验发布包所有必需文件实体存在（不依赖版本号判定）
$requiredFiles = @(
    (Join-Path $stageDir 'ModSorterAgent.exe'),
    (Join-Path $stageDir 'ModSorterAgent.dll'),
    (Join-Path $stageDir 'ModSorterAgent.runtimeconfig.json'),
    (Join-Path $stageToolsDotnet 'dotnet.exe'),
    (Join-Path $stageToolsDotnet 'ThirdPartyNotices.txt'),
    (Join-Path $stageToolsDotnet "host\fxr\$highestFxr\hostfxr.dll"),
    (Join-Path $stageToolsDotnet "shared\Microsoft.NETCore.App\$highestRuntime\System.Runtime.dll"),
    (Join-Path $stageToolsDotnet "sdk\$highestSdk\dotnet.dll"),
    (Join-Path $stageToolsDotnet "packs\Microsoft.NETCore.App.Ref\$highestPack\ref\net10.0\System.Runtime.dll"),
    (Join-Path $stageBundledMods 'Harmony\About\About.xml'),
    (Join-Path $stageBundledMods 'Harmony\LoadFolders.xml'),
    (Join-Path $stageBundledMods 'Harmony\LICENSE'),
    (Join-Path $stageBundledMods 'Harmony\1.7\Assemblies\0Harmony.dll'),
    (Join-Path $stageBundledMods 'RimBridgeServer\About\About.xml'),
    (Join-Path $stageBundledMods 'RimBridgeServer\LoadFolders.xml'),
    (Join-Path $stageBundledMods 'RimBridgeServer\LICENSE'),
    (Join-Path $stageBundledMods 'RimBridgeServer\1.6\Assemblies\RimBridgeServer.dll'),
    (Join-Path $stageBundledMods 'RuntimePatchProbe\About\About.xml'),
    (Join-Path $stageBundledMods 'RuntimePatchProbe\LoadFolders.xml'),
    (Join-Path $stageBundledMods 'RuntimePatchProbe\BridgeTools\RuntimePatchProbe\RuntimePatchProbe.dll')
)

foreach ($req in $requiredFiles) {
    if (-not (Test-Path -LiteralPath $req)) {
        throw "发布包结构不完整，缺少必需文件：$req"
    }
}

# 4. 离线编译 Smoke 测试（使用临时工作目录隔离 global.json 与 NuGet，验证内置 SDK 可离线编译）
Write-Host '  - 执行内置 .NET SDK 离线编译 Smoke 测试…'
$smokeDir = Join-Path $env:TEMP ("modsorter-smoke-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $smokeDir | Out-Null
$stageDotnetExe = Join-Path $stageToolsDotnet 'dotnet.exe'

$oldDotnetRoot = $env:DOTNET_ROOT
$oldLookup = $env:DOTNET_MULTILEVEL_LOOKUP
$oldWorkloadResolver = $env:MSBuildEnableWorkloadResolver
$oldWorkloadNotify = $env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE

$env:DOTNET_ROOT = $stageToolsDotnet
$env:DOTNET_MULTILEVEL_LOOKUP = '0'
$env:MSBuildEnableWorkloadResolver = 'false'
$env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = 'true'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'

try {
    $nugetConfigContent = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
  </packageSources>
</configuration>
"@
    Set-Content -LiteralPath (Join-Path $smokeDir 'NuGet.Config') -Value $nugetConfigContent -Encoding UTF8

    $smokeCsprojContent = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Library</OutputType>
    <NuGetAudit>false</NuGetAudit>
    <DisableTransitiveFrameworkReferenceDownloads>true</DisableTransitiveFrameworkReferenceDownloads>
  </PropertyGroup>
</Project>
"@
    Set-Content -LiteralPath (Join-Path $smokeDir 'Smoke.csproj') -Value $smokeCsprojContent -Encoding UTF8

    $smokeCsContent = @"
namespace ModSorterAgentSmoke;
public static class SmokeProbe
{
    public static string Ping() => "OK";
}
"@
    Set-Content -LiteralPath (Join-Path $smokeDir 'Smoke.cs') -Value $smokeCsContent -Encoding UTF8

    Push-Location $smokeDir
    try {
        & $stageDotnetExe build 'Smoke.csproj' -c Release --nologo /p:NuGetAudit=false /p:UseSharedCompilation=false
        if ($LASTEXITCODE -ne 0) {
            throw "内置 SDK 离线 Smoke 编译失败，退出码 $LASTEXITCODE。"
        }
        $smokeOutputDll = Join-Path $smokeDir 'bin\Release\net10.0\Smoke.dll'
        if (-not (Test-Path -LiteralPath $smokeOutputDll)) {
            throw "内置 SDK 离线 Smoke 未生成目标程序集：$smokeOutputDll"
        }
    } finally {
        Pop-Location
        & $stageDotnetExe build-server shutdown
    }
} finally {
    Remove-Item -LiteralPath $smokeDir -Recurse -Force -ErrorAction SilentlyContinue
    $env:DOTNET_ROOT = $oldDotnetRoot
    $env:DOTNET_MULTILEVEL_LOOKUP = $oldLookup
    $env:MSBuildEnableWorkloadResolver = $oldWorkloadResolver
    $env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = $oldWorkloadNotify
}

Write-Host '[5/6] 出厂自检'
$exePath = Join-Path $stageDir 'ModSorterAgent.exe'
$proc = Start-Process -FilePath $exePath -ArgumentList '--selftest' -Wait -PassThru -NoNewWindow
if ($proc.ExitCode -ne 0) {
    throw "发布包自检失败，退出码 $($proc.ExitCode)。"
}

Write-Host '[6/6] 汇总与打包'
$files = Get-ChildItem -LiteralPath $stageDir -Recurse -File
$sizeMb = [math]::Round((($files | Measure-Object -Property Length -Sum).Sum / 1MB), 1)
Write-Host "  目录：$stageDir"
Write-Host "  文件：$($files.Count) 个，合计 $sizeMb MB"
Write-Host "  版本：ModSorterAgent $version（$RuntimeIdentifier）"

if ($Zip) {
    if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
    Write-Host "  - 压缩归档至 $zipPath…"
    foreach ($pf in $personalFiles) {
        $target = Join-Path $stageDir $pf
        if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Force }
    }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::CreateFromDirectory($stageDir, $zipPath, [System.IO.Compression.CompressionLevel]::Optimal, $false)
    $hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
    $zipMb = [math]::Round((Get-Item -LiteralPath $zipPath).Length / 1MB, 1)
    Write-Host "  压缩包：$zipPath（$zipMb MB）"
    Write-Host "  SHA256：$hash"
}

# 压缩包生成完毕后恢复本地运行配置，保证本地测试不丢设置与档案
foreach ($kv in $backupUserFiles.GetEnumerator()) {
    $restorePath = Join-Path $stageDir $kv.Key
    Set-Content -LiteralPath $restorePath -Value $kv.Value -Encoding UTF8
    Write-Host "  - 保留本地配置：$($kv.Key)"
}
