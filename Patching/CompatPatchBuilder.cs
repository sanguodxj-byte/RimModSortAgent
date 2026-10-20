using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using ModSorterAgent.Agent;
using ModSorterAgent.Reporting;

namespace ModSorterAgent.Patching;

/// <summary>
/// 把 Agent 的补丁方案落成真实 mod 包：
/// - harmony：生成 C# 源码 → 用本机 .NET 10 SDK 对游戏程序集编译 → 输出 DLL；
/// - xml：校验并写入 Patches/*.xml；
/// - order_only / manual：只产出可直接照做的说明，不伪造"可安装补丁"。
/// 生成过程不会安装任何东西，安装由 <see cref="CompatPatchInstaller"/> 在用户确认后执行。
/// </summary>
public sealed class CompatPatchBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _gameDir;
    private readonly string _outputRoot;
    private readonly string _targetVersion;

    public CompatPatchBuilder(string gameDir, string? outputRoot = null, string targetVersion = "1.7")
    {
        _gameDir = gameDir;
        _outputRoot = string.IsNullOrWhiteSpace(outputRoot)
            ? AgentConfigLoader.DefaultPatchOutputDir
            : outputRoot;
        _targetVersion = string.IsNullOrWhiteSpace(targetVersion) ? "1.7" : targetVersion.Trim();
    }

    public CompatPatchResult Build(PlayerConflict conflict, AgentReport report)
    {
        if (!report.CanBuildPatch || report.PatchPlan == null)
            return WritePlanOnly(conflict, report, "该结论没有可自动生成的补丁方案，已输出人工处理说明。");

        return report.PatchPlan.Kind switch
        {
            "harmony" => BuildHarmonyPackage(conflict, report, report.PatchPlan),
            "xml" => BuildXmlPackage(conflict, report, report.PatchPlan),
            "order_only" => WritePlanOnly(conflict, report,
                "加载顺序方案不需要新 mod 包，已输出排序说明。"),
            _ => WritePlanOnly(conflict, report, "Agent 建议人工处理，已输出处理说明。")
        };
    }

    private CompatPatchResult BuildHarmonyPackage(
        PlayerConflict conflict,
        AgentReport report,
        AgentPatchPlan plan)
    {
        var code = CleanCodeBlock(plan.HarmonyCode);
        if (string.IsNullOrWhiteSpace(code))
            return WritePlanOnly(conflict, report, "Agent 未给出可编译的 Harmony 源码。");

        if (!code.Contains("HarmonyPatch", StringComparison.Ordinal))
            return WritePlanOnly(conflict, report, "Agent 给出的源码不包含 HarmonyPatch 标记，已拒绝编译。");

        var harmonyPath = FindHarmonyAssembly();
        if (harmonyPath == null)
            return WritePlanOnly(conflict, report,
                "未找到可用的 0Harmony.dll（Harmony 3），无法编译补丁。请确认创意工坊路径设置。");

        var outputDir = CreateOutputDirectory(conflict);
        var sourceDir = Path.Combine(outputDir, "Source");
        var assemblyName = "ModSorterAgentCompat_" + HashOf(conflict.Key)[..8];
        Directory.CreateDirectory(sourceDir);

        try
        {
            var sourcePath = Path.Combine(sourceDir, "CompatPatch.cs");
            File.WriteAllText(sourcePath, EnsureHarmonyUsings(code), new UTF8Encoding(false));

            var csprojPath = Path.Combine(sourceDir, assemblyName + ".csproj");
            File.WriteAllText(csprojPath, BuildCsproj(assemblyName, harmonyPath), new UTF8Encoding(false));

            var buildOutput = Path.Combine(sourceDir, "build-output");
            var (ok, compilerOutput) = RunDotnetBuild(csprojPath, buildOutput);
            if (!ok)
            {
                WriteReadme(conflict, report, outputDir, plan, compilerOutput);
                return new CompatPatchResult(
                    Success: false,
                    IsModPackage: false,
                    OutputDirectory: outputDir,
                    PackageId: null,
                    AssemblyPath: null,
                    CompilerOutput: compilerOutput,
                    Message: "补丁源码编译失败，已保留源码与编译器输出，未生成可安装 mod 包。",
                    Files: EnumerateFiles(outputDir));
            }

            var builtDll = Path.Combine(buildOutput, assemblyName + ".dll");
            if (!File.Exists(builtDll))
            {
                WriteReadme(conflict, report, outputDir, plan, compilerOutput);
                return new CompatPatchResult(
                    Success: false,
                    IsModPackage: false,
                    OutputDirectory: outputDir,
                    PackageId: null,
                    AssemblyPath: null,
                    CompilerOutput: compilerOutput,
                    Message: "编译命令成功，但没有找到输出 DLL。",
                    Files: EnumerateFiles(outputDir));
            }

            var assemblyDir = Path.Combine(outputDir, _targetVersion, "Assemblies");
            Directory.CreateDirectory(assemblyDir);
            var finalDll = Path.Combine(assemblyDir, assemblyName + ".dll");
            File.Copy(builtDll, finalDll, overwrite: true);

            var packageId = BuildPackageId(conflict);
            WriteAbout(conflict, report, outputDir, packageId, plan);
            WriteXmlFiles(outputDir, plan.Files);
            WriteReadme(conflict, report, outputDir, plan, compilerOutput);
            WriteReportJson(report, outputDir);

            TryDeleteDirectory(buildOutput);
            TryDeleteDirectory(Path.Combine(sourceDir, "obj"));

            return new CompatPatchResult(
                Success: true,
                IsModPackage: true,
                OutputDirectory: outputDir,
                PackageId: packageId,
                AssemblyPath: finalDll,
                CompilerOutput: compilerOutput,
                Message: $"兼容补丁已编译完成：{packageId}",
                Files: EnumerateFiles(outputDir));
        }
        catch (Exception ex)
        {
            return new CompatPatchResult(
                Success: false,
                IsModPackage: false,
                OutputDirectory: outputDir,
                PackageId: null,
                AssemblyPath: null,
                CompilerOutput: null,
                Message: "生成补丁包时出错：" + ex.Message,
                Files: EnumerateFiles(outputDir));
        }
    }

    private CompatPatchResult BuildXmlPackage(
        PlayerConflict conflict,
        AgentReport report,
        AgentPatchPlan plan)
    {
        var validFiles = new List<AgentPatchFile>();
        foreach (var file in plan.Files)
        {
            var content = CleanXmlBlock(file.Content);
            if (!IsValidXml(content))
                return WritePlanOnly(conflict, report,
                    $"Agent 给出的 XML 文件「{file.RelativePath}」无法解析，已拒绝打包。");

            validFiles.Add(file with { Content = content });
        }

        if (validFiles.Count == 0)
            return WritePlanOnly(conflict, report, "Agent 未给出可用的 XML 补丁文件。");

        var outputDir = CreateOutputDirectory(conflict);
        var packageId = BuildPackageId(conflict);

        try
        {
            WriteXmlFiles(outputDir, validFiles);
            WriteAbout(conflict, report, outputDir, packageId, plan);
            WriteReadme(conflict, report, outputDir, plan, null);
            WriteReportJson(report, outputDir);

            return new CompatPatchResult(
                Success: true,
                IsModPackage: true,
                OutputDirectory: outputDir,
                PackageId: packageId,
                AssemblyPath: null,
                CompilerOutput: null,
                Message: $"XML 兼容补丁已生成：{packageId}",
                Files: EnumerateFiles(outputDir));
        }
        catch (Exception ex)
        {
            return new CompatPatchResult(
                Success: false,
                IsModPackage: false,
                OutputDirectory: outputDir,
                PackageId: null,
                AssemblyPath: null,
                CompilerOutput: null,
                Message: "生成 XML 补丁包时出错：" + ex.Message,
                Files: EnumerateFiles(outputDir));
        }
    }

    private CompatPatchResult WritePlanOnly(PlayerConflict conflict, AgentReport report, string message)
    {
        var outputDir = CreateOutputDirectory(conflict);
        try
        {
            WriteReadme(conflict, report, outputDir, report.PatchPlan, null);
            WriteReportJson(report, outputDir);
        }
        catch
        {
            // 说明文件写不出去时仍然返回失败详情
        }

        return new CompatPatchResult(
            Success: false,
            IsModPackage: false,
            OutputDirectory: outputDir,
            PackageId: null,
            AssemblyPath: null,
            CompilerOutput: null,
            Message: message,
            Files: EnumerateFiles(outputDir));
    }

    private string CreateOutputDirectory(PlayerConflict conflict)
    {
        Directory.CreateDirectory(_outputRoot);

        var baseName = "ModSorterAgentCompat_" + HashOf(conflict.Key)[..8];
        var target = Path.Combine(_outputRoot, baseName);
        if (!Directory.Exists(target))
        {
            Directory.CreateDirectory(target);
            return target;
        }

        var marker = Path.Combine(target, ".modsorteragent");
        if (File.Exists(marker))
        {
            TryDeleteDirectory(target);
            Directory.CreateDirectory(target);
            return target;
        }

        target = Path.Combine(_outputRoot, $"{baseName}_{DateTime.Now:yyyyMMdd_HHmmss}");
        Directory.CreateDirectory(target);
        return target;
    }

    private string BuildCsproj(string assemblyName, string harmonyPath)
    {
        var gameDll = Path.Combine(_gameDir, "Assembly-CSharp.dll");
        var unityStubs = Path.Combine(_gameDir, "UnityStubs.dll");
        var unityCompat = Path.Combine(_gameDir, "Waylith.UnityCompat.dll");

        var references = new StringBuilder();
        references.AppendLine($"    <Reference Include=\"Assembly-CSharp\"><HintPath>{Escape(gameDll)}</HintPath><Private>false</Private></Reference>");
        references.AppendLine($"    <Reference Include=\"0Harmony\"><HintPath>{Escape(harmonyPath)}</HintPath><Private>false</Private></Reference>");
        if (File.Exists(unityStubs))
            references.AppendLine($"    <Reference Include=\"UnityStubs\"><HintPath>{Escape(unityStubs)}</HintPath><Private>false</Private></Reference>");
        if (File.Exists(unityCompat))
            references.AppendLine($"    <Reference Include=\"Waylith.UnityCompat\"><HintPath>{Escape(unityCompat)}</HintPath><Private>false</Private></Reference>");

        return $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <OutputType>Library</OutputType>
                    <AssemblyName>{assemblyName}</AssemblyName>
                    <RootNamespace>ModSorterAgentCompat</RootNamespace>
                    <LangVersion>latest</LangVersion>
                    <Nullable>disable</Nullable>
                    <EnableDefaultCompileItems>true</EnableDefaultCompileItems>
                    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
                    <DebugType>none</DebugType>
                    <GenerateDocumentationFile>false</GenerateDocumentationFile>
                  </PropertyGroup>
                  <ItemGroup>
                {references}  </ItemGroup>
                </Project>
                """;
    }

    private static (bool Ok, string Output) RunDotnetBuild(string csprojPath, string outputDir)
    {
        var dotnet = FindDotnet();
        if (dotnet == null)
            return (false, "未找到 dotnet。请安装 .NET 10 SDK，或设置 DOTNET_HOST_PATH 环境变量。");

        var args = $"build \"{csprojPath}\" -c Release -o \"{outputDir}\" --nologo";
        var startInfo = new ProcessStartInfo(dotnet, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(csprojPath) ?? Environment.CurrentDirectory
        };
        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        startInfo.Environment["DOTNET_NOLOGO"] = "1";
        startInfo.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";

        try
        {
            using var process = Process.Start(startInfo);
            if (process == null)
                return (false, "无法启动 dotnet 编译进程。");

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            if (!process.WaitForExit(180_000))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // 超时后杀掉编译进程即可
                }

                return (false, "编译超时（180 秒）。\n" + TrimOutput(stdout + "\n" + stderr));
            }

            var combined = TrimOutput(stdout + "\n" + stderr);
            return (process.ExitCode == 0, combined);
        }
        catch (Exception ex)
        {
            return (false, "启动 dotnet 失败：" + ex.Message);
        }
    }

    private static string TrimOutput(string text)
    {
        text = text.Trim();
        return text.Length <= 12000 ? text : text[..6000] + "\n…（输出已截断）…\n" + text[^6000..];
    }

    private static string? FindDotnet()
    {
        var candidates = new List<string>();

        var host = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (!string.IsNullOrWhiteSpace(host))
            candidates.Add(host);

        candidates.Add(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet", "dotnet.exe"));
        candidates.Add(@"C:\Program Files\dotnet\dotnet.exe");

        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                candidates.Add(Path.Combine(dir.Trim(), "dotnet.exe"));
            }
            catch
            {
                // PATH 中无效段忽略
            }
        }

        var existing = candidates
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // RimWorld 1.7 程序集面向 .NET 10：必须选带 10.x SDK 的 dotnet，
        // 否则会出现 "当前 SDK 不支持 .NET 10.0" 的编译失败。
        foreach (var candidate in existing)
        {
            if (HasNet10Sdk(candidate))
                return candidate;
        }

        return existing.FirstOrDefault();
    }

    private static bool HasNet10Sdk(string dotnet)
    {
        try
        {
            var startInfo = new ProcessStartInfo(dotnet, "--list-sdks")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
                return false;

            var output = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(5000))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // 查询超时后忽略该候选
                }

                return false;
            }

            return output
                .Split('\n')
                .Any(line => line.TrimStart().StartsWith("10.", StringComparison.Ordinal));
        }
        catch
        {
            return false;
        }
    }

    private string? FindHarmonyAssembly()
    {
        var candidates = new List<string>();

        foreach (var modsDir in new[] { Path.Combine(_gameDir, "Mods") })
        {
            try
            {
                if (Directory.Exists(modsDir))
                    candidates.AddRange(Directory.EnumerateFiles(modsDir, "0Harmony.dll", SearchOption.AllDirectories));
            }
            catch
            {
                // 无权限目录忽略
            }
        }

        var workshopDir = Discovery.ModListResolver.PathOptions.WorkshopDir;
        try
        {
            if (Directory.Exists(workshopDir))
            {
                foreach (var modDir in Directory.EnumerateDirectories(workshopDir))
                {
                    foreach (var versionDir in new[] { Path.Combine(modDir, _targetVersion, "Assemblies"), Path.Combine(modDir, "Assemblies") })
                    {
                        var candidate = Path.Combine(versionDir, "0Harmony.dll");
                        if (File.Exists(candidate))
                            candidates.Add(candidate);
                    }
                }
            }
        }
        catch
        {
            // 工坊目录不可读时忽略
        }

        return candidates
            .Where(File.Exists)
            .Select(path => new FileInfo(path))
            .Where(info => info.Length > 500_000)
            .OrderByDescending(info => info.Length)
            .Select(info => info.FullName)
            .FirstOrDefault();
    }

    private void WriteXmlFiles(string outputDir, IReadOnlyList<AgentPatchFile> files)
    {
        foreach (var file in files)
        {
            var relative = file.RelativePath.Replace('/', Path.DirectorySeparatorChar);
            var path = Path.Combine(outputDir, relative);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(path, file.Content, new UTF8Encoding(false));
        }
    }

    private void WriteAbout(
        PlayerConflict conflict,
        AgentReport report,
        string outputDir,
        string packageId,
        AgentPatchPlan plan)
    {
        var aboutDir = Path.Combine(outputDir, "About");
        Directory.CreateDirectory(aboutDir);

        var root = new XElement("ModMetaData",
            new XElement("name", $"兼容修复 · {conflict.ModAName} × {conflict.ModBName}"),
            new XElement("author", "ModSorterAgent"),
            new XElement("packageId", packageId),
            new XElement("supportedVersions", new XElement("li", _targetVersion)),
            new XElement("description",
                $"由 ModSorterAgent 基于真实代码核查生成的兼容补丁。\n\n" +
                $"冲突：{conflict.PairLabel} · {conflict.CategoryLabel}\n" +
                $"目标：{conflict.TargetLabel}\n" +
                $"结论：{report.Headline}\n\n" +
                $"补丁内容：{plan.Summary}\n\n" +
                "请在启用前阅读包内 README.md。"),
            new XElement("loadAfter",
                new XElement("li", conflict.ModAPackageId),
                new XElement("li", conflict.ModBPackageId)));

        if (!string.IsNullOrWhiteSpace(conflict.ModAPackageId))
        {
            root.Add(new XElement("modDependencies",
                new XElement("li",
                    new XElement("packageId", conflict.ModAPackageId),
                    new XElement("displayName", conflict.ModAName)),
                new XElement("li",
                    new XElement("packageId", conflict.ModBPackageId),
                    new XElement("displayName", conflict.ModBName))));
        }

        var doc = new XDocument(new XDeclaration("1.0", "utf-8", null), root);
        doc.Save(Path.Combine(aboutDir, "About.xml"));
        File.WriteAllText(Path.Combine(outputDir, ".modsorteragent"),
            $"{packageId}\n{conflict.Key}\n{DateTime.UtcNow:O}\n", new UTF8Encoding(false));
    }

    private static void WriteReadme(
        PlayerConflict conflict,
        AgentReport report,
        string outputDir,
        AgentPatchPlan? plan,
        string? compilerOutput)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# 兼容修复 · {conflict.ModAName} × {conflict.ModBName}");
        builder.AppendLine();
        builder.AppendLine($"> 生成时间：{DateTime.Now:yyyy-MM-dd HH:mm} · 核查方式：{report.Mode}");
        builder.AppendLine();
        builder.AppendLine("## 冲突");
        builder.AppendLine();
        builder.AppendLine($"- 两个模组：{conflict.PairLabel}");
        builder.AppendLine($"- 问题类型：{conflict.CategoryLabel}");
        builder.AppendLine($"- 涉及内容：{conflict.TargetLabel}");
        builder.AppendLine($"- 严重程度：{conflict.SeverityLabel}");
        builder.AppendLine();
        builder.AppendLine(conflict.What);
        builder.AppendLine();
        builder.AppendLine($"影响：{conflict.Impact}");
        builder.AppendLine();
        builder.AppendLine("## Agent 核查结论");
        builder.AppendLine();
        builder.AppendLine($"- 结论：{report.VerdictLabel}（置信度 {report.Confidence:P0}）");
        builder.AppendLine($"- {report.Headline}");
        builder.AppendLine();
        builder.AppendLine(report.Explanation);
        builder.AppendLine();
        builder.AppendLine($"证据：{report.EvidenceSummary}");
        builder.AppendLine();

        if (report.HiddenRisks.Count > 0)
        {
            builder.AppendLine("## 隐患");
            builder.AppendLine();
            foreach (var risk in report.HiddenRisks)
                builder.AppendLine($"- **{risk.Title}**（{risk.Severity}）：{risk.Detail}");
            builder.AppendLine();
        }

        if (plan != null)
        {
            builder.AppendLine("## 补丁内容");
            builder.AppendLine();
            builder.AppendLine(plan.Summary);
            if (!string.IsNullOrWhiteSpace(plan.RiskNote))
            {
                builder.AppendLine();
                builder.AppendLine($"> 风险：{plan.RiskNote}");
            }
            builder.AppendLine();
        }

        builder.AppendLine("## 安装");
        builder.AppendLine();
        builder.AppendLine("1. 将整个文件夹复制到游戏的 `Mods` 目录（或使用工具内的「安装补丁」）。");
        builder.AppendLine("2. 在模组列表中同时启用两个原模组和本兼容补丁。");
        builder.AppendLine("3. 本补丁已在 About.xml 中声明排在两个原模组之后。");
        builder.AppendLine("4. 启动游戏，确认日志中没有 Harmony 异常。");
        builder.AppendLine();
        builder.AppendLine("## 卸载");
        builder.AppendLine();
        builder.AppendLine("停用本补丁并删除文件夹即可；不会修改两个原模组的文件。");
        builder.AppendLine();

        if (!string.IsNullOrWhiteSpace(compilerOutput))
        {
            builder.AppendLine("## 编译输出");
            builder.AppendLine();
            builder.AppendLine("```");
            builder.AppendLine(compilerOutput);
            builder.AppendLine("```");
            builder.AppendLine();
        }

        File.WriteAllText(Path.Combine(outputDir, "README.md"), builder.ToString(), new UTF8Encoding(false));
    }

    private static void WriteReportJson(AgentReport report, string outputDir)
    {
        try
        {
            File.WriteAllText(
                Path.Combine(outputDir, "agent-report.json"),
                JsonSerializer.Serialize(report, JsonOptions),
                new UTF8Encoding(false));
        }
        catch
        {
            // 报告 JSON 只是追溯用，失败不影响补丁
        }
    }

    private static IReadOnlyList<string> EnumerateFiles(string dir)
    {
        try
        {
            return Directory.Exists(dir)
                ? Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
                    .Select(f => Path.GetRelativePath(dir, f))
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                    .ToList()
                : Array.Empty<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static void TryDeleteDirectory(string dir)
    {
        try
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
        catch
        {
            // 编译产物残留不影响 mod 包本身
        }
    }

    private static string CleanCodeBlock(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return string.Empty;

        var text = code.Trim();
        if (text.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = text.IndexOf('\n');
            if (firstNewline >= 0)
                text = text[(firstNewline + 1)..];
            if (text.EndsWith("```", StringComparison.Ordinal))
                text = text[..^3];
        }

        return text.Trim();
    }

    private static string CleanXmlBlock(string? xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
            return string.Empty;

        var text = xml.Trim();
        if (text.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = text.IndexOf('\n');
            if (firstNewline >= 0)
                text = text[(firstNewline + 1)..];
            if (text.EndsWith("```", StringComparison.Ordinal))
                text = text[..^3];
        }

        return text.Trim();
    }

    private static bool IsValidXml(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return false;
        try
        {
            var doc = XDocument.Parse(content);
            return doc.Root != null;
        }
        catch
        {
            return false;
        }
    }

    private static string EnsureHarmonyUsings(string code)
    {
        if (code.Contains("using HarmonyLib;", StringComparison.Ordinal))
            return code;

        return "using HarmonyLib;\n" + code;
    }

    private static string BuildPackageId(PlayerConflict conflict)
        => "modsorteragent.compat." + HashOf(conflict.Key)[..8].ToLowerInvariant();

    private static string HashOf(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string Escape(string path)
        => System.Security.SecurityElement.Escape(path) ?? path;
}
