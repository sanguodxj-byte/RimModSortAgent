using ModSorterAgent.Models;

namespace ModSorterAgent.Reporting;

/// <summary>
/// 面向玩家的冲突条目：只说"哪两个模组、会发生什么、该怎么办"。
/// 技术分类、XPath、补丁方法名等留在 <see cref="Findings"/> 里供 Agent 与高级视图使用。
/// </summary>
public sealed record PlayerConflict(
    string Key,
    ConflictGrade Grade,
    string SeverityLabel,
    string ModAName,
    string ModAPackageId,
    string ModBName,
    string ModBPackageId,
    string Category,
    string CategoryLabel,
    string Headline,
    string What,
    string Impact,
    string Action,
    string TargetLabel,
    IReadOnlyList<string> Targets,
    IReadOnlyList<PairwiseConflictFinding> Findings,
    bool InvolvesInactiveMod)
{
    /// <summary>同一模组自身的问题（例如它自己的 Def 配置错误）不显示成「X ↔ X」。</summary>
    public string PairLabel => string.Equals(ModAPackageId, ModBPackageId, StringComparison.OrdinalIgnoreCase)
        ? ModAName
        : $"{ModAName} ↔ {ModBName}";

    public string ActionSummary => Grade switch
    {
        ConflictGrade.Purple => "作者声明不兼容",
        ConflictGrade.Red => "会让模组失效",
        _ => "需要确认"
    };
}

/// <summary>
/// 把评分器的技术发现翻译成玩家能直接理解的冲突清单。
/// 只保留黄色/红色/紫色，且至少一方处于启用状态；同一对模组的同类问题合并为一条。
/// </summary>
public static class ConflictNarrative
{
    public static IReadOnlyList<PlayerConflict> Build(ModSortReport report)
    {
        var activeById = report.SortedMods
            .GroupBy(m => m.PackageId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().IsActive, StringComparer.OrdinalIgnoreCase);

        var groups = new Dictionary<string, List<PairwiseConflictFinding>>(StringComparer.OrdinalIgnoreCase);

        foreach (var finding in report.ConflictFindings)
        {
            if (finding.Grade == ConflictGrade.Green)
                continue;

            var pairKey = string.Compare(finding.ModAPackageId, finding.ModBPackageId, StringComparison.OrdinalIgnoreCase) <= 0
                ? $"{finding.ModAPackageId}|{finding.ModBPackageId}"
                : $"{finding.ModBPackageId}|{finding.ModAPackageId}";

            var key = $"{pairKey}|{finding.Category}";
            if (!groups.TryGetValue(key, out var list))
                groups[key] = list = new List<PairwiseConflictFinding>();
            list.Add(finding);
        }

        var conflicts = new List<PlayerConflict>(groups.Count);
        foreach (var (key, findings) in groups)
        {
            var first = findings[0];
            var aActive = activeById.GetValueOrDefault(first.ModAPackageId, false);
            var bActive = IsMissingDependencyPair(first)
                ? aActive
                : activeById.GetValueOrDefault(first.ModBPackageId, false);

            if (!aActive && !bActive)
                continue;

            conflicts.Add(BuildConflict(key, findings, !aActive || !bActive));
        }

        return conflicts
            .OrderByDescending(c => c.Grade)
            .ThenBy(c => c.InvolvesInactiveMod)
            .ThenBy(c => c.ModAName, StringComparer.CurrentCulture)
            .ThenBy(c => c.ModBName, StringComparer.CurrentCulture)
            .ToList();
    }

    private static bool IsMissingDependencyPair(PairwiseConflictFinding finding)
        => finding.Category.Equals("MissingDependency", StringComparison.OrdinalIgnoreCase);

    private static PlayerConflict BuildConflict(
        string key,
        IReadOnlyList<PairwiseConflictFinding> findings,
        bool involvesInactive)
    {
        var first = findings[0];
        var grade = findings.Max(f => f.Grade);
        var severity = grade switch
        {
            ConflictGrade.Purple => "作者声明不兼容",
            ConflictGrade.Red => "明确冲突",
            ConflictGrade.Yellow => "可能有问题",
            _ => "正常"
        };

        var (aName, aId, bName, bId) = ResolveRoles(first);
        var targets = findings
            .Select(f => ShortTarget(f.TargetSignature))
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var targetLabel = targets.Count == 0
            ? "游戏内容"
            : targets.Count == 1
                ? targets[0]
                : $"{targets[0]} 等 {targets.Count} 处";

        var (categoryLabel, headline, what, impact, action) =
            Describe(first.Category, aName, bName, targetLabel, targets, findings, grade);

        return new PlayerConflict(
            Key: key,
            Grade: grade,
            SeverityLabel: severity,
            ModAName: aName,
            ModAPackageId: aId,
            ModBName: bName,
            ModBPackageId: bId,
            Category: first.Category,
            CategoryLabel: categoryLabel,
            Headline: headline,
            What: what,
            Impact: impact,
            Action: action,
            TargetLabel: targetLabel,
            Targets: targets,
            Findings: findings,
            InvolvesInactiveMod: involvesInactive);
    }

    /// <summary>缺前置时 B 是"不存在的前置"，不是冲突另一方，展示上单独处理。</summary>
    private static (string AName, string AId, string BName, string BId) ResolveRoles(PairwiseConflictFinding finding)
    {
        if (finding.Category.Equals("HarmonyBlockingPrefixConflict", StringComparison.OrdinalIgnoreCase) &&
            finding.RecommendedAfterPackageId is { Length: > 0 } blockerId)
        {
            // 截断型 Prefix 是造成失效的一方，放在前面说
            var blockerIsA = blockerId.Equals(finding.ModAPackageId, StringComparison.OrdinalIgnoreCase);
            return blockerIsA
                ? (finding.ModAName, finding.ModAPackageId, finding.ModBName, finding.ModBPackageId)
                : (finding.ModBName, finding.ModBPackageId, finding.ModAName, finding.ModAPackageId);
        }

        return (finding.ModAName, finding.ModAPackageId, finding.ModBName, finding.ModBPackageId);
    }

    private static (string CategoryLabel, string Headline, string What, string Impact, string Action) Describe(
        string category,
        string a,
        string b,
        string target,
        IReadOnlyList<string> targets,
        IReadOnlyList<PairwiseConflictFinding> findings,
        ConflictGrade grade)
    {
        var countHint = findings.Count > 1 ? $"（共 {findings.Count} 处）" : string.Empty;

        return category switch
        {
            "HarmonyBlockingPrefixConflict" => (
                "功能被抢先截断",
                $"「{a}」会让「{b}」的功能失效{countHint}",
                $"「{a}」把游戏方法「{target}」整个接管了，并且跳过了原方法。于是「{b}」对原方法做的修改完全没有机会执行。",
                $"「{b}」的对应功能会失效或不完整，通常表现为渲染、动作或界面效果缺失。",
                $"这类冲突无法靠调整加载顺序解决。二选一：暂时禁用「{a}」或「{b}」；或者让 Agent 核查后生成兼容补丁。"),

            "ConditionalPrefixOrResultMutationOverlap" => (
                "同一方法争夺结果",
                $"「{a}」与「{b}」在争夺同一个方法的结果{countHint}",
                $"两个模组都会改写「{target}」的执行结果。谁最后写入，谁的结果就生效，另一个的结果会被覆盖。",
                "可能出现数值异常、判定时灵时不灵，或某个模组的功能只生效一半。",
                "先按推荐顺序排列；如果游戏内仍不对劲，让 Agent 核查两条修改逻辑是否真的互斥。"),

            "MultipleTranspilersSameTarget" => (
                "同一段逻辑被双重改写",
                $"「{a}」与「{b}」同时改写了同一段游戏逻辑{countHint}",
                $"两个模组都在「{target}」内部插入或改写代码。改写互相穿插时，可能找不到预期位置而报错，也可能改变彼此的判断。",
                "轻则其中一个模组效果不对，重则加载或运行时报 Harmony 异常。",
                "先按推荐顺序排列。如果日志仍出现 Harmony 异常，禁用其中一个，或让 Agent 生成兼容补丁。"),

            "DuplicateXmlDefOverwrite" => (
                "扩展覆盖了原有内容",
                $"「{a}」与「{b}」都定义了同一项内容{countHint}",
                $"扩展模组覆盖原模组内容属于常见做法。两个模组都定义了「{target}」，未声明先后时，后加载的会整段替换先加载的版本。",
                "如果覆盖结果不是预期版本，先前新增的字段可能丢失，出现物品/种族/建筑缺属性或红字。",
                $"确认加载顺序符合预期即可；需要同时保留两边改动时，再让 Agent 生成合并补丁。"),

            "DestructiveXmlPatchCollision" => (
                "扩展替换了同一段数据",
                $"「{a}」与「{b}」都在替换同一段游戏数据{countHint}",
                $"扩展覆盖原模组内容通常是有意为之。两个模组都对「{target}」执行替换或删除，且未声明先后，最终结果由加载顺序决定。",
                "若后执行的不是预期版本，可能丢失前一方写入的属性、列表项或整套设定。",
                "先确认加载顺序符合预期；出现内容缺失时再调整顺序，或让 Agent 生成修复补丁。"),

            "ConditionalOrMixedXmlPatchOverlap" => (
                "追加与替换撞车",
                $"「{a}」与「{b}」对同一节点分别做了追加和替换{countHint}",
                $"一个模组在「{target}」上追加内容，另一个在替换或删除同一节点。如果先追加后替换，追加的内容会被一起抹掉。",
                "表现为部分内容缺失，且看起来像随机丢失，很难靠肉眼定位。",
                "按推荐顺序排列（先替换、后追加）；若已按顺序仍缺失，让 Agent 核查并生成修复补丁。"),

            "StructuralXmlPatchOverlap" => (
                "列表顺序不确定",
                $"「{a}」与「{b}」在同一列表节点上混用整体追加与定点修改{countHint}",
                $"两个模组都以不同方式修改「{target}」的列表结构，最终节点顺序无法仅靠静态分析确定。",
                "可能出现条目顺序错乱、索引错位，或个别条目被挤掉。",
                "先按推荐顺序排列；若游戏内仍有异常，让 Agent 核查具体节点。"),

            "ExplicitIncompatibility" => (
                "作者声明不兼容",
                $"「{a}」的作者明确声明与「{b}」不兼容",
                $"「{a}」的 About.xml 中直接声明了 incompatibleWith，这是模组作者给出的确定性结论。",
                "同时启用时通常会出现红字、功能直接失效，或存档损坏风险。",
                "在右侧紫色冲突里选择保留哪一方；只选一边会按正常顺序写入配置，两边都保留则进入兼容生成范围。"),

            "BrokenXmlDefReference" => (
                "引用了对不上的内容",
                $"「{a}」引用了一份对不上的游戏内容{countHint}",
                findings[0].Summary,
                "游戏载入时会打印红字并丢弃这条引用，相关的物品、音效或伤害数值会缺失、不生效。",
                "调整加载顺序解决不了：要么补上（或启用）定义它的那个模组，要么让 Agent 生成一条 XML 补丁改掉这处引用。"),

            "XmlDefConfigError" => (
                "内容配置有误",
                $"「{a}」的一项内容被游戏判定为配置错误{countHint}",
                findings[0].Summary,
                "游戏会忽略出错的那部分数据，通常表现为数值不对或抗性失效，一般不会导致崩溃。",
                "属于该模组自身的数据问题：可以让 Agent 生成 XML 补丁修正，或等作者更新。"),

            "MissingDependency" => (
                "缺少前置模组",
                $"「{a}」缺少前置模组「{b}」",
                $"「{a}」要求必须先安装并启用「{b}」，但当前列表里没有找到它。",
                "游戏启动或加载存档时会直接报错，模组内容不会正常工作。",
                $"安装并启用「{b}」，或者暂时禁用「{a}」。"),

            "CyclicLoadOrderConstraint" => (
                "加载顺序互相矛盾",
                $"「{a}」与「{b}」互相要求排在对方后面",
                "两个模组的 About.xml 声明了相反的加载顺序，无法同时满足。工具已强制断开其中一条边。",
                "排序只能满足其中一边，另一边可能因加载顺序不对而出现异常。",
                "优先检查是否有模组更新修复了声明；否则禁用其中一个。"),

            _ => (
                UiCategoryLabel(category),
                $"{a} 与 {b} 存在{GradeWord(grade)}",
                $"检测到「{a}」与「{b}」在「{target}」上存在重叠：{StripModNames(findings[0].Summary, a, b)}",
                "可能影响相关功能，具体表现取决于运行时的条件分支。",
                "先按推荐顺序排列；若游戏内出现异常，交给 Agent 做代码核查。")
        };
    }

    private static string StripModNames(string summary, string a, string b)
    {
        return summary
            .Replace(a, "其中一方", StringComparison.Ordinal)
            .Replace(b, "另一方", StringComparison.Ordinal);
    }

    private static string GradeWord(ConflictGrade grade) => grade switch
    {
        ConflictGrade.Purple => "作者声明不兼容",
        ConflictGrade.Red => "明确冲突",
        _ => "潜在冲突"
    };

    private static string UiCategoryLabel(string category) => category switch
    {
        "HarmonyBlockingPrefixConflict" => "功能被抢先截断",
        "ConditionalPrefixOrResultMutationOverlap" => "同一方法争夺结果",
        "MultipleTranspilersSameTarget" => "同一段逻辑被双重改写",
        "DuplicateXmlDefOverwrite" => "游戏内容被重复定义",
        "DestructiveXmlPatchCollision" => "游戏数据被互相替换",
        "ConditionalOrMixedXmlPatchOverlap" => "追加与替换撞车",
        "StructuralXmlPatchOverlap" => "列表顺序不确定",
        "ExplicitIncompatibility" => "作者声明不兼容",
        "MissingDependency" => "缺少前置模组",
        "CyclicLoadOrderConstraint" => "加载顺序互相矛盾",
        "BrokenXmlDefReference" => "引用了对不上的内容",
        "XmlDefConfigError" => "内容配置有误",
        _ => "模组冲突"
    };

    /// <summary>把技术目标缩短成玩家能认出来的名字：类名.方法名 / XPath 末段。</summary>
    public static string ShortTarget(string target)
    {
        if (string.IsNullOrWhiteSpace(target))
            return string.Empty;

        var value = target.Trim();

        if (value.Contains('/'))
        {
            var trimmed = value.TrimStart('/');
            if (trimmed.StartsWith("Defs/", StringComparison.OrdinalIgnoreCase))
                trimmed = trimmed[5..];
            return trimmed.Length > 90 ? trimmed[..87] + "…" : trimmed;
        }

        var parts = value.Split('.');
        if (parts.Length >= 2 && parts[^1].Length > 0)
            return $"{parts[^2]}.{parts[^1]}";

        return value;
    }
}
