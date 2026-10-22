using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using ModSorterAgent.Discovery;
using ModSorterAgent.Models;
using ModSorterAgent.Reporting;
using ModSorterAgent.Services;
using ModSorterAgent.UI;

namespace ModSorterAgent;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        var launchGui = args.Length == 0 || args.Any(a => a.Equals("--gui", StringComparison.OrdinalIgnoreCase));
        if (!launchGui || args.Any(a => a.Equals("--selftest", StringComparison.OrdinalIgnoreCase)))
            EnsureConsoleAttached();

        Console.OutputEncoding = Encoding.UTF8;

        if (args.Any(a => a.Equals("--selftest", StringComparison.OrdinalIgnoreCase)))
            return RunSelfTests();

        ModSorterAgent.Verification.BridgeModActivator.RestoreStaleState();

        if (args.Any(a => a.Equals("--check-bridge", StringComparison.OrdinalIgnoreCase)))
            return RunBridgeCheck(args);

        if (args.Any(a => a.Equals("--live-verify", StringComparison.OrdinalIgnoreCase)))
            return RunAgentLiveVerify(args).GetAwaiter().GetResult();

        launchGui = args.Length == 0 || args.Any(a => a.Equals("--gui", StringComparison.OrdinalIgnoreCase));
        string? autoScreenshotPath = null;
        var exitAfterScreenshot = false;

        for (var i = 0; i < args.Length; i++)
        {
            if (args[i].Equals("--screenshot", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                autoScreenshotPath = args[++i];
                launchGui = true;
            }
            else if (args[i].Equals("--exit-after-screenshot", StringComparison.OrdinalIgnoreCase))
            {
                exitAfterScreenshot = true;
            }
        }

        if (launchGui)
        {
            var includeWorkshopForGui = args.Any(a => a.Equals("--include-workshop", StringComparison.OrdinalIgnoreCase));
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm(autoScreenshotPath, exitAfterScreenshot, includeWorkshopForGui));
            return 0;
        }

        string? modsConfigPath = null;
        string? inputFilePath = null;
        string? rawModsList = null;
        string? gameVersion = null;
        string? outputJsonPath = null;
        string? outputReportPath = null;
        string? exportModsConfigPath = null;
        string? workshopDir = null;
        string? exportRmlPath = null;
        var includeWorkshop = false;
        var verifyGabs = true;
        var loadLocal = false;
        var loadWorkshop = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--local":
                    loadLocal = true;
                    break;
                case "--workshop":
                    loadWorkshop = true;
                    break;
                case "--from-mods-config":
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                        modsConfigPath = args[++i];
                    else
                        modsConfigPath = ModListResolver.DefaultModsConfigPath;
                    break;
                case "--input-file":
                case "--input":
                    if (i + 1 < args.Length)
                        inputFilePath = args[++i];
                    break;
                case "--mods":
                    if (i + 1 < args.Length)
                        rawModsList = args[++i];
                    break;
                case "--game-version":
                    if (i + 1 < args.Length)
                        gameVersion = args[++i];
                    break;
                case "--output-json":
                    if (i + 1 < args.Length)
                        outputJsonPath = args[++i];
                    break;
                case "--output-report":
                    if (i + 1 < args.Length)
                        outputReportPath = args[++i];
                    break;
                case "--export-mods-config":
                    if (i + 1 < args.Length)
                        exportModsConfigPath = args[++i];
                    break;
                case "--no-verify-gabs":
                    verifyGabs = false;
                    break;
                case "--verify-gabs":
                    verifyGabs = true;
                    break;
                case "--include-workshop":
                    includeWorkshop = true;
                    break;
                case "--workshop-dir":
                    if (i + 1 < args.Length)
                        workshopDir = args[++i];
                    includeWorkshop = true;
                    break;
                case "--export-rml":
                    if (i + 1 < args.Length)
                        exportRmlPath = args[++i];
                    break;
            }
        }

        var resolver = new ModListResolver();
        List<string> requestedTokens;
        var knownExpansions = new List<string>();
        var detectedVersion = "1.7";

        if (loadLocal)
        {
            requestedTokens = resolver.GetLocalModTokens();
            knownExpansions = resolver.GetInstalledOfficialExpansions();
        }
        else if (loadWorkshop)
        {
            requestedTokens = resolver.GetWorkshopSubscribedTokens();
            knownExpansions = resolver.GetInstalledOfficialExpansions();
        }
        else if (!string.IsNullOrWhiteSpace(rawModsList))
        {
            requestedTokens = rawModsList
                .Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
        }
        else if (!string.IsNullOrWhiteSpace(inputFilePath))
        {
            requestedTokens = resolver.ParseInputFileTokens(inputFilePath, out var fileVer);
            if (!string.IsNullOrWhiteSpace(fileVer))
                detectedVersion = fileVer!;
        }
        else
        {
            var cfgPath = modsConfigPath ?? ModListResolver.DefaultModsConfigPath;
            var (ver, activeMods, expansions) = resolver.ParseModsConfigFile(cfgPath);
            detectedVersion = ver;
            requestedTokens = activeMods;
            knownExpansions = expansions;
        }

        var effectiveVersion = gameVersion ?? detectedVersion;

        var pipeline = new ModSortPipelineService();
        var result = pipeline.Run(new PipelineRequest(
            ModTokens: requestedTokens,
            TargetGameVersion: effectiveVersion,
            KnownExpansions: knownExpansions,
            VerifyWithGabs: verifyGabs,
            ModsConfigPath: modsConfigPath,
            IncludeWorkshopSubscriptions: includeWorkshop,
            WorkshopContentDirectory: workshopDir));

        var report = result.Report;
        var markdown = result.MarkdownReport;
        Console.WriteLine(markdown);

        if (!string.IsNullOrWhiteSpace(outputReportPath))
        {
            var dir = Path.GetDirectoryName(outputReportPath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(outputReportPath, markdown, new UTF8Encoding(false));
        }

        if (!string.IsNullOrWhiteSpace(outputJsonPath))
        {
            var dir = Path.GetDirectoryName(outputJsonPath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            File.WriteAllText(outputJsonPath, JsonSerializer.Serialize(report, jsonOptions), new UTF8Encoding(false));
        }

        if (!string.IsNullOrWhiteSpace(exportModsConfigPath))
        {
            var activeIds = report.SortedMods
                .Where(m => m.IsActive)
                .Select(m => m.PackageId)
                .ToList();

            var validation = ModSorterAgent.Sorting.SortOrderValidator.ValidateActiveModSet(activeIds, result);

            if (!validation.IsValid)
            {
                var ex = new ModSorterAgent.Sorting.SortValidationException("导出 ModsConfig.xml 最终启用集合校验失败", validation);
                Console.Error.WriteLine(ex.Message);
                return 1;
            }

            var currentValidation = ProfileValidationService.ValidatePackageIds(activeIds, effectiveVersion);
            if (!currentValidation.IsValid)
            {
                Console.Error.WriteLine(new ModSorterAgent.Sorting.SortValidationException(currentValidation.Result).Message);
                return 1;
            }

            ModSortReportBuilder.ApplyModsConfigWithBackup(
                exportModsConfigPath,
                effectiveVersion,
                report.SortedMods,
                knownExpansions);
        }

        if (!string.IsNullOrWhiteSpace(exportRmlPath))
        {
            var activeIds = report.SortedMods.Where(m => m.IsActive).Select(m => m.PackageId).ToList();
            var validation = ModSorterAgent.Sorting.SortOrderValidator.ValidateActiveModSet(activeIds, result);
            if (!validation.IsValid)
            {
                Console.Error.WriteLine(new ModSorterAgent.Sorting.SortValidationException(validation).Message);
                return 1;
            }

            // 只导出启用项，和 ModsConfig.xml 的口径保持一致
            var entries = report.SortedMods
                .Where(m => m.IsActive)
                .Select(m => (m.PackageId, m.Name, m.WorkshopId))
                .ToList();

            RimSortListFile.Save(exportRmlPath, effectiveVersion, entries);
            Console.WriteLine($"已导出 RimSort 列表：{exportRmlPath}（{entries.Count} 项）");
        }

        return 0;
    }

    /// <summary>
    /// 不启动界面，跑完整的「分析 → 实机验证」链路：
    /// 先按当前 ModsConfig.xml 做冲突分析，再把生效冲突交给实机验证逐条核验。
    /// </summary>
    private static async Task<int> RunAgentLiveVerify(string[] args)
    {
        var version = "1.7";
        var maxConflicts = 0;
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals("--game-version", StringComparison.OrdinalIgnoreCase))
                version = args[i + 1];
            if (args[i].Equals("--max-conflicts", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(args[i + 1], out var parsed) && parsed > 0)
                maxConflicts = parsed;
        }

        Console.WriteLine("正在分析当前加载顺序…");
        var resolver = new ModListResolver();
        var (detectedVersion, activeMods, expansions) = resolver.ParseModsConfigFile(ModListResolver.DefaultModsConfigPath);
        var effectiveVersion = string.IsNullOrWhiteSpace(detectedVersion) ? version : detectedVersion;

        var pipeline = new ModSortPipelineService();
        var analysis = pipeline.Run(new PipelineRequest(
            ModTokens: activeMods,
            TargetGameVersion: effectiveVersion,
            KnownExpansions: expansions,
            VerifyWithGabs: false,
            ModsConfigPath: ModListResolver.DefaultModsConfigPath,
            IncludeWorkshopSubscriptions: false,
            WorkshopContentDirectory: null));

        var conflicts = ModSorterAgent.Reporting.ConflictNarrative.Build(analysis.Report);

        var effective = new HashSet<string>(
            analysis.Report.SortedMods.Where(m => m.IsActive).Select(m => m.PackageId),
            StringComparer.OrdinalIgnoreCase);

        var scoped = conflicts
            .GroupBy(c => c.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .Where(c => c.Grade != ModSorterAgent.Models.ConflictGrade.Green)
            .Where(c => c.Grade == ModSorterAgent.Models.ConflictGrade.Purple
                ? effective.Contains(c.ModAPackageId) && effective.Contains(c.ModBPackageId)
                : c.Category.Equals("MissingDependency", StringComparison.Ordinal)
                    ? effective.Contains(c.ModAPackageId)
                    : effective.Contains(c.ModAPackageId) && effective.Contains(c.ModBPackageId))
            .OrderByDescending(c => c.Grade)
            .ToList();

        Console.WriteLine($"  模组 {analysis.Report.SortedMods.Count} 项，待核验冲突 {scoped.Count} 项");

        if (maxConflicts > 0 && scoped.Count > maxConflicts)
        {
            scoped = scoped.Take(maxConflicts).ToList();
            Console.WriteLine($"  （本次只核验前 {maxConflicts} 项）");
        }

        var settings = ModSorterAgent.Agent.AgentConfigLoader.Load(
            new ModSorterAgent.Services.SettingsStore().Current);
        if (!settings.IsConfigured)
        {
            Console.WriteLine("未配置模型接口，实机核查需要 Agent 全权操作游戏。请在界面「设置」中配置。");
            return 2;
        }

        using var investigator = new ModSorterAgent.Agent.AgentInvestigator(
            settings,
            analysis.Mods,
            ModListResolver.PathOptions.RimWorldInstallDir,
            text => Console.WriteLine("    " + text),
            enableGabsTools: true);

        var progress = new Progress<(int Done, int Total, string Label)>(p =>
            Console.WriteLine($"  [{p.Done}/{p.Total}] {p.Label}"));

        var reports = await investigator.InvestigateAllAsync(
            scoped,
            progress,
            CancellationToken.None,
            useModel: true);

        Console.WriteLine();
        foreach (var report in reports)
        {
            Console.WriteLine($"· {report.ConflictKey}");
            Console.WriteLine($"  结论：{report.VerdictLabel}（置信度 {report.Confidence:P0}）");
            Console.WriteLine($"  {report.Headline}");
            if (!string.IsNullOrWhiteSpace(report.Explanation))
                Console.WriteLine($"  {report.Explanation}");
            Console.WriteLine();
        }

        return 0;
    }

    /// <summary>
    /// 实机工具链自检：只报告 GABS 与桥接模组是否存在于磁盘。
    /// 不依据版本号判断能否加载——模组版本号无意义，能否生效只看实际加载结果。
    /// </summary>
    private static int RunBridgeCheck(string[] args)
    {
        var gabs = ModSorterAgent.Agent.AgentGabsTools.ResolveGabsExePath();
        Console.WriteLine($"GABS     : {gabs} ({(File.Exists(gabs) ? "存在" : "缺失")})");

        var bridgeDir = ModSorterAgent.Verification.BridgeModLocator.FindBridgeModDirectory();
        Console.WriteLine($"桥接模组 : {bridgeDir ?? "(未找到)"}");

        if (!File.Exists(gabs))
        {
            Console.WriteLine();
            Console.WriteLine("缺少 GABS，实机操作不可用。");
            return 1;
        }

        if (bridgeDir == null)
        {
            Console.WriteLine();
            Console.WriteLine("未找到 RimBridgeServer 模组，实机桥接无法建立。");
            return 1;
        }

        return 0;
    }

    /// <summary>检测器的合成夹具自检：全绿才说明"该报的报、不该报的不报"。</summary>
    private static int RunSelfTests()
    {
        var suites = new (string Name, Func<IReadOnlyList<string>> Run)[]
        {
            ("XmlCrossReferenceAnalyzer", ModSorterAgent.Analysis.XmlCrossReferenceSelfTest.Run),
            ("TopologicalModSorter", ModSorterAgent.Sorting.TopologicalSortSelfTest.Run),
            ("ProfileValidation", ModSorterAgent.Services.ProfileValidationSelfTest.Run),
            ("LiveVerification", ModSorterAgent.Analysis.LiveVerificationSelfTest.Run),
            ("BundledBridge", ModSorterAgent.Verification.BundledBridgeSelfTest.Run),
            ("AgentConfig", ModSorterAgent.Agent.AgentConfigSelfTest.Run),
            ("AgentConsole", ModSorterAgent.UI.AgentConsoleSelfTest.Run)
        };

        var failed = 0;
        foreach (var (name, run) in suites)
        {
            var failures = run();
            if (failures.Count == 0)
            {
                Console.WriteLine($"[PASS] {name}");
                continue;
            }

            failed++;
            Console.WriteLine($"[FAIL] {name}：{failures.Count} 条");
            foreach (var failure in failures)
                Console.WriteLine($"    - {failure}");
        }

        Console.WriteLine(failed == 0 ? "自检全部通过。" : $"自检失败：{failed}/{suites.Length} 套。");
        return failed == 0 ? 0 : 1;
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int dwProcessId);

    private const int ATTACH_PARENT_PROCESS = -1;

    /// <summary>
    /// 当程序以 WinExe（纯图形界面）模式构建时，Windows 默认不为进程分配黑框控制台。
    /// 当从终端运行 --selftest 或 CLI 参数时，调用 AttachConsole 挂靠到调用方的父控制台，
    /// 并重设标准输出流，保证终端能正常看到输出。
    /// </summary>
    private static void EnsureConsoleAttached()
    {
        if (AttachConsole(ATTACH_PARENT_PROCESS))
        {
            var stdOut = Console.OpenStandardOutput();
            if (stdOut != Stream.Null)
            {
                var writer = new StreamWriter(stdOut, Encoding.UTF8) { AutoFlush = true };
                Console.SetOut(writer);
            }
            var stdErr = Console.OpenStandardError();
            if (stdErr != Stream.Null)
            {
                var errWriter = new StreamWriter(stdErr, Encoding.UTF8) { AutoFlush = true };
                Console.SetError(errWriter);
            }
        }
    }
}
