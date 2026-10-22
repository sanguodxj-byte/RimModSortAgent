using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using ModSorterAgent.Discovery;

namespace ModSorterAgent.Verification;

/// <summary>
/// 定位并部署自动化实机验证所需的桥接模组（RimBridgeServer 与 RuntimePatchProbe）。
///
/// 模组版本号无意义：supportedVersions / LoadFolders 版本号不参与任何有效性判定，
/// 仅校验 About.xml 的 packageId 保证标识正确。
/// </summary>
public static class BridgeModLocator
{
    public const string BridgePackageId = "brrainz.rimbridgeserver";
    public const string BridgeModName = "RimBridgeServer";

    public const string RuntimePatchProbePackageId = "modsorter.runtimepatchprobe";
    public const string RuntimePatchProbeModName = "RuntimePatchProbe";
    public const string HarmonyPackageId = "brrainz.harmony";
    public const string HarmonyModName = "Harmony";

    /// <summary>发布包内嵌模组的存放根目录。</summary>
    public static string DefaultBundledModsDir =>
        Path.Combine(AppContext.BaseDirectory, "BundledMods");

    /// <summary>
    /// 在 Mods 目录与工坊订阅池中查找桥接模组根目录。
    /// 优先使用已安装模组；缺失且包内模组存在时自动复制到游戏 Mods 目录。找不到返回 null。
    /// </summary>
    public static string? FindBridgeModDirectory()
        => FindBridgeModDirectory(null, null);

    /// <summary>
    /// 可注入游戏安装根目录与包根目录的桥接模组定位重载，支持测试与自检夹具。
    /// </summary>
    public static string? FindBridgeModDirectory(string? customInstallDir, string? customBundledRoot)
    {
        EnsureModDeployed(HarmonyModName, HarmonyPackageId, customInstallDir, customBundledRoot);
        EnsureModDeployed(RuntimePatchProbeModName, RuntimePatchProbePackageId, customInstallDir, customBundledRoot);
        return EnsureModDeployed(BridgeModName, BridgePackageId, customInstallDir, customBundledRoot);
    }

    /// <summary>
    /// 在 Mods 目录与工坊订阅池中查找运行时补丁探测模组（RuntimePatchProbe）根目录。
    /// 优先使用已安装模组；缺失且包内模组存在时自动复制到游戏 Mods 目录。
    /// </summary>
    public static string? FindRuntimePatchProbeDirectory(string? customInstallDir = null, string? customBundledRoot = null)
    {
        return EnsureModDeployed(RuntimePatchProbeModName, RuntimePatchProbePackageId, customInstallDir, customBundledRoot);
    }

    /// <summary>
    /// 确保指定模组在盘上就绪：已有合法安装直接返回；缺失时且包内存在则部署到游戏 Mods 目录。
    /// 目标目录已存在非本工具安装内容时严禁覆盖。
    /// </summary>
    public static string? EnsureModDeployed(
        string modName,
        string packageId,
        string? customInstallDir = null,
        string? customBundledRoot = null)
    {
        var existing = LocateInstalledModDirectory(modName, packageId, customInstallDir);
        if (existing != null)
            return existing;

        var bundledRoot = !string.IsNullOrWhiteSpace(customBundledRoot)
            ? customBundledRoot
            : DefaultBundledModsDir;

        var sourceDir = Path.Combine(bundledRoot, modName);
        if (!Directory.Exists(sourceDir) || !ValidateModDirectory(sourceDir, packageId))
            return null;

        var installDir = !string.IsNullOrWhiteSpace(customInstallDir)
            ? customInstallDir
            : ModListResolver.PathOptions.RimWorldInstallDir;

        if (string.IsNullOrWhiteSpace(installDir))
            return null;

        var modsRoot = Path.Combine(installDir, "Mods");
        var targetDir = Path.Combine(modsRoot, modName);

        if (Directory.Exists(targetDir))
        {
            if (ValidateModDirectory(targetDir, packageId))
                return targetDir;

            return null;
        }

        var stagingDir = Path.Combine(modsRoot, ".modsorteragent-" + Guid.NewGuid().ToString("N"));
        try
        {
            CopyModFilesSafe(sourceDir, stagingDir);
            if (!ValidateModDirectory(stagingDir, packageId))
                return null;
            Directory.Move(stagingDir, targetDir);
            return targetDir;
        }
        catch
        {
            return null;
        }
        finally
        {
            TryDeleteDirectory(stagingDir);
        }
    }

    /// <summary>
    /// 在游戏 Mods 目录与工坊订阅池中查找指定模组，通过 packageId 判定合法性。
    /// </summary>
    public static string? LocateInstalledModDirectory(string modName, string packageId, string? installDir = null)
    {
        var candidates = new List<string>();

        var resolvedInstallDir = !string.IsNullOrWhiteSpace(installDir)
            ? installDir
            : ModListResolver.PathOptions.RimWorldInstallDir;

        if (!string.IsNullOrWhiteSpace(resolvedInstallDir))
            candidates.Add(Path.Combine(resolvedInstallDir, "Mods", modName));

        candidates.Add(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Steam", "steamapps", "common", "RimWorld", "Mods", modName));

        candidates.Add(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Steam", "steamapps", "common", "RimWorld", "Mods", modName));

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (ValidateModDirectory(candidate, packageId))
                return candidate;
        }

        try
        {
            var workshop = ModListResolver.PathOptions.WorkshopDir;
            if (!string.IsNullOrWhiteSpace(workshop) && Directory.Exists(workshop))
            {
                foreach (var dir in Directory.EnumerateDirectories(workshop))
                {
                    if (ValidateModDirectory(dir, packageId))
                        return dir;
                }
            }
        }
        catch
        {
            // 工坊目录不可读时忽略
        }

        return null;
    }

    /// <summary>
    /// 校验指定目录是否包含具有匹配 packageId 的合法 About.xml。
    /// </summary>
    public static bool ValidateModDirectory(string modDir, string expectedPackageId)
    {
        if (!Directory.Exists(modDir))
            return false;

        var candidateAboutFiles = new[]
        {
            Path.Combine(modDir, "About", "About.xml"),
            Path.Combine(modDir, "1.6", "About", "About.xml"),
            Path.Combine(modDir, "1.7", "About", "About.xml"),
            Path.Combine(modDir, "Common", "About", "About.xml")
        };

        foreach (var about in candidateAboutFiles)
        {
            if (!File.Exists(about))
                continue;

            try
            {
                var doc = XDocument.Load(about);
                var id = doc.Root?.Element("packageId")?.Value;
                if (string.Equals(id?.Trim(), expectedPackageId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            catch
            {
                // XML 损坏时跳过
            }
        }

        return false;
    }

    /// <summary>
    /// 安全复制模组文件至目标目录，剔除源码工程文件与开发构建目录。
    /// </summary>
    public static void CopyModFilesSafe(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        foreach (var dir in Directory.EnumerateDirectories(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relativeDir = Path.GetRelativePath(sourceDir, dir);
            if (IsDevOrSourceDirectory(relativeDir))
                continue;

            var targetSubDir = Path.Combine(targetDir, relativeDir);
            Directory.CreateDirectory(targetSubDir);
        }

        foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relativeFile = Path.GetRelativePath(sourceDir, file);
            if (IsDevOrSourceFile(relativeFile))
                continue;

            var targetFile = Path.Combine(targetDir, relativeFile);
            var parent = Path.GetDirectoryName(targetFile);
            if (!string.IsNullOrEmpty(parent))
                Directory.CreateDirectory(parent);

            File.Copy(file, targetFile, overwrite: true);
        }
    }

    /// <summary>判定相对路径是否命中开发或源码专属目录。</summary>
    public static bool IsDevOrSourceDirectory(string relativePath)
    {
        var parts = relativePath.Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            if (part.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
                part.Equals(".vs", StringComparison.OrdinalIgnoreCase) ||
                part.Equals(".idea", StringComparison.OrdinalIgnoreCase) ||
                part.Equals(".vscode", StringComparison.OrdinalIgnoreCase) ||
                part.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                part.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
                part.Equals("Source", StringComparison.OrdinalIgnoreCase) ||
                part.Equals("src", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>判定相对路径是否命中源码或开发工程文件。</summary>
    public static bool IsDevOrSourceFile(string relativePath)
    {
        if (IsDevOrSourceDirectory(relativePath))
            return true;

        var ext = Path.GetExtension(relativePath);
        if (ext.Equals(".cs", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".csproj", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".sln", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".user", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".suo", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".dotsettings", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".pdb", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // 防御性忽略清理异常
        }
    }
}
