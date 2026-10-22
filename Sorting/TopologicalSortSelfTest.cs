using System.Xml.Linq;
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
/// · 依赖启用要求不覆盖显式加载顺序，真正的显式排序环必须留下诊断；
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
            AssertExplicitOrderOverridesDependencyPreference(failures);
            AssertBasicConstraints(failures);
            AssertCycleIsReportedNotFatal(failures);
            AssertAlternativePackageIdsDependencyIsSatisfied(failures);
            AssertUnsatisfiedPoolModDependencyFails(failures);
            AssertAlternativeSatisfiesWhenPrimaryInPool(failures);
            AssertAboutXmlDependencyParsingWithAlternatives(failures);
            AssertUnconstrainedSortingValidationPasses(failures);
            AssertHardEdgeConstraintValidation(failures);
            AssertDuplicateAndMissingModValidationFails(failures);
            AssertInvalidEdgesAndHardCycleRejection(failures);
            AssertSortValidationExceptionThrown(failures);
            AssertFinalActiveSet_PurpleFilteredDependencyBlocked(failures);
            AssertFinalActiveSet_LegalSubsetPasses(failures);
            AssertFinalActiveSet_FilteredPrerequisiteAdjacentHardEdgeFails(failures);
            AssertFinalActiveSet_MissingAnalysisMetadataFails(failures);
            AssertFinalActiveSet_InactivePoolCannotSatisfyDependency(failures);
            AssertFinalActiveSet_AlternativeDependencySatisfiedAndOrderChecked(failures);
            AssertFinalActiveSet_ExplicitConstraintsOmittedFromAppliedEdgesChecked(failures);
            AssertFinalActiveSet_BaseIdDuplicateDetected(failures);
            AssertDeterministicOrderingAndRandomGraphProperties(failures);
            AssertIndependentDeclarationAndWriteGuards(failures);
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
    /// 模组声明 loadBefore 某目标，同时在 modDependencies 中依赖该目标：
    /// modDependencies 为启用满足性关系（弱偏好），显式 loadBefore 为硬顺序约束，
    /// 显式约束优先生效（Weird 在 Framework 之前），不能因依赖而丢弃显式 loadBefore，也不得造出硬性环路，且排序结果可通过校验写入配置。
    /// </summary>
    private static void AssertExplicitOrderOverridesDependencyPreference(List<string> failures)
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
            failures.Add($"modDependencies 与 loadBefore 并存不应产生硬性环路：{result.HardCycleDescriptions[0]}");

        var actual = result.SortedMods.Select(m => m.PackageId).ToList();
        if (actual.IndexOf("selftest.weird") > actual.IndexOf("selftest.framework"))
            failures.Add($"显式 loadBefore 必须优先于 modDependencies 默认前置偏好：SelfTest Weird 应在 SelfTest Framework 之前，实际 {string.Join(" → ", actual)}");

        var validation = SortOrderValidator.Validate(mods, result);
        if (!validation.IsValid)
            failures.Add($"排序结果后置校验失败：{string.Join("; ", validation.Issues.Select(i => i.Message))}");

        var activeValidation = SortOrderValidator.ValidateActiveModSet(actual, mods, result.AppliedEdges);
        if (!activeValidation.IsValid)
            failures.Add($"最终启用集合校验失败：{string.Join("; ", activeValidation.Issues.Select(i => i.Message))}");
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

    private static void AssertAlternativePackageIdsDependencyIsSatisfied(List<string> failures)
    {
        var depItem = new ModDependencyItem(
            packageId: "selftest.missing.primary",
            displayName: "Missing Primary",
            alternativePackageIds: new[] { "selftest.alt.active" });

        var mods = new List<ModDescriptor>
        {
            Mod("selftest.consumer", "SelfTest Consumer", originalIndex: 0, dependencyItems: new[] { depItem }),
            Mod("selftest.alt.active", "SelfTest Alt Active", originalIndex: 1)
        };

        var grading = ConflictGrader.AnalyzeAndGrade(mods);
        if (grading.MissingDependencies.Count > 0)
            failures.Add($"备选 PackageId 应满足依赖：实际报错 {string.Join("; ", grading.MissingDependencies)}");

        if (grading.Findings.Any(f => f.Category == "MissingDependency"))
            failures.Add("备选 PackageId 存在时不应上报 MissingDependency 冲突");

        var result = TopologicalModSorter.Sort(mods, grading.OrderingEdges);
        var actual = result.SortedMods.Select(m => m.PackageId).ToList();

        if (!IsBefore(actual, "selftest.alt.active", "selftest.consumer"))
            failures.Add($"备选 PackageId 前置约束未生效：SelfTest Alt Active 应在 SelfTest Consumer 之前，实际 {string.Join(" → ", actual)}");
    }

    private static void AssertUnsatisfiedPoolModDependencyFails(List<string> failures)
    {
        var mods = new List<ModDescriptor>
        {
            Mod("selftest.active.mod", "SelfTest Active", originalIndex: 0, dependencies: new[] { "selftest.pool.lib" }),
            Mod("selftest.pool.lib", "SelfTest Pool Lib", originalIndex: 1, isActive: false)
        };

        var grading = ConflictGrader.AnalyzeAndGrade(mods);
        if (grading.MissingDependencies.Count == 0)
            failures.Add("未启用的订阅池模组不能满足依赖：应上报缺失前置，实际未上报");

        var hasMissingFinding = grading.Findings.Any(f =>
            f.Category == "MissingDependency" &&
            f.ModAPackageId == "selftest.active.mod" &&
            f.ModBPackageId == "selftest.pool.lib");

        if (!hasMissingFinding)
            failures.Add("应生成 MissingDependency 评级结果，实际未找到对应 Finding");
    }

    private static void AssertAlternativeSatisfiesWhenPrimaryInPool(List<string> failures)
    {
        var depItem = new ModDependencyItem(
            packageId: "selftest.pool.primary",
            displayName: "Pool Primary",
            alternativePackageIds: new[] { "selftest.active.alt" });

        var mods = new List<ModDescriptor>
        {
            Mod("selftest.consumer", "SelfTest Consumer", originalIndex: 0, dependencyItems: new[] { depItem }),
            Mod("selftest.pool.primary", "SelfTest Pool Primary", originalIndex: 1, isActive: false),
            Mod("selftest.active.alt", "SelfTest Active Alt", originalIndex: 2, isActive: true)
        };

        var grading = ConflictGrader.AnalyzeAndGrade(mods);
        if (grading.MissingDependencies.Count > 0)
            failures.Add($"主要依赖在订阅池但备选依赖已启用时应满足依赖：实际报错 {string.Join("; ", grading.MissingDependencies)}");

        var result = TopologicalModSorter.Sort(mods, grading.OrderingEdges);
        var actual = result.SortedMods.Select(m => m.PackageId).ToList();

        if (!IsBefore(actual, "selftest.active.alt", "selftest.consumer"))
            failures.Add($"已启用的备选依赖应排在引用模组之前：实际 {string.Join(" → ", actual)}");
    }

    private static void AssertAboutXmlDependencyParsingWithAlternatives(List<string> failures)
    {
        var xml = """
            <ModMetaData>
              <name>TestMod</name>
              <packageId>selftest.parser.test</packageId>
              <modDependencies>
                <LI>
                  <PACKAGEID>brrainz.harmony</PACKAGEID>
                  <displayName>Harmony</displayName>
                  <AlternativePackageIds>
                    <LI>brrainz.harmony.dev</LI>
                    <li>brrainz.harmony.alt</li>
                  </AlternativePackageIds>
                </LI>
              </modDependencies>
            </ModMetaData>
            """;

        var doc = XDocument.Parse(xml);
        var deps = ModListResolver.ExtractDependencies(doc.Root!, "1.5");

        if (deps.Count != 1)
        {
            failures.Add($"About.xml 依赖解析项数错误：期望 1 项，实际 {deps.Count} 项");
            return;
        }

        var dep = deps[0];
        if (dep.PackageId != "brrainz.harmony")
            failures.Add($"主 PackageId 大小写不敏感解析错误：期望 brrainz.harmony，实际 {dep.PackageId}");

        if (dep.AlternativePackageIds.Count != 2 ||
            !dep.AlternativePackageIds.Contains("brrainz.harmony.dev") ||
            !dep.AlternativePackageIds.Contains("brrainz.harmony.alt"))
        {
            failures.Add($"alternativePackageIds 大小写不敏感解析错误：实际 {string.Join(", ", dep.AlternativePackageIds)}");
        }
    }

    private static void AssertUnconstrainedSortingValidationPasses(List<string> failures)
    {
        var mods = new List<ModDescriptor>
        {
            Mod("selftest.uncon.a", "Unconstrained A", originalIndex: 0),
            Mod("selftest.uncon.b", "Unconstrained B", originalIndex: 1),
            Mod("selftest.uncon.c", "Unconstrained C", originalIndex: 2)
        };

        var sortResult = TopologicalModSorter.Sort(mods, Array.Empty<OrderingEdge>());
        var validation = SortOrderValidator.Validate(mods, sortResult);
        if (!sortResult.SortedMods.Select(m => m.PackageId).SequenceEqual(mods.Select(m => m.PackageId)))
            failures.Add("无约束同层模组必须保留原始相对顺序。");

        if (!validation.IsValid)
        {
            failures.Add($"无约束模组排序后置校验应通过，实际失败：{string.Join("; ", validation.Issues.Select(i => i.Message))}");
            return;
        }

        if (validation.Issues.Count > 0)
        {
            failures.Add($"无约束模组排序后置校验不应存在任何问题，实际存在 {validation.Issues.Count} 项");
            return;
        }

        try
        {
            SortOrderValidator.ValidateAndThrow(mods, sortResult);
        }
        catch (Exception ex)
        {
            failures.Add($"无约束校验通过时不应抛出异常：{ex.Message}");
        }
    }

    private static void AssertHardEdgeConstraintValidation(List<string> failures)
    {
        var edge1 = new OrderingEdge("selftest.hard.first", "selftest.hard.second", true, 100, "About.loadAfter", "硬依赖");
        var edge2 = new OrderingEdge("selftest.hard.second", "selftest.hard.third", true, 90, "About.loadAfter", "硬依赖");
        var mods = new List<ModDescriptor>
        {
            Mod("selftest.hard.third", "Hard Third", originalIndex: 0),
            Mod("selftest.hard.second", "Hard Second", originalIndex: 1),
            Mod("selftest.hard.first", "Hard First", originalIndex: 2)
        };

        var sortResult = TopologicalModSorter.Sort(mods, new[] { edge1, edge2 });
        var validation = SortOrderValidator.Validate(mods, sortResult);

        if (!validation.IsValid)
        {
            failures.Add($"硬边约束满足时校验应通过，实际失败：{string.Join("; ", validation.Issues.Select(i => i.Message))}");
            return;
        }

        if (!SortOrderValidator.IsEdgeSuccessfullyApplied(edge1, sortResult))
            failures.Add("硬边 edge1 应被判定为成功生效");

        if (!SortOrderValidator.IsEdgeSuccessfullyApplied(edge2, sortResult))
            failures.Add("硬边 edge2 应被判定为成功生效");

        var invertedMods = new List<ModDescriptor> { mods[0], mods[1], mods[2] };
        var violatedEdge = new OrderingEdge("selftest.hard.first", "selftest.hard.third", true, 100, "About.loadAfter", "硬依赖");
        var invertedResult = sortResult with
        {
            SortedMods = invertedMods,
            AppliedEdges = new List<OrderingEdge> { violatedEdge }
        };

        var invertedValidation = SortOrderValidator.Validate(mods, invertedResult);
        if (invertedValidation.IsValid)
        {
            failures.Add("倒置顺序违反硬边约束时应校验失败，实际却通过");
            return;
        }

        if (!invertedValidation.Issues.Any(i => i.ErrorType == SortValidationErrorType.EdgeOrderViolation))
        {
            failures.Add("硬边顺序违反应上报 EdgeOrderViolation 异常类型");
        }
    }

    private static void AssertDuplicateAndMissingModValidationFails(List<string> failures)
    {
        var mods = new List<ModDescriptor>
        {
            Mod("selftest.dm.one", "One", originalIndex: 0),
            Mod("selftest.dm.two", "Two", originalIndex: 1)
        };
        var validResult = TopologicalModSorter.Sort(mods, Array.Empty<OrderingEdge>());

        // 1. 重复项校验
        var duplicateResult = validResult with
        {
            SortedMods = new List<ModDescriptor> { mods[0], mods[1], mods[0] }
        };
        var dupValidation = SortOrderValidator.Validate(mods, duplicateResult);
        if (dupValidation.IsValid)
            failures.Add("SortedMods 存在重复项时应校验失败，实际却通过");
        else if (!dupValidation.Issues.Any(i => i.ErrorType == SortValidationErrorType.DuplicatePackageId))
            failures.Add("SortedMods 存在重复项时应上报 DuplicatePackageId");

        // 2. 遗漏项校验
        var missingResult = validResult with
        {
            SortedMods = new List<ModDescriptor> { mods[0] }
        };
        var missValidation = SortOrderValidator.Validate(mods, missingResult);
        if (missValidation.IsValid)
            failures.Add("SortedMods 遗漏输入项时应校验失败，实际却通过");
        else if (!missValidation.Issues.Any(i => i.ErrorType == SortValidationErrorType.MissingPackageId))
            failures.Add("SortedMods 遗漏输入项时应上报 MissingPackageId");

        // 3. 未知未预期项校验
        var extraResult = validResult with
        {
            SortedMods = new List<ModDescriptor> { mods[0], mods[1], Mod("selftest.dm.extra", "Extra", 2) }
        };
        var extraValidation = SortOrderValidator.Validate(mods, extraResult);
        if (extraValidation.IsValid)
            failures.Add("SortedMods 出现未预期项时应校验失败，实际却通过");
        else if (!extraValidation.Issues.Any(i => i.ErrorType == SortValidationErrorType.UnexpectedPackageId))
            failures.Add("SortedMods 出现未预期项时应上报 UnexpectedPackageId");
    }

    private static void AssertInvalidEdgesAndHardCycleRejection(List<string> failures)
    {
        var mods = new List<ModDescriptor>
        {
            Mod("selftest.edge.a", "Mod A", originalIndex: 0),
            Mod("selftest.edge.b", "Mod B", originalIndex: 1)
        };
        var baseResult = TopologicalModSorter.Sort(mods, Array.Empty<OrderingEdge>());

        // 1. 引用不存在模组的非法边
        var ghostEdge = new OrderingEdge("selftest.ghost", "selftest.edge.a", true, 100, "test", "test");
        var ghostResult = baseResult with { AppliedEdges = new List<OrderingEdge> { ghostEdge } };
        var ghostValidation = SortOrderValidator.Validate(mods, ghostResult);
        if (ghostValidation.IsValid)
            failures.Add("生效边引用不存在模组时应校验失败，实际却通过");
        else if (!ghostValidation.Issues.Any(i => i.ErrorType == SortValidationErrorType.InvalidEdgeNode))
            failures.Add("生效边引用不存在模组时应上报 InvalidEdgeNode");

        // 2. 自环非法边
        var selfLoopEdge = new OrderingEdge("selftest.edge.a", "selftest.edge.a", true, 100, "test", "test");
        var selfLoopResult = baseResult with { AppliedEdges = new List<OrderingEdge> { selfLoopEdge } };
        var selfLoopValidation = SortOrderValidator.Validate(mods, selfLoopResult);
        if (selfLoopValidation.IsValid)
            failures.Add("生效边为自环时应校验失败，实际却通过");
        else if (!selfLoopValidation.Issues.Any(i => i.ErrorType == SortValidationErrorType.SelfLoopEdge))
            failures.Add("生效边为自环时应上报 SelfLoopEdge");

        // 3. 硬环已记录的边不当作成功
        var edgeAB = new OrderingEdge("selftest.cyc.a", "selftest.cyc.b", true, 100, "About.loadAfter", "循环 A->B");
        var edgeBA = new OrderingEdge("selftest.cyc.b", "selftest.cyc.a", true, 100, "About.loadAfter", "循环 B->A");
        var cycMods = new List<ModDescriptor>
        {
            Mod("selftest.cyc.a", "Cyc A", originalIndex: 0),
            Mod("selftest.cyc.b", "Cyc B", originalIndex: 1)
        };

        var cycResult = TopologicalModSorter.Sort(cycMods, new[] { edgeAB, edgeBA });
        if (cycResult.HardCycleDescriptions.Count == 0)
        {
            failures.Add("双向硬约束应被记录为硬性环路");
            return;
        }

        if (cycResult.BrokenEdges.Count == 0)
        {
            failures.Add("被断开的硬边应被记录到 BrokenEdges");
            return;
        }

        var brokenEdge = cycResult.BrokenEdges[0];
        if (SortOrderValidator.IsEdgeSuccessfullyApplied(brokenEdge, cycResult))
            failures.Add("被断开的硬环边不应被判定为成功生效");

        var illegalAppliedResult = cycResult with
        {
            AppliedEdges = new List<OrderingEdge>(cycResult.AppliedEdges) { brokenEdge }
        };
        var illegalValidation = SortOrderValidator.Validate(cycMods, illegalAppliedResult);
        if (illegalValidation.IsValid)
            failures.Add("硬环已断开边若出现在生效边中应校验失败，实际却通过");
        else if (!illegalValidation.Issues.Any(i => i.ErrorType == SortValidationErrorType.HardCycleEdgeTreatedAsApplied))
            failures.Add("硬环断开边出现在生效边中应上报 HardCycleEdgeTreatedAsApplied");
    }

    private static void AssertSortValidationExceptionThrown(List<string> failures)
    {
        var mods = new List<ModDescriptor>
        {
            Mod("selftest.ex.a", "Mod A", originalIndex: 0),
            Mod("selftest.ex.b", "Mod B", originalIndex: 1)
        };
        var validResult = TopologicalModSorter.Sort(mods, Array.Empty<OrderingEdge>());
        var corruptedResult = validResult with { SortedMods = new List<ModDescriptor> { mods[0] } };

        try
        {
            SortOrderValidator.ValidateAndThrow(mods, corruptedResult);
            failures.Add("校验失败时 ValidateAndThrow 必须抛出 SortValidationException，实际未抛出");
        }
        catch (SortValidationException ex)
        {
            if (ex.ValidationResult.IsValid)
                failures.Add("异常中的 ValidationResult.IsValid 应为 false");
            if (ex.Issues.Count == 0)
                failures.Add("异常中的 Issues 不应为空");
        }
        catch (Exception ex)
        {
            failures.Add($"ValidateAndThrow 应抛出 SortValidationException，实际抛出 {ex.GetType().Name}");
        }
    }

    private static void AssertFinalActiveSet_PurpleFilteredDependencyBlocked(List<string> failures)
    {
        var modPurpleA = Mod("selftest.purple.a", "Mod Purple A", originalIndex: 0);
        var modPurpleX = Mod("selftest.purple.x", "Mod Purple X", originalIndex: 1);
        var modConsumer = Mod("selftest.consumer.b", "Mod Consumer B", originalIndex: 2,
            dependencies: new[] { "selftest.purple.a" });

        var allMods = new List<ModDescriptor> { modPurpleA, modPurpleX, modConsumer };
        var edge = new OrderingEdge("selftest.purple.a", "selftest.consumer.b", true, 100, "About.modDependencies", "强依赖");
        var appliedEdges = new List<OrderingEdge> { edge };

        var filteredActiveIds = new[] { "selftest.purple.x", "selftest.consumer.b" };

        var validation = SortOrderValidator.ValidateActiveModSet(filteredActiveIds, allMods, appliedEdges);
        if (validation.IsValid)
        {
            failures.Add("紫色冲突过滤掉依赖前置后应校验失败阻止导出，实际却通过");
            return;
        }

        var hasMissingDepIssue = validation.Issues.Any(i =>
            i.ErrorType == SortValidationErrorType.MissingActiveDependency &&
            i.PackageId == "selftest.consumer.b");

        if (!hasMissingDepIssue)
        {
            failures.Add("紫色冲突过滤掉依赖前置后应报 MissingActiveDependency 异常，实际未找到");
        }

        try
        {
            SortOrderValidator.ValidateActiveModSetAndThrow(filteredActiveIds, allMods, appliedEdges);
            failures.Add("ValidateActiveModSetAndThrow 在依赖前置缺失时应抛出 SortValidationException，实际未抛出");
        }
        catch (SortValidationException)
        {
        }
        catch (Exception ex)
        {
            failures.Add($"ValidateActiveModSetAndThrow 抛出了非预期的异常类型：{ex.GetType().Name}");
        }
    }

    private static void AssertFinalActiveSet_LegalSubsetPasses(List<string> failures)
    {
        var modCore = Mod("ludeon.rimworld", "RimWorld Core", originalIndex: 0, official: true);
        var modA = Mod("selftest.subset.a", "Mod Subset A", originalIndex: 1,
            dependencies: new[] { "ludeon.rimworld" });
        var modB = Mod("selftest.subset.b", "Mod Subset B", originalIndex: 2,
            dependencies: new[] { "selftest.subset.a" },
            loadAfter: new[] { "selftest.subset.a" });
        var modUnrelated = Mod("selftest.unrelated.c", "Mod Unrelated C", originalIndex: 3);

        var allMods = new List<ModDescriptor> { modCore, modA, modB, modUnrelated };
        var edgeCoreA = new OrderingEdge("ludeon.rimworld", "selftest.subset.a", true, 100, "About.modDependencies", "Core依赖");
        var edgeAB = new OrderingEdge("selftest.subset.a", "selftest.subset.b", true, 100, "About.modDependencies", "A依赖");
        var appliedEdges = new List<OrderingEdge> { edgeCoreA, edgeAB };

        var legalSubsetIds = new[] { "ludeon.rimworld", "selftest.subset.a", "selftest.subset.b" };

        var validation = SortOrderValidator.ValidateActiveModSet(legalSubsetIds, allMods, appliedEdges);
        if (!validation.IsValid)
        {
            failures.Add($"合法子集应校验通过，实际失败：{string.Join("; ", validation.Issues.Select(i => i.Message))}");
            return;
        }

        try
        {
            SortOrderValidator.ValidateActiveModSetAndThrow(legalSubsetIds, allMods, appliedEdges);
        }
        catch (Exception ex)
        {
            failures.Add($"合法子集校验通过时不应抛出异常：{ex.Message}");
        }
    }

    private static void AssertFinalActiveSet_FilteredPrerequisiteAdjacentHardEdgeFails(List<string> failures)
    {
        var mod1 = Mod("selftest.chain.1", "Mod Chain 1", originalIndex: 0);
        var mod2 = Mod("selftest.chain.2", "Mod Chain 2", originalIndex: 1, loadAfter: new[] { "selftest.chain.1" });
        var mod3 = Mod("selftest.chain.3", "Mod Chain 3", originalIndex: 2, loadAfter: new[] { "selftest.chain.2" });

        var allMods = new List<ModDescriptor> { mod1, mod2, mod3 };
        var edge12 = new OrderingEdge("selftest.chain.1", "selftest.chain.2", true, 90, "About.loadAfter", "硬边 1->2");
        var edge23 = new OrderingEdge("selftest.chain.2", "selftest.chain.3", true, 90, "About.loadAfter", "硬边 2->3");
        var appliedEdges = new List<OrderingEdge> { edge12, edge23 };

        var invertedActiveIds = new[] { "selftest.chain.3", "selftest.chain.2" };

        var validation = SortOrderValidator.ValidateActiveModSet(invertedActiveIds, allMods, appliedEdges);
        if (validation.IsValid)
        {
            failures.Add("被过滤前置导致相邻硬边顺序未满足时应校验失败，实际却通过");
            return;
        }

        var hasEdgeViolation = validation.Issues.Any(i =>
            i.ErrorType == SortValidationErrorType.EdgeOrderViolation &&
            i.Edge?.BeforePackageId == "selftest.chain.2" &&
            i.Edge?.AfterPackageId == "selftest.chain.3");

        if (!hasEdgeViolation)
        {
            failures.Add("相邻硬边顺序违反应上报 EdgeOrderViolation 异常类型");
        }

        try
        {
            SortOrderValidator.ValidateActiveModSetAndThrow(invertedActiveIds, allMods, appliedEdges);
            failures.Add("ValidateActiveModSetAndThrow 在相邻硬边失败时应抛出 SortValidationException，实际未抛出");
        }
        catch (SortValidationException)
        {
        }
        catch (Exception ex)
        {
            failures.Add($"ValidateActiveModSetAndThrow 抛出了非预期的异常类型：{ex.GetType().Name}");
        }
    }

    private static void AssertFinalActiveSet_MissingAnalysisMetadataFails(List<string> failures)
    {
        var validation1 = SortOrderValidator.ValidateActiveModSet(
            new[] { "mod.a" },
            Array.Empty<ModDescriptor>());

        if (validation1.IsValid || !validation1.Issues.Any(i => i.ErrorType == SortValidationErrorType.MissingAnalysisMetadata))
            failures.Add("全局缺失分析元数据时应报 MissingAnalysisMetadata");

        var known = new List<ModDescriptor> { Mod("known.mod", "Known", 0) };
        var validation2 = SortOrderValidator.ValidateActiveModSet(
            new[] { "known.mod", "unknown.mod" },
            known);

        if (validation2.IsValid || !validation2.Issues.Any(i => i.ErrorType == SortValidationErrorType.MissingAnalysisMetadata && i.PackageId == "unknown.mod"))
            failures.Add("Profile 包含未知模组时应报 MissingAnalysisMetadata");
    }

    private static void AssertFinalActiveSet_InactivePoolCannotSatisfyDependency(List<string> failures)
    {
        var modActive = Mod("selftest.active.consumer", "Active Consumer", originalIndex: 0,
            dependencies: new[] { "selftest.pool.prereq" });
        var modPool = Mod("selftest.pool.prereq", "Pool Prerequisite", originalIndex: 1, isActive: false);

        var allMods = new List<ModDescriptor> { modActive, modPool };

        // 最终启用集合仅包含 active 模组，pool 模组未启用
        var finalActive = new[] { "selftest.active.consumer" };

        var validation = SortOrderValidator.ValidateActiveModSet(finalActive, allMods);
        if (validation.IsValid)
        {
            failures.Add("未启用的订阅池模组不能满足依赖：最终集合校验应失败阻止导出，实际却通过");
            return;
        }

        if (!validation.Issues.Any(i => i.ErrorType == SortValidationErrorType.MissingActiveDependency))
        {
            failures.Add("未启用订阅池模组未能满足依赖时应报 MissingActiveDependency");
        }
    }

    private static void AssertFinalActiveSet_AlternativeDependencySatisfiedAndOrderChecked(List<string> failures)
    {
        var depItem = new ModDependencyItem(
            packageId: "selftest.primary.missing",
            displayName: "Missing Primary",
            alternativePackageIds: new[] { "selftest.alt.present" });

        var modConsumer = Mod("selftest.alt.consumer", "Alt Consumer", originalIndex: 0,
            dependencyItems: new[] { depItem });
        var modAlt = Mod("selftest.alt.present", "Alt Present", originalIndex: 1);

        var allMods = new List<ModDescriptor> { modConsumer, modAlt };

        // 备选依赖在集合内且顺序 [alt, consumer]
        var validOrder = new[] { "selftest.alt.present", "selftest.alt.consumer" };
        var validResult = SortOrderValidator.ValidateActiveModSet(validOrder, allMods);
        if (!validResult.IsValid)
        {
            failures.Add($"备选依赖在集合内时应通过校验：实际失败 {string.Join("; ", validResult.Issues.Select(i => i.Message))}");
        }

        // 备选依赖在集合内且顺序 [consumer, alt]：依赖仅校验启用满足性，不强制顺序，应同样通过
        var alternativeLaterOrder = new[] { "selftest.alt.consumer", "selftest.alt.present" };
        var alternativeLaterResult = SortOrderValidator.ValidateActiveModSet(alternativeLaterOrder, allMods);
        if (!alternativeLaterResult.IsValid)
        {
            failures.Add($"依赖仅校验启用满足性（不强制顺序），合法alternate在后应放行：实际失败 {string.Join("; ", alternativeLaterResult.Issues.Select(i => i.Message))}");
        }
    }

    /// <summary>
    /// 最终启用集合校验：不在 AppliedEdges 的显式 loadBefore / loadAfter 独立校验、去重且放行合法排序。
    /// </summary>
    private static void AssertFinalActiveSet_ExplicitConstraintsOmittedFromAppliedEdgesChecked(List<string> failures)
    {
        var modA = Mod("selftest.explicit.a", "Explicit A", originalIndex: 0);
        var modB = Mod("selftest.explicit.b", "Explicit B", originalIndex: 1,
            loadAfter: new[] { "selftest.explicit.a" });
        var modC = Mod("selftest.explicit.c", "Explicit C", originalIndex: 2,
            loadBefore: new[] { "selftest.explicit.d" });
        var modD = Mod("selftest.explicit.d", "Explicit D", originalIndex: 3);
        var modE = Mod("selftest.explicit.e", "Explicit E", originalIndex: 4,
            loadAfter: new[] { "selftest.nonexistent.target" });
        var modF = Mod("selftest.steam.f_steam", "Explicit Steam F", originalIndex: 5);
        var modG = Mod("selftest.steam.g", "Explicit Steam G", originalIndex: 6,
            loadAfter: new[] { "selftest.steam.f" });

        var allMods = new List<ModDescriptor> { modA, modB, modC, modD, modE, modF, modG };

        var appliedEdgesEmpty = Array.Empty<OrderingEdge>();

        // 1. 显式 loadAfter 违规：B 在 A 之前 [B, A, C, D, E, F_steam, G]
        var invertedLoadAfter = new[]
        {
            "selftest.explicit.b",
            "selftest.explicit.a",
            "selftest.explicit.c",
            "selftest.explicit.d",
            "selftest.explicit.e",
            "selftest.steam.f_steam",
            "selftest.steam.g"
        };
        var res1 = SortOrderValidator.ValidateActiveModSet(invertedLoadAfter, allMods, appliedEdgesEmpty);
        if (res1.IsValid)
        {
            failures.Add("不在 AppliedEdges 的显式 loadAfter 被颠倒时应校验失败，实际却通过");
        }
        else if (!res1.Issues.Any(i => i.ErrorType == SortValidationErrorType.EdgeOrderViolation &&
                                      i.BeforeIndex > i.AfterIndex))
        {
            failures.Add("不在 AppliedEdges 的显式 loadAfter 颠倒应报 EdgeOrderViolation");
        }

        // 2. 显式 loadBefore 违规：D 在 C 之前 [A, B, D, C, E, F_steam, G]
        var invertedLoadBefore = new[]
        {
            "selftest.explicit.a",
            "selftest.explicit.b",
            "selftest.explicit.d",
            "selftest.explicit.c",
            "selftest.explicit.e",
            "selftest.steam.f_steam",
            "selftest.steam.g"
        };
        var res2 = SortOrderValidator.ValidateActiveModSet(invertedLoadBefore, allMods, appliedEdgesEmpty);
        if (res2.IsValid)
        {
            failures.Add("不在 AppliedEdges 的显式 loadBefore 被颠倒时应校验失败，实际却通过");
        }
        else if (!res2.Issues.Any(i => i.ErrorType == SortValidationErrorType.EdgeOrderViolation))
        {
            failures.Add("不在 AppliedEdges 的显式 loadBefore 颠倒应报 EdgeOrderViolation");
        }

        // 3. 正确排序放行，且指向不存在目标的 loadAfter 不产生虚构冲突，base-steam 解析成功
        var validOrder = new[]
        {
            "selftest.explicit.a",
            "selftest.explicit.b",
            "selftest.explicit.c",
            "selftest.explicit.d",
            "selftest.explicit.e",
            "selftest.steam.f_steam",
            "selftest.steam.g"
        };
        var res3 = SortOrderValidator.ValidateActiveModSet(validOrder, allMods, appliedEdgesEmpty);
        if (!res3.IsValid)
        {
            failures.Add($"显式约束满足且包含不存在目标时的合法排序应通过校验，实际失败：{string.Join("; ", res3.Issues.Select(i => i.Message))}");
        }

        try
        {
            SortOrderValidator.ValidateActiveModSetAndThrow(validOrder, allMods, appliedEdgesEmpty);
        }
        catch (Exception ex)
        {
            failures.Add($"显式约束满足时 ValidateActiveModSetAndThrow 不应抛出异常：{ex.Message}");
        }

        // 4. 去重校验：当 AppliedEdges 与原始声明重复时，同一违规边只上报一次
        var duplicateEdge = new OrderingEdge(
            "selftest.explicit.a",
            "selftest.explicit.b",
            true,
            90,
            "About.loadAfter",
            "A在B前");
        var appliedWithDup = new[] { duplicateEdge };
        var res4 = SortOrderValidator.ValidateActiveModSet(invertedLoadAfter, allMods, appliedWithDup);
        var edgeViolationsForAB = res4.Issues.Count(i =>
            i.ErrorType == SortValidationErrorType.EdgeOrderViolation &&
            ((i.Edge != null && i.Edge.BeforePackageId == "selftest.explicit.a" && i.Edge.AfterPackageId == "selftest.explicit.b") ||
             (i.PackageId == "selftest.explicit.b")));
        if (edgeViolationsForAB != 1)
        {
            failures.Add($"AppliedEdges 与原始声明重复时应去重，同一违规边应仅报 1 处，实际报 {edgeViolationsForAB} 处");
        }
    }

    private static void AssertFinalActiveSet_BaseIdDuplicateDetected(List<string> failures)
    {
        var modNormal = Mod("selftest.dup.alias", "Alias Normal", originalIndex: 0);
        var modSteam = Mod("selftest.dup.alias_steam", "Alias Steam", originalIndex: 1);
        var allMods = new List<ModDescriptor> { modNormal, modSteam };

        var bothInActive = new[] { "selftest.dup.alias", "selftest.dup.alias_steam" };
        var validation = SortOrderValidator.ValidateActiveModSet(bothInActive, allMods);
        if (validation.IsValid)
        {
            failures.Add("base 与 _steam 别名同时输入最终启用集合时应识别为重复并报错，实际却通过");
            return;
        }

        if (!validation.Issues.Any(i => i.ErrorType == SortValidationErrorType.DuplicatePackageId))
        {
            failures.Add("base 与 _steam 别名重复应上报 DuplicatePackageId");
        }
    }

    private static void AssertDeterministicOrderingAndRandomGraphProperties(List<string> failures)
    {
        const int seed = 20261022;
        var rng = new Random(seed);
        const int nodeCount = 30;

        for (var sample = 0; sample < 64; sample++)
        {
            var mods = Enumerable.Range(0, nodeCount)
                .Select(i => Mod($"selftest.rand.{i:D2}", $"RandMod {i:D2}", originalIndex: i))
                .ToList();

            var candidateEdges = new List<OrderingEdge>();
            for (var i = 0; i < nodeCount; i++)
            {
                for (var j = i + 1; j < nodeCount; j++)
                {
                    // 30% 概率生成前向边以天然保证基础图是 DAG
                    if (rng.NextDouble() < 0.25)
                    {
                        var isHard = rng.NextDouble() < 0.4;
                        var weight = rng.Next(20, 100);
                        candidateEdges.Add(new OrderingEdge(
                            BeforePackageId: mods[i].PackageId,
                            AfterPackageId: mods[j].PackageId,
                            IsHardConstraint: isHard,
                            Weight: weight,
                            SourceKind: isHard ? "About.loadAfter" : "HarmonyTranspilerHeuristic",
                            Reason: $"随机边 {i} -> {j}"));
                    }
                }
            }

            // 插入若干反向边形成环路候选，验证剪枝与硬环/软边舍弃
            candidateEdges.Add(new OrderingEdge(mods[5].PackageId, mods[2].PackageId, false, 30, "SoftCycleEdge", "软环边"));
            candidateEdges.Add(new OrderingEdge(mods[10].PackageId, mods[1].PackageId, true, 80, "About.loadBefore", "硬环边"));

            var result1 = TopologicalModSorter.Sort(mods, candidateEdges);
            var val1 = SortOrderValidator.Validate(mods, result1);
            if (!val1.IsValid)
            {
                failures.Add($"固定种子随机图排序后置校验失败：{string.Join("; ", val1.Issues.Select(i => i.Message))}");
                return;
            }

            // 验证节点不丢失且唯一
            if (result1.SortedMods.Count != nodeCount)
            {
                failures.Add($"随机图排序节点数量不符：期望 {nodeCount}，实际 {result1.SortedMods.Count}");
            }

            var sortedIds1 = result1.SortedMods.Select(m => m.PackageId).ToList();
            var indexMap = sortedIds1.Select((id, idx) => (id, idx)).ToDictionary(x => x.id, x => x.idx, StringComparer.OrdinalIgnoreCase);

            // 验证所有生效边 Before < After
            foreach (var edge in result1.AppliedEdges)
            {
                if (indexMap[edge.BeforePackageId] >= indexMap[edge.AfterPackageId])
                {
                    failures.Add($"随机图生效边顺序未满足：{edge.BeforePackageId} 应在 {edge.AfterPackageId} 之前");
                    break;
                }
            }

            // 将 candidateEdges 顺序彻底乱序重排多次，验证输出严格确定
            for (var run = 0; run < 3; run++)
            {
                var shuffledEdges = candidateEdges.OrderBy(_ => rng.Next()).ToList();
                var shuffledMods = mods.OrderBy(_ => rng.Next()).ToList();
                // 注意：Sort 依靠 mod.OriginalIndex 保留原始顺位优先级
                var resultShuffled = TopologicalModSorter.Sort(shuffledMods, shuffledEdges);

                var sortedIdsShuffled = resultShuffled.SortedMods.Select(m => m.PackageId).ToList();
                if (!sortedIds1.SequenceEqual(sortedIdsShuffled, StringComparer.OrdinalIgnoreCase))
                {
                    failures.Add($"随机图候选边重排后输出顺序不确定（sample {sample}, run {run} 与初始结果不一致）");
                    return;
                }
            }
        }
    }

    private static void AssertIndependentDeclarationAndWriteGuards(List<string> failures)
    {
        var first = Mod("selftest.guard.first", "First", 0, loadAfter: new[] { "selftest.guard.second" });
        var second = Mod("selftest.guard.second", "Second", 1);
        var mods = new[] { first, second };
        var invalid = TopologicalModSorter.Sort(mods, Array.Empty<OrderingEdge>());
        if (SortOrderValidator.Validate(mods, invalid).IsValid)
            failures.Add("排序后置校验未独立识别漏建的 loadAfter 边。");

        var cyclicMods = new[] { first, second with { LoadAfter = new[] { first.PackageId } } };
        var cyclic = Sort(cyclicMods);
        if (cyclic.BrokenEdges.Count == 0 || !SortOrderValidator.Validate(cyclicMods, cyclic).IsValid)
            failures.Add("显式硬环必须保留诊断结果而不伪造内部校验失败。");

        var report = ModSorterAgent.Reporting.ModSortReportBuilder.BuildReport(
            "1.7", cyclic.SortedMods, cyclic.CycleConflictFindings, cyclic.AppliedEdges,
            Array.Empty<string>(), cyclic.HardCycleDescriptions, null) with
        {
            RejectedHardOrderingEdges = cyclic.BrokenEdges
        };
        var result = new PipelineResult(report, string.Empty, Array.Empty<string>(), "1.7", cyclicMods);
        if (SortOrderValidator.ValidateActiveModSet(cyclic.SortedMods.Select(m => m.PackageId), result).IsValid)
            failures.Add("硬环两端均启用时不得作为有效配置写入。");
        if (!SortOrderValidator.ValidateActiveModSet(new[] { first.PackageId }, result).IsValid)
            failures.Add("硬环一端被排除后不应继续阻断有效子集。");

        var plainMods = new[] { first with { LoadAfter = Array.Empty<string>() }, second };
        result = result with { Mods = plainMods, UnresolvedActiveTokens = new[] { "selftest.guard.missing" } };
        if (!SortOrderValidator.ValidateActiveModSet(new[] { first.PackageId }, result).Issues
                .Any(i => i.ErrorType == SortValidationErrorType.MissingAnalysisMetadata))
            failures.Add("未解析启用项必须阻止写入，即使它已从最终排序中丢失。");
        if (SortOrderValidator.ValidateActiveModSet(new[] { first.PackageId, first.PackageId + "_steam" }, plainMods).IsValid)
            failures.Add("基础标识与 _steam 别名重复必须阻止写入。");
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
        IEnumerable<string>? dependencies = null,
        IEnumerable<ModDependencyItem>? dependencyItems = null,
        bool isActive = true)
        => new()
        {
            PackageId = packageId,
            Name = name,
            RootDirectory = Path.Combine(Path.GetTempPath(), "msa-sort-selftest", packageId),
            OriginalIndex = originalIndex,
            IsOfficialCoreOrDlc = official,
            IsActiveInModsConfig = isActive,
            Dependencies = dependencyItems?.ToArray() ??
                (dependencies ?? Array.Empty<string>())
                    .Select(d => new ModDependencyItem(d, d))
                    .ToArray(),
            LoadAfter = loadAfter?.ToArray() ?? Array.Empty<string>(),
            LoadBefore = loadBefore?.ToArray() ?? Array.Empty<string>(),
            ForceLoadAfter = forceLoadAfter?.ToArray() ?? Array.Empty<string>(),
            ForceLoadBefore = forceLoadBefore?.ToArray() ?? Array.Empty<string>()
        };
}
