using System.Text.RegularExpressions;
using DecompilerServer.Services;
using ModSorterAgent.Analysis;
using ModSorterAgent.Discovery;
using ModSorterAgent.Models;

namespace ModSorterAgent.Grading;

/// <summary>
/// 三级冲突评级器：把静态分析得到的"证据"折算为 1.绿色 / 2.黄色 / 3.红色，
/// 同时产出供拓扑排序使用的有向约束边。
/// </summary>
public static class ConflictGrader
{
    private const int MaxStructuralPairsPerKey = 40;

    private static readonly Regex IndexPredicateRegex = new(@"\[\s*\d+\s*\]", RegexOptions.Compiled);

    public sealed record GradingOutput(
        List<PairwiseConflictFinding> Findings,
        List<OrderingEdge> OrderingEdges,
        List<string> MissingDependencies);

    private sealed class ModIndex
    {
        public Dictionary<string, ModDescriptor> ByExactId { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, ModDescriptor> ByBaseId { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, ModDescriptor> ByHarmonyId { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, List<ModDescriptor>> ByAssemblyName { get; } = new(StringComparer.OrdinalIgnoreCase);

        public ModDescriptor? Resolve(string rawId)
        {
            if (string.IsNullOrWhiteSpace(rawId))
                return null;

            var norm = ModListResolver.NormalizePackageId(rawId);
            if (ByExactId.TryGetValue(norm, out var exact))
                return exact;
            if (ByBaseId.TryGetValue(ModListResolver.StripSteamSuffix(norm), out var byBase))
                return byBase;
            if (ByHarmonyId.TryGetValue(norm, out var byHarmony))
                return byHarmony;
            return null;
        }

        public void RegisterAssemblyName(string assemblyName, ModDescriptor mod)
        {
            if (string.IsNullOrWhiteSpace(assemblyName))
                return;

            if (!ByAssemblyName.TryGetValue(assemblyName, out var list))
            {
                list = new List<ModDescriptor>(1);
                ByAssemblyName[assemblyName] = list;
            }

            if (!list.Any(m => m.PackageId.Equals(mod.PackageId, StringComparison.OrdinalIgnoreCase)))
                list.Add(mod);
        }

        public IReadOnlyList<ModDescriptor> ResolveAssemblyName(string assemblyName)
        {
            return ByAssemblyName.TryGetValue(assemblyName, out var list)
                ? list
                : Array.Empty<ModDescriptor>();
        }
    }

    public static GradingOutput AnalyzeAndGrade(
        IReadOnlyList<ModDescriptor> mods,
        CancellationToken cancellationToken = default,
        string? playerLogPath = null)
    {
        var findings = new List<PairwiseConflictFinding>();
        var edges = new List<OrderingEdge>();
        var missingDeps = new List<string>();
        var index = BuildIndex(mods);

        AnalyzeExplicitDeclarations(mods, index, findings, edges, missingDeps, cancellationToken);
        AnalyzeHarmonyCollisions(mods, index, findings, edges, cancellationToken);
        AnalyzeXmlCollisions(mods, findings, edges, cancellationToken);

        if (!string.IsNullOrWhiteSpace(playerLogPath))
            AnalyzeXmlDefErrors(mods, playerLogPath!, findings, cancellationToken);

        return new GradingOutput(
            DeduplicateFindings(findings),
            DeduplicateEdges(edges),
            missingDeps.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
    }

    // ───────────────────── 四、游戏日志里的 Def 错误 ─────────────────────
    // 判据来自游戏自己的输出，静态 XML 只负责回答"这条红字该算在谁头上"。

    private static void AnalyzeXmlDefErrors(
        IReadOnlyList<ModDescriptor> mods,
        string playerLogPath,
        List<PairwiseConflictFinding> findings,
        CancellationToken cancellationToken)
    {
        var logErrors = XmlCrossReferenceAnalyzer.ParseGameLogFile(playerLogPath);
        if (logErrors.Count == 0)
            return;

        foreach (var issue in XmlCrossReferenceAnalyzer.Analyze(mods, logErrors, cancellationToken))
        {
            // 归因不到具体模组时不造一条假配对，日志原文仍保留在报告的日志核验段里
            if (issue.ReferencingPackageId.Length == 0)
                continue;

            var hasOtherDefiner = !string.IsNullOrWhiteSpace(issue.DefiningPackageId) &&
                                  !string.Equals(issue.DefiningPackageId, issue.ReferencingPackageId, StringComparison.OrdinalIgnoreCase);

            findings.Add(new PairwiseConflictFinding(
                ModAPackageId: issue.ReferencingPackageId,
                ModAName: issue.ReferencingModName,
                ModBPackageId: hasOtherDefiner ? issue.DefiningPackageId! : issue.ReferencingPackageId,
                ModBName: hasOtherDefiner ? issue.DefiningModName! : issue.ReferencingModName,
                Grade: GradeXmlDefIssue(issue.Kind),
                Category: issue.Kind == XmlDefIssueKind.ConfigError ? "XmlDefConfigError" : "BrokenXmlDefReference",
                TargetSignature: BuildXmlDefTarget(issue),
                Summary: SummarizeXmlDefIssue(issue, hasOtherDefiner),
                Evidence: BuildXmlDefEvidence(issue, hasOtherDefiner),
                RecommendedBeforePackageId: null,
                RecommendedAfterPackageId: null,
                RecommendationReason: "调整加载顺序解决不了这类问题，需要修 XML 引用或增删模组"));
        }
    }

    private static ConflictGrade GradeXmlDefIssue(XmlDefIssueKind kind) => kind switch
    {
        XmlDefIssueKind.TypeMismatch => ConflictGrade.Red,
        XmlDefIssueKind.NotDefined => ConflictGrade.Red,
        _ => ConflictGrade.Yellow
    };

    private static string SummarizeXmlDefIssue(XmlDefReferenceIssue issue, bool hasOtherDefiner)
    {
        var actual = issue.ActualDefTypes.Count > 0 ? string.Join("/", issue.ActualDefTypes) : "未知类型";

        return issue.Kind switch
        {
            XmlDefIssueKind.TypeMismatch =>
                $"{issue.ReferencingModName} 把「{issue.ReferencedName}」当 {issue.ExpectedDefType} 引用，" +
                $"但它实际是 {actual}{(hasOtherDefiner ? $"（由 {issue.DefiningModName} 定义）" : string.Empty)}；游戏载入时报红字并丢弃这条引用",

            XmlDefIssueKind.NotDefined =>
                $"{issue.ReferencingModName} 引用的「{issue.ReferencedName}」在当前启用清单里没有任何模组定义，游戏报红字并忽略它",

            XmlDefIssueKind.NotEnabled =>
                $"{issue.ReferencingModName} 引用的「{issue.ReferencedName}」只由未启用的 {issue.DefiningModName} 定义；启用它，或让引用方不再依赖它",

            XmlDefIssueKind.StrayWhitespace =>
                $"{issue.ReferencingModName} 引用「{issue.ReferencedName}」时值里带了多余空白（原文「{issue.RawReferencedValue}」），" +
                "游戏不裁剪空白因此解析失败，对应内容会缺失",

            XmlDefIssueKind.DefinedButUnresolved =>
                $"游戏没能解析到「{issue.ReferencedName}」，但 {issue.DefiningModName ?? issue.ReferencingModName} 的文件里确实定义了它；" +
                "通常是版本文件夹没被载入、重名被拒或该 XML 解析失败",

            XmlDefIssueKind.ConfigError =>
                $"{issue.ReferencingModName} 的 Def「{issue.ReferencedName}」被游戏判定为配置错误" +
                (string.IsNullOrWhiteSpace(issue.Detail) ? string.Empty : $"：{issue.Detail}"),

            _ => $"{issue.ReferencingModName} 的「{issue.ReferencedName}」引用有问题"
        };
    }

    private static IReadOnlyList<string> BuildXmlDefEvidence(XmlDefReferenceIssue issue, bool hasOtherDefiner)
    {
        var evidence = new List<string>
        {
            "游戏日志：" + issue.RawLogLine,
            $"{issue.ReferencingModName}：{issue.SourceFile}"
        };

        if (hasOtherDefiner)
        {
            var actual = issue.ActualDefTypes.Count > 0 ? string.Join("/", issue.ActualDefTypes) : "未知类型";
            evidence.Add($"{issue.DefiningModName} 把「{issue.ReferencedName}」定义为 {actual}");
        }

        return evidence;
    }

    /// <summary>
    /// 取证侧按 "DefType:DefName" 去模组 XML 里捞原文，所以优先给出引用点所在的那个 Def；
    /// 补丁文件里的引用没有归属 Def，退回引用字段本身。
    /// </summary>
    private static string BuildXmlDefTarget(XmlDefReferenceIssue issue)
    {
        if (!string.IsNullOrWhiteSpace(issue.EnclosingDefName) && !string.IsNullOrWhiteSpace(issue.EnclosingDefType))
            return $"{issue.EnclosingDefType}:{issue.EnclosingDefName}";

        return $"{issue.ElementName}:{issue.ReferencedName}";
    }

    private static ModIndex BuildIndex(IReadOnlyList<ModDescriptor> mods)
    {
        var index = new ModIndex();
        foreach (var mod in mods)
        {
            index.ByExactId[mod.PackageId] = mod;
            index.ByBaseId[ModListResolver.StripSteamSuffix(mod.PackageId)] = mod;
            index.ByHarmonyId[mod.PackageId] = mod;
            index.ByHarmonyId[ModListResolver.StripSteamSuffix(mod.PackageId)] = mod;

            foreach (var asm in mod.AssemblyAnalyses)
            {
                if (!string.IsNullOrWhiteSpace(asm.AssemblyName))
                {
                    if (!index.ByAssemblyName.ContainsKey(asm.AssemblyName))
                        index.RegisterAssemblyName(asm.AssemblyName, mod);
                }

                foreach (var harmonyId in asm.HarmonyIds)
                {
                    if (!string.IsNullOrWhiteSpace(harmonyId))
                        index.ByHarmonyId.TryAdd(harmonyId, mod);
                }
            }
        }
        return index;
    }

    // ───────────────────────────── 一、About.xml 显式声明 ─────────────────────────────

    private static void AnalyzeExplicitDeclarations(
        IReadOnlyList<ModDescriptor> mods,
        ModIndex index,
        List<PairwiseConflictFinding> findings,
        List<OrderingEdge> edges,
        List<string> missingDeps,
        CancellationToken cancellationToken)
    {
        foreach (var mod in mods)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var dep in mod.Dependencies)
            {
                var target = index.Resolve(dep.PackageId);
                if (target == null)
                {
                    var label = string.IsNullOrWhiteSpace(dep.DisplayName)
                        ? dep.PackageId
                        : $"{dep.DisplayName} ({dep.PackageId})";

                    missingDeps.Add($"模组 {mod.Name} ({mod.PackageId}) 缺少必需前置依赖: {label}");
                    findings.Add(new PairwiseConflictFinding(
                        ModAPackageId: mod.PackageId,
                        ModAName: mod.Name,
                        ModBPackageId: dep.PackageId,
                        ModBName: dep.DisplayName ?? dep.PackageId,
                        Grade: ConflictGrade.Red,
                        Category: "MissingDependency",
                        TargetSignature: dep.PackageId,
                        Summary: $"缺少必需前置模组 {label}",
                        Evidence: new[] { $"About.xml 的 modDependencies 声明依赖 {dep.PackageId}，但当前启用列表中未包含该模组" },
                        RecommendedBeforePackageId: dep.PackageId,
                        RecommendedAfterPackageId: mod.PackageId,
                        RecommendationReason: "必须补充启用该前置模组，并置于当前模组之前"));
                }
                else if (!IsSelf(target, mod))
                {
                    edges.Add(new OrderingEdge(
                        BeforePackageId: target.PackageId,
                        AfterPackageId: mod.PackageId,
                        IsHardConstraint: true,
                        Weight: 100,
                        SourceKind: "About.modDependencies",
                        Reason: $"{mod.Name} 强依赖前置 {target.Name}"));
                }
            }

            foreach (var incompatibleId in mod.IncompatibleWith)
            {
                var target = index.Resolve(incompatibleId);
                if (target == null || IsSelf(target, mod))
                    continue;

                findings.Add(new PairwiseConflictFinding(
                    ModAPackageId: mod.PackageId,
                    ModAName: mod.Name,
                    ModBPackageId: target.PackageId,
                    ModBName: target.Name,
                    Grade: ConflictGrade.Purple,
                    Category: "ExplicitIncompatibility",
                    TargetSignature: $"{mod.PackageId} <-> {target.PackageId}",
                    Summary: $"{mod.Name} 在 About.xml 中显式声明与 {target.Name} 不兼容",
                    Evidence: new[] { $"About.xml 的 incompatibleWith 包含 {incompatibleId}" },
                    RecommendedBeforePackageId: null,
                    RecommendedAfterPackageId: null,
                    RecommendationReason: "两个模组显式互斥，不能同时启用"));
            }

            foreach (var afterId in mod.LoadAfter.Concat(mod.ForceLoadAfter))
            {
                var target = index.Resolve(afterId);
                if (target == null || IsSelf(target, mod))
                    continue;

                if (IsContradictoryBaseDeclaration(mod, target, loadsBefore: false))
                    continue;

                edges.Add(new OrderingEdge(
                    BeforePackageId: target.PackageId,
                    AfterPackageId: mod.PackageId,
                    IsHardConstraint: true,
                    Weight: 90,
                    SourceKind: "About.loadAfter",
                    Reason: $"{mod.Name} 声明 loadAfter {target.Name}"));
            }

            foreach (var beforeId in mod.LoadBefore.Concat(mod.ForceLoadBefore))
            {
                var target = index.Resolve(beforeId);
                if (target == null || IsSelf(target, mod))
                    continue;

                if (IsContradictoryBaseDeclaration(mod, target, loadsBefore: true))
                    continue;

                edges.Add(new OrderingEdge(
                    BeforePackageId: mod.PackageId,
                    AfterPackageId: target.PackageId,
                    IsHardConstraint: true,
                    Weight: 90,
                    SourceKind: "About.loadBefore",
                    Reason: $"{mod.Name} 声明 loadBefore {target.Name}"));
            }

            foreach (var condId in mod.ConditionalLoadFolderMods)
            {
                var target = index.Resolve(condId);
                if (target == null || IsSelf(target, mod))
                    continue;

                edges.Add(new OrderingEdge(
                    BeforePackageId: target.PackageId,
                    AfterPackageId: mod.PackageId,
                    IsHardConstraint: false,
                    Weight: 85,
                    SourceKind: "LoadFolders.IfModActive",
                    Reason: $"{mod.Name} 包含针对 {target.Name} 的 IfModActive 条件兼容目录"));
            }

            foreach (var refAsm in mod.ReferencedExternalAssemblies)
            {
                foreach (var refMod in index.ResolveAssemblyName(refAsm))
                {
                    if (IsSelf(refMod, mod))
                        continue;

                    // 底座（官方内容 / Harmony 运行时）永远先于社区模组；
                    // 指向它的"被引用"边多半是把 0PrepatcherAPI 这类接入层 DLL 误归给了宿主模组。
                    if (IsBaseModule(refMod))
                        continue;

                    edges.Add(new OrderingEdge(
                        BeforePackageId: refMod.PackageId,
                        AfterPackageId: mod.PackageId,
                        IsHardConstraint: true,
                        Weight: 95,
                        SourceKind: "AssemblyReference",
                        Reason: $"{mod.Name} 的程序集直接引用了 {refMod.Name} 的程序集 ({refAsm}.dll)"));
                }
            }
        }
    }

    // ───────────────────────────── 二、Harmony 补丁碰撞 ─────────────────────────────

    private static void AnalyzeHarmonyCollisions(
        IReadOnlyList<ModDescriptor> mods,
        ModIndex index,
        List<PairwiseConflictFinding> findings,
        List<OrderingEdge> edges,
        CancellationToken cancellationToken)
    {
        var patchesByTarget = new Dictionary<string, List<(ModDescriptor Mod, HarmonyPatchRecord Patch)>>(StringComparer.Ordinal);

        foreach (var mod in mods)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (mod.IsOfficialCoreOrDlc)
                continue;

            foreach (var asm in mod.AssemblyAnalyses)
            {
                foreach (var patch in asm.Patches)
                {
                    AddHarmonyOrderingEdges(mod, patch, index, edges);

                    if (patch.UsesDynamicTargetMethod &&
                        (string.IsNullOrWhiteSpace(patch.TargetTypeFullName) ||
                         string.IsNullOrWhiteSpace(patch.TargetMethodName)))
                    {
                        AddDynamicTargetFinding(mod, patch, index, findings);
                        continue;
                    }

                    var targetKey = BuildTargetKey(patch);
                    if (!patchesByTarget.TryGetValue(targetKey, out var list))
                    {
                        list = new List<(ModDescriptor, HarmonyPatchRecord)>();
                        patchesByTarget[targetKey] = list;
                    }
                    list.Add((mod, patch));
                }

                AddDynamicReflectionEdges(mod, asm.DynamicPatchSites, index, edges);
            }
        }

        foreach (var (targetKey, entries) in patchesByTarget)
        {
            var byMod = entries
                .GroupBy(e => e.Mod.PackageId, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (byMod.Count < 2)
                continue;

            for (var i = 0; i < byMod.Count; i++)
            {
                for (var j = i + 1; j < byMod.Count; j++)
                {
                    var groupA = byMod[i].ToList();
                    var groupB = byMod[j].ToList();

                    EvaluateMethodCollisionPair(
                        targetKey,
                        groupA[0].Mod,
                        groupA.Select(x => x.Patch).ToList(),
                        groupB[0].Mod,
                        groupB.Select(x => x.Patch).ToList(),
                        findings,
                        edges);
                }
            }
        }
    }

    private static void AddHarmonyOrderingEdges(
        ModDescriptor mod,
        HarmonyPatchRecord patch,
        ModIndex index,
        List<OrderingEdge> edges)
    {
        foreach (var beforeId in patch.BeforeIds)
        {
            var target = index.Resolve(beforeId);
            if (target == null || IsSelf(target, mod))
                continue;

            edges.Add(new OrderingEdge(
                BeforePackageId: mod.PackageId,
                AfterPackageId: target.PackageId,
                IsHardConstraint: false,
                Weight: 75,
                SourceKind: "HarmonyBefore",
                Reason: $"{mod.Name} 的补丁 {patch.PatchClassFullName} 声明 HarmonyBefore({beforeId})"));
        }

        foreach (var afterId in patch.AfterIds)
        {
            var target = index.Resolve(afterId);
            if (target == null || IsSelf(target, mod))
                continue;

            edges.Add(new OrderingEdge(
                BeforePackageId: target.PackageId,
                AfterPackageId: mod.PackageId,
                IsHardConstraint: false,
                Weight: 75,
                SourceKind: "HarmonyAfter",
                Reason: $"{mod.Name} 的补丁 {patch.PatchClassFullName} 声明 HarmonyAfter({afterId})"));
        }
    }

    private static void AddDynamicTargetFinding(
        ModDescriptor mod,
        HarmonyPatchRecord patch,
        ModIndex index,
        List<PairwiseConflictFinding> findings)
    {
        var hasSatisfiedBridgeDeps =
            (mod.Dependencies.Count > 0 && mod.Dependencies.All(d => index.Resolve(d.PackageId) != null)) ||
            (mod.LoadAfter.Count > 0 && mod.LoadAfter.Any(id => index.Resolve(id) != null)) ||
            mod.ReferencedExternalAssemblies.Any(a => index.ResolveAssemblyName(a).Count > 0);

        var evidence = patch.DynamicTargetHints.Count > 0
            ? patch.DynamicTargetHints.Take(5).ToArray()
            : new[] { $"PatchClass: {patch.PatchClassFullName}, Method: {patch.PatchMethodName}" };

        findings.Add(new PairwiseConflictFinding(
            ModAPackageId: mod.PackageId,
            ModAName: mod.Name,
            ModBPackageId: mod.PackageId,
            ModBName: mod.Name,
            Grade: hasSatisfiedBridgeDeps ? ConflictGrade.Green : ConflictGrade.Yellow,
            Category: hasSatisfiedBridgeDeps ? "CompatibleHarmonyOverlap" : "DynamicHarmonyTarget",
            TargetSignature: $"{patch.PatchClassFullName}.{patch.PatchMethodName}",
            Summary: hasSatisfiedBridgeDeps
                ? $"{mod.Name} 通过动态反射桥接前置模组方法（{patch.PatchKind}），对应前置模组已启用并排在前面"
                : $"{mod.Name} 包含未声明明确前置的动态目标 Harmony 补丁（{patch.PatchKind}），静态分析无法锁定实际目标",
            Evidence: evidence,
            RecommendedBeforePackageId: null,
            RecommendedAfterPackageId: null,
            RecommendationReason: hasSatisfiedBridgeDeps
                ? "跨模组动态反射适配补丁，前置依赖完备"
                : "需结合实机日志确认动态目标绑定无报错"));
    }

    private static void AddDynamicReflectionEdges(
        ModDescriptor mod,
        IReadOnlyList<DynamicHarmonyPatchSite> sites,
        ModIndex index,
        List<OrderingEdge> edges)
    {
        if (sites.Count == 0)
            return;

        var targets = new Dictionary<string, ModDescriptor>(StringComparer.OrdinalIgnoreCase);
        foreach (var site in sites)
        {
            foreach (var literal in site.ReferencedStrings)
            {
                if (string.IsNullOrWhiteSpace(literal) || literal.Length < 4)
                    continue;

                foreach (var (assemblyName, owners) in index.ByAssemblyName)
                {
                    if (assemblyName.Length < 4)
                        continue;

                    // 词边界匹配：程序集名必须作为完整 token 出现。
                    // 裸子串匹配会让短程序集名（如 Vanilla）在任何长字符串里"命中"，
                    // 凭空建出大量误边并锁死无关模组的相对顺序。
                    if (!ContainsAsWord(literal, assemblyName))
                        continue;

                    foreach (var owner in owners)
                    {
                        if (IsSelf(owner, mod) || owner.IsOfficialCoreOrDlc)
                            continue;

                        targets.TryAdd(owner.PackageId, owner);
                    }
                }
            }
        }

        foreach (var target in targets.Values)
        {
            edges.Add(new OrderingEdge(
                BeforePackageId: target.PackageId,
                AfterPackageId: mod.PackageId,
                IsHardConstraint: false,
                Weight: 70,
                SourceKind: "DynamicReflectionPatch",
                Reason: $"{mod.Name} 通过反射动态修补 {target.Name}"));
        }
    }

    private static void EvaluateMethodCollisionPair(
        string targetKey,
        ModDescriptor modA,
        IReadOnlyList<HarmonyPatchRecord> patchesA,
        ModDescriptor modB,
        IReadOnlyList<HarmonyPatchRecord> patchesB,
        List<PairwiseConflictFinding> findings,
        List<OrderingEdge> edges)
    {
        var hasExplicitOrder = HasExplicitOrderingBetween(modA, patchesA, modB, patchesB);

        var aAlwaysSkips = patchesA.Any(p => p.PatchKind == "Prefix" && p.Behavior.AlwaysSkipsOriginal);
        var bAlwaysSkips = patchesB.Any(p => p.PatchKind == "Prefix" && p.Behavior.AlwaysSkipsOriginal);
        var aCondSkips = patchesA.Any(p => p.PatchKind == "Prefix" && p.Behavior.ConditionalSkipOriginal);
        var bCondSkips = patchesB.Any(p => p.PatchKind == "Prefix" && p.Behavior.ConditionalSkipOriginal);
        var aHasTranspiler = patchesA.Any(p => p.PatchKind == "Transpiler");
        var bHasTranspiler = patchesB.Any(p => p.PatchKind == "Transpiler");
        var aHasPrefix = patchesA.Any(p => p.PatchKind == "Prefix");
        var bHasPrefix = patchesB.Any(p => p.PatchKind == "Prefix");
        var aUnguardedResultOverwrite = patchesA.Any(p => p.Behavior.ModifiesResult && !p.Behavior.IsGuardedOrAdditiveResultMod);
        var bUnguardedResultOverwrite = patchesB.Any(p => p.Behavior.ModifiesResult && !p.Behavior.IsGuardedOrAdditiveResultMod);

        var evidence = BuildHarmonyEvidence(modA, patchesA, modB, patchesB);

        // 情形 1：任一方无条件截断原方法，另一方仍在原方法体上做 Transpiler 或同样无条件截断 → 红色
        if ((aAlwaysSkips && bAlwaysSkips) ||
            (aAlwaysSkips && bHasTranspiler) ||
            (bAlwaysSkips && aHasTranspiler))
        {
            var beforeMod = aAlwaysSkips ? modB : modA;
            var afterMod = aAlwaysSkips ? modA : modB;

            edges.Add(new OrderingEdge(
                BeforePackageId: beforeMod.PackageId,
                AfterPackageId: afterMod.PackageId,
                IsHardConstraint: false,
                Weight: 65,
                SourceKind: "BlockingPrefixAfterNonBlocking",
                Reason: $"将 {beforeMod.Name} 置于无条件截断原方法的 {afterMod.Name} 之前"));

            findings.Add(new PairwiseConflictFinding(
                ModAPackageId: modA.PackageId,
                ModAName: modA.Name,
                ModBPackageId: modB.PackageId,
                ModBName: modB.Name,
                Grade: ConflictGrade.Red,
                Category: "HarmonyBlockingPrefixConflict",
                TargetSignature: targetKey,
                Summary: $"{afterMod.Name} 在 {targetKey} 上无条件返回 false 跳过原方法，会使 {beforeMod.Name} 针对原方法体的补丁无法生效",
                Evidence: evidence,
                RecommendedBeforePackageId: beforeMod.PackageId,
                RecommendedAfterPackageId: afterMod.PackageId,
                RecommendationReason: "无条件截断 Prefix 会跳过原方法体，导致针对原方法 IL 的 Transpiler 失效"));
            return;
        }

        // 情形 1b：一方无条件截断，另一方仅有非截断 Prefix → 非截断者前置，黄色
        if ((aAlwaysSkips && bHasPrefix) || (bAlwaysSkips && aHasPrefix))
        {
            var beforeMod = aAlwaysSkips ? modB : modA;
            var afterMod = aAlwaysSkips ? modA : modB;
            var reason = $"已将非截断 Prefix 模组 {beforeMod.Name} 排在截断型 Prefix 模组 {afterMod.Name} 之前，确保双方前置逻辑依次执行";

            edges.Add(new OrderingEdge(
                BeforePackageId: beforeMod.PackageId,
                AfterPackageId: afterMod.PackageId,
                IsHardConstraint: false,
                Weight: 65,
                SourceKind: "NonBlockingPrefixBeforeBlocking",
                Reason: reason));

            findings.Add(new PairwiseConflictFinding(
                ModAPackageId: modA.PackageId,
                ModAName: modA.Name,
                ModBPackageId: modB.PackageId,
                ModBName: modB.Name,
                Grade: ConflictGrade.Yellow,
                Category: "ConditionalPrefixOrResultMutationOverlap",
                TargetSignature: targetKey,
                Summary: $"{afterMod.Name} 在 {targetKey} 上包含返回 false 的接管型 Prefix，需确保 {beforeMod.Name} 的前置补丁先执行",
                Evidence: evidence,
                RecommendedBeforePackageId: beforeMod.PackageId,
                RecommendedAfterPackageId: afterMod.PackageId,
                RecommendationReason: reason));
            return;
        }

        // 情形 2：双方同一目标多个 Transpiler 且无显式顺序 → 黄色
        if (aHasTranspiler && bHasTranspiler && !hasExplicitOrder)
        {
            var (recBefore, recAfter, reason) = RecommendOrderForCollision(modA, patchesA, modB, patchesB);
            edges.Add(new OrderingEdge(
                BeforePackageId: recBefore.PackageId,
                AfterPackageId: recAfter.PackageId,
                IsHardConstraint: false,
                Weight: 45,
                SourceKind: "HarmonyTranspilerHeuristic",
                Reason: reason));

            findings.Add(new PairwiseConflictFinding(
                ModAPackageId: modA.PackageId,
                ModAName: modA.Name,
                ModBPackageId: modB.PackageId,
                ModBName: modB.Name,
                Grade: ConflictGrade.Yellow,
                Category: "MultipleTranspilersSameTarget",
                TargetSignature: targetKey,
                Summary: $"{modA.Name} 与 {modB.Name} 同时对 {targetKey} 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响",
                Evidence: evidence,
                RecommendedBeforePackageId: recBefore.PackageId,
                RecommendedAfterPackageId: recAfter.PackageId,
                RecommendationReason: reason));
            return;
        }

        // 情形 3：双方均无守卫地直接覆盖返回值 → 黄色
        if (!hasExplicitOrder && aUnguardedResultOverwrite && bUnguardedResultOverwrite)
        {
            var (recBefore, recAfter, reason) = RecommendOrderForCollision(modA, patchesA, modB, patchesB);
            edges.Add(new OrderingEdge(
                BeforePackageId: recBefore.PackageId,
                AfterPackageId: recAfter.PackageId,
                IsHardConstraint: false,
                Weight: 40,
                SourceKind: "HarmonyResultOverwriteHeuristic",
                Reason: reason));

            findings.Add(new PairwiseConflictFinding(
                ModAPackageId: modA.PackageId,
                ModAName: modA.Name,
                ModBPackageId: modB.PackageId,
                ModBName: modB.Name,
                Grade: ConflictGrade.Yellow,
                Category: "ConditionalPrefixOrResultMutationOverlap",
                TargetSignature: targetKey,
                Summary: $"{modA.Name} 与 {modB.Name} 在 {targetKey} 上均存在无守卫的返回值直接赋值，后加载者会覆盖先加载者结果",
                Evidence: evidence,
                RecommendedBeforePackageId: recBefore.PackageId,
                RecommendedAfterPackageId: recAfter.PackageId,
                RecommendationReason: reason));
            return;
        }

        // 情形 4：带条件守卫 / 增量修饰 / 已有显式顺序 → 绿色
        var (softBefore, softAfter, softReason) = RecommendOrderForCollision(modA, patchesA, modB, patchesB);
        if (!hasExplicitOrder && aCondSkips != bCondSkips)
        {
            edges.Add(new OrderingEdge(
                BeforePackageId: softBefore.PackageId,
                AfterPackageId: softAfter.PackageId,
                IsHardConstraint: false,
                Weight: 35,
                SourceKind: "GuardedPrefixOrdering",
                Reason: softReason));
        }

        findings.Add(new PairwiseConflictFinding(
            ModAPackageId: modA.PackageId,
            ModAName: modA.Name,
            ModBPackageId: modB.PackageId,
            ModBName: modB.Name,
            Grade: ConflictGrade.Green,
            Category: "CompatibleHarmonyOverlap",
            TargetSignature: targetKey,
            Summary: $"{modA.Name} 与 {modB.Name} 共同扩展 {targetKey}，均带有条件守卫或增量计算，作用域正交",
            Evidence: evidence,
            RecommendedBeforePackageId: softBefore.PackageId,
            RecommendedAfterPackageId: softAfter.PackageId,
            RecommendationReason: "各自按特定条件或增量修饰执行，可安全共存"));
    }

    private static List<string> BuildHarmonyEvidence(
        ModDescriptor modA,
        IReadOnlyList<HarmonyPatchRecord> patchesA,
        ModDescriptor modB,
        IReadOnlyList<HarmonyPatchRecord> patchesB)
    {
        var evidence = new List<string>(patchesA.Count + patchesB.Count);
        foreach (var p in patchesA)
        {
            evidence.Add($"{modA.Name}: [{p.PatchKind}] {p.PatchClassFullName}.{p.PatchMethodName} (Priority={p.Priority}, AlwaysSkip={p.Behavior.AlwaysSkipsOriginal}, CondSkip={p.Behavior.ConditionalSkipOriginal}, Guarded={p.Behavior.HasTargetGuardClause || p.Behavior.IsGuardedOrAdditiveResultMod})");
        }
        foreach (var p in patchesB)
        {
            evidence.Add($"{modB.Name}: [{p.PatchKind}] {p.PatchClassFullName}.{p.PatchMethodName} (Priority={p.Priority}, AlwaysSkip={p.Behavior.AlwaysSkipsOriginal}, CondSkip={p.Behavior.ConditionalSkipOriginal}, Guarded={p.Behavior.HasTargetGuardClause || p.Behavior.IsGuardedOrAdditiveResultMod})");
        }
        return evidence;
    }

    private static (ModDescriptor Before, ModDescriptor After, string Reason) RecommendOrderForCollision(
        ModDescriptor modA,
        IReadOnlyList<HarmonyPatchRecord> patchesA,
        ModDescriptor modB,
        IReadOnlyList<HarmonyPatchRecord> patchesB)
    {
        var aCondSkip = patchesA.Any(p => p.Behavior.ConditionalSkipOriginal);
        var bCondSkip = patchesB.Any(p => p.Behavior.ConditionalSkipOriginal);

        if (aCondSkip && !bCondSkip)
            return (modB, modA, $"建议将非截断补丁模组 {modB.Name} 置于含条件截断 Prefix 的 {modA.Name} 之前");
        if (bCondSkip && !aCondSkip)
            return (modA, modB, $"建议将非截断补丁模组 {modA.Name} 置于含条件截断 Prefix 的 {modB.Name} 之前");

        var maxPrioA = patchesA.Count == 0 ? 0 : patchesA.Max(p => p.Priority);
        var maxPrioB = patchesB.Count == 0 ? 0 : patchesB.Max(p => p.Priority);
        if (maxPrioA != maxPrioB)
        {
            return maxPrioA > maxPrioB
                ? (modA, modB, $"{modA.Name} 声明了更高的 HarmonyPriority ({maxPrioA} > {maxPrioB})")
                : (modB, modA, $"{modB.Name} 声明了更高的 HarmonyPriority ({maxPrioB} > {maxPrioA})");
        }

        return modA.OriginalIndex <= modB.OriginalIndex
            ? (modA, modB, $"保持 {modA.Name} 在 {modB.Name} 之前的稳定加载顺序")
            : (modB, modA, $"保持 {modB.Name} 在 {modA.Name} 之前的稳定加载顺序");
    }

    private static bool HasExplicitOrderingBetween(
        ModDescriptor modA,
        IReadOnlyList<HarmonyPatchRecord> patchesA,
        ModDescriptor modB,
        IReadOnlyList<HarmonyPatchRecord> patchesB)
    {
        var idsA = CollectModIds(modA);
        var idsB = CollectModIds(modB);

        if (modA.Dependencies.Select(d => d.PackageId).Any(idsB.Contains) ||
            modB.Dependencies.Select(d => d.PackageId).Any(idsA.Contains) ||
            modA.ConditionalLoadFolderMods.Any(idsB.Contains) ||
            modB.ConditionalLoadFolderMods.Any(idsA.Contains))
        {
            return true;
        }

        if (modA.LoadAfter.Concat(modA.ForceLoadAfter).Any(idsB.Contains) ||
            modA.LoadBefore.Concat(modA.ForceLoadBefore).Any(idsB.Contains) ||
            modB.LoadAfter.Concat(modB.ForceLoadAfter).Any(idsA.Contains) ||
            modB.LoadBefore.Concat(modB.ForceLoadBefore).Any(idsA.Contains))
        {
            return true;
        }

        if (patchesA.Any(p => p.BeforeIds.Any(idsB.Contains) || p.AfterIds.Any(idsB.Contains)) ||
            patchesB.Any(p => p.BeforeIds.Any(idsA.Contains) || p.AfterIds.Any(idsA.Contains)))
        {
            return true;
        }

        return false;
    }

    private static HashSet<string> CollectModIds(ModDescriptor mod)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            mod.PackageId,
            ModListResolver.StripSteamSuffix(mod.PackageId)
        };

        foreach (var asm in mod.AssemblyAnalyses)
        {
            foreach (var harmonyId in asm.HarmonyIds)
                ids.Add(harmonyId);
        }

        return ids;
    }

    // ───────────────────────────── 三、XML Def / Patch 碰撞 ─────────────────────────────

    private static void AnalyzeXmlCollisions(
        IReadOnlyList<ModDescriptor> mods,
        List<PairwiseConflictFinding> findings,
        List<OrderingEdge> edges,
        CancellationToken cancellationToken)
    {
        var defsByKey = new Dictionary<string, List<(ModDescriptor Mod, XmlDefRecord Def)>>(StringComparer.Ordinal);
        var patchesByExactXPath = new Dictionary<string, List<(ModDescriptor Mod, XmlPatchRecord Patch)>>(StringComparer.Ordinal);
        var patchesByStructuralXPath = new Dictionary<string, List<(ModDescriptor Mod, XmlPatchRecord Patch)>>(StringComparer.Ordinal);

        foreach (var mod in mods)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (mod.IsOfficialCoreOrDlc)
                continue;

            foreach (var def in mod.XmlDefs)
            {
                if (def.IsAbstract)
                    continue;

                foreach (var key in BuildDefKeys(def))
                {
                    if (!defsByKey.TryGetValue(key, out var list))
                    {
                        list = new List<(ModDescriptor, XmlDefRecord)>();
                        defsByKey[key] = list;
                    }
                    list.Add((mod, def));
                }
            }

            foreach (var patch in mod.XmlPatches)
            {
                if (string.IsNullOrWhiteSpace(patch.XPath))
                    continue;

                AddToIndex(patchesByExactXPath, patch.XPath, (mod, patch));

                var structural = BuildStructuralXPathKey(patch.XPath);
                if (!structural.Equals(patch.XPath, StringComparison.Ordinal))
                    AddToIndex(patchesByStructuralXPath, structural, (mod, patch));
            }
        }

        // 1) 模组 A 定义 Def，模组 B 通过 XPath 修改它 → A 先于 B
        foreach (var mod in mods)
        {
            if (mod.IsOfficialCoreOrDlc)
                continue;

            foreach (var patch in mod.XmlPatches)
            {
                foreach (var (defType, defName) in XmlConflictAnalyzer.ExtractAllTargetedDefsFromXPath(patch.XPath))
                {
                    foreach (var key in new[] { $"{defType}:{defName}", $"{defType}@{defName}" })
                    {
                        if (!defsByKey.TryGetValue(key, out var owners))
                            continue;

                        foreach (var (ownerMod, _) in owners)
                        {
                            if (IsSelf(ownerMod, mod))
                                continue;

                            edges.Add(new OrderingEdge(
                                BeforePackageId: ownerMod.PackageId,
                                AfterPackageId: mod.PackageId,
                                IsHardConstraint: false,
                                Weight: 80,
                                SourceKind: "XmlPatchTargetsDef",
                                Reason: $"{mod.Name} 的 XML Patch 修改了 {ownerMod.Name} 定义的 {defType}:{defName}"));
                        }
                    }
                }
            }
        }

        // 2) 多个模组重复定义同一 Def
        foreach (var (defKey, entries) in defsByKey)
        {
            var byMod = entries
                .GroupBy(e => e.Mod.PackageId, StringComparer.OrdinalIgnoreCase)
                .Select(g => (Mod: g.First().Mod, Def: g.First().Def))
                .ToList();

            if (byMod.Count < 2)
                continue;

            for (var i = 0; i < byMod.Count; i++)
            {
                for (var j = i + 1; j < byMod.Count; j++)
                {
                    EvaluateDuplicateDef(defKey, byMod[i].Mod, byMod[i].Def, byMod[j].Mod, byMod[j].Def, findings);
                }
            }
        }

        // 3) 完全相同的 XPath 重叠
        var reportedPairs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (xpath, entries) in patchesByExactXPath)
        {
            var byMod = entries
                .GroupBy(e => e.Mod.PackageId, StringComparer.OrdinalIgnoreCase)
                .Select(g => (Mod: g.First().Mod, Patches: g.Select(x => x.Patch).ToList()))
                .ToList();

            if (byMod.Count < 2)
                continue;

            for (var i = 0; i < byMod.Count; i++)
            {
                for (var j = i + 1; j < byMod.Count; j++)
                {
                    reportedPairs.Add(BuildPairKey(xpath, byMod[i].Mod, byMod[j].Mod));
                    EvaluateXmlPatchPair(xpath, byMod[i].Mod, byMod[i].Patches, byMod[j].Mod, byMod[j].Patches, defsByKey, findings);
                }
            }
        }

        // 4) 结构相同但索引不同的 XPath（如同一定义的 li 与 li[3]）→ 追加/定点修改互扰
        foreach (var (structuralKey, entries) in patchesByStructuralXPath)
        {
            var byMod = entries
                .GroupBy(e => e.Mod.PackageId, StringComparer.OrdinalIgnoreCase)
                .Select(g => (Mod: g.First().Mod, Patches: g.Select(x => x.Patch).ToList()))
                .ToList();

            if (byMod.Count < 2)
                continue;

            var emitted = 0;
            for (var i = 0; i < byMod.Count && emitted < MaxStructuralPairsPerKey; i++)
            {
                for (var j = i + 1; j < byMod.Count && emitted < MaxStructuralPairsPerKey; j++)
                {
                    var pairKey = BuildPairKey(structuralKey, byMod[i].Mod, byMod[j].Mod);
                    if (!reportedPairs.Add(pairKey))
                        continue;

                    var hasIndexSpecific = byMod[i].Patches.Concat(byMod[j].Patches)
                        .Any(p => p.XPath != null && IndexPredicateRegex.IsMatch(p.XPath));
                    var hasContainerLevel = byMod[i].Patches.Concat(byMod[j].Patches)
                        .Any(p => p.XPath != null && !IndexPredicateRegex.IsMatch(p.XPath));

                    if (!hasIndexSpecific || !hasContainerLevel)
                        continue;

                    EvaluateStructuralXPathPair(structuralKey, byMod[i].Mod, byMod[i].Patches, byMod[j].Mod, byMod[j].Patches, findings);
                    emitted++;
                }
            }
        }
    }

    private static void EvaluateDuplicateDef(
        string defKey,
        ModDescriptor modA,
        XmlDefRecord defA,
        ModDescriptor modB,
        XmlDefRecord defB,
        List<PairwiseConflictFinding> findings)
    {
        var hasExplicitOrder = HasExplicitOrderingBetween(modA, Array.Empty<HarmonyPatchRecord>(), modB, Array.Empty<HarmonyPatchRecord>());
        var isIdempotent = string.Equals(defA.ContentSignature, defB.ContentSignature, StringComparison.Ordinal);
        var isPureTagOrShapeDef =
            string.IsNullOrWhiteSpace(defA.ThingClass) &&
            string.IsNullOrWhiteSpace(defB.ThingClass) &&
            string.IsNullOrWhiteSpace(defA.DriverClass) &&
            string.IsNullOrWhiteSpace(defB.DriverClass) &&
            string.IsNullOrWhiteSpace(defA.WorkerClass) &&
            string.IsNullOrWhiteSpace(defB.WorkerClass) &&
            defA.CompClasses.Count == 0 &&
            defB.CompClasses.Count == 0;

        ConflictGrade grade;
        string summary;
        string recReason;

        if (isIdempotent || isPureTagOrShapeDef)
        {
            grade = ConflictGrade.Green;
            summary = $"{modA.Name} 与 {modB.Name} 共享声明了纯标签/外观 Def（{defKey}），无逻辑冲突";
            recReason = "纯外观标签或幂等 Def 定义，任意顺序均可安全兼容";
        }
        else if (hasExplicitOrder)
        {
            grade = ConflictGrade.Green;
            summary = $"{modA.Name} 与 {modB.Name} 存在明确上下游扩展关系，后者正常覆盖更新 XML Def（{defKey}）";
            recReason = "正常上下游扩展覆盖关系，拓扑排序已保证父模组在前、扩展模组在后";
        }
        else
        {
            // 扩展模组覆盖原模组 Def 是常见做法；只有未声明顺序时才有覆盖风险，降为黄色提示
            grade = ConflictGrade.Yellow;
            summary = $"{modA.Name} 与 {modB.Name} 都定义了 {defKey}，且未声明加载顺序；后加载者会整块覆盖先加载者";
            recReason = "扩展覆盖通常是有意为之；确认加载顺序符合预期即可，需要保留双方改动时再合并";
        }

        findings.Add(new PairwiseConflictFinding(
            ModAPackageId: modA.PackageId,
            ModAName: modA.Name,
            ModBPackageId: modB.PackageId,
            ModBName: modB.Name,
            Grade: grade,
            Category: "DuplicateXmlDefOverwrite",
            TargetSignature: defKey,
            Summary: summary,
            Evidence: new[]
            {
                $"{modA.Name}: {defA.SourceFile}",
                $"{modB.Name}: {defB.SourceFile}"
            },
            RecommendedBeforePackageId: modA.OriginalIndex <= modB.OriginalIndex ? modA.PackageId : modB.PackageId,
            RecommendedAfterPackageId: modA.OriginalIndex <= modB.OriginalIndex ? modB.PackageId : modA.PackageId,
            RecommendationReason: recReason));
    }

    private static void EvaluateXmlPatchPair(
        string xpath,
        ModDescriptor modA,
        IReadOnlyList<XmlPatchRecord> patchesA,
        ModDescriptor modB,
        IReadOnlyList<XmlPatchRecord> patchesB,
        Dictionary<string, List<(ModDescriptor Mod, XmlDefRecord Def)>> defsByKey,
        List<PairwiseConflictFinding> findings)
    {
        var bothDestructive = patchesA.Any(p => p.IsDestructive) && patchesB.Any(p => p.IsDestructive);
        var anyDestructive = patchesA.Any(p => p.IsDestructive) || patchesB.Any(p => p.IsDestructive);
        var allGated = patchesA.All(p => p.IsConditionalOrModGated) && patchesB.All(p => p.IsConditionalOrModGated);

        var targetsKnownModDef = false;
        foreach (var (defType, defName) in XmlConflictAnalyzer.ExtractAllTargetedDefsFromXPath(xpath))
        {
            if (defsByKey.TryGetValue($"{defType}:{defName}", out var owners) ||
                defsByKey.TryGetValue($"{defType}@{defName}", out owners))
            {
                targetsKnownModDef = owners.Any(o =>
                    IsSameMod(o.Mod, modA) || IsSameMod(o.Mod, modB));
            }
            if (targetsKnownModDef)
                break;
        }

        var hasExplicitOrder = targetsKnownModDef ||
                               HasExplicitOrderingBetween(modA, Array.Empty<HarmonyPatchRecord>(), modB, Array.Empty<HarmonyPatchRecord>());

        var evidence = new List<string>
        {
            $"{modA.Name}: {string.Join(", ", patchesA.Select(p => p.OperationClass))} ({patchesA[0].SourceFile})",
            $"{modB.Name}: {string.Join(", ", patchesB.Select(p => p.OperationClass))} ({patchesB[0].SourceFile})"
        };

        var orderedBefore = modA.OriginalIndex <= modB.OriginalIndex ? modA.PackageId : modB.PackageId;
        var orderedAfter = modA.OriginalIndex <= modB.OriginalIndex ? modB.PackageId : modA.PackageId;

        if (hasExplicitOrder)
        {
            findings.Add(new PairwiseConflictFinding(
                ModAPackageId: modA.PackageId,
                ModAName: modA.Name,
                ModBPackageId: modB.PackageId,
                ModBName: modB.Name,
                Grade: ConflictGrade.Green,
                Category: "AdditiveXmlPatchOverlap",
                TargetSignature: xpath,
                Summary: $"{modA.Name} 与 {modB.Name} 存在明确上下游依赖，按顺序对同一 XPath 节点执行扩展更新",
                Evidence: evidence,
                RecommendedBeforePackageId: orderedBefore,
                RecommendedAfterPackageId: orderedAfter,
                RecommendationReason: "上下游子模组/补丁包的正常覆盖更新关系"));
            return;
        }

        if (bothDestructive && !allGated)
        {
            // 替换/删除是扩展改原模组内容的常规手段：结果由顺序决定，但不应判成红色明确冲突
            findings.Add(new PairwiseConflictFinding(
                ModAPackageId: modA.PackageId,
                ModAName: modA.Name,
                ModBPackageId: modB.PackageId,
                ModBName: modB.Name,
                Grade: ConflictGrade.Yellow,
                Category: "DestructiveXmlPatchCollision",
                TargetSignature: xpath,
                Summary: $"{modA.Name} 与 {modB.Name} 对同一 XPath 节点都执行了替换或删除，且未声明先后；最终结果取决于加载顺序",
                Evidence: evidence,
                RecommendedBeforePackageId: orderedBefore,
                RecommendedAfterPackageId: orderedAfter,
                RecommendationReason: "扩展替换原模组内容通常是预期行为；确认加载顺序即可，出现内容缺失时再调整顺序或合并补丁"));
            return;
        }

        if (anyDestructive && !allGated)
        {
            findings.Add(new PairwiseConflictFinding(
                ModAPackageId: modA.PackageId,
                ModAName: modA.Name,
                ModBPackageId: modB.PackageId,
                ModBName: modB.Name,
                Grade: ConflictGrade.Yellow,
                Category: "ConditionalOrMixedXmlPatchOverlap",
                TargetSignature: xpath,
                Summary: $"{modA.Name} 与 {modB.Name} 对同一 XPath 节点分别执行了追加与替换/删除，需先替换再追加",
                Evidence: evidence,
                RecommendedBeforePackageId: orderedBefore,
                RecommendedAfterPackageId: orderedAfter,
                RecommendationReason: "建议先执行节点替换再执行列表追加，避免追加内容被后续 Replace 抹除"));
            return;
        }

        findings.Add(new PairwiseConflictFinding(
            ModAPackageId: modA.PackageId,
            ModAName: modA.Name,
            ModBPackageId: modB.PackageId,
            ModBName: modB.Name,
            Grade: ConflictGrade.Green,
            Category: "AdditiveXmlPatchOverlap",
            TargetSignature: xpath,
            Summary: $"{modA.Name} 与 {modB.Name} 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存",
            Evidence: evidence,
            RecommendedBeforePackageId: null,
            RecommendedAfterPackageId: null,
            RecommendationReason: "列表追加或条件兼容分支互不破坏"));
    }

    private static void EvaluateStructuralXPathPair(
        string structuralKey,
        ModDescriptor modA,
        IReadOnlyList<XmlPatchRecord> patchesA,
        ModDescriptor modB,
        IReadOnlyList<XmlPatchRecord> patchesB,
        List<PairwiseConflictFinding> findings)
    {
        var evidence = patchesA.Concat(patchesB)
            .Select(p => $"{p.OperationClass}: {p.XPath}")
            .Distinct(StringComparer.Ordinal)
            .Take(6)
            .ToList();

        findings.Add(new PairwiseConflictFinding(
            ModAPackageId: modA.PackageId,
            ModAName: modA.Name,
            ModBPackageId: modB.PackageId,
            ModBName: modB.Name,
            Grade: ConflictGrade.Yellow,
            Category: "StructuralXmlPatchOverlap",
            TargetSignature: structuralKey,
            Summary: $"{modA.Name} 与 {modB.Name} 在同一节点容器上分别做整体追加与定点索引修改，静态分析无法确定最终节点顺序",
            Evidence: evidence,
            RecommendedBeforePackageId: modA.OriginalIndex <= modB.OriginalIndex ? modA.PackageId : modB.PackageId,
            RecommendedAfterPackageId: modA.OriginalIndex <= modB.OriginalIndex ? modB.PackageId : modA.PackageId,
            RecommendationReason: "建议保持原列表顺序；若出现内容缺失，再调整二者的相对顺位"));
    }

    // ───────────────────────────── 工具方法 ─────────────────────────────

    private static IEnumerable<string> BuildDefKeys(XmlDefRecord def)
    {
        // defName 是 Def 的唯一标识；只有缺失 defName 时才退化到 Name 属性，
        // 否则同一个 Def 会被登记两次，导致重复的覆盖判定。
        if (!string.IsNullOrWhiteSpace(def.DefName))
            yield return $"{def.DefType}:{def.DefName}";
        else if (!string.IsNullOrWhiteSpace(def.NameAttribute))
            yield return $"{def.DefType}@{def.NameAttribute}";
    }

    private static void AddToIndex<T>(
        Dictionary<string, List<T>> index,
        string key,
        T value)
    {
        if (!index.TryGetValue(key, out var list))
        {
            list = new List<T>();
            index[key] = list;
        }
        list.Add(value);
    }

    private static string BuildStructuralXPathKey(string xpath)
    {
        return IndexPredicateRegex.Replace(xpath, string.Empty);
    }

    private static string BuildPairKey(string targetKey, ModDescriptor a, ModDescriptor b)
    {
        var first = a.PackageId.ToLowerInvariant();
        var second = b.PackageId.ToLowerInvariant();
        return string.CompareOrdinal(first, second) <= 0
            ? $"{targetKey}|{first}|{second}"
            : $"{targetKey}|{second}|{first}";
    }

    /// <summary>
    /// 是否为"底座"（官方内容或 Harmony 运行时）。
    /// </summary>
    private static bool IsBaseModule(ModDescriptor mod) =>
        mod.IsOfficialCoreOrDlc ||
        mod.AssemblyPaths.Any(path =>
            Path.GetFileName(path).Equals("0Harmony.dll", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// 是否为"自相矛盾的底座先后声明"——只有这种声明才该被丢弃。
    ///
    /// 底座的先后声明绝大多数是<b>权威顺序</b>，必须生效：官方 DLC 之间只用 forceLoadAfter/forceLoadBefore
    /// 表达先后（Core → Royalty → Ideology → Biotech → Anomaly → Odyssey），Harmony 声明 loadBefore Core。
    /// 把这类声明整类丢弃会让 DLC 链彻底失效，乱序输入下甚至能把 Core 排到最后、游戏无法启动。
    ///
    /// 真正该丢的只有一种：模组一边依赖对方、一边要求自己排在对方之前（Prepatcher 依赖 Harmony
    /// 却声明 loadBefore Harmony）。这种声明若生效会凭空造环，才需要跳过。
    /// </summary>
    private static bool IsContradictoryBaseDeclaration(ModDescriptor mod, ModDescriptor target, bool loadsBefore)
    {
        // 底座之间的声明是官方权威顺序，一律保留。
        if (IsBaseModule(mod) && IsBaseModule(target))
            return false;

        var modDependsOnTarget = DependsOn(mod, target);
        var targetDependsOnMod = DependsOn(target, mod);

        if (loadsBefore)
        {
            // 要求自己先于对方，却依赖对方（或已声明 loadAfter 对方）→ 矛盾
            return modDependsOnTarget || DeclaresOrder(mod, target, loadAfter: true);
        }

        // 要求自己后于对方，却反过来被对方依赖（或已声明 loadBefore 对方）→ 矛盾
        return targetDependsOnMod || DeclaresOrder(mod, target, loadAfter: false);
    }

    private static bool DependsOn(ModDescriptor mod, ModDescriptor target) =>
        mod.Dependencies.Any(d => IsSameId(d.PackageId, target.PackageId));

    private static bool DeclaresOrder(ModDescriptor mod, ModDescriptor target, bool loadAfter)
    {
        var ids = loadAfter
            ? mod.LoadAfter.Concat(mod.ForceLoadAfter)
            : mod.LoadBefore.Concat(mod.ForceLoadBefore);

        return ids.Any(id => IsSameId(id, target.PackageId));
    }

    private static bool IsSameId(string left, string right)
    {
        var a = ModListResolver.NormalizePackageId(left);
        var b = ModListResolver.NormalizePackageId(right);
        return a.Equals(b, StringComparison.OrdinalIgnoreCase) ||
               ModListResolver.StripSteamSuffix(a).Equals(ModListResolver.StripSteamSuffix(b), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 判断 literal 中是否出现作为完整词的 assemblyName：
    /// 其前后要么是字符串边界，要么是非字母数字字符（. _ - 空格等分隔符）。
    /// 例如 "Vanilla" 不再命中 "VanillaExpandedFramework"，但命中 "Vanilla.dll" / "ref: Vanilla"。
    /// </summary>
    private static bool ContainsAsWord(string literal, string assemblyName)
    {
        var searchStart = 0;
        while (searchStart <= literal.Length - assemblyName.Length)
        {
            var idx = literal.IndexOf(assemblyName, searchStart, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
                return false;

            var beforeOk = idx == 0 || !char.IsLetterOrDigit(literal[idx - 1]);
            var afterIdx = idx + assemblyName.Length;
            var afterOk = afterIdx == literal.Length || !char.IsLetterOrDigit(literal[afterIdx]);

            if (beforeOk && afterOk)
                return true;

            searchStart = idx + 1;
        }

        return false;
    }

    private static bool IsSelf(ModDescriptor candidate, ModDescriptor reference)
    {
        return candidate.PackageId.Equals(reference.PackageId, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSameMod(ModDescriptor candidate, ModDescriptor reference)
    {
        return candidate.PackageId.Equals(reference.PackageId, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildTargetKey(HarmonyPatchRecord patch)
    {
        var typeName = string.IsNullOrWhiteSpace(patch.TargetTypeFullName)
            ? "<UnknownType>"
            : patch.TargetTypeFullName.TrimStart('.');
        var methodName = string.IsNullOrWhiteSpace(patch.TargetMethodName) ? "<UnknownMethod>" : patch.TargetMethodName;
        var kindSuffix = patch.TargetMethodKind == "Normal" ? string.Empty : $"[{patch.TargetMethodKind}]";
        return $"{typeName}.{methodName}{kindSuffix}";
    }

    private static List<OrderingEdge> DeduplicateEdges(List<OrderingEdge> rawEdges)
    {
        return rawEdges
            .Where(e => !e.BeforePackageId.Equals(e.AfterPackageId, StringComparison.OrdinalIgnoreCase))
            .GroupBy(e => $"{e.BeforePackageId.ToLowerInvariant()}->{e.AfterPackageId.ToLowerInvariant()}")
            .Select(g =>
            {
                var best = g.OrderByDescending(x => x.IsHardConstraint).ThenByDescending(x => x.Weight).First();
                var combinedReasons = string.Join("；", g.Select(x => x.Reason).Distinct());
                return best with { Reason = combinedReasons };
            })
            .ToList();
    }

    private static List<PairwiseConflictFinding> DeduplicateFindings(List<PairwiseConflictFinding> rawFindings)
    {
        return rawFindings
            .GroupBy(f => $"{f.Category}|{f.TargetSignature}|{f.ModAPackageId.ToLowerInvariant()}|{f.ModBPackageId.ToLowerInvariant()}")
            .Select(g => g.OrderByDescending(f => f.Grade).First())
            .OrderByDescending(f => f.Grade)
            .ThenBy(f => f.Category, StringComparer.Ordinal)
            .ToList();
    }
}
