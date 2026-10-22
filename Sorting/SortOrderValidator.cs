using System.Text;
using ModSorterAgent.Discovery;
using ModSorterAgent.Models;

namespace ModSorterAgent.Sorting;

public enum SortValidationErrorType
{
    DuplicatePackageId,
    MissingPackageId,
    UnexpectedPackageId,
    InvalidEdgeNode,
    SelfLoopEdge,
    EdgeOrderViolation,
    HardCycleEdgeTreatedAsApplied,
    MissingActiveDependency,
    MissingAnalysisMetadata,
    InvalidPackageId
}

public sealed record SortValidationIssue(
    SortValidationErrorType ErrorType,
    string Message,
    string? PackageId = null,
    OrderingEdge? Edge = null,
    int? BeforeIndex = null,
    int? AfterIndex = null);

public sealed record SortValidationResult(
    bool IsValid,
    IReadOnlyList<SortValidationIssue> Issues)
{
    public static SortValidationResult Success() =>
        new(true, Array.Empty<SortValidationIssue>());

    public static SortValidationResult Failure(IReadOnlyList<SortValidationIssue> issues) =>
        new(false, issues);
}

public sealed class SortValidationException : InvalidOperationException
{
    public SortValidationResult ValidationResult { get; }
    public IReadOnlyList<SortValidationIssue> Issues => ValidationResult.Issues;

    public SortValidationException(SortValidationResult validationResult)
        : this("排序后置校验失败", validationResult)
    {
    }

    public SortValidationException(string prefix, SortValidationResult validationResult)
        : base(FormatMessage(validationResult, prefix))
    {
        ValidationResult = validationResult ?? throw new ArgumentNullException(nameof(validationResult));
    }

    private static string FormatMessage(SortValidationResult result, string prefix)
    {
        var sb = new StringBuilder(prefix);
        sb.Append("，发现 ").Append(result.Issues.Count).Append(" 处异常：\n");
        foreach (var issue in result.Issues)
        {
            sb.Append("  - [").Append(issue.ErrorType).Append("] ").Append(issue.Message).Append('\n');
        }
        return sb.ToString().TrimEnd();
    }
}

/// <summary>
/// 本地排序结果后置校验器：校验集合完整性、有向约束边单调性以及断开环路边不当作生效边。
/// </summary>
public static class SortOrderValidator
{
    public static SortValidationResult Validate(
        IReadOnlyList<ModDescriptor> inputMods,
        TopologicalModSorter.SortResult sortResult)
    {
        ArgumentNullException.ThrowIfNull(inputMods);
        ArgumentNullException.ThrowIfNull(sortResult);

        var validation = Validate(
            expectedPackageIds: inputMods.Select(m => m.PackageId),
            sortedPackageIds: sortResult.SortedMods.Select(m => m.PackageId),
            appliedEdges: sortResult.AppliedEdges,
            hardCycleDescriptions: sortResult.HardCycleDescriptions,
            cycleConflictFindings: sortResult.CycleConflictFindings,
            brokenEdges: sortResult.BrokenEdges);

        var issues = validation.Issues.ToList();
        var declarations = ValidateActiveModSet(
            sortResult.SortedMods.Select(m => m.PackageId), inputMods, Array.Empty<OrderingEdge>());
        var rejectedPairs = sortResult.BrokenEdges
            .Select(e => (ModListResolver.StripSteamSuffix(e.BeforePackageId), ModListResolver.StripSteamSuffix(e.AfterPackageId)))
            .ToHashSet(EdgeTupleComparer.Instance);
        foreach (var issue in declarations.Issues.Where(i => i.ErrorType == SortValidationErrorType.EdgeOrderViolation))
        {
            if (issue.Edge == null || !rejectedPairs.Contains((
                    ModListResolver.StripSteamSuffix(issue.Edge.BeforePackageId),
                    ModListResolver.StripSteamSuffix(issue.Edge.AfterPackageId))))
                issues.Add(issue);
        }
        return issues.Count == 0 ? SortValidationResult.Success() : SortValidationResult.Failure(issues);
    }

    public static SortValidationResult Validate(
        IEnumerable<string> expectedPackageIds,
        TopologicalModSorter.SortResult sortResult)
    {
        ArgumentNullException.ThrowIfNull(expectedPackageIds);
        ArgumentNullException.ThrowIfNull(sortResult);

        return Validate(
            expectedPackageIds: expectedPackageIds,
            sortedPackageIds: sortResult.SortedMods.Select(m => m.PackageId),
            appliedEdges: sortResult.AppliedEdges,
            hardCycleDescriptions: sortResult.HardCycleDescriptions,
            cycleConflictFindings: sortResult.CycleConflictFindings,
            brokenEdges: sortResult.BrokenEdges);
    }

    public static SortValidationResult Validate(
        IEnumerable<string> expectedPackageIds,
        IEnumerable<string> sortedPackageIds,
        IReadOnlyList<OrderingEdge> appliedEdges,
        IReadOnlyList<string>? hardCycleDescriptions = null,
        IReadOnlyList<PairwiseConflictFinding>? cycleConflictFindings = null,
        IReadOnlyList<OrderingEdge>? brokenEdges = null)
    {
        ArgumentNullException.ThrowIfNull(expectedPackageIds);
        ArgumentNullException.ThrowIfNull(sortedPackageIds);
        ArgumentNullException.ThrowIfNull(appliedEdges);

        var issues = new List<SortValidationIssue>();

        var expectedSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawId in expectedPackageIds)
        {
            if (string.IsNullOrWhiteSpace(rawId))
                continue;
            expectedSet.Add(ModListResolver.NormalizePackageId(rawId));
        }

        var sortedList = sortedPackageIds.ToList();
        var seenSorted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var indexByPackageId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < sortedList.Count; i++)
        {
            var rawId = sortedList[i];
            if (string.IsNullOrWhiteSpace(rawId))
            {
                issues.Add(new SortValidationIssue(SortValidationErrorType.InvalidPackageId, "排序结果包含空模组标识。"));
                continue;
            }
            var normId = ModListResolver.NormalizePackageId(rawId);

            if (!seenSorted.Add(normId))
            {
                issues.Add(new SortValidationIssue(
                    SortValidationErrorType.DuplicatePackageId,
                    $"SortedMods 包含重复 PackageId: '{normId}'",
                    PackageId: normId));
            }
            else
            {
                indexByPackageId[normId] = i;
            }

            if (!expectedSet.Contains(normId))
            {
                issues.Add(new SortValidationIssue(
                    SortValidationErrorType.UnexpectedPackageId,
                    $"SortedMods 包含未在输入规范化集合中的 PackageId: '{normId}'",
                    PackageId: normId));
            }
        }

        foreach (var expectedId in expectedSet)
        {
            if (!seenSorted.Contains(expectedId))
            {
                issues.Add(new SortValidationIssue(
                    SortValidationErrorType.MissingPackageId,
                    $"SortedMods 遗漏输入中的 PackageId: '{expectedId}'",
                    PackageId: expectedId));
            }
        }

        var hardCycleEdgePairs = ExtractHardCyclePairs(hardCycleDescriptions, cycleConflictFindings, brokenEdges);

        foreach (var edge in appliedEdges)
        {
            var beforeNorm = ModListResolver.NormalizePackageId(edge.BeforePackageId);
            var afterNorm = ModListResolver.NormalizePackageId(edge.AfterPackageId);

            if (hardCycleEdgePairs.Contains((beforeNorm, afterNorm)))
            {
                indexByPackageId.TryGetValue(beforeNorm, out var bIdx);
                indexByPackageId.TryGetValue(afterNorm, out var aIdx);
                issues.Add(new SortValidationIssue(
                    SortValidationErrorType.HardCycleEdgeTreatedAsApplied,
                    $"硬环已断开记录的边不能作为生效边: '{edge.BeforePackageId}' -> '{edge.AfterPackageId}'",
                    Edge: edge,
                    BeforeIndex: indexByPackageId.ContainsKey(beforeNorm) ? bIdx : null,
                    AfterIndex: indexByPackageId.ContainsKey(afterNorm) ? aIdx : null));
            }

            if (beforeNorm.Equals(afterNorm, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new SortValidationIssue(
                    SortValidationErrorType.SelfLoopEdge,
                    $"生效边指向自身: '{edge.BeforePackageId}' -> '{edge.AfterPackageId}'",
                    Edge: edge));
                continue;
            }

            var hasBefore = indexByPackageId.TryGetValue(beforeNorm, out var beforeIndex);
            var hasAfter = indexByPackageId.TryGetValue(afterNorm, out var afterIndex);

            if (!hasBefore || !hasAfter)
            {
                var missingTarget = !hasBefore && !hasAfter
                    ? $"Before '{edge.BeforePackageId}' 与 After '{edge.AfterPackageId}'"
                    : !hasBefore
                        ? $"Before '{edge.BeforePackageId}'"
                        : $"After '{edge.AfterPackageId}'";

                issues.Add(new SortValidationIssue(
                    SortValidationErrorType.InvalidEdgeNode,
                    $"生效边引用的模组未在 SortedMods 中找到 ({missingTarget}): '{edge.BeforePackageId}' -> '{edge.AfterPackageId}'",
                    Edge: edge,
                    BeforeIndex: hasBefore ? beforeIndex : null,
                    AfterIndex: hasAfter ? afterIndex : null));
                continue;
            }

            if (beforeIndex >= afterIndex)
            {
                issues.Add(new SortValidationIssue(
                    SortValidationErrorType.EdgeOrderViolation,
                    $"生效边顺序未满足 Before < After: '{edge.BeforePackageId}' (顺位 {beforeIndex}) 应在 '{edge.AfterPackageId}' (顺位 {afterIndex}) 之前",
                    Edge: edge,
                    BeforeIndex: beforeIndex,
                    AfterIndex: afterIndex));
            }
        }

        return issues.Count == 0
            ? SortValidationResult.Success()
            : SortValidationResult.Failure(issues);
    }

    public static void ValidateAndThrow(
        IReadOnlyList<ModDescriptor> inputMods,
        TopologicalModSorter.SortResult sortResult)
    {
        var result = Validate(inputMods, sortResult);
        if (!result.IsValid)
            throw new SortValidationException(result);
    }

    public static void ValidateAndThrow(
        IEnumerable<string> expectedPackageIds,
        TopologicalModSorter.SortResult sortResult)
    {
        var result = Validate(expectedPackageIds, sortResult);
        if (!result.IsValid)
            throw new SortValidationException(result);
    }

    /// <summary>
    /// 对即将写入游戏配置或导出的最终启用集合执行本地硬性门禁校验：
    /// 1. 缺失分析元数据阻断（Profile 缺元数据不可静默写）；
    /// 2. 检查集合内是否存在重复 PackageId；
    /// 3. 前置依赖必须在最终启用集合内得到满足（未启用工坊订阅池不能满足依赖，缺失或取舍过滤丢前置必须阻止）；
    /// 4. 依赖满足性与显式加载顺序分别校验；
    /// 5. 硬约束边两端都在最终启用集合时，其相对次序必须满足（Before 顺位严格小于 After 顺位）。
    /// </summary>
    public static SortValidationResult ValidateActiveModSet(
        IEnumerable<string> finalActivePackageIds,
        IReadOnlyList<ModDescriptor> allMods,
        IReadOnlyList<OrderingEdge>? appliedOrderingEdges = null)
    {
        ArgumentNullException.ThrowIfNull(finalActivePackageIds);

        if (allMods == null || allMods.Count == 0)
        {
            return SortValidationResult.Failure(new[]
            {
                new SortValidationIssue(
                    SortValidationErrorType.MissingAnalysisMetadata,
                    "缺失模组元数据，无法完成最终启用集合校验。")
            });
        }

        var issues = new List<SortValidationIssue>();
        var activeList = new List<string>();
        var activeSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var indexInActive = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var baseIndexInActive = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var pos = 0;
        foreach (var rawId in finalActivePackageIds)
        {
            if (string.IsNullOrWhiteSpace(rawId))
            {
                issues.Add(new SortValidationIssue(SortValidationErrorType.InvalidPackageId, "最终启用集合包含空模组标识。"));
                continue;
            }

            var normId = ModListResolver.NormalizePackageId(rawId);
            var baseId = ModListResolver.StripSteamSuffix(normId);
            indexInActive.TryAdd(normId, pos);

            if (!activeSet.Add(normId))
            {
                issues.Add(new SortValidationIssue(
                    SortValidationErrorType.DuplicatePackageId,
                    $"最终启用集合包含重复 PackageId: '{normId}'",
                    PackageId: normId));
            }
            else if (baseIndexInActive.ContainsKey(baseId))
            {
                issues.Add(new SortValidationIssue(
                    SortValidationErrorType.DuplicatePackageId,
                    $"最终启用集合按基础 ID 识别到重复项（如 base 与 _steam 别名同时存在）: '{normId}' 与已存在的项冲突",
                    PackageId: normId));
            }
            else
            {
                indexInActive[normId] = pos;
                baseIndexInActive.TryAdd(baseId, pos);
            }
            activeList.Add(normId);
            pos++;
        }

        var modByPackageId = new Dictionary<string, ModDescriptor>(StringComparer.OrdinalIgnoreCase);
        var modByBaseId = new Dictionary<string, ModDescriptor>(StringComparer.OrdinalIgnoreCase);
        foreach (var mod in allMods)
        {
            var norm = ModListResolver.NormalizePackageId(mod.PackageId);
            modByPackageId.TryAdd(norm, mod);
            modByBaseId.TryAdd(ModListResolver.StripSteamSuffix(norm), mod);
        }

        ModDescriptor? FindDescriptor(string id)
        {
            var norm = ModListResolver.NormalizePackageId(id);
            if (modByPackageId.TryGetValue(norm, out var m))
                return m;
            if (modByBaseId.TryGetValue(ModListResolver.StripSteamSuffix(norm), out m))
                return m;
            return null;
        }

        bool TryResolveActiveMod(string rawId, out string activeNormId, out int activeIndex)
        {
            if (string.IsNullOrWhiteSpace(rawId))
            {
                activeNormId = string.Empty;
                activeIndex = -1;
                return false;
            }

            var norm = ModListResolver.NormalizePackageId(rawId);
            if (indexInActive.TryGetValue(norm, out var idx))
            {
                activeNormId = norm;
                activeIndex = idx;
                return true;
            }

            var baseId = ModListResolver.StripSteamSuffix(norm);
            if (baseIndexInActive.TryGetValue(baseId, out idx))
            {
                activeNormId = activeList[idx];
                activeIndex = idx;
                return true;
            }

            activeNormId = string.Empty;
            activeIndex = -1;
            return false;
        }

        foreach (var normId in activeSet)
        {
            if (FindDescriptor(normId) == null)
            {
                issues.Add(new SortValidationIssue(
                    SortValidationErrorType.MissingAnalysisMetadata,
                    $"模组 '{normId}' 缺失分析元数据（未在已知模组分析集合中找到），无法校验前置依赖与排序约束。",
                    PackageId: normId));
            }
        }

        foreach (var normId in activeList)
        {
            var mod = FindDescriptor(normId);
            if (mod == null)
                continue;

            foreach (var dep in mod.Dependencies)
            {
                string? matchedSatisfyingId = null;

                if (TryResolveActiveMod(dep.PackageId, out var primaryMatchedId, out _))
                {
                    matchedSatisfyingId = primaryMatchedId;
                }
                else
                {
                    foreach (var alt in dep.AlternativePackageIds)
                    {
                        if (TryResolveActiveMod(alt, out var altMatchedId, out _))
                        {
                            matchedSatisfyingId = altMatchedId;
                            break;
                        }
                    }
                }

                if (matchedSatisfyingId == null)
                {
                    var depLabel = string.IsNullOrWhiteSpace(dep.DisplayName)
                        ? dep.PackageId
                        : $"{dep.DisplayName} ({dep.PackageId})";

                    issues.Add(new SortValidationIssue(
                        SortValidationErrorType.MissingActiveDependency,
                        $"模组 '{mod.Name}' ({mod.PackageId}) 缺少必需前置依赖: '{depLabel}'。未启用订阅池或被过滤放弃的前置无法满足依赖。",
                        PackageId: mod.PackageId));
                }
            }
        }

        var seenHardConstraints = new HashSet<(string Before, string After)>(EdgeTupleComparer.Instance);

        if (appliedOrderingEdges != null)
        {
            foreach (var edge in appliedOrderingEdges)
            {
                if (!edge.IsHardConstraint)
                    continue;

                // 忽略传入的 About.modDependencies 旧硬边，依赖不强制顺序
                if (string.Equals(edge.SourceKind, "About.modDependencies", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!TryResolveActiveMod(edge.BeforePackageId, out var beforeNorm, out var bIdx) ||
                    !TryResolveActiveMod(edge.AfterPackageId, out var afterNorm, out var aIdx))
                {
                    continue;
                }

                if (beforeNorm.Equals(afterNorm, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!seenHardConstraints.Add((beforeNorm, afterNorm)))
                    continue;

                if (bIdx >= aIdx)
                {
                    issues.Add(new SortValidationIssue(
                        SortValidationErrorType.EdgeOrderViolation,
                        $"硬约束排序未满足：模组 '{edge.BeforePackageId}' (顺位 {bIdx + 1}) 必须在 '{edge.AfterPackageId}' (顺位 {aIdx + 1}) 之前加载（来源：{edge.SourceKind}，原因：{edge.Reason}）。",
                        Edge: edge,
                        BeforeIndex: bIdx,
                        AfterIndex: aIdx));
                }
            }
        }

        foreach (var normId in activeList)
        {
            var mod = FindDescriptor(normId);
            if (mod == null)
                continue;

            var modIdx = indexInActive[normId];

            foreach (var afterRaw in mod.LoadAfter.Concat(mod.ForceLoadAfter))
            {
                if (!TryResolveActiveMod(afterRaw, out var targetNorm, out var targetIdx))
                    continue;

                if (targetNorm.Equals(normId, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!seenHardConstraints.Add((targetNorm, normId)))
                    continue;

                if (targetIdx >= modIdx)
                {
                    issues.Add(new SortValidationIssue(
                        SortValidationErrorType.EdgeOrderViolation,
                        $"硬约束排序未满足：模组 '{afterRaw}' (顺位 {targetIdx + 1}) 必须在 '{mod.Name}' ({mod.PackageId}, 顺位 {modIdx + 1}) 之前加载（来源：About.loadAfter）。",
                        PackageId: mod.PackageId,
                        Edge: new OrderingEdge(
                            BeforePackageId: targetNorm,
                            AfterPackageId: normId,
                            IsHardConstraint: true,
                            Weight: 90,
                            SourceKind: "About.loadAfter",
                            Reason: $"{mod.Name} 声明 loadAfter {afterRaw}"),
                        BeforeIndex: targetIdx,
                        AfterIndex: modIdx));
                }
            }

            foreach (var beforeRaw in mod.LoadBefore.Concat(mod.ForceLoadBefore))
            {
                if (!TryResolveActiveMod(beforeRaw, out var targetNorm, out var targetIdx))
                    continue;

                if (targetNorm.Equals(normId, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!seenHardConstraints.Add((normId, targetNorm)))
                    continue;

                if (modIdx >= targetIdx)
                {
                    issues.Add(new SortValidationIssue(
                        SortValidationErrorType.EdgeOrderViolation,
                        $"硬约束排序未满足：模组 '{mod.Name}' ({mod.PackageId}, 顺位 {modIdx + 1}) 必须在 '{beforeRaw}' (顺位 {targetIdx + 1}) 之前加载（来源：About.loadBefore）。",
                        PackageId: mod.PackageId,
                        Edge: new OrderingEdge(
                            BeforePackageId: normId,
                            AfterPackageId: targetNorm,
                            IsHardConstraint: true,
                            Weight: 90,
                            SourceKind: "About.loadBefore",
                            Reason: $"{mod.Name} 声明 loadBefore {beforeRaw}"),
                        BeforeIndex: modIdx,
                        AfterIndex: targetIdx));
                }
            }
        }

        return issues.Count == 0
            ? SortValidationResult.Success()
            : SortValidationResult.Failure(issues);
    }

    public static SortValidationResult ValidateActiveModSet(
        IEnumerable<string> finalActivePackageIds,
        PipelineResult? pipelineResult)
    {
        if (pipelineResult == null)
        {
            return SortValidationResult.Failure(new[]
            {
                new SortValidationIssue(
                    SortValidationErrorType.MissingAnalysisMetadata,
                    "缺失模组元数据，无法完成最终启用集合校验。")
            });
        }

        var issues = new List<SortValidationIssue>();

        // 校验 UnresolvedActiveTokens：只要存在来自主启用输入的未解析 token，无条件阻断防止静默丢失
        if (pipelineResult.UnresolvedActiveTokens != null && pipelineResult.UnresolvedActiveTokens.Count > 0)
        {
            foreach (var unresolvedToken in pipelineResult.UnresolvedActiveTokens)
            {
                issues.Add(new SortValidationIssue(
                    SortValidationErrorType.MissingAnalysisMetadata,
                    $"启用模组 '{unresolvedToken}' 缺失分析元数据（未在本地安装或工坊中解析成功），阻止静默丢失写入。",
                    PackageId: unresolvedToken));
            }
        }

        var candidateEdges = new List<OrderingEdge>();
        if (pipelineResult.Report?.AppliedOrderingEdges != null)
            candidateEdges.AddRange(pipelineResult.Report.AppliedOrderingEdges);
        if (pipelineResult.Report?.RejectedHardOrderingEdges != null)
            candidateEdges.AddRange(pipelineResult.Report.RejectedHardOrderingEdges);

        var innerResult = ValidateActiveModSet(
            finalActivePackageIds,
            pipelineResult.Mods,
            candidateEdges);

        if (!innerResult.IsValid)
            issues.AddRange(innerResult.Issues);

        return issues.Count == 0
            ? SortValidationResult.Success()
            : SortValidationResult.Failure(issues);
    }

    public static void ValidateActiveModSetAndThrow(
        IEnumerable<string> finalActivePackageIds,
        IReadOnlyList<ModDescriptor> allMods,
        IReadOnlyList<OrderingEdge>? appliedOrderingEdges = null)
    {
        var result = ValidateActiveModSet(finalActivePackageIds, allMods, appliedOrderingEdges);
        if (!result.IsValid)
            throw new SortValidationException("最终启用集合校验失败", result);
    }

    public static void ValidateActiveModSetAndThrow(
        IEnumerable<string> finalActivePackageIds,
        PipelineResult? pipelineResult)
    {
        var result = ValidateActiveModSet(finalActivePackageIds, pipelineResult);
        if (!result.IsValid)
            throw new SortValidationException("最终启用集合校验失败", result);
    }

    public static bool IsEdgeSuccessfullyApplied(
        OrderingEdge edge,
        TopologicalModSorter.SortResult sortResult)
    {
        ArgumentNullException.ThrowIfNull(edge);
        ArgumentNullException.ThrowIfNull(sortResult);

        return IsEdgeSuccessfullyApplied(
            edge,
            sortResult.SortedMods.Select(m => m.PackageId),
            sortResult.AppliedEdges,
            sortResult.HardCycleDescriptions,
            sortResult.CycleConflictFindings,
            sortResult.BrokenEdges);
    }

    public static bool IsEdgeSuccessfullyApplied(
        OrderingEdge edge,
        IEnumerable<string> sortedPackageIds,
        IReadOnlyList<OrderingEdge> appliedEdges,
        IReadOnlyList<string>? hardCycleDescriptions = null,
        IReadOnlyList<PairwiseConflictFinding>? cycleConflictFindings = null,
        IReadOnlyList<OrderingEdge>? brokenEdges = null)
    {
        ArgumentNullException.ThrowIfNull(edge);
        ArgumentNullException.ThrowIfNull(sortedPackageIds);
        ArgumentNullException.ThrowIfNull(appliedEdges);

        var beforeNorm = ModListResolver.NormalizePackageId(edge.BeforePackageId);
        var afterNorm = ModListResolver.NormalizePackageId(edge.AfterPackageId);

        var hardCyclePairs = ExtractHardCyclePairs(hardCycleDescriptions, cycleConflictFindings, brokenEdges);
        if (hardCyclePairs.Contains((beforeNorm, afterNorm)))
            return false;

        var isApplied = appliedEdges.Any(e =>
            ModListResolver.NormalizePackageId(e.BeforePackageId).Equals(beforeNorm, StringComparison.OrdinalIgnoreCase) &&
            ModListResolver.NormalizePackageId(e.AfterPackageId).Equals(afterNorm, StringComparison.OrdinalIgnoreCase));

        if (!isApplied)
            return false;

        var sortedList = sortedPackageIds.ToList();
        var beforeIdx = -1;
        var afterIdx = -1;

        for (var i = 0; i < sortedList.Count; i++)
        {
            var norm = ModListResolver.NormalizePackageId(sortedList[i]);
            if (beforeIdx < 0 && norm.Equals(beforeNorm, StringComparison.OrdinalIgnoreCase))
                beforeIdx = i;
            if (afterIdx < 0 && norm.Equals(afterNorm, StringComparison.OrdinalIgnoreCase))
                afterIdx = i;
        }

        return beforeIdx >= 0 && afterIdx >= 0 && beforeIdx < afterIdx;
    }

    private static HashSet<(string Before, string After)> ExtractHardCyclePairs(
        IReadOnlyList<string>? hardCycleDescriptions,
        IReadOnlyList<PairwiseConflictFinding>? cycleConflictFindings,
        IReadOnlyList<OrderingEdge>? brokenEdges)
    {
        var set = new HashSet<(string Before, string After)>(EdgeTupleComparer.Instance);

        if (brokenEdges != null)
        {
            foreach (var edge in brokenEdges)
            {
                set.Add((
                    ModListResolver.NormalizePackageId(edge.BeforePackageId),
                    ModListResolver.NormalizePackageId(edge.AfterPackageId)));
            }
        }

        if (cycleConflictFindings != null)
        {
            foreach (var finding in cycleConflictFindings)
            {
                if (finding.Category.Equals("CyclicLoadOrderConstraint", StringComparison.OrdinalIgnoreCase))
                {
                    set.Add((
                        ModListResolver.NormalizePackageId(finding.ModAPackageId),
                        ModListResolver.NormalizePackageId(finding.ModBPackageId)));
                }
            }
        }

        return set;
    }

    private sealed class EdgeTupleComparer : IEqualityComparer<(string Before, string After)>
    {
        public static readonly EdgeTupleComparer Instance = new();

        public bool Equals((string Before, string After) x, (string Before, string After) y) =>
            string.Equals(x.Before, y.Before, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.After, y.After, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string Before, string After) obj) =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Before),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.After));
    }
}
