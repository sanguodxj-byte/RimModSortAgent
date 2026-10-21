using ModSorterAgent.Discovery;
using ModSorterAgent.Models;

namespace ModSorterAgent.Sorting;

/// <summary>
/// 分层拓扑排序器。
///
/// 旧实现会为"所有低层 → 所有高层"的两两组合强行插入有向边（O(N²) 边膨胀），
/// 既拖慢求解，也会错误地锁定同层/跨层模组之间的相对顺序。
///
/// 新实现把"架构分层"从<b>约束</b>降级为<b>启发式优先级</b>：
///   1. 只把About/Harmony/XML 分析产出的真实约束作为图边；
///   2. 用"按权重降序增量加边 + 成环即拒绝"的方式保证图为 DAG，并记录硬性环路；
///   3. 用 Kahn 算法配合优先队列出队，队列键 = (架构层, 层内序号, 原顺位)，
///      在满足全部真实约束的前提下尽量贴近理想分层。
/// </summary>
public static class TopologicalModSorter
{
    public sealed record ModTierInfo(int TierRank, int SubRank, string TierLabel);

    public sealed record SortResult(
        List<ModDescriptor> SortedMods,
        List<OrderingEdge> AppliedEdges,
        List<string> HardCycleDescriptions,
        List<PairwiseConflictFinding> CycleConflictFindings,
        IReadOnlyDictionary<string, ModTierInfo> TierByPackageId,
        int TierInversionCount);

    public static SortResult Sort(
        IReadOnlyList<ModDescriptor> inputMods,
        IReadOnlyList<OrderingEdge> candidateEdges,
        CancellationToken cancellationToken = default)
    {
        // 入口去重：上游若泄漏重复 PackageId（如 base/_steam 双形态解析），
        // 保留 OriginalIndex 最小的一个，避免 ToDictionary 抛异常导致整个分析崩溃。
        var mods = inputMods
            .GroupBy(m => m.PackageId, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderBy(m => m.OriginalIndex).First())
            .OrderBy(m => m.OriginalIndex)
            .ToList();

        var modById = mods.ToDictionary(m => m.PackageId, StringComparer.OrdinalIgnoreCase);
        var allEdges = new List<OrderingEdge>(candidateEdges);

        var deduplicated = allEdges
            .Where(e => modById.ContainsKey(e.BeforePackageId) &&
                        modById.ContainsKey(e.AfterPackageId) &&
                        !e.BeforePackageId.Equals(e.AfterPackageId, StringComparison.OrdinalIgnoreCase))
            .GroupBy(e => $"{e.BeforePackageId.ToLowerInvariant()}->{e.AfterPackageId.ToLowerInvariant()}")
            .Select(g => g.OrderByDescending(x => x.IsHardConstraint).ThenByDescending(x => x.Weight).First())
            .OrderByDescending(e => e.IsHardConstraint)
            .ThenByDescending(e => e.Weight)
            .ToList();

        var adjacency = mods.ToDictionary(
            m => m.PackageId,
            _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);

        var activeEdges = new List<OrderingEdge>(deduplicated.Count);
        var hardCycles = new List<string>();
        var cycleFindings = new List<PairwiseConflictFinding>();

        // 增量加边：按约束强度降序尝试，成环则拒绝。
        // 被拒绝的硬约束上报为红色环路；被拒绝的软约束上报为黄色提示，不再静默丢弃。
        foreach (var edge in deduplicated)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (CreatesCycle(adjacency, edge.BeforePackageId, edge.AfterPackageId))
            {
                if (edge.IsHardConstraint)
                    RecordHardCycle(edge, modById, hardCycles, cycleFindings);
                else
                    RecordSoftCycle(edge, modById, cycleFindings);
                continue;
            }

            adjacency[edge.BeforePackageId].Add(edge.AfterPackageId);
            activeEdges.Add(edge);
        }

        // 只有"硬约束"的入边才代表真正的上游依赖（About 强依赖 / DLL 引用 / loadAfter），
        // 把 XML 补丁目标、动态反射等软边计入会导致几乎所有模组都被误判为"前置框架"。
        var hardDependentCount = ComputeHardDependentCounts(mods, activeEdges);
        var assemblyReferencedBy = ComputeAssemblyReferencedBy(mods);
        var tiers = mods.ToDictionary(
            m => m.PackageId,
            m => ClassifyModArchitectureTier(m, hardDependentCount[m.PackageId], assemblyReferencedBy[m.PackageId]),
            StringComparer.OrdinalIgnoreCase);

        var sorted = RunKahnWithPriority(mods, modById, adjacency, tiers);

        var inversionCount = CountAdjacentTierInversions(sorted, tiers);

        return new SortResult(sorted, activeEdges, hardCycles, cycleFindings, tiers, inversionCount);
    }

    /// <summary>
    /// 底座位次：只依据"是否官方内容"与"是否提供 Harmony 运行时"判断，不做任何模组命名匹配。
    /// 0 = Harmony 运行时；≥1 = 官方内容（按官方权威加载次序）；-1 = 社区模组。
    /// 注意：这是<b>启发式优先级</b>，不生成任何图边。
    /// 官方 DLC 不带 modDependencies，彼此只用 forceLoadAfter/forceLoadBefore 表达先后，
    /// 因此必须用固定的权威次序，不能靠"依赖数"推断——否则乱序输入会把 Core 排到 DLC 之后。
    /// </summary>
    private static int GetBaseRank(ModDescriptor mod)
    {
        if (ProvidesHarmonyRuntime(mod))
            return 0;

        if (!mod.IsOfficialCoreOrDlc)
            return -1;

        var officialRank = ModListResolver.GetOfficialLoadOrderRank(mod.PackageId);
        return officialRank >= 0
            ? 1 + officialRank
            : 1 + ModListResolver.OfficialLoadOrderCount;
    }

    /// <summary>程序集身份判定：文件名就是 Harmony 运行时（0Harmony.dll），与模组命名无关。</summary>
    private static bool ProvidesHarmonyRuntime(ModDescriptor mod) =>
        mod.AssemblyPaths.Any(path =>
            Path.GetFileName(path).Equals("0Harmony.dll", StringComparison.OrdinalIgnoreCase));

    private static Dictionary<string, int> ComputeHardDependentCounts(
        IReadOnlyList<ModDescriptor> mods,
        IReadOnlyList<OrderingEdge> edges)
    {
        var counts = mods.ToDictionary(m => m.PackageId, _ => 0, StringComparer.OrdinalIgnoreCase);

        foreach (var edge in edges)
        {
            if (!edge.IsHardConstraint)
                continue;

            // edge.Before 是"被依赖方"，被依赖次数越多越说明它是底座
            if (counts.ContainsKey(edge.BeforePackageId))
                counts[edge.BeforePackageId]++;
        }

        return counts;
    }

    /// <summary>
    /// 统计每个模组的程序集被多少个<b>其他</b>模组的程序集引用。
    /// 这是"框架 / API"最可靠的结构信号：别人编译时引用了你的 DLL，说明你提供的是代码底座。
    /// </summary>
    private static Dictionary<string, int> ComputeAssemblyReferencedBy(IReadOnlyList<ModDescriptor> mods)
    {
        var counts = mods.ToDictionary(m => m.PackageId, _ => 0, StringComparer.OrdinalIgnoreCase);

        var modByAssembly = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var mod in mods)
        {
            foreach (var analysis in mod.AssemblyAnalyses)
            {
                if (!string.IsNullOrWhiteSpace(analysis.AssemblyName))
                    modByAssembly.TryAdd(analysis.AssemblyName, mod.PackageId);
            }
        }

        foreach (var mod in mods)
        {
            var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in mod.ReferencedExternalAssemblies)
            {
                if (!modByAssembly.TryGetValue(name, out var ownerId))
                    continue;

                if (ownerId.Equals(mod.PackageId, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (referenced.Add(ownerId) && counts.ContainsKey(ownerId))
                    counts[ownerId]++;
            }
        }

        return counts;
    }

    private static List<ModDescriptor> RunKahnWithPriority(
        IReadOnlyList<ModDescriptor> mods,
        Dictionary<string, ModDescriptor> modById,
        Dictionary<string, HashSet<string>> adjacency,
        Dictionary<string, ModTierInfo> tiers)
    {
        var inDegree = mods.ToDictionary(m => m.PackageId, _ => 0, StringComparer.OrdinalIgnoreCase);
        foreach (var (_, targets) in adjacency)
        {
            foreach (var target in targets)
            {
                if (inDegree.ContainsKey(target))
                    inDegree[target]++;
            }
        }

        var ready = new PriorityQueue<ModDescriptor, (int First, int Second, int Third, int Fourth, string Id)>();
        foreach (var mod in mods)
        {
            if (inDegree[mod.PackageId] == 0)
                ready.Enqueue(mod, BuildPriority(mod, tiers[mod.PackageId]));
        }

        var sorted = new List<ModDescriptor>(mods.Count);
        while (ready.Count > 0)
        {
            var current = ready.Dequeue();
            sorted.Add(current);

            foreach (var nextId in adjacency[current.PackageId])
            {
                if (!inDegree.TryGetValue(nextId, out var deg))
                    continue;

                deg--;
                inDegree[nextId] = deg;
                if (deg != 0)
                    continue;

                var nextMod = modById[nextId];
                ready.Enqueue(nextMod, BuildPriority(nextMod, tiers[nextId]));
            }
        }

        if (sorted.Count < mods.Count)
        {
            // 理论上不会发生（图已被强制为 DAG），此处仅作为防御性兜底。
            var emitted = new HashSet<string>(sorted.Select(s => s.PackageId), StringComparer.OrdinalIgnoreCase);
            var remaining = mods
                .Where(m => !emitted.Contains(m.PackageId))
                .OrderBy(m => tiers[m.PackageId].TierRank)
                .ThenBy(m => tiers[m.PackageId].SubRank)
                .ThenBy(m => m.OriginalIndex);
            sorted.AddRange(remaining);
        }

        return sorted;
    }
    private static (int First, int Second, int Third, int Fourth, string Id) BuildPriority(
        ModDescriptor mod,
        ModTierInfo tier)
    {
        return (tier.TierRank, tier.SubRank, mod.OriginalIndex, -mod.XmlDefs.Count, mod.PackageId.ToLowerInvariant());
    }

    private static int CountAdjacentTierInversions(
        IReadOnlyList<ModDescriptor> sorted,
        Dictionary<string, ModTierInfo> tiers)
    {
        var inversions = 0;
        for (var i = 1; i < sorted.Count; i++)
        {
            var prev = tiers[sorted[i - 1].PackageId].TierRank;
            var curr = tiers[sorted[i].PackageId].TierRank;
            if (prev > curr)
                inversions++;
        }
        return inversions;
    }

    private static void RecordHardCycle(
        OrderingEdge edge,
        Dictionary<string, ModDescriptor> modById,
        List<string> hardCycles,
        List<PairwiseConflictFinding> cycleFindings)
    {
        var modA = modById[edge.BeforePackageId];
        var modB = modById[edge.AfterPackageId];
        var desc = $"检测到硬性排序循环依赖: {modA.Name} ({modA.PackageId}) → {modB.Name} ({modB.PackageId})，已断开该边以保证排序可解（{edge.Reason}）";

        hardCycles.Add(desc);
        cycleFindings.Add(new PairwiseConflictFinding(
            ModAPackageId: modA.PackageId,
            ModAName: modA.Name,
            ModBPackageId: modB.PackageId,
            ModBName: modB.Name,
            Grade: ConflictGrade.Red,
            Category: "CyclicLoadOrderConstraint",
            TargetSignature: $"{modA.PackageId} <-> {modB.PackageId}",
            Summary: desc,
            Evidence: new[] { edge.Reason },
            RecommendedBeforePackageId: modB.PackageId,
            RecommendedAfterPackageId: modA.PackageId,
            RecommendationReason: "存在双向循环依赖，已自动断开后置边以保证拓扑可解；建议联系模组作者修正 About.xml 声明"));
    }

    /// <summary>
    /// 软约束（HarmonyBefore/After、Transpiler 启发、XML 补丁目标等）与已生效的更强约束成环被拒。
    /// 不影响排序可解性，但意味着最终顺序与该启发式建议相反，上报为黄色提示供用户复核。
    /// </summary>
    private static void RecordSoftCycle(
        OrderingEdge edge,
        Dictionary<string, ModDescriptor> modById,
        List<PairwiseConflictFinding> cycleFindings)
    {
        var modA = modById[edge.BeforePackageId];
        var modB = modById[edge.AfterPackageId];

        cycleFindings.Add(new PairwiseConflictFinding(
            ModAPackageId: modA.PackageId,
            ModAName: modA.Name,
            ModBPackageId: modB.PackageId,
            ModBName: modB.Name,
            Grade: ConflictGrade.Yellow,
            Category: "SoftConstraintCycleDropped",
            TargetSignature: $"{modA.PackageId} <-> {modB.PackageId}",
            Summary: $"软约束 {modA.Name} → {modB.Name} 与已生效的更强约束成环，已舍弃该建议并按现有约束求解",
            Evidence: new[] { edge.Reason },
            RecommendedBeforePackageId: null,
            RecommendedAfterPackageId: null,
            RecommendationReason: "该启发式顺序与强约束（About 依赖 / DLL 引用）矛盾而被舍弃；如需该顺序请手动调整后复跑分析"));
    }

    /// <summary>
    /// 架构分层判定。<b>只用代码与 XML 结构信号</b>，不做任何模组名称 / PackageId 子串匹配：
    /// 官方标记、依赖关系、Def 类型分布、Harmony 补丁形态、Languages 目录。
    /// </summary>
    /// <param name="hardDependents">
    /// 以<b>硬约束</b>方式依赖本模组的模组数量（About 强依赖 / DLL 引用 / loadAfter）。
    /// 软边（XML 补丁目标、动态反射、Transpiler 启发）不计入。
    /// </param>
    public static ModTierInfo ClassifyModArchitectureTier(
        ModDescriptor mod,
        int hardDependents,
        int assemblyReferencedBy = 0)
    {
        var baseRank = GetBaseRank(mod);
        if (baseRank >= 0)
            return new ModTierInfo(0, baseRank, "核心底座");

        // 显式声明要排在官方内容 / Harmony 之前（Prepatcher、Loading Progress 这类）。
        // 它们本质上属于底座的一部分，必须与底座同层，否则其它无关模组会插进它们和 Core 之间。
        var hasAssemblies = mod.AssemblyPaths.Count > 0;
        var defCount = mod.XmlDefs.Count;
        var patchCount = mod.XmlPatches.Count;

        // 语言汉化包：没有任何代码 / Def / Patch 载荷，只有 Languages 目录
        if (!hasAssemblies && defCount == 0 && patchCount == 0 && HasLanguagesPayload(mod))
            return new ModTierInfo(7, 0, "语言汉化包");

        // 纯 XML 补丁：无程序集、无自建 Def、只有 Patch
        if (!hasAssemblies && defCount == 0 && patchCount > 0)
            return new ModTierInfo(6, 0, "纯 XML 补丁");

        // 前置框架：别人依赖它的代码（DLL 被引用）或大量模组硬依赖它。
        // 纯 API 包（无 Def）门槛更低；同时是内容包的按硬依赖数判定。
        var dependencyScore = hardDependents + assemblyReferencedBy;
        if (dependencyScore >= 3 && defCount <= FrameworkDefCeiling)
            return new ModTierInfo(2, 0, "前置框架");
        if (assemblyReferencedBy >= 2)
            return new ModTierInfo(2, 0, "前置框架");
        if (hardDependents >= 6)
            return new ModTierInfo(2, 0, "前置框架");

        // 无 Def、也不改游戏行为的模组分两种：
        // ① 带程序集 → 程序类工具（服务端、编辑器、加载器外壳）；
        // ② 不带程序集 → 纯素材包（贴图 / 模型替换），归渲染层。
        if (defCount == 0 && !HasAnyCodeBehavior(mod))
        {
            return hasAssemblies
                ? new ModTierInfo(1, 20, "系统与诊断")
                : new ModTierInfo(5, 10, "渲染与动画");
        }

        // 种族与阵营：Def 结构信号（AlienRace 系列 / FactionDef / 挂 Pawn 类）
        if (DefinesRaceOrFaction(mod))
            return new ModTierInfo(3, HasBlockingPrefix(mod) ? 10 : 0, "种族与阵营");

        // 渲染与动画：Def 全是视觉类型，或补丁目标高度集中在图形/渲染 API
        if (IsVisualOnly(mod) || VisualPatchShare(mod) >= 0.6)
            return new ModTierInfo(5, hasAssemblies ? 5 : 0, "渲染与动画");

        // 工具（二类）：没有内容产出，却对游戏做了大量运行时介入。
        // ① 零 Def + 海量静态补丁 → 外部桥接 / 改造层（把游戏当宿主跑别的进程）；
        // ② 零静态补丁 + 大量动态注入点 → 纯观测型工具（性能分析、日志埋点）。
        if (defCount == 0 && HarmonyPatchCount(mod) >= ToolBridgePatchThreshold)
            return new ModTierInfo(1, 25, "系统与诊断");

        if (HarmonyPatchCount(mod) == 0 && DynamicPatchSiteCount(mod) >= ToolDynamicSiteThreshold)
            return new ModTierInfo(1, 25, "系统与诊断");

        // 系统与诊断：不建 Def、只挂被动补丁（Postfix/Transpiler/Finalizer），目标类型分散
        if (IsPassiveInstrumentation(mod))
            return new ModTierInfo(1, 20, "系统与诊断");

        return new ModTierInfo(4, 0, "玩法与扩展");
    }

    /// <summary>零内容模组被判为"桥接/改造类工具"所需的静态补丁数。</summary>
    private const int ToolBridgePatchThreshold = 30;

    /// <summary>零静态补丁时，被判为"观测类工具"所需的动态注入点数。</summary>
    private const int ToolDynamicSiteThreshold = 10;

    private static int HarmonyPatchCount(ModDescriptor mod) =>
        mod.AssemblyAnalyses.Sum(a => a.Patches.Count);

    private static int DynamicPatchSiteCount(ModDescriptor mod) =>
        mod.AssemblyAnalyses.Sum(a => a.DynamicPatchSites.Count);

    /// <summary>程序集里是否有任何"改游戏行为"的痕迹：Harmony 补丁、动态补丁点、类覆写。</summary>
    private static bool HasAnyCodeBehavior(ModDescriptor mod) =>
        mod.AssemblyAnalyses.Any(a =>
            a.Patches.Count > 0 ||
            a.DynamicPatchSites.Count > 0 ||
            a.ClassOverrides.Count > 0);

    /// <summary>框架判定时"内容体量"的上限：超过它说明这个被依赖的模组同时是内容包。</summary>
    private const int FrameworkDefCeiling = 50;

    private static bool HasLanguagesPayload(ModDescriptor mod) =>
        mod.ActiveContentDirectories.Any(dir => Directory.Exists(Path.Combine(dir, "Languages")));

    private static bool DefinesRaceOrFaction(ModDescriptor mod)
    {
        foreach (var def in mod.XmlDefs)
        {
            if (def.DefType.Contains("AlienRace", StringComparison.OrdinalIgnoreCase))
                return true;

            if (def.DefType.Equals("FactionDef", StringComparison.OrdinalIgnoreCase))
                return true;

            // ThingDef 挂的是 Pawn 系 C# 类：结构信号，不是名字匹配
            if (def.ThingClass is { } thingClass &&
                (thingClass.Contains("AlienRace", StringComparison.OrdinalIgnoreCase) ||
                 thingClass.Contains("Pawn", StringComparison.OrdinalIgnoreCase)))
                return true;
        }

        return false;
    }

    private static bool HasBlockingPrefix(ModDescriptor mod) =>
        mod.AssemblyAnalyses
            .SelectMany(a => a.Patches)
            .Any(p => p.PatchKind == "Prefix" && p.Behavior.AlwaysSkipsOriginal);

    private static bool IsVisualOnly(ModDescriptor mod)
    {
        if (mod.XmlDefs.Count == 0)
            return false;

        return mod.XmlDefs.All(d => IsVisualDefType(d.DefType));
    }

    private static bool IsVisualDefType(string defType) =>
        defType.Contains("ShapeDef", StringComparison.OrdinalIgnoreCase) ||
        defType.Contains("Animation", StringComparison.OrdinalIgnoreCase) ||
        defType.Contains("Graphic", StringComparison.OrdinalIgnoreCase) ||
        defType.Contains("Shader", StringComparison.OrdinalIgnoreCase) ||
        defType.Contains("Texture", StringComparison.OrdinalIgnoreCase);

    /// <summary>补丁目标落在 Unity / 渲染 / 绘制 API 上的比例（按目标类型全名判断，属于代码符号）。</summary>
    private static double VisualPatchShare(ModDescriptor mod)
    {
        var targets = mod.AssemblyAnalyses
            .SelectMany(a => a.Patches)
            .Select(p => p.TargetTypeFullName)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Cast<string>()
            .ToList();

        if (targets.Count < 3)
            return 0;

        var visual = targets.Count(IsVisualTargetType);
        return visual / (double)targets.Count;
    }

    private static bool IsVisualTargetType(string typeFullName) =>
        typeFullName.Contains("UnityEngine", StringComparison.OrdinalIgnoreCase) ||
        typeFullName.Contains("Graphic", StringComparison.OrdinalIgnoreCase) ||
        typeFullName.Contains("Render", StringComparison.OrdinalIgnoreCase) ||
        typeFullName.Contains("Mesh", StringComparison.OrdinalIgnoreCase) ||
        typeFullName.Contains("Shader", StringComparison.OrdinalIgnoreCase) ||
        typeFullName.Contains("Texture", StringComparison.OrdinalIgnoreCase) ||
        typeFullName.Contains("Draw", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 被动插桩特征：不建任何 Def/Patch，只挂 Postfix/Transpiler/Finalizer（不拦截原方法），
    /// 且目标类型足够分散 —— 典型是性能分析、日志、调试类工具。
    /// </summary>
    private static bool IsPassiveInstrumentation(ModDescriptor mod)
    {
        if (mod.XmlDefs.Count > 0 || mod.XmlPatches.Count > 0)
            return false;

        var patches = mod.AssemblyAnalyses.SelectMany(a => a.Patches).ToList();
        if (patches.Count < 5)
            return false;

        if (patches.Any(p => p.PatchKind == "Prefix" && p.Behavior.CanSkipOriginal))
            return false;

        var targetTypes = patches
            .Select(p => p.TargetTypeFullName)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.Ordinal)
            .Count();

        return targetTypes >= 5;
    }

    /// <summary>
    /// 判断加入 start → target 这条边是否会形成环（即 target 已能到达 start）。
    /// </summary>
    private static bool CreatesCycle(
        Dictionary<string, HashSet<string>> adjacency,
        string startId,
        string targetId)
    {
        if (startId.Equals(targetId, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!adjacency.TryGetValue(targetId, out var firstHop))
            return false;

        if (firstHop.Count == 0)
            return false;

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { targetId };
        var queue = new Queue<string>();
        queue.Enqueue(targetId);

        while (queue.Count > 0)
        {
            var curr = queue.Dequeue();
            if (!adjacency.TryGetValue(curr, out var neighbors))
                continue;

            foreach (var next in neighbors)
            {
                if (next.Equals(startId, StringComparison.OrdinalIgnoreCase))
                    return true;

                if (visited.Add(next))
                    queue.Enqueue(next);
            }
        }

        return false;
    }
}
