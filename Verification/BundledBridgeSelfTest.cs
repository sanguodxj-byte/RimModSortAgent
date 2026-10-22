using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace ModSorterAgent.Verification;

/// <summary>
/// 包内桥接模组自动部署、文件过滤、防覆盖边界及双模组临时启用的合成夹具自检。
/// 所有测试均在独立临时目录沙箱中执行，不读写真实游戏目录。
/// </summary>
public static class BundledBridgeSelfTest
{
    public static IReadOnlyList<string> Run()
    {
        var failures = new List<string>();

        Check(failures, "About包名校验-有效标识通过与非法标识拦截", ValidateModDirectoryBehavior);
        Check(failures, "文件过滤-源码与开发构建文件均被排除", CopyFiltersDevAndSourceFiles);
        Check(failures, "自动部署-双模组从包根正确部署至空Mods目录", AutoDeployFromBundledWhenMissing);
        Check(failures, "严禁覆盖-已有安装保持原样且不被覆盖", ExistingInstallIsNotOverwritten);
        Check(failures, "严禁覆盖-已有非本工具或非法同名目录不被覆盖", NonMatchingExistingDirectoryIsNotOverwritten);
        Check(failures, "双模组临时启用-顺序正确且Dispose精确恢复", DualModActivationAndExactRestore);
        Check(failures, "崩溃残留复原-双模组遗留均被正确还原", StaleCrashStateRestoresBothMods);
        Check(failures, "零副作用-定位与部署完全不修改游戏配置", FindBridgeModDirectoryDoesNotTouchModsConfig);

        return failures;
    }

    private static void Check(List<string> failures, string name, Func<string?> probe)
    {
        var error = probe();
        if (error != null)
            failures.Add($"{name}：{error}");
    }

    private static string? ValidateModDirectoryBehavior()
    {
        var sandbox = CreateSandbox();
        try
        {
            var validDir = Path.Combine(sandbox, "ValidMod");
            WriteModMetaData(validDir, "test.author.validmod");

            if (!BridgeModLocator.ValidateModDirectory(validDir, "test.author.validmod"))
                return "有效 packageId 未能通过校验";

            if (BridgeModLocator.ValidateModDirectory(validDir, "test.author.othermod"))
                return "不匹配的 packageId 未被拦截";

            var corruptDir = Path.Combine(sandbox, "CorruptMod");
            var corruptAbout = Path.Combine(corruptDir, "About");
            Directory.CreateDirectory(corruptAbout);
            File.WriteAllText(Path.Combine(corruptAbout, "About.xml"), "<ModMetaData><packageId>");

            if (BridgeModLocator.ValidateModDirectory(corruptDir, "test.author.validmod"))
                return "损坏的 About.xml 未被拦截";

            var emptyDir = Path.Combine(sandbox, "EmptyMod");
            Directory.CreateDirectory(emptyDir);

            if (BridgeModLocator.ValidateModDirectory(emptyDir, "test.author.validmod"))
                return "无 About.xml 的目录未被拦截";

            return null;
        }
        finally
        {
            CleanupSandbox(sandbox);
        }
    }

    private static string? CopyFiltersDevAndSourceFiles()
    {
        var sandbox = CreateSandbox();
        try
        {
            var source = Path.Combine(sandbox, "SourceMod");
            var target = Path.Combine(sandbox, "TargetMod");

            WriteModMetaData(source, BridgeModLocator.BridgePackageId);
            WriteDummyFile(Path.Combine(source, "1.6", "Assemblies", "Test.dll"));
            WriteDummyFile(Path.Combine(source, "BridgeTools", "RuntimePatchProbe", "Probe.dll"));
            WriteDummyFile(Path.Combine(source, "LoadFolders.xml"));
            WriteDummyFile(Path.Combine(source, "README.md"));

            WriteDummyFile(Path.Combine(source, "Source", "Core.cs"));
            WriteDummyFile(Path.Combine(source, "SubDir", "Helper.cs"));
            WriteDummyFile(Path.Combine(source, "Mod.csproj"));
            WriteDummyFile(Path.Combine(source, "Mod.sln"));
            WriteDummyFile(Path.Combine(source, "Mod.csproj.user"));
            WriteDummyFile(Path.Combine(source, "bin", "Debug", "Mod.dll"));
            WriteDummyFile(Path.Combine(source, "obj", "Debug", "Mod.dll"));
            WriteDummyFile(Path.Combine(source, ".git", "config"));
            WriteDummyFile(Path.Combine(source, ".vs", "state.bin"));

            BridgeModLocator.CopyModFilesSafe(source, target);

            if (!File.Exists(Path.Combine(target, "About", "About.xml")))
                return "About\\About.xml 未被复制";
            if (!File.Exists(Path.Combine(target, "1.6", "Assemblies", "Test.dll")))
                return "程序集 dll 未被复制";
            if (!File.Exists(Path.Combine(target, "BridgeTools", "RuntimePatchProbe", "Probe.dll")))
                return "BridgeTools 程序集未被复制";
            if (!File.Exists(Path.Combine(target, "LoadFolders.xml")))
                return "LoadFolders.xml 未被复制";

            if (File.Exists(Path.Combine(target, "Source", "Core.cs")) || Directory.Exists(Path.Combine(target, "Source")))
                return "Source 源码目录或 .cs 文件未被排除";
            if (File.Exists(Path.Combine(target, "SubDir", "Helper.cs")))
                return "子目录中的 .cs 源码文件未被排除";
            if (File.Exists(Path.Combine(target, "Mod.csproj")) || File.Exists(Path.Combine(target, "Mod.sln")))
                return "工程构建描述文件未被排除";
            if (Directory.Exists(Path.Combine(target, "bin")) || Directory.Exists(Path.Combine(target, "obj")))
                return "bin 或 obj 临时构建目录未被排除";
            if (Directory.Exists(Path.Combine(target, ".git")) || Directory.Exists(Path.Combine(target, ".vs")))
                return "版本控制或 IDE 目录未被排除";

            return null;
        }
        finally
        {
            CleanupSandbox(sandbox);
        }
    }

    private static string? AutoDeployFromBundledWhenMissing()
    {
        var sandbox = CreateSandbox();
        try
        {
            var installDir = Path.Combine(sandbox, "RimWorldInstall");
            var bundledRoot = Path.Combine(sandbox, "BundledMods");

            Directory.CreateDirectory(Path.Combine(installDir, "Mods"));

            var bundledBridge = Path.Combine(bundledRoot, BridgeModLocator.BridgeModName);
            var bundledProbe = Path.Combine(bundledRoot, BridgeModLocator.RuntimePatchProbeModName);

            WriteModMetaData(bundledBridge, BridgeModLocator.BridgePackageId);
            WriteDummyFile(Path.Combine(bundledBridge, "1.6", "Assemblies", "RimBridgeServer.dll"));

            WriteModMetaData(bundledProbe, BridgeModLocator.RuntimePatchProbePackageId);
            WriteDummyFile(Path.Combine(bundledProbe, "BridgeTools", "RuntimePatchProbe", "RuntimePatchProbe.dll"));

            var result = BridgeModLocator.FindBridgeModDirectory(installDir, bundledRoot);

            var expectedBridgeTarget = Path.Combine(installDir, "Mods", BridgeModLocator.BridgeModName);
            var expectedProbeTarget = Path.Combine(installDir, "Mods", BridgeModLocator.RuntimePatchProbeModName);

            if (!string.Equals(result, expectedBridgeTarget, StringComparison.OrdinalIgnoreCase))
                return $"返回目录不匹配，期望：{expectedBridgeTarget}，实际：{result}";

            if (!BridgeModLocator.ValidateModDirectory(expectedBridgeTarget, BridgeModLocator.BridgePackageId))
                return "RimBridgeServer 部署后校验失败";

            if (!BridgeModLocator.ValidateModDirectory(expectedProbeTarget, BridgeModLocator.RuntimePatchProbePackageId))
                return "RuntimePatchProbe 未一并部署或校验失败";

            return null;
        }
        finally
        {
            CleanupSandbox(sandbox);
        }
    }

    private static string? ExistingInstallIsNotOverwritten()
    {
        var sandbox = CreateSandbox();
        try
        {
            var installDir = Path.Combine(sandbox, "RimWorldInstall");
            var bundledRoot = Path.Combine(sandbox, "BundledMods");

            var existingBridgeDir = Path.Combine(installDir, "Mods", BridgeModLocator.BridgeModName);
            WriteModMetaData(existingBridgeDir, BridgeModLocator.BridgePackageId);
            var sentinelFile = Path.Combine(existingBridgeDir, "user_custom_marker.txt");
            File.WriteAllText(sentinelFile, "user-custom-content");

            var bundledBridge = Path.Combine(bundledRoot, BridgeModLocator.BridgeModName);
            WriteModMetaData(bundledBridge, BridgeModLocator.BridgePackageId);
            WriteDummyFile(Path.Combine(bundledBridge, "bundled_marker.txt"));

            var found = BridgeModLocator.FindBridgeModDirectory(installDir, bundledRoot);

            if (!string.Equals(found, existingBridgeDir, StringComparison.OrdinalIgnoreCase))
                return "未优先返回已有安装目录";

            if (!File.Exists(sentinelFile))
                return "已有安装目录内的原有文件被删除";

            if (File.Exists(Path.Combine(existingBridgeDir, "bundled_marker.txt")))
                return "已有安装目录被包内文件覆盖写入";

            return null;
        }
        finally
        {
            CleanupSandbox(sandbox);
        }
    }

    private static string? NonMatchingExistingDirectoryIsNotOverwritten()
    {
        var sandbox = CreateSandbox();
        try
        {
            var installDir = Path.Combine(sandbox, "RimWorldInstall");
            var bundledRoot = Path.Combine(sandbox, "BundledMods");

            var existingForeignDir = Path.Combine(installDir, "Mods", BridgeModLocator.BridgeModName);
            WriteModMetaData(existingForeignDir, "someone.else.custommod");
            var sentinelFile = Path.Combine(existingForeignDir, "foreign_data.bin");
            File.WriteAllText(sentinelFile, "foreign");

            var bundledBridge = Path.Combine(bundledRoot, BridgeModLocator.BridgeModName);
            WriteModMetaData(bundledBridge, BridgeModLocator.BridgePackageId);

            var result = BridgeModLocator.EnsureModDeployed(
                BridgeModLocator.BridgeModName,
                BridgeModLocator.BridgePackageId,
                installDir,
                bundledRoot);

            if (result != null)
                return "对于非匹配 packageId 的已有同名目录未拒绝";

            if (!File.Exists(sentinelFile))
                return "非匹配的已有目录内容被破坏";

            if (!BridgeModLocator.ValidateModDirectory(existingForeignDir, "someone.else.custommod"))
                return "非匹配已有目录的元数据被覆盖篡改";

            return null;
        }
        finally
        {
            CleanupSandbox(sandbox);
        }
    }

    private static string? DualModActivationAndExactRestore()
    {
        var sandbox = CreateSandbox();
        try
        {
            var configPath = Path.Combine(sandbox, "ModsConfig.xml");
            const string originalXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <ModsConfigData>
              <version>1.7.4982 rev1280</version>
              <activeMods>
                <li>brrainz.harmony</li>
                <li>ludeon.rimworld</li>
                <li>some.user.mod</li>
              </activeMods>
            </ModsConfigData>
            """;

            File.WriteAllText(configPath, originalXml);
            var initialBytes = File.ReadAllBytes(configPath);

            using (var activator = BridgeModActivator.Activate(configPath))
            {
                if (activator.WasAlreadyActive)
                    return "初始未激活却判定为已激活";

                var activeText = File.ReadAllText(configPath);

                if (!activeText.Contains(BridgeModLocator.BridgePackageId, StringComparison.OrdinalIgnoreCase))
                    return "未包含 RimBridgeServer 包名";

                if (!activeText.Contains(BridgeModLocator.RuntimePatchProbePackageId, StringComparison.OrdinalIgnoreCase))
                    return "未包含 RuntimePatchProbe 包名";

                var bridgeIndex = activeText.IndexOf(BridgeModLocator.BridgePackageId, StringComparison.OrdinalIgnoreCase);
                var probeIndex = activeText.IndexOf(BridgeModLocator.RuntimePatchProbePackageId, StringComparison.OrdinalIgnoreCase);
                var coreIndex = activeText.IndexOf("ludeon.rimworld", StringComparison.OrdinalIgnoreCase);

                if (bridgeIndex < coreIndex)
                    return "RimBridgeServer 顺序位于官方核心之前";

                if (probeIndex < bridgeIndex)
                    return "RuntimePatchProbe 顺序位于 RimBridgeServer 之前";
            }

            var restoredBytes = File.ReadAllBytes(configPath);
            if (!initialBytes.SequenceEqual(restoredBytes))
                return "释放后配置未精确恢复至原始字节";

            if (File.Exists(configPath + ".modsorteragent-livetest"))
                return "释放后残留备份文件未清理";

            return null;
        }
        finally
        {
            CleanupSandbox(sandbox);
        }
    }

    private static string? StaleCrashStateRestoresBothMods()
    {
        var sandbox = CreateSandbox();
        try
        {
            var configPath = Path.Combine(sandbox, "ModsConfig.xml");
            const string originalXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <ModsConfigData>
              <activeMods>
                <li>brrainz.harmony</li>
                <li>ludeon.rimworld</li>
              </activeMods>
            </ModsConfigData>
            """;

            File.WriteAllText(configPath, originalXml);
            var originalBytes = File.ReadAllBytes(configPath);

            var crashedXml = originalXml.Replace(
                "<li>ludeon.rimworld</li>",
                "<li>ludeon.rimworld</li>\n    <li>" + BridgeModLocator.BridgePackageId + "</li>\n    <li>" + BridgeModLocator.RuntimePatchProbePackageId + "</li>");

            File.WriteAllText(configPath, crashedXml);
            File.WriteAllBytes(configPath + ".modsorteragent-livetest", originalBytes);

            BridgeModActivator.RestoreStaleState(configPath);

            var restoredText = File.ReadAllText(configPath);
            if (restoredText.Contains(BridgeModLocator.BridgePackageId, StringComparison.OrdinalIgnoreCase) ||
                restoredText.Contains(BridgeModLocator.RuntimePatchProbePackageId, StringComparison.OrdinalIgnoreCase))
            {
                return "崩溃残留还原后仍存在桥接或探测模组";
            }

            if (File.Exists(configPath + ".modsorteragent-livetest"))
                return "还原后残留备份标记文件未清理";

            return null;
        }
        finally
        {
            CleanupSandbox(sandbox);
        }
    }

    private static string? FindBridgeModDirectoryDoesNotTouchModsConfig()
    {
        var sandbox = CreateSandbox();
        try
        {
            var installDir = Path.Combine(sandbox, "RimWorldInstall");
            var bundledRoot = Path.Combine(sandbox, "BundledMods");

            var configDir = Path.Combine(sandbox, "Config");
            Directory.CreateDirectory(configDir);
            var configPath = Path.Combine(configDir, "ModsConfig.xml");

            const string initialConfig = "<ModsConfigData><activeMods><li>ludeon.rimworld</li></activeMods></ModsConfigData>";
            File.WriteAllText(configPath, initialConfig);
            var initialBytes = File.ReadAllBytes(configPath);

            var bundledBridge = Path.Combine(bundledRoot, BridgeModLocator.BridgeModName);
            var bundledProbe = Path.Combine(bundledRoot, BridgeModLocator.RuntimePatchProbeModName);
            WriteModMetaData(bundledBridge, BridgeModLocator.BridgePackageId);
            WriteModMetaData(bundledProbe, BridgeModLocator.RuntimePatchProbePackageId);

            BridgeModLocator.FindBridgeModDirectory(installDir, bundledRoot);

            var currentBytes = File.ReadAllBytes(configPath);
            if (!initialBytes.SequenceEqual(currentBytes))
                return "查找与部署阶段篡改了配置内容";

            return null;
        }
        finally
        {
            CleanupSandbox(sandbox);
        }
    }

    private static void WriteModMetaData(string modDir, string packageId)
    {
        var aboutDir = Path.Combine(modDir, "About");
        Directory.CreateDirectory(aboutDir);

        var xml = $"""
        <?xml version="1.0" encoding="utf-8"?>
        <ModMetaData>
          <name>{Path.GetFileName(modDir)}</name>
          <packageId>{packageId}</packageId>
        </ModMetaData>
        """;

        File.WriteAllText(Path.Combine(aboutDir, "About.xml"), xml);
    }

    private static void WriteDummyFile(string fullPath)
    {
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllText(fullPath, "placeholder");
    }

    private static string CreateSandbox()
    {
        var path = Path.Combine(Path.GetTempPath(), "ModSorter_BridgeTest_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(path);
        return path;
    }

    private static void CleanupSandbox(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // 清理失败不影响测试结论
        }
    }
}
