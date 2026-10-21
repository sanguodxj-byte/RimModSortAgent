using ModSorterAgent.Discovery;
using ModSorterAgent.Grading;
using ModSorterAgent.Models;

namespace ModSorterAgent.Sorting;

/// <summary>
/// <see cref="TopologicalModSorter"/> 与约束生成的合成夹具自检。
///
/// 覆盖的是"排序正确性"，不是"排序好看不好看"：
/// · 官方 DLC 的权威次序必须在任意乱序输入下都成立（Core 永远最前）；
/// · 社区模组声明 loadBefore 官方底座时必须生效（Loading Progress 这类启动期模组）；
/// · 真正自相矛盾的声明（依赖对方却要求排在其前）仍要被丢弃，不能凭空造环；
/// · 硬依赖 / loadAfter / loadBefore 的基本传递性。
///
/// 夹具名字全是 SelfTest* 自造名，不复用线上真实案例。
/// </summary>
public static class TopologicalSortSelfTest
{
    public static IReadOnlyList<string> Run()
    {
        var failures = new List<string>();

        try
        {
            AssertOfficialDlcOrderSurvivesShuffledInput(failures);
            AssertLoadBeforeBaseIsHonored(failures);
            AssertContradictoryDeclarationIsDropped(failures);
            AssertBasicConstraints(failures);
            AssertCycleIsReportedNotFatal(failures);
        }
        catch (Exception ex)
        {
            failures.Add($"自检异常：{ex.GetType().Name}: {ex.Message}");
        }

        return failures;
    }

    /// <summary>
    /// 官方 DLC 只用 forceLoadAfter/forceLoadBefore 表达先后，不带 modDependencies。
    /// 输入顺序完全打乱时，输出仍必须是 Core → Royalty → Ideology → Biotech → Anomaly → Odyssey。
    /// </summary>
    private static void AssertOfficialDlcOrderSurvivesShuffledInput(List<string> failures)
    {
        var official = new[]
        {
            "ludeon.rimworld",
            "ludeon.rimworld.royalty",
            "ludeon.rimworld.ideology",
            "ludeon.rimworld.biotech",
            "ludeon.rimworld.anomaly",
            "ludeon.rimworld.odyssey"
        };

        // 故意反向输入：旧实现会把 Core 排到最后
        var mods = official
            .Reverse()
            .Select((id, i) => Mod(
                id,
                id,
                originalIndex: i,
                official: true,
                forceLoadAfter: ForceLoadAfterFor(id),
                forceLoadBefore: ForceLoadBeforeFor(id)))
            .ToList();

        var result = Sort(mods);
        var actual = result.SortedMods.Select(m => m.PackageId).ToList();

        for (var i = 0; i < official.Length; i++)
        {
            if (actual[i] != official[i])
            {
                failures.Add($"官方 DLC 次序错误：第 {i + 1} 位应为 {official[i]}，实际 {actual[i]}（实际序列 {string.Join(" → ", actual)}）");
                return;
            }
        }
    }

    private static IEnumerable<string> ForceLoadAfterFor(string id)
    {
        var order = new[]
        {
            "ludeon.rimworld",
            "ludeon.rimworld.royalty",
            "ludeon.rimworld.ideology",
            "ludeon.rimworld.biotech",
            "ludeon.rimworld.anomaly",
            "ludeon.rimworld.odyssey"
        };

        var idx = Array.IndexOf(order, id);
        return idx <= 0 ? Array.Empty<string>() : order.Take(idx);
    }

    private static IEnumerable<string> ForceLoadBeforeFor(string id)
    {
        var order = new[]
        {
            "ludeon.rimworld",
            "ludeon.rimworld.royalty",
            "ludeon.rimworld.ideology",
            "ludeon.rimworld.biotech",
            "ludeon.rimworld.anomaly",
            "ludeon.rimworld.odyssey"
        };

        var idx = Array.IndexOf(order, id);
        return idx < 0 || idx >= order.Length - 1 ? Array.Empty<string>() : order.Skip(idx + 1);
    }

    /// <summary>
    /// 社区模组声明 loadBefore 官方 Core 时必须生效——这是启动期模组的硬需求。
    /// 旧实现把"与底座相关"的声明整类丢弃，导致该模组被排到 Core 之后。
    /// </summary>
    private static void AssertLoadBeforeBaseIsHonored(List<string> failures)
    {
        var mods = new List<ModDescriptor>
        {
            Mod("ludeon.rimworld", "Core", originalIndex: 0, official: true),
            Mod("selftest.loader", "SelfTest Loader", originalIndex: 1, loadBefore: new[] { "ludeon.rimworld" })
        };

        var actual = Sort(mods).SortedMods.Select(m => m.PackageId).ToList();

        if (actual.IndexOf("selftest.loader") > actual.IndexOf("ludeon.rimworld"))
            failures.Add($"loadBefore 官方底座未生效：SelfTest Loader 应在 Core 之前，实际 {string.Join(" → ", actual)}");
    }

    /// <summary>
    /// 依赖对方却声明 loadBefore 对方——自相矛盾，必须丢弃该声明而不是造出环路。
    /// </summary>
    private static void AssertContradictoryDeclarationIsDropped(List<string> failures)
    {
        var mods = new List<ModDescriptor>
        {
            Mod("selftest.framework", "SelfTest Framework", originalIndex: 0),
            Mod(
                "selftest.weird",
                "SelfTest Weird",
                originalIndex: 1,
                loadBefore: new[] { "selftest.framework" },
                dependencies: new[] { "selftest.framework" })
        };

        var result = Sort(mods);

        if (result.HardCycleDescriptions.Count > 0)
            failures.Add($"自相矛盾声明不应产生硬性环路：{result.HardCycleDescriptions[0]}");

        var actual = result.SortedMods.Select(m => m.PackageId).ToList();
        if (actual.IndexOf("selftest.framework") > actual.IndexOf("selftest.weird"))
            failures.Add($"强依赖必须优先于矛盾的 loadBefore：SelfTest Framework 应在 SelfTest Weird 之前，实际 {string.Join(" → ", actual)}");
    }

    private static void AssertBasicConstraints(List<string> failures)
    {
        var mods = new List<ModDescriptor>
        {
            Mod("selftest.lib", "SelfTest Lib", originalIndex: 0),
            Mod("selftest.mid", "SelfTest Mid", originalIndex: 1, dependencies: new[] { "selftest.lib" }),
            Mod("selftest.top", "SelfTest Top", originalIndex: 2, loadAfter: new[] { "selftest.mid" })
        };

        var actual = Sort(mods).SortedMods.Select(m => m.PackageId).ToList();

        if (!IsBefore(actual, "selftest.lib", "selftest.mid"))
            failures.Add($"强依赖未生效：SelfTest Lib 应在 SelfTest Mid 之前，实际 {string.Join(" → ", actual)}");

        if (!IsBefore(actual, "selftest.mid", "selftest.top"))
            failures.Add($"loadAfter 未生效：SelfTest Mid 应在 SelfTest Top 之前，实际 {string.Join(" → ", actual)}");
    }

    /// <summary>
    /// 真环路（双方互声明 loadAfter）必须被断开并上报，而不是抛异常或丢模组。
    /// </summary>
    private static void AssertCycleIsReportedNotFatal(List<string> failures)
    {
        var mods = new List<ModDescriptor>
        {
            Mod("selftest.alpha", "SelfTest Alpha", originalIndex: 0, loadAfter: new[] { "selftest.beta" }),
            Mod("selftest.beta", "SelfTest Beta", originalIndex: 1, loadAfter: new[] { "selftest.alpha" })
        };

        var result = Sort(mods);

        if (result.SortedMods.Count != mods.Count)
            failures.Add($"环路不应丢模组：应输出 {mods.Count} 项，实际 {result.SortedMods.Count} 项");

        if (result.HardCycleDescriptions.Count == 0)
            failures.Add("互声明 loadAfter 应被上报为硬性环路，实际未上报");
    }

    private static bool IsBefore(IReadOnlyList<string> order, string first, string second) =>
        IndexOf(order, first) >= 0 && IndexOf(order, second) >= 0 &&
        IndexOf(order, first) < IndexOf(order, second);

    private static int IndexOf(IReadOnlyList<string> order, string value)
    {
        for (var i = 0; i < order.Count; i++)
        {
            if (order[i] == value)
                return i;
        }

        return -1;
    }

    private static TopologicalModSorter.SortResult Sort(IReadOnlyList<ModDescriptor> mods)
    {
        var grading = ConflictGrader.AnalyzeAndGrade(mods);
        return TopologicalModSorter.Sort(mods, grading.OrderingEdges);
    }

    private static ModDescriptor Mod(
        string packageId,
        string name,
        int originalIndex,
        bool official = false,
        IEnumerable<string>? loadAfter = null,
        IEnumerable<string>? loadBefore = null,
        IEnumerable<string>? forceLoadAfter = null,
        IEnumerable<string>? forceLoadBefore = null,
        IEnumerable<string>? dependencies = null)
        => new()
        {
            PackageId = packageId,
            Name = name,
            RootDirectory = Path.Combine(Path.GetTempPath(), "msa-sort-selftest", packageId),
            OriginalIndex = originalIndex,
            IsOfficialCoreOrDlc = official,
            Dependencies = (dependencies ?? Array.Empty<string>())
                .Select(d => new ModDependencyItem(d, d))
                .ToArray(),
            LoadAfter = loadAfter?.ToArray() ?? Array.Empty<string>(),
            LoadBefore = loadBefore?.ToArray() ?? Array.Empty<string>(),
            ForceLoadAfter = forceLoadAfter?.ToArray() ?? Array.Empty<string>(),
            ForceLoadBefore = forceLoadBefore?.ToArray() ?? Array.Empty<string>()
        };
}
