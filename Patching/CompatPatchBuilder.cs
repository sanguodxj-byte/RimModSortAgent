using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using ModSorterAgent.Agent;
using ModSorterAgent.Models;
using ModSorterAgent.Reporting;

namespace ModSorterAgent.Patching;

public sealed record CompatPatchEntry(PlayerConflict Conflict, AgentReport Report);

public sealed record CompatPatchSkip(PlayerConflict Conflict, string Reason);

/// <summary>
/// 把 Agent 的补丁方案合并落成同一个 mod 包：
/// 每项 Harmony 补丁独立编译成一个 DLL（避免模型源码里的类名互相覆盖），
/// XML 补丁按冲突分目录写入 Patches/，About.xml 与 README 统一描述全部冲突。
/// </summary>
public sealed class CompatPatchBuilder
{
    private const string BundleFolderName = "ModSorterAgentCompat_Bundle";
    private const string BundlePackageId = "modsorteragent.compat.bundle";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _gameDir;
    private readonly string _outputRoot;
    private readonly string _targetVersion;
    private readonly string _supportedVersion;
    private readonly IReadOnlySet<string>? _approvedAuthorDeclared;
    private readonly IReadOnlyList<ModDescriptor> _mods;

    public CompatPatchBuilder(
        string gameDir,
        string? outputRoot = null,
        string targetVersion = "1.7",
        IReadOnlySet<string>? approvedAuthorDeclared = null,
        IReadOnlyList<ModDescriptor>? mods = null)
    {
        _gameDir = gameDir;
        _outputRoot = string.IsNullOrWhiteSpace(outputRoot)
            ? AgentConfigLoader.DefaultPatchOutputDir
            : outputRoot;
        _targetVersion = string.IsNullOrWhiteSpace(targetVersion) ? "1.7" : targetVersion.Trim();
        _supportedVersion = ToMajorMinor(_targetVersion);
        _approvedAuthorDeclared = approvedAuthorDeclared;
        _mods = mods ?? Array.Empty<ModDescriptor>();
    }

    public CompatPatchResult Build(
        IReadOnlyList<CompatPatchEntry> entries,
        IReadOnlyList<CompatPatchSkip>? skipped = null)
    {
        var notes = new List<string>();
        var skippedCount = 0;

        if (skipped != null)
        {
            notes.AddRange(skipped.Select(s => $"{s.Conflict.PairLabel}：{s.Reason}"));
            skippedCount += skipped.Count;
        }

        var candidates = new List<CompatPatchEntry>();
        foreach (var entry in entries)
        {
            if (CompatPatchEligibility.CanBundle(entry.Conflict, entry.Report, _approvedAuthorDeclared))
            {
                candidates.Add(entry);
            }
            else
            {
                notes.Add($"{entry.Conflict.PairLabel}：{CompatPatchEligibility.ExplainNotEligible(entry.Conflict, entry.Report, _approvedAuthorDeclared)}");
                skippedCount++;
            }
        }

        if (candidates.Count == 0)
        {
            return new CompatPatchResult(
                Success: false,
                IsModPackage: false,
                OutputDirectory: string.Empty,
                PackageId: null,
                AssemblyPath: null,
                CompilerOutput: null,
                Message: "没有符合条件的冲突可生成补丁。",
                Files: Array.Empty<string>())
            {
                SkippedReasons = notes
            };
        }

        var outputDir = CreateBundleDirectory();
        var harmonyPath = candidates.Any(e => e.Report.PatchPlan!.Kind == "harmony")
            ? FindHarmonyAssembly()
            : null;

        var built = new List<BuiltEntry>();
        var compilerOutputs = new List<string>();
        var failures = new List<CompatPatchFailure>();

        foreach (var entry in candidates)
        {
            var plan = entry.Report.PatchPlan!;
            var hash = HashOf(entry.Conflict.Key)[..12];
            string? dllPath = null;
            var ok = true;

            if (plan.Kind == "harmony")
            {
                if (harmonyPath == null)
                {
                    notes.Add($"{entry.Conflict.PairLabel}：未找到 0Harmony.dll，Harmony 补丁未编译");
                    failures.Add(new CompatPatchFailure(
                        entry.Conflict.Key,
                        entry.Conflict.PairLabel,
                        "未找到 0Harmony.dll，Harmony 补丁未编译"));
                    ok = false;
                }
                else
                {
                    var compile = CompileHarmony(entry, plan, outputDir, hash, harmonyPath);
                    if (compile.Success)
                    {
                        dllPath = compile.DllPath;
                    }
                    else
                    {
                        notes.Add($"{entry.Conflict.PairLabel}：Harmony 补丁编译失败");
                        if (!string.IsNullOrWhiteSpace(compile.Output))
                        {
                            compilerOutputs.Add($"[{entry.Conflict.PairLabel}]\n{compile.Output}");
                            WriteBuildLog(outputDir, hash, compile.Output!);
                        }
                        failures.Add(new CompatPatchFailure(
                            entry.Conflict.Key,
                            entry.Conflict.PairLabel,
                            compile.Output ?? "Harmony 补丁编译失败"));
                        ok = false;
                    }
                }
            }

            // Harmony 编译失败时不再写入附带的 XML，避免包里留下没有归属的补丁文件。
            var xmlWritten = false;
            if (ok)
            {
                xmlWritten = WritePlanXml(entry, plan, outputDir, hash, notes, required: plan.Kind == "xml");
                if (plan.Kind == "xml" && !xmlWritten)
                    ok = false;
            }

            if (ok)
                built.Add(new BuiltEntry(entry, dllPath, xmlWritten));
            else
                skippedCount++;
        }

        if (built.Count == 0)
        {
            var compilerOutput = compilerOutputs.Count > 0 ? string.Join("\n\n", compilerOutputs) : null;
            WriteFailureReadme(outputDir, notes, compilerOutput);
            return new CompatPatchResult(
                Success: false,
                IsModPackage: false,
                OutputDirectory: outputDir,
                PackageId: null,
                AssemblyPath: null,
                CompilerOutput: compilerOutput,
                Message: "所有补丁都未能生成，已保留编译输出。",
                Files: EnumerateFiles(outputDir))
            {
                SkippedReasons = notes,
                Failures = failures
            };
        }

        WriteAbout(outputDir, built);
        WriteReadme(outputDir, built, notes);
        WriteReports(outputDir, built, notes);

        var message = skippedCount == 0
            ? $"合并兼容补丁已生成：包含 {built.Count} 项冲突"
            : $"合并兼容补丁已生成：包含 {built.Count} 项冲突，{skippedCount} 项未并入";

        return new CompatPatchResult(
            Success: true,
            IsModPackage: true,
            OutputDirectory: outputDir,
            PackageId: BundlePackageId,
            AssemblyPath: built.Select(b => b.DllPath).FirstOrDefault(p => !string.IsNullOrWhiteSpace(p)),
            CompilerOutput: compilerOutputs.Count > 0 ? string.Join("\n\n", compilerOutputs) : null,
            Message: message,
            Files: EnumerateFiles(outputDir))
        {
            IncludedConflictKeys = built.Select(b => b.Entry.Conflict.Key).ToList(),
            SkippedReasons = notes,
            Failures = failures
        };
    }

    private sealed record BuiltEntry(CompatPatchEntry Entry, string? DllPath, bool HasXml);

    private (bool Success, string? DllPath, string? Output) CompileHarmony(
        CompatPatchEntry entry,
        AgentPatchPlan plan,
        string outputDir,
        string hash,
        string harmonyPath)
    {
        var code = CleanCodeBlock(plan.HarmonyCode);
        if (string.IsNullOrWhiteSpace(code))
            return (false, null, "Agent 未给出可编译的 Harmony 源码。");

        var hasHarmony = code.Contains("Harmony", StringComparison.Ordinal);
        var hasPatchOperation = code.Contains("HarmonyPatch", StringComparison.Ordinal) ||
                                code.Contains(".Patch(", StringComparison.Ordinal) ||
                                code.Contains("Unpatch(", StringComparison.Ordinal);
        if (!hasHarmony || !hasPatchOperation)
            return (false, null, "Agent 给出的源码不包含 Harmony 补丁注册，已拒绝编译。");

        var assemblyName = "ModSorterAgentCompat_" + hash;
        var sourceDir = Path.Combine(outputDir, "Source", hash);
        Directory.CreateDirectory(sourceDir);

        try
        {
            var sourcePath = Path.Combine(sourceDir, "CompatPatch.cs");
            File.WriteAllText(sourcePath, EnsureHarmonyUsings(code), new UTF8Encoding(false));

            var csprojPath = Path.Combine(sourceDir, assemblyName + ".csproj");
            var modReferences = CollectModReferences(entry.Conflict);
            File.WriteAllText(
                csprojPath,
                BuildCsproj(assemblyName, harmonyPath, modReferences),
                new UTF8Encoding(false));

            var buildOutput = Path.Combine(sourceDir, "build-output");
            var (ok, compilerOutput) = RunDotnetBuild(csprojPath, buildOutput);
            if (!ok)
                return (false, null, compilerOutput);

            var builtDll = Path.Combine(buildOutput, assemblyName + ".dll");
            if (!File.Exists(builtDll))
                return (false, null, "编译命令成功，但没有找到输出 DLL。\n" + compilerOutput);

            var assemblyDir = Path.Combine(outputDir, "Assemblies");
            Directory.CreateDirectory(assemblyDir);
            var finalDll = Path.Combine(assemblyDir, assemblyName + ".dll");
            File.Copy(builtDll, finalDll, overwrite: true);

            TryDeleteDirectory(buildOutput);
            TryDeleteDirectory(Path.Combine(sourceDir, "obj"));

            return (true, finalDll, compilerOutput);
        }
        catch (Exception ex)
        {
            return (false, null, "生成 Harmony 补丁时出错：" + ex.Message);
        }
    }

    private bool WritePlanXml(
        CompatPatchEntry entry,
        AgentPatchPlan plan,
        string outputDir,
        string hash,
        List<string> notes,
        bool required)
    {
        if (plan.Files.Count == 0)
        {
            if (required)
                notes.Add($"{entry.Conflict.PairLabel}：Agent 未给出可用的 XML 补丁文件");
            return false;
        }

        var valid = new List<(string Relative, string Content)>();
        var allValid = true;

        for (var i = 0; i < plan.Files.Count; i++)
        {
            var file = plan.Files[i];
            var content = CleanXmlBlock(file.Content);
            if (!IsValidXml(content))
            {
                notes.Add($"{entry.Conflict.PairLabel}：XML「{file.RelativePath}」无法解析");
                allValid = false;
                continue;
            }

            var name = SafeXmlFileName(file.RelativePath, i);
            valid.Add((Path.Combine("Patches", hash, $"{i:00}_{name}"), content));
        }

        if (!allValid)
        {
            notes.Add(required
                ? $"{entry.Conflict.PairLabel}：XML 补丁存在无效文件，整项未并入"
                : $"{entry.Conflict.PairLabel}：附带的 XML 文件存在无效内容，已忽略");
            return false;
        }

        foreach (var (relative, content) in valid)
        {
            var path = Path.Combine(outputDir, relative);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(path, content, new UTF8Encoding(false));
        }

        return valid.Count > 0;
    }

    private string CreateBundleDirectory()
    {
        Directory.CreateDirectory(_outputRoot);

        var target = Path.Combine(_outputRoot, BundleFolderName);
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

        target = Path.Combine(_outputRoot, $"{BundleFolderName}_{DateTime.Now:yyyyMMdd_HHmmss}");
        Directory.CreateDirectory(target);
        return target;
    }

    private void WriteAbout(string outputDir, IReadOnlyList<BuiltEntry> built)
    {
        var aboutDir = Path.Combine(outputDir, "About");
        Directory.CreateDirectory(aboutDir);

        var relatedIds = built
            .SelectMany(b => new[] { b.Entry.Conflict.ModAPackageId, b.Entry.Conflict.ModBPackageId })
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var included = string.Join("\n", built.Select(b =>
            $"· {b.Entry.Conflict.PairLabel} · {b.Entry.Conflict.CategoryLabel}"));

        var root = new XElement("ModMetaData",
            new XElement("name", "ModSorterAgent 合并兼容补丁"),
            new XElement("author", "ModSorterAgent"),
            new XElement("packageId", BundlePackageId),
            new XElement("supportedVersions", new XElement("li", _supportedVersion)),
            new XElement("description",
                "由 ModSorterAgent 基于真实代码核查生成的合并兼容补丁。\n\n" +
                $"包含 {built.Count} 项冲突：\n{included}\n\n" +
                "请在启用前阅读包内 README.md。"),
            new XElement("loadAfter", relatedIds.Select(id => new XElement("li", id))));

        var doc = new XDocument(new XDeclaration("1.0", "utf-8", null), root);
        doc.Save(Path.Combine(aboutDir, "About.xml"));

        var keys = string.Join("\n", built.Select(b => b.Entry.Conflict.Key));
        File.WriteAllText(
            Path.Combine(outputDir, ".modsorteragent"),
            $"{BundlePackageId}\n{keys}\n{DateTime.UtcNow:O}\n",
            new UTF8Encoding(false));
    }

    private void WriteReadme(string outputDir, IReadOnlyList<BuiltEntry> built, IReadOnlyList<string> notes)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# ModSorterAgent 合并兼容补丁");
        builder.AppendLine();
        builder.AppendLine($"> 生成时间：{DateTime.Now:yyyy-MM-dd HH:mm} · 包含 {built.Count} 项冲突");
        builder.AppendLine();
        builder.AppendLine("## 包含的冲突");
        builder.AppendLine();

        for (var i = 0; i < built.Count; i++)
        {
            var item = built[i];
            var conflict = item.Entry.Conflict;
            var report = item.Entry.Report;
            var plan = report.PatchPlan;

            builder.AppendLine($"### {i + 1}. {conflict.PairLabel}");
            builder.AppendLine();
            builder.AppendLine($"- 问题类型：{conflict.CategoryLabel}");
            builder.AppendLine($"- 涉及内容：{conflict.TargetLabel}");
            builder.AppendLine($"- 严重程度：{conflict.SeverityLabel}");
            builder.AppendLine($"- 核查方式：{report.Mode}");
            builder.AppendLine($"- 结论：{report.VerdictLabel}（置信度 {report.Confidence:P0}）");
            builder.AppendLine();
            builder.AppendLine(report.Headline);
            builder.AppendLine();
            builder.AppendLine(report.Explanation);
            builder.AppendLine();
            builder.AppendLine(conflict.What);
            builder.AppendLine();

            if (report.HiddenRisks.Count > 0)
            {
                builder.AppendLine("隐患：");
                foreach (var risk in report.HiddenRisks)
                    builder.AppendLine($"- **{risk.Title}**（{risk.Severity}）：{risk.Detail}");
                builder.AppendLine();
            }

            if (plan != null)
            {
                builder.AppendLine($"补丁内容：{plan.Summary}");
                if (!string.IsNullOrWhiteSpace(plan.RiskNote))
                    builder.AppendLine($"\n> 风险：{plan.RiskNote}");
                builder.AppendLine();
            }
        }

        if (notes.Count > 0)
        {
            builder.AppendLine("## 未并入与生成提示");
            builder.AppendLine();
            foreach (var note in notes)
                builder.AppendLine($"- {note}");
            builder.AppendLine();
        }

        builder.AppendLine("## 安装");
        builder.AppendLine();
        builder.AppendLine("1. 将整个文件夹复制到游戏的 `Mods` 目录（或使用工具内的「安装补丁」）。");
        builder.AppendLine("2. 在模组列表中启用本补丁；上面列出的原模组需要同时启用。");
        builder.AppendLine("3. 本补丁已在 About.xml 中声明排在相关原模组之后。");
        builder.AppendLine("4. 启动游戏，确认日志中没有 Harmony 异常。");
        builder.AppendLine();
        builder.AppendLine("## 卸载");
        builder.AppendLine();
        builder.AppendLine("停用本补丁并删除文件夹即可；不会修改原模组的文件。");
        builder.AppendLine();

        File.WriteAllText(Path.Combine(outputDir, "README.md"), builder.ToString(), new UTF8Encoding(false));
    }

    private static void WriteFailureReadme(
        string outputDir,
        IReadOnlyList<string> notes,
        string? compilerOutput)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# 合并兼容补丁生成失败");
        builder.AppendLine();

        if (notes.Count > 0)
        {
            builder.AppendLine("## 原因");
            builder.AppendLine();
            foreach (var note in notes)
                builder.AppendLine($"- {note}");
            builder.AppendLine();
        }

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

    private static void WriteReports(
        string outputDir,
        IReadOnlyList<BuiltEntry> built,
        IReadOnlyList<string> notes)
    {
        try
        {
            var payload = new
            {
                GeneratedAtUtc = DateTime.UtcNow,
                PackageId = BundlePackageId,
                Included = built.Select(b => new { b.Entry.Conflict, b.Entry.Report }).ToList(),
                Notes = notes
            };

            File.WriteAllText(
                Path.Combine(outputDir, "agent-reports.json"),
                JsonSerializer.Serialize(payload, JsonOptions),
                new UTF8Encoding(false));
        }
        catch
        {
            // 报告 JSON 只是追溯用，失败不影响补丁
        }
    }

    private static void WriteBuildLog(string outputDir, string hash, string content)
    {
        try
        {
            var dir = Path.Combine(outputDir, "BuildLogs");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, hash + ".log"), content, new UTF8Encoding(false));
        }
        catch
        {
            // 编译日志写不出去时仍把输出放进结果
        }
    }

    /// <summary>
    /// 兼容补丁经常要同时调用冲突双方的类型，所以把这两个模组的 DLL 也加入引用。
    /// 引擎与 Harmony 由固定引用提供，重复引用只会造成 MSB3277 冲突。
    /// </summary>
    private IReadOnlyList<string> CollectModReferences(PlayerConflict conflict)
    {
        if (_mods.Count == 0)
            return Array.Empty<string>();

        var wanted = new[] { conflict.ModAPackageId, conflict.ModBPackageId };
        var paths = new List<string>();
        foreach (var mod in _mods)
        {
            if (!wanted.Contains(mod.PackageId, StringComparer.OrdinalIgnoreCase))
                continue;

            foreach (var path in mod.AssemblyPaths)
            {
                if (File.Exists(path) && !IsEngineReference(Path.GetFileNameWithoutExtension(path)))
                    paths.Add(path);
            }
        }

        return paths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool IsEngineReference(string assemblyName)
        => assemblyName.Equals("Assembly-CSharp", StringComparison.OrdinalIgnoreCase) ||
           assemblyName.StartsWith("0Harmony", StringComparison.OrdinalIgnoreCase) ||
           assemblyName.StartsWith("Harmony", StringComparison.OrdinalIgnoreCase) ||
           assemblyName.StartsWith("UnityEngine", StringComparison.OrdinalIgnoreCase) ||
           assemblyName.StartsWith("Unity.", StringComparison.OrdinalIgnoreCase) ||
           assemblyName.Equals("UnityStubs", StringComparison.OrdinalIgnoreCase) ||
           assemblyName.Equals("Waylith.UnityCompat", StringComparison.OrdinalIgnoreCase) ||
           assemblyName.StartsWith("System.", StringComparison.OrdinalIgnoreCase) ||
           assemblyName.Equals("mscorlib", StringComparison.OrdinalIgnoreCase) ||
           assemblyName.Equals("netstandard", StringComparison.OrdinalIgnoreCase);

    private string BuildCsproj(string assemblyName, string harmonyPath, IReadOnlyList<string> modReferences)
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

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Assembly-CSharp", "0Harmony", "UnityStubs", "Waylith.UnityCompat"
        };
        foreach (var path in modReferences)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            if (!seen.Add(name))
                continue;

            references.AppendLine($"    <Reference Include=\"{Escape(name)}\"><HintPath>{Escape(path)}</HintPath><Private>false</Private></Reference>");
        }

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

        var nugetConfig = Path.Combine(Path.GetDirectoryName(csprojPath)!, "NuGet.Config");
        File.WriteAllText(nugetConfig, "<configuration><packageSources><clear /></packageSources></configuration>", new UTF8Encoding(false));
        var args = $"build \"{csprojPath}\" -c Release -o \"{outputDir}\" --nologo --configfile \"{nugetConfig}\" -p:NuGetAudit=false";
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
        startInfo.Environment["MSBuildEnableWorkloadResolver"] = "false";
        startInfo.Environment["DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE"] = "true";

        var dotnetDir = Path.GetDirectoryName(dotnet);
        if (!string.IsNullOrWhiteSpace(dotnetDir))
        {
            startInfo.Environment["DOTNET_ROOT"] = dotnetDir;
            var currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            startInfo.Environment["PATH"] = dotnetDir + Path.PathSeparator + currentPath;
        }

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
        var bundled = Path.Combine(AppContext.BaseDirectory, "Tools", "dotnet", "dotnet.exe");
        if (File.Exists(bundled))
            return bundled;

        var candidates = new List<string>();

        var host = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (!string.IsNullOrWhiteSpace(host))
            candidates.Add(host);

        candidates.Add(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet", "dotnet.exe"));
        candidates.Add(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "dotnet.exe"));

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

    private static string ToMajorMinor(string version)
    {
        var token = version.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? version;
        return System.Version.TryParse(token, out var parsed) && parsed.Major > 0
            ? $"{parsed.Major}.{parsed.Minor}"
            : "1.7";
    }

    private string? FindHarmonyAssembly()
    {
        var bundled = Path.Combine(AppContext.BaseDirectory, "BundledMods", "Harmony", "1.7", "Assemblies", "0Harmony.dll");
        if (File.Exists(bundled))
            return bundled;
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
                    foreach (var versionDir in new[]
                             {
                                 Path.Combine(modDir, _supportedVersion, "Assemblies"),
                                 Path.Combine(modDir, "Assemblies")
                             }.Distinct(StringComparer.OrdinalIgnoreCase))
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

    private static string SafeXmlFileName(string? raw, int index)
    {
        var fileName = Path.GetFileName((raw ?? string.Empty).Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(fileName))
            fileName = $"Patch_{index + 1}.xml";

        foreach (var invalid in Path.GetInvalidFileNameChars())
            fileName = fileName.Replace(invalid, '_');

        if (!fileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            fileName += ".xml";

        return fileName;
    }

    private static string EnsureHarmonyUsings(string code)
    {
        if (code.Contains("using HarmonyLib;", StringComparison.Ordinal))
            return code;

        return "using HarmonyLib;\n" + code;
    }

    private static string HashOf(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string Escape(string path)
        => System.Security.SecurityElement.Escape(path) ?? path;
}
