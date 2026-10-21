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
        Console.OutputEncoding = Encoding.UTF8;

        if (args.Any(a => a.Equals("--selftest", StringComparison.OrdinalIgnoreCase)))
            return RunSelfTests();

        var launchGui = args.Length == 0 || args.Any(a => a.Equals("--gui", StringComparison.OrdinalIgnoreCase));
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
            ModSortReportBuilder.ExportModsConfigXml(
                exportModsConfigPath,
                effectiveVersion,
                report.SortedMods,
                knownExpansions);
        }

        if (!string.IsNullOrWhiteSpace(exportRmlPath))
        {
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

    /// <summary>检测器的合成夹具自检：全绿才说明"该报的报、不该报的不报"。</summary>
    private static int RunSelfTests()
    {
        var suites = new (string Name, Func<IReadOnlyList<string>> Run)[]
        {
            ("XmlCrossReferenceAnalyzer", ModSorterAgent.Analysis.XmlCrossReferenceSelfTest.Run),
            ("TopologicalModSorter", ModSorterAgent.Sorting.TopologicalSortSelfTest.Run)
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
}
