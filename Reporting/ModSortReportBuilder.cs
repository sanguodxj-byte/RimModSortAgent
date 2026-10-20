using System.Text;
using System.Xml.Linq;
using ModSorterAgent.Discovery;
using ModSorterAgent.Models;
using ModSorterAgent.Sorting;

namespace ModSorterAgent.Reporting;

/// <summary>
/// 报告汇总器：把分析结果折叠为界面与导出可直接消费的不可变结构。
/// 所有聚合都在一次遍历内完成，避免在界面重绘时反复扫描大集合。
/// </summary>
public static class ModSortReportBuilder
{
    public static ModSortReport BuildReport(
        string targetGameVersion,
        IReadOnlyList<ModDescriptor> sortedMods,
        IReadOnlyList<PairwiseConflictFinding> findings,
        IReadOnlyList<OrderingEdge> appliedEdges,
        IReadOnlyList<string> missingDependencies,
        IReadOnlyList<string> hardCycles,
        GabsLiveVerificationSummary? gabsVerification,
        long analysisDurationMs = 0,
        IReadOnlyDictionary<string, TopologicalModSorter.ModTierInfo>? tierByPackageId = null,
        IReadOnlyList<string>? unresolvedTokens = null,
        WorkshopScanResult? workshopScan = null)
    {
        var nameById = sortedMods
            .GroupBy(m => m.PackageId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Name, StringComparer.OrdinalIgnoreCase);

        var findingsByMod = BuildFindingsIndex(findings);
        var (incoming, outgoing) = BuildEdgeIndex(appliedEdges);

        var summaries = new List<ModGradeSummary>(sortedMods.Count);

        for (var idx = 0; idx < sortedMods.Count; idx++)
        {
            var mod = sortedMods[idx];
            var related = findingsByMod.TryGetValue(mod.PackageId, out var list)
                ? list
                : (IReadOnlyList<PairwiseConflictFinding>)Array.Empty<PairwiseConflictFinding>();

            var redCount = related.Count(f => f.Grade == ConflictGrade.Red);
            var yellowCount = related.Count(f => f.Grade == ConflictGrade.Yellow);

            var grade = redCount > 0
                ? ConflictGrade.Red
                : yellowCount > 0
                    ? ConflictGrade.Yellow
                    : ConflictGrade.Green;

            var reasons = new List<string>(capacity: 8);
            foreach (var finding in related.Where(f => f.Grade != ConflictGrade.Green).Take(6))
                reasons.Add($"[{GetGradeLabelShort(finding.Grade)}] {finding.Summary}");

            var incomingEdges = incoming.TryGetValue(mod.PackageId, out var inList)
                ? inList
                : new List<OrderingEdge>();
            var outgoingEdges = outgoing.TryGetValue(mod.PackageId, out var outList)
                ? outList
                : new List<OrderingEdge>();

            var upstream = BuildLinks(incomingEdges, e => e.BeforePackageId, nameById);
            var downstream = BuildLinks(outgoingEdges, e => e.AfterPackageId, nameById);

            foreach (var link in upstream.Take(4))
                reasons.Add($"前置约束：排在 {link.RelatedModName} 之后（{link.SourceKind}）");

            if (reasons.Count == 0)
                reasons.Add("未发现代码或 XML 冲突，加载顺序约束完备");

            var tier = tierByPackageId != null && tierByPackageId.TryGetValue(mod.PackageId, out var t)
                ? t
                : TopologicalModSorter.ClassifyModArchitectureTier(
                    mod,
                    outgoingEdges.Count(e => e.IsHardConstraint));

            summaries.Add(new ModGradeSummary(
                PackageId: mod.PackageId,
                Name: mod.Name,
                ArchitectureTierLabel: tier.TierLabel,
                RecommendedOrderIndex: idx + 1,
                OriginalOrderIndex: mod.OriginalIndex + 1,
                Grade: grade,
                GradeLabel: GetGradeLabel(grade),
                RedConflictCount: redCount,
                YellowUncertaintyCount: yellowCount,
                HarmonyPatchCount: mod.AssemblyAnalyses.Sum(a => a.Patches.Count),
                DynamicPatchSiteCount: mod.AssemblyAnalyses.Sum(a => a.DynamicPatchSites.Count),
                XmlDefCount: mod.XmlDefs.Count,
                XmlPatchCount: mod.XmlPatches.Count,
                KeyReasons: reasons.Distinct(StringComparer.Ordinal).ToArray())
            {
                Author = mod.Author,
                RootDirectory = mod.RootDirectory,
                IsOfficialCoreOrDlc = mod.IsOfficialCoreOrDlc,
                IsWorkshopMod = mod.IsWorkshopMod,
                IsActive = mod.IsActiveInModsConfig,
                WorkshopId = mod.WorkshopPublishedFileId,
                TierRank = tier.TierRank,
                SupportedVersions = mod.SupportedVersions,
                AssemblyNames = mod.AssemblyPaths
                    .Select(Path.GetFileNameWithoutExtension)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Cast<string>()
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                UpstreamDependencies = upstream,
                DownstreamDependents = downstream
            });
        }

        // 全量深度分析：启用列表与订阅池口径一致，三档统计覆盖排序集合里的所有模组
        var greenMods = summaries.Count(m => m.Grade == ConflictGrade.Green);
        var yellowMods = summaries.Count(m => m.Grade == ConflictGrade.Yellow);
        var redMods = summaries.Count(m => m.Grade == ConflictGrade.Red);
        var movedMods = summaries.Count(m => m.IsActive && m.OrderDelta != 0);

        var overallGrade = ConflictGrade.Green;
        if (redMods > 0 || missingDependencies.Count > 0 || hardCycles.Count > 0)
            overallGrade = ConflictGrade.Red;
        else if (yellowMods > 0)
            overallGrade = ConflictGrade.Yellow;

        var sortedFindings = findings
            .OrderByDescending(f => f.Grade)
            .ThenBy(f => f.Category, StringComparer.Ordinal)
            .ThenBy(f => f.TargetSignature, StringComparer.Ordinal)
            .ToArray();

        return new ModSortReport(
            GeneratedAtUtc: DateTime.UtcNow,
            TargetGameVersion: targetGameVersion,
            OverallGrade: overallGrade,
            OverallGradeLabel: GetGradeLabel(overallGrade),
            TotalModsAnalyzed: sortedMods.Count,
            GreenModCount: greenMods,
            YellowModCount: yellowMods,
            RedModCount: redMods,
            MissingDependencies: missingDependencies,
            HardCycleDescriptions: hardCycles,
            SortedMods: summaries,
            ConflictFindings: sortedFindings,
            AppliedOrderingEdges: appliedEdges,
            GabsVerification: gabsVerification)
        {
            MovedModCount = movedMods,
            TotalHarmonyPatchCount = summaries.Sum(m => m.HarmonyPatchCount),
            TotalDynamicPatchSiteCount = summaries.Sum(m => m.DynamicPatchSiteCount),
            TotalXmlDefCount = summaries.Sum(m => m.XmlDefCount),
            TotalXmlPatchCount = summaries.Sum(m => m.XmlPatchCount),
            AnalysisDurationMs = analysisDurationMs,
            UnresolvedTokens = unresolvedTokens ?? Array.Empty<string>(),
            TierInversionCount = CountTierInversions(summaries),
            WorkshopScopeEnabled = workshopScan != null,
            WorkshopSubscribedCount = workshopScan?.SubscribedCount ?? 0,
            WorkshopPoolModCount = summaries.Count(m => !m.IsActive),
            WorkshopMissingOnDiskCount = workshopScan?.SubscribedButNotInstalled.Count ?? 0,
            WorkshopManifestPath = workshopScan?.ManifestPath,
            WorkshopWarnings = workshopScan?.Warnings ?? Array.Empty<string>()
        };
    }

    private static Dictionary<string, List<PairwiseConflictFinding>> BuildFindingsIndex(
        IReadOnlyList<PairwiseConflictFinding> findings)
    {
        var index = new Dictionary<string, List<PairwiseConflictFinding>>(StringComparer.OrdinalIgnoreCase);
        foreach (var finding in findings)
        {
            Append(index, finding.ModAPackageId, finding);
            if (!finding.ModBPackageId.Equals(finding.ModAPackageId, StringComparison.OrdinalIgnoreCase))
                Append(index, finding.ModBPackageId, finding);
        }
        return index;

        static void Append(
            Dictionary<string, List<PairwiseConflictFinding>> target,
            string key,
            PairwiseConflictFinding value)
        {
            if (!target.TryGetValue(key, out var list))
            {
                list = new List<PairwiseConflictFinding>();
                target[key] = list;
            }
            list.Add(value);
        }
    }

    private static (Dictionary<string, List<OrderingEdge>> Incoming, Dictionary<string, List<OrderingEdge>> Outgoing)
        BuildEdgeIndex(IReadOnlyList<OrderingEdge> edges)
    {
        var incoming = new Dictionary<string, List<OrderingEdge>>(StringComparer.OrdinalIgnoreCase);
        var outgoing = new Dictionary<string, List<OrderingEdge>>(StringComparer.OrdinalIgnoreCase);

        foreach (var edge in edges)
        {
            if (!incoming.TryGetValue(edge.AfterPackageId, out var inList))
            {
                inList = new List<OrderingEdge>();
                incoming[edge.AfterPackageId] = inList;
            }
            inList.Add(edge);

            if (!outgoing.TryGetValue(edge.BeforePackageId, out var outList))
            {
                outList = new List<OrderingEdge>();
                outgoing[edge.BeforePackageId] = outList;
            }
            outList.Add(edge);
        }

        return (incoming, outgoing);
    }

    private static IReadOnlyList<DependencyLinkSummary> BuildLinks(
        List<OrderingEdge> edges,
        Func<OrderingEdge, string> relatedSelector,
        IReadOnlyDictionary<string, string> nameById)
    {
        return edges
            .OrderByDescending(e => e.IsHardConstraint)
            .ThenByDescending(e => e.Weight)
            .Select(e =>
            {
                var relatedId = relatedSelector(e);
                return new DependencyLinkSummary(
                    RelatedPackageId: relatedId,
                    RelatedModName: nameById.TryGetValue(relatedId, out var name) ? name : relatedId,
                    IsHardConstraint: e.IsHardConstraint,
                    Weight: e.Weight,
                    SourceKind: FormatEdgeSourceLabel(e.SourceKind),
                    Reason: e.Reason);
            })
            .ToArray();
    }

    private static int CountTierInversions(IReadOnlyList<ModGradeSummary> mods)
    {
        var inversions = 0;
        for (var i = 1; i < mods.Count; i++)
        {
            if (mods[i - 1].TierRank > mods[i].TierRank)
                inversions++;
        }
        return inversions;
    }

    public static string FormatEdgeSourceLabel(string rawSourceKind)
    {
        return rawSourceKind switch
        {
            "About.modDependencies" => "About 强依赖",
            "About.loadAfter" => "About.loadAfter",
            "About.loadBefore" => "About.loadBefore",
            "AssemblyReference" => "DLL 程序集引用",
            "LoadFolders.IfModActive" => "条件兼容目录",
            "HarmonyBefore" => "HarmonyBefore",
            "HarmonyAfter" => "HarmonyAfter",
            "DynamicReflectionPatch" => "动态反射修补",
            "BlockingPrefixAfterNonBlocking" => "截断 Prefix 后置",
            "NonBlockingPrefixBeforeBlocking" => "非截断 Prefix 前置",
            "HarmonyTranspilerHeuristic" => "Transpiler 顺序启发",
            "HarmonyResultOverwriteHeuristic" => "返回值覆盖启发",
            "GuardedPrefixOrdering" => "守卫 Prefix 顺位",
            "XmlPatchTargetsDef" => "XML Patch 目标 Def",
            "XmlDestructiveBeforeAdditive" => "XML 替换先于追加",
            _ => rawSourceKind
        };
    }

    public static string GetGradeLabel(ConflictGrade grade)
    {
        return grade switch
        {
            ConflictGrade.Green => "1.绿色 (确认无冲突)",
            ConflictGrade.Yellow => "2.黄色 (无法通过代码分析判断)",
            ConflictGrade.Red => "3.红色 (明确冲突)",
            _ => "未知"
        };
    }

    public static string GetGradeLabelShort(ConflictGrade grade)
    {
        return grade switch
        {
            ConflictGrade.Green => "1.绿色",
            ConflictGrade.Yellow => "2.黄色",
            ConflictGrade.Red => "3.红色",
            _ => "未知"
        };
    }

    public static string FormatMarkdownReport(ModSortReport report)
    {
        var sb = new StringBuilder(64 * 1024);

        sb.AppendLine("# RimWorld 模组自动排序与代码冲突分级报告");
        sb.AppendLine();
        sb.AppendLine($"- 生成时间: {report.GeneratedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"- 目标游戏版本: {report.TargetGameVersion}");
        sb.AppendLine($"- 总体评级: {report.OverallGradeLabel}");
        if (report.WorkshopScopeEnabled)
        {
            var activeCount = report.TotalModsAnalyzed - report.WorkshopPoolModCount;
            sb.AppendLine($"- 排序集合: {report.TotalModsAnalyzed} 项（已启用 {activeCount} + 工坊订阅池 {report.WorkshopPoolModCount}）");
            sb.AppendLine($"- 工坊订阅: {report.WorkshopSubscribedCount} 项" +
                          (report.WorkshopMissingOnDiskCount > 0 ? $"，其中 {report.WorkshopMissingOnDiskCount} 项未下载完成" : string.Empty));
            sb.AppendLine($"- 全量定级: 1.绿色 {report.GreenModCount}，2.黄色 {report.YellowModCount}，3.红色 {report.RedModCount}（全部模组同一分析口径）");
            sb.AppendLine($"- 顺位调整模组数: {report.MovedModCount} / {activeCount}（订阅池无原始顺位，不计入）");
        }
        else
        {
            sb.AppendLine($"- 模组总数: {report.TotalModsAnalyzed}（1.绿色: {report.GreenModCount}，2.黄色: {report.YellowModCount}，3.红色: {report.RedModCount}）");
            sb.AppendLine($"- 顺位调整模组数: {report.MovedModCount} / {report.TotalModsAnalyzed}");
        }
        sb.AppendLine($"- 扫描统计: Harmony 补丁 {report.TotalHarmonyPatchCount} 处（其中动态反射点 {report.TotalDynamicPatchSiteCount} 处），XML Def {report.TotalXmlDefCount} 项，XML Patch {report.TotalXmlPatchCount} 项");
        sb.AppendLine($"- 分析耗时: {report.AnalysisDurationMs / 1000.0:F1} 秒");
        if (report.GabsVerification != null)
        {
            sb.AppendLine($"- 实机状态: {report.GabsVerification.GameStatus}");
            if (report.GabsVerification.GameRunning)
                sb.AppendLine("- 写入提示: 游戏正在运行，退出时会用内存列表回写 ModsConfig.xml，建议先退出游戏再写入");
        }
        sb.AppendLine();

        AppendTierDistribution(sb, report);

        if (report.WorkshopScopeEnabled)
            AppendWorkshopPoolSummary(sb, report);

        if (report.MissingDependencies.Count > 0)
        {
            sb.AppendLine("## 缺失前置依赖（3.红色）");
            foreach (var dep in report.MissingDependencies)
                sb.AppendLine($"- {dep}");
            sb.AppendLine();
        }

        if (report.UnresolvedTokens.Count > 0)
        {
            sb.AppendLine("## 未能定位的模组标识");
            foreach (var token in report.UnresolvedTokens)
                sb.AppendLine($"- {token}");
            sb.AppendLine();
        }

        if (report.HardCycleDescriptions.Count > 0)
        {
            sb.AppendLine("## 硬性排序环路告警（3.红色）");
            foreach (var cycle in report.HardCycleDescriptions)
                sb.AppendLine($"- {cycle}");
            sb.AppendLine();
        }

        sb.AppendLine("## 一、推荐加载顺序（含三级定级）");
        sb.AppendLine();
        if (report.WorkshopScopeEnabled)
        {
            sb.AppendLine("> 顺位是「已启用 + 工坊订阅池」统一求解后的绝对位置：若要写入 ModsConfig.xml，" +
                          "未启用项会被自动跳过，只按其中「已启用」的行重排。");
            sb.AppendLine();
        }
        sb.AppendLine("| 推荐顺位 | 原顺位 | 变动 | 架构分层 | 状态 | 分级 | 模组名称 | PackageId | Harmony | Def/Patch | 关键依据 |");
        sb.AppendLine("|---:|---:|:--:|---|---|---|---|---|---:|---:|---|");

        foreach (var mod in report.SortedMods)
        {
            var delta = mod.OrderDelta switch
            {
                > 0 => $"▲{mod.OrderDelta}",
                < 0 => $"▼{Math.Abs(mod.OrderDelta)}",
                _ => "—"
            };

            var originalIndex = mod.IsActive ? mod.OriginalOrderIndex.ToString() : "—";
            var deltaText = mod.IsActive ? delta : "新增";
            var status = mod.IsActive ? "已启用" : "未启用";

            var reason = string.Join("；", mod.KeyReasons.Take(2)).Replace("|", "/").Replace("\r", " ").Replace("\n", " ");
            sb.AppendLine($"| {mod.RecommendedOrderIndex} | {originalIndex} | {deltaText} | {mod.ArchitectureTierLabel} | {status} | {mod.GradeLabel} | {mod.Name} | {mod.PackageId} | {mod.HarmonyPatchCount} | {mod.XmlDefCount}/{mod.XmlPatchCount} | {reason} |");
        }
        sb.AppendLine();

        AppendFindingsSection(sb, report, ConflictGrade.Red, "二、3.红色（明确冲突）明细");
        AppendFindingsSection(sb, report, ConflictGrade.Yellow, "三、2.黄色（无法通过代码分析完全判断）明细");
        AppendFindingsSection(sb, report, ConflictGrade.Green, "四、1.绿色（共同作用于同一目标但确认无冲突）明细");

        return sb.ToString();
    }

    private static void AppendTierDistribution(StringBuilder sb, ModSortReport report)
    {
        var tiers = report.SortedMods
            .GroupBy(m => m.ArchitectureTierLabel)
            .OrderBy(g => g.Min(m => m.TierRank))
            .Select(g => $"{g.Key} {g.Count()}")
            .ToList();

        if (tiers.Count == 0)
            return;

        sb.AppendLine("## 架构分层分布");
        sb.AppendLine();
        foreach (var tier in tiers)
            sb.AppendLine($"- {tier}");
        sb.AppendLine();
    }

    private static void AppendWorkshopPoolSummary(StringBuilder sb, ModSortReport report)
    {
        sb.AppendLine("## 工坊订阅检测");
        sb.AppendLine();
        sb.AppendLine($"- 订阅清单: {report.WorkshopManifestPath ?? "未找到"}");
        sb.AppendLine($"- 订阅条目: {report.WorkshopSubscribedCount} 项");
        sb.AppendLine($"- 并入排序: {report.WorkshopPoolModCount} 项未启用模组（与启用列表同口径：XML Def 扫描 + 程序集反编译 + Harmony 特征提取）");
        if (report.WorkshopMissingOnDiskCount > 0)
            sb.AppendLine($"- 未下载完成: {report.WorkshopMissingOnDiskCount} 项（未纳入排序）");

        foreach (var warning in report.WorkshopWarnings)
            sb.AppendLine($"- 提示: {warning}");
        sb.AppendLine();

        var poolByTier = report.SortedMods
            .Where(m => !m.IsActive)
            .GroupBy(m => m.ArchitectureTierLabel)
            .OrderBy(g => g.Min(m => m.TierRank))
            .Select(g => $"{g.Key} {g.Count()}")
            .ToList();

        if (poolByTier.Count > 0)
        {
            sb.AppendLine("订阅池分层分布：" + string.Join("，", poolByTier));
            sb.AppendLine();
        }
    }

    private static void AppendFindingsSection(
        StringBuilder sb,
        ModSortReport report,
        ConflictGrade grade,
        string heading)
    {
        var items = report.ConflictFindings.Where(f => f.Grade == grade).ToList();
        sb.AppendLine($"## {heading}（共 {items.Count} 项）");
        sb.AppendLine();

        if (items.Count == 0)
        {
            sb.AppendLine(grade == ConflictGrade.Green
                ? "- 各模组补丁目标完全正交，无重叠目标。"
                : "- 未检测到该类问题。");
            sb.AppendLine();
            return;
        }

        if (grade == ConflictGrade.Green)
        {
            foreach (var f in items.Take(80))
                sb.AppendLine($"- [{f.Category}] {f.ModAName} + {f.ModBName} 于 {f.TargetSignature}: {f.Summary}");
            if (items.Count > 80)
                sb.AppendLine($"- …（另有 {items.Count - 80} 项绿色重叠记录）");
            sb.AppendLine();
            return;
        }

        foreach (var f in items)
        {
            sb.AppendLine($"### [{f.Category}] {f.ModAName} ↔ {f.ModBName}");
            sb.AppendLine($"- 冲突目标: {f.TargetSignature}");
            sb.AppendLine($"- 结论说明: {f.Summary}");
            if (!string.IsNullOrWhiteSpace(f.RecommendationReason))
                sb.AppendLine($"- 处置建议: {f.RecommendationReason}");
            foreach (var ev in f.Evidence.Take(4))
                sb.AppendLine($"  - 证据: {ev}");
            sb.AppendLine();
        }
    }

    /// <summary>
    /// 导出 ModsConfig.xml。只写入 <see cref="ModGradeSummary.IsActive"/> 的模组：
    /// 订阅池里的未启用项仅用于排序参考，绝不能被自动激活。
    /// </summary>
    public static void ExportModsConfigXml(
        string outputPath,
        string gameVersion,
        IReadOnlyList<ModGradeSummary> sortedMods,
        IReadOnlyList<string> knownExpansions)
    {
        WriteModsConfigXml(
            outputPath,
            gameVersion,
            sortedMods.Where(m => m.IsActive).Select(m => m.PackageId),
            knownExpansions);
    }

    /// <summary>
    /// 按排序列表（PackageId 快照）写入 ModsConfig.xml。快照在保存时已过滤为激活项。
    /// </summary>
    public static void ExportPackageIdsAsModsConfigXml(
        string outputPath,
        string gameVersion,
        IReadOnlyList<string> packageIds,
        IReadOnlyList<string> knownExpansions)
    {
        WriteModsConfigXml(outputPath, gameVersion, packageIds, knownExpansions);
    }

    private static void WriteModsConfigXml(
        string outputPath,
        string gameVersion,
        IEnumerable<string> packageIds,
        IReadOnlyList<string> knownExpansions)
    {
        var expansions = knownExpansions.Count > 0
            ? knownExpansions
            : new[]
            {
                "ludeon.rimworld",
                "ludeon.rimworld.royalty",
                "ludeon.rimworld.ideology",
                "ludeon.rimworld.biotech",
                "ludeon.rimworld.anomaly",
                "ludeon.rimworld.odyssey"
            };

        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("ModsConfigData",
                new XElement("version", gameVersion),
                new XElement("activeMods",
                    packageIds.Select(id => new XElement("li", id.ToLowerInvariant()))),
                new XElement("knownExpansions",
                    expansions.Select(e => new XElement("li", e.ToLowerInvariant())))));

        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        doc.Save(outputPath);
    }

    /// <summary>
    /// 先备份再写入 ModsConfig.xml。返回备份文件路径（原文件不存在时返回 null）。
    /// </summary>
    public static string? ApplyModsConfigWithBackup(
        string targetModsConfigPath,
        string gameVersion,
        IReadOnlyList<ModGradeSummary> sortedMods,
        IReadOnlyList<string> knownExpansions)
    {
        string? backupPath = BackupModsConfig(targetModsConfigPath);

        ExportModsConfigXml(targetModsConfigPath, gameVersion, sortedMods, knownExpansions);
        return backupPath;
    }

    /// <summary>
    /// 按排序列表（PackageId 快照）先备份再写入 ModsConfig.xml。
    /// 返回备份文件路径（原文件不存在时返回 null）。
    /// </summary>
    public static string? ApplyPackageIdsWithBackup(
        string targetModsConfigPath,
        string gameVersion,
        IReadOnlyList<string> packageIds,
        IReadOnlyList<string> knownExpansions)
    {
        string? backupPath = BackupModsConfig(targetModsConfigPath);

        ExportPackageIdsAsModsConfigXml(targetModsConfigPath, gameVersion, packageIds, knownExpansions);
        return backupPath;
    }

    /// <summary>
    /// 备份 ModsConfig.xml 为 ModsConfig.backup.时间戳.xml（文件存在时）。
    /// 返回备份路径；原文件不存在时返回 null。供启动游戏前单独调用做"进游戏前快照"。
    /// </summary>
    public static string? BackupModsConfigFile(string targetModsConfigPath)
    {
        if (!File.Exists(targetModsConfigPath))
            return null;

        var dir = Path.GetDirectoryName(targetModsConfigPath) ?? string.Empty;
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupPath = Path.Combine(dir, $"ModsConfig.backup.{stamp}.xml");
        File.Copy(targetModsConfigPath, backupPath, overwrite: true);
        return backupPath;
    }

    private static string? BackupModsConfig(string targetModsConfigPath)
        => BackupModsConfigFile(targetModsConfigPath);

    /// <summary>
    /// 读取磁盘上 ModsConfig.xml 的版本号与 activeMods 列表（统一小写）。
    /// 用于启动前比对当前激活排序列表是否已同步，文件缺失或格式异常时返回 false。
    /// </summary>
    public static bool TryReadActiveMods(
        string modsConfigPath,
        out string gameVersion,
        out IReadOnlyList<string> activeMods)
    {
        gameVersion = string.Empty;
        activeMods = Array.Empty<string>();

        try
        {
            if (!File.Exists(modsConfigPath))
                return false;

            var doc = XDocument.Load(modsConfigPath);
            var root = doc.Root;
            if (root == null || root.Name != "ModsConfigData")
                return false;

            gameVersion = root.Element("version")?.Value ?? string.Empty;
            activeMods = root
                .Element("activeMods")?
                .Elements("li")
                .Select(li => li.Value.Trim().ToLowerInvariant())
                .Where(id => id.Length > 0)
                .ToList() ?? new List<string>();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
