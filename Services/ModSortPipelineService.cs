using System.Diagnostics;
using ModSorterAgent.Analysis;
using ModSorterAgent.Discovery;
using ModSorterAgent.Grading;
using ModSorterAgent.Models;
using ModSorterAgent.Reporting;
using ModSorterAgent.Sorting;
using ModSorterAgent.Verification;

namespace ModSorterAgent.Services;

/// <summary>
/// 分析管线：模组发现 → XML 静态扫描 → 程序集反编译 → 冲突定级 → 分层拓扑排序 → 实机核验 → 报告汇总。
/// 所有跨线程交互都通过 <see cref="IProgress{T}"/> 与 <see cref="CancellationToken"/> 完成，
/// 因此既可以跑在命令行，也可以跑在界面后台线程。
/// </summary>
public sealed class ModSortPipelineService : IModSortPipeline
{
    private const int TotalStages = 7;

    public PipelineResult Run(
        PipelineRequest request,
        IProgress<PipelineProgressReport>? progress = null,
        Action<string>? onStatusUpdate = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var clock = Stopwatch.StartNew();
        var tokens = request.ModTokens
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (tokens.Count == 0)
            throw new InvalidOperationException("模组列表为空：请先读取 ModsConfig.xml 或导入列表文件。");

        void Report(PipelineStage stage, int stageIndex, int current, int total, int overallPercent, string message)
        {
            onStatusUpdate?.Invoke(message);
            progress?.Report(new PipelineProgressReport(
                Stage: stage,
                StageIndex: stageIndex,
                TotalStages: TotalStages,
                CurrentItem: current,
                TotalItems: Math.Max(1, total),
                Percentage: Math.Clamp(overallPercent, 0, 100),
                StatusText: message,
                ElapsedMs: clock.ElapsedMilliseconds));
        }

        // ── 阶段 1：模组发现与目录解析 ───────────────────────────────────────
        cancellationToken.ThrowIfCancellationRequested();
        Report(PipelineStage.ResolvingCatalog, 1, 0, tokens.Count, 3, "正在检索本地与 Steam 创意工坊模组目录…");

        var resolver = new ModListResolver(request.CustomSearchRoots);
        var (resolvedMods, unresolvedTokens) = resolver.ResolveModList(tokens, request.TargetGameVersion);

        if (resolvedMods.Count == 0)
        {
            throw new InvalidOperationException(
                $"未能定位任何模组（共 {tokens.Count} 个标识）。请确认 RimWorld 安装目录与创意工坊路径是否正确。");
        }

        Report(PipelineStage.ResolvingCatalog, 1, resolvedMods.Count, resolvedMods.Count, 8,
            $"已定位 {resolvedMods.Count} 项模组" + (unresolvedTokens.Count > 0 ? $"，{unresolvedTokens.Count} 项未能匹配" : string.Empty));

        // ── 阶段 1b：Steam 创意工坊订阅检测（可选） ───────────────────────────
        WorkshopScanResult? workshopScan = null;
        var poolMods = new List<ModDescriptor>();
        var poolUnresolved = new List<string>();

        if (request.IncludeWorkshopSubscriptions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Report(PipelineStage.ResolvingCatalog, 1, resolvedMods.Count, resolvedMods.Count, 9,
                "正在检测 Steam 创意工坊订阅清单…");

            workshopScan = WorkshopSubscriptionScanner.Scan(request.WorkshopContentDirectory);

            var resolvableIds = workshopScan.Subscriptions
                .Where(s => s.IsSubscribed && s.IsInstalledOnDisk && s.HasAboutFile)
                .Select(s => s.PublishedFileId)
                .ToList();

            var (pool, unresolvedIds) = resolver.ResolveWorkshopPool(
                resolvableIds, request.TargetGameVersion, resolvedMods);

            poolMods = pool;
            poolUnresolved = unresolvedIds;

            Report(PipelineStage.ResolvingCatalog, 1, resolvedMods.Count, resolvedMods.Count, 11,
                $"工坊订阅 {workshopScan.SubscribedCount} 项，其中 {poolMods.Count} 项未启用已纳入排序" +
                (workshopScan.SubscribedButNotInstalled.Count > 0
                    ? $"，{workshopScan.SubscribedButNotInstalled.Count} 项未下载完成"
                    : string.Empty));
        }

        // ── 阶段 2：XML Def / Patch 静态扫描（启用列表 + 订阅池一视同仁） ──────
        cancellationToken.ThrowIfCancellationRequested();

        var analysisTargets = poolMods.Count == 0
            ? resolvedMods
            : resolvedMods.Concat(poolMods).ToList();

        Report(PipelineStage.AnalyzingXml, 2, 0, analysisTargets.Count, 12,
            $"正在并行扫描 XML Defs 与 Patches（共 {analysisTargets.Count} 项）…");

        var xmlEnriched = ScanXml(analysisTargets, clock, Report, cancellationToken);

        // ── 阶段 3：程序集反编译与 Harmony 特征提取 ──────────────────────────
        cancellationToken.ThrowIfCancellationRequested();
        Report(PipelineStage.AnalyzingCode, 3, 0, xmlEnriched.Count, 38, "正在反编译程序集并提取 Harmony 补丁特征…");

        var codeAnalyzer = new CodeConflictAnalyzer(xmlEnriched, request.RimWorldInstallDir);
        var fullyAnalyzed = codeAnalyzer.AnalyzeAll(
            xmlEnriched,
            info =>
            {
                var pct = 38 + (int)(info.Completed / (double)Math.Max(1, xmlEnriched.Count) * 40);
                Report(PipelineStage.AnalyzingCode, 3, info.Completed, xmlEnriched.Count, pct,
                    $"反编译分析（{info.Completed}/{xmlEnriched.Count}）：{info.ModName}，累计 Harmony 补丁 {info.PatchCount} 处");
            },
            cancellationToken);

        // ── 阶段 4：三级冲突定级 ────────────────────────────────────────────
        cancellationToken.ThrowIfCancellationRequested();
        Report(PipelineStage.GradingConflicts, 4, 0, 1, 80, "正在计算三级冲突定级与依赖约束…");

        var grading = ConflictGrader.AnalyzeAndGrade(fullyAnalyzed, cancellationToken);

        foreach (var unresolved in unresolvedTokens.Concat(poolUnresolved))
            grading.MissingDependencies.Add($"未能定位模组标识: {unresolved}");

        var reportFindings = grading.Findings;
        var reportMissingDependencies = grading.MissingDependencies;

        Report(PipelineStage.GradingConflicts, 4, 1, 1, 84,
            $"冲突定级完成：红色 {reportFindings.Count(f => f.Grade == ConflictGrade.Red)} 项，黄色 {reportFindings.Count(f => f.Grade == ConflictGrade.Yellow)} 项");

        // ── 阶段 5：分层拓扑排序 ────────────────────────────────────────────
        cancellationToken.ThrowIfCancellationRequested();
        Report(PipelineStage.TopologicalSort, 5, 0, 1, 87, "正在求解分层拓扑排序…");

        var sortResult = TopologicalModSorter.Sort(fullyAnalyzed, grading.OrderingEdges, cancellationToken);
        var allFindings = reportFindings
            .Concat(sortResult.CycleConflictFindings)
            .ToList();

        Report(PipelineStage.TopologicalSort, 5, 1, 1, 90,
            $"排序完成：{sortResult.AppliedEdges.Count} 条约束生效" +
            (sortResult.HardCycleDescriptions.Count > 0 ? $"，{sortResult.HardCycleDescriptions.Count} 处硬性环路已断开" : string.Empty));

        // ── 阶段 6：实机 / 日志核验 ─────────────────────────────────────────
        cancellationToken.ThrowIfCancellationRequested();
        Report(PipelineStage.LiveVerification, 6, 0, 2, 92,
            request.VerifyWithGabs ? "正在核验 GABS 常驻状态与实机日志…" : "正在扫描离线 Player.log…");

        var gabsSummary = GabsLiveVerifier.VerifyAndRefineFindings(
            sortResult.SortedMods,
            allFindings,
            useGabsMcp: request.VerifyWithGabs,
            cancellationToken: cancellationToken);

        Report(PipelineStage.LiveVerification, 6, 2, 2, 96,
            gabsSummary.Connected
                ? $"实机核验完成：{gabsSummary.GameStatus}"
                : "实机核验完成：未检测到常驻游戏，仅使用离线日志");

        // ── 阶段 7：报告汇总 ────────────────────────────────────────────────
        cancellationToken.ThrowIfCancellationRequested();
        Report(PipelineStage.BuildingReport, 7, 0, 1, 98, "正在汇总分析与排序报告…");

        var report = ModSortReportBuilder.BuildReport(
            targetGameVersion: request.TargetGameVersion,
            sortedMods: sortResult.SortedMods,
            findings: allFindings,
            appliedEdges: sortResult.AppliedEdges,
            missingDependencies: reportMissingDependencies,
            hardCycles: sortResult.HardCycleDescriptions,
            gabsVerification: gabsSummary,
            analysisDurationMs: clock.ElapsedMilliseconds,
            tierByPackageId: sortResult.TierByPackageId,
            unresolvedTokens: unresolvedTokens.Concat(poolUnresolved).ToList(),
            workshopScan: workshopScan);

        var markdown = ModSortReportBuilder.FormatMarkdownReport(report);

        clock.Stop();
        Report(PipelineStage.Completed, TotalStages, analysisTargets.Count, analysisTargets.Count, 100,
            $"分析完成，耗时 {clock.Elapsed.TotalSeconds:F1} 秒");

        return new PipelineResult(
            Report: report,
            MarkdownReport: markdown,
            KnownExpansions: request.KnownExpansions,
            EffectiveGameVersion: request.TargetGameVersion,
            Mods: fullyAnalyzed);
    }

    /// <summary>
    /// CLI 兼容入口：保留旧的调用形态，内部统一走 <see cref="Run"/>。
    /// </summary>
    public PipelineResult RunAnalysis(
        IReadOnlyList<string> requestedTokens,
        string effectiveVersion,
        IReadOnlyList<string> knownExpansions,
        bool verifyGabs,
        Action<string>? onStatusUpdate = null,
        CancellationToken cancellationToken = default,
        IProgress<PipelineProgressReport>? progress = null)
    {
        return Run(
            PipelineRequest.Create(requestedTokens, effectiveVersion, knownExpansions, verifyGabs),
            progress,
            onStatusUpdate,
            cancellationToken);
    }

    private static List<ModDescriptor> ScanXml(
        IReadOnlyList<ModDescriptor> mods,
        Stopwatch clock,
        Action<PipelineStage, int, int, int, int, string> report,
        CancellationToken cancellationToken)
    {
        var results = new ModDescriptor[mods.Count];
        var completed = 0;
        var total = Math.Max(1, mods.Count);

        var options = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = Math.Max(2, Math.Min(mods.Count, Environment.ProcessorCount))
        };

        Parallel.For(0, mods.Count, options, idx =>
        {
            options.CancellationToken.ThrowIfCancellationRequested();

            var enriched = XmlConflictAnalyzer.EnrichWithXmlAnalysis(mods[idx]);
            results[idx] = enriched;

            var done = Interlocked.Increment(ref completed);
            if (done % 8 == 0 || done == mods.Count)
            {
                var pct = 12 + (int)(done / (double)total * 24);
                report(PipelineStage.AnalyzingXml, 2, done, mods.Count, pct,
                    $"扫描 XML（{done}/{mods.Count}）：{enriched.Name}");
            }
        });

        return results.ToList();
    }
}
