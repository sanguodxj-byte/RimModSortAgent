using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ModSorterAgent.Models;

namespace ModSorterAgent.Reporting;

/// <summary>
/// 冲突字段的白话呈现层。界面只允许输出这里产出的句子，
/// 原生类别枚举、XPath、程序集方法签名、Harmony 参数串一律不外露。
/// </summary>
public static class ConflictPlainText
{
    private static readonly Regex HarmonyEvidence = new(
        @"^(?<mod>.+?): \[(?<kind>[A-Za-z]+)\] (?<member>[\w\.`<>+]+) \(Priority=(?<p>-?\d+), AlwaysSkip=(?<skip>True|False), CondSkip=(?<cond>True|False), Guarded=(?<guard>True|False)\)$",
        RegexOptions.Compiled);

    private static readonly Regex MissingDependencyEvidence = new(
        @"^About\.xml 的 modDependencies 声明依赖 (?<id>.+?)，但当前启用列表中未包含该模组$",
        RegexOptions.Compiled);

    private static readonly Regex IncompatibleEvidence = new(
        @"^About\.xml 的 incompatibleWith 包含 (?<id>.+)$",
        RegexOptions.Compiled);

    private static readonly Regex DriftLibrary = new(
        @"^共享依赖库: (?<asm>.+)\.dll$",
        RegexOptions.Compiled);

    private static readonly Regex DriftNewerSuperset = new(
        @"^(?<mod>.+?) 携带新版 \(v(?<v>[^)]+)\) 包含旧版全部方法，且有自身专有新方法$",
        RegexOptions.Compiled);

    private static readonly Regex DriftOlder = new(
        @"^(?<mod>.+?) 携带旧版 \(v(?<v>[^)]+)\)(?:，路径: .+)?$",
        RegexOptions.Compiled);

    private static readonly Regex DriftNewer = new(
        @"^(?<mod>.+?) 携带新版 \(v(?<v>[^)]+)\)(?:，路径: .+)?$",
        RegexOptions.Compiled);

    private static readonly Regex DriftMissingHeader = new(
        @"^新版中已缺失的调用方法 \(\d+ 项\):$",
        RegexOptions.Compiled);

    private static readonly Regex DriftMissingItem = new(
        @"^\s*[×x] (?<sig>.+)$",
        RegexOptions.Compiled);

    private static readonly Regex DriftOrderEdge = new(
        @"^已建立优先排序约束: (?<a>.+) 必须排在 (?<b>.+) 之前$",
        RegexOptions.Compiled);

    private static readonly Regex FileOnlyEvidence = new(
        @"^(?<mod>.+?)[：:]\s*.+\.(xml|cs|dll)$",
        RegexOptions.Compiled);

    private static readonly Regex DefTypeMismatch = new(
        @"^(?<mod>.+?) 把「(?<name>.+)」定义为 (?<types>.+)$",
        RegexOptions.Compiled);

    private static readonly Regex PatchOperationWithMod = new(
        @"^(?<mod>.+?): (?<ops>PatchOperation[\w, ]+) \((?<file>.+)\)$",
        RegexOptions.Compiled);

    private static readonly Regex PatchOperationOnly = new(
        @"^(?<op>PatchOperation\w+): (?<path>/.+)$",
        RegexOptions.Compiled);

    private static readonly Regex PackageIdShape = new(
        @"^[a-z0-9_]+(\.[a-z0-9_]+)+$",
        RegexOptions.Compiled);

    private static readonly Regex CjkGap = new(
        @"(?<=[\u4e00-\u9fff])\s+(?=[\u4e00-\u9fff])",
        RegexOptions.Compiled);

    private static readonly Regex PunctuationGap = new(
        @"(?<=[「」『』，。；：、（）])\s+(?=[\u4e00-\u9fff])|(?<=[\u4e00-\u9fff])\s+(?=[「」『』，。；：、（）])",
        RegexOptions.Compiled);

    private static readonly (string From, string To)[] Phrasebook =
    {
        ("无条件返回 false 跳过原方法", "无条件让原逻辑直接结束，不再往下执行"),
        ("返回 false 的接管型 Prefix", "会接管并终止原逻辑的前置补丁"),
        ("无守卫的返回值直接赋值", "直接改写返回值，且没有任何条件保护"),
        ("静态分析无法排除 IL 锚点相互影响", "无法确定两处代码改写是否会互相对不上"),
        ("均带有条件守卫或增量计算", "两边都只做条件判断或追加计算"),
        ("作用域正交", "改动范围互不重叠"),
        ("拓扑排序", "排序"),
        ("显式声明", "明确声明"),
        ("增量修饰", "追加修改"),
        ("增量 Patch", "追加修改"),
        ("条件守卫", "条件判断"),
        ("硬约束", "强制顺序"),
        ("软约束", "建议顺序"),
        ("原方法 IL", "原方法体"),
        ("XML 引用", "内容引用"),
        ("Replace", "替换"),
        ("同一 XPath 节点", "同一处配置内容"),
        ("XPath 节点", "配置内容"),
        ("XPath", "配置内容"),
        ("IL 锚点", "代码插入点"),
        ("HarmonyPriority", "补丁优先级"),
        ("MissingMethodException", "方法缺失异常"),
        ("Transpiler", "代码改写补丁"),
        ("Prefix", "前置补丁"),
        ("Postfix", "后置补丁"),
        ("Finalizer", "收尾补丁"),
        ("静态分析", "静态检查"),
    };

    public static string CategoryLabel(string raw) => raw switch
    {
        "DuplicateXmlDefOverwrite" => "同名内容被覆盖",
        "HarmonyBlockingPrefixConflict" => "补丁互相压制",
        "ConditionalPrefixOrResultMutationOverlap" => "条件逻辑互相覆盖",
        "ConditionalOrMixedXmlPatchOverlap" => "修改方式互相冲突",
        "DestructiveXmlPatchCollision" => "替换与删除互相冲撞",
        "StructuralXmlPatchOverlap" => "同一处的增删顺序不明",
        "DynamicHarmonyTarget" => "运行期才确定改动目标",
        "MultipleTranspilersSameTarget" => "同一段逻辑被多次改写",
        "CompatibleHarmonyOverlap" => "功能重叠但可共存",
        "AdditiveXmlPatchOverlap" => "各自追加内容，可共存",
        "ExplicitIncompatibility" => "作者声明不兼容",
        "MissingDependency" => "缺少前置模组",
        "CyclicLoadOrderConstraint" => "加载顺序互相矛盾",
        "BrokenXmlDefReference" => "引用的内容不存在",
        "XmlDefConfigError" => "内容配置有误",
        "AssemblyDependencyDriftBreaking" => "依赖库版本断裂",
        "AssemblyDependencyDriftAutoHealed" => "依赖库版本已自动兼容",
        _ => "未归类问题"
    };

    /// <summary>
    /// 产出"问题出在哪"的白话描述；对模组级依赖/互斥这类没有额外落点的问题返回 false，
    /// 让界面直接省略该字段而不是显示一个空壳。
    /// </summary>
    public static bool TryDescribeTarget(PairwiseConflictFinding finding, out string description)
    {
        description = string.Empty;

        switch (finding.Category)
        {
            case "MissingDependency":
            case "ExplicitIncompatibility":
            case "CyclicLoadOrderConstraint":
                return false;
        }

        var signature = (finding.TargetSignature ?? string.Empty).Trim();
        if (signature.Length == 0)
            return false;

        if (finding.Category is "AssemblyDependencyDriftBreaking" or "AssemblyDependencyDriftAutoHealed")
        {
            description = $"两个模组各自打包的同名库「{ExtractAssemblyName(signature)}」";
            return true;
        }

        var arrow = signature.IndexOf(" -> ", StringComparison.Ordinal);
        if (arrow > 0)
        {
            description = $"两个模组各自打包的同名库「{ExtractAssemblyName(signature)}」";
            return true;
        }

        if (signature.StartsWith("/", StringComparison.Ordinal))
        {
            description = "同一处配置内容";
            return true;
        }

        var colon = signature.IndexOf(':');
        if (colon > 0 && colon < signature.Length - 1)
        {
            description = $"同名内容「{signature.Substring(colon + 1)}」";
            return true;
        }

        if (signature.IndexOf('.') > 0 && !PackageIdShape.IsMatch(signature))
        {
            description = $"游戏方法「{ShortMemberName(signature)}」";
            return true;
        }

        if (PackageIdShape.IsMatch(signature))
        {
            description = $"模组「{signature}」";
            return true;
        }

        description = $"「{signature}」";
        return true;
    }

    /// <summary>
    /// 摘要里嵌着原生签名，替换成与"问题所在"一致的白话说法，并顺带替换开发术语。
    /// </summary>
    public static string PlainSummary(PairwiseConflictFinding finding)
    {
        return TranslateAnalysisText(finding.Summary, finding);
    }

    /// <summary>
    /// 处置建议同样来自分析层，带 Prefix/Transpiler 一类术语，界面必须走这里。
    /// </summary>
    public static string PlainRecommendation(PairwiseConflictFinding finding)
    {
        return TranslateAnalysisText(finding.RecommendationReason, finding);
    }

    private static string TranslateAnalysisText(string? raw, PairwiseConflictFinding finding)
    {
        var text = raw ?? string.Empty;
        if (text.Length == 0)
            return text;

        foreach (var (from, to) in Phrasebook)
            text = text.Replace(from, to);

        var signature = (finding.TargetSignature ?? string.Empty).Trim();
        if (signature.Length > 0 && TryDescribeTarget(finding, out var target))
            text = text.Replace(signature, target);

        return PunctuationGap.Replace(CjkGap.Replace(text, string.Empty), string.Empty);
    }

    public static string PartnerName(PairwiseConflictFinding finding, string selfPackageId)
    {
        // 自引用条目（模组自己引用了不存在的定义、类型写错等）没有"对方"，
        // 不能把自己的名字当成相关模组列出来。
        if (IsSelfReference(finding))
            return "该模组自身";

        return string.Equals(finding.ModAPackageId, selfPackageId, StringComparison.OrdinalIgnoreCase)
            ? finding.ModBName
            : finding.ModAName;
    }

    /// <summary>条目是否描述「单个模组自身的问题」，而不是两个模组之间的相互作用。</summary>
    public static bool IsSelfReference(PairwiseConflictFinding finding)
    {
        return !string.IsNullOrEmpty(finding.ModAPackageId)
               && string.Equals(finding.ModAPackageId, finding.ModBPackageId, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 逐条把取证原文翻成白话。无法安全翻译的条目（日志原文、裸文件路径以外的机器输出）直接丢弃，
    /// 保证界面上不出现玩家读不懂的字段串。
    /// </summary>
    public static IReadOnlyList<string> PlainEvidence(PairwiseConflictFinding finding)
    {
        var result = new List<string>();
        if (finding.Evidence == null || finding.Evidence.Count == 0)
            return result;

        foreach (var raw in finding.Evidence)
        {
            var translated = TranslateEvidenceLine(raw);
            if (!string.IsNullOrWhiteSpace(translated))
                result.Add(translated);
        }

        return result;
    }

    private static string? TranslateEvidenceLine(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var line = raw.Trim();

        var harmony = HarmonyEvidence.Match(line);
        if (harmony.Success)
        {
            var mod = harmony.Groups["mod"].Value;
            var kind = PatchKindLabel(harmony.Groups["kind"].Value);
            var priority = harmony.Groups["p"].Value;
            var skip = harmony.Groups["skip"].Value == "True"
                ? "，会直接终止原逻辑"
                : harmony.Groups["cond"].Value == "True"
                    ? "，仅在条件满足时终止原逻辑"
                    : "，不会终止原逻辑";
            var guard = harmony.Groups["guard"].Value == "True"
                ? "，带有保护判断"
                : "，没有保护判断";

            return $"「{mod}」在同一处逻辑上做{kind}（执行顺序号 {priority}{skip}{guard}）";
        }

        var missingDependency = MissingDependencyEvidence.Match(line);
        if (missingDependency.Success)
            return $"模组信息里写明必须有前置「{missingDependency.Groups["id"].Value}」，但当前没有启用它";

        var incompatible = IncompatibleEvidence.Match(line);
        if (incompatible.Success)
            return $"模组信息里写明与「{incompatible.Groups["id"].Value}」不兼容";

        var library = DriftLibrary.Match(line);
        if (library.Success)
            return $"两个模组各自打包了同名的库「{library.Groups["asm"].Value}」";

        var superset = DriftNewerSuperset.Match(line);
        if (superset.Success)
            return $"「{superset.Groups["mod"].Value}」的新版（{superset.Groups["v"].Value}）包含旧版全部方法，并新增了自己的方法";

        var older = DriftOlder.Match(line);
        if (older.Success)
            return $"「{older.Groups["mod"].Value}」带的是旧版（{older.Groups["v"].Value}）";

        var newer = DriftNewer.Match(line);
        if (newer.Success)
            return $"「{newer.Groups["mod"].Value}」带的是新版（{newer.Groups["v"].Value}）";

        if (DriftMissingHeader.IsMatch(line))
            return "新版里已经找不到这些方法：";

        var missingItem = DriftMissingItem.Match(line);
        if (missingItem.Success)
            return $"• 方法「{ShortMemberName(missingItem.Groups["sig"].Value)}」";

        var orderEdge = DriftOrderEdge.Match(line);
        if (orderEdge.Success)
            return $"排序时已自动让「{orderEdge.Groups["a"].Value}」排在「{orderEdge.Groups["b"].Value}」之前";

        var mishandled = DefTypeMismatch.Match(line);
        if (mishandled.Success)
            return $"「{mishandled.Groups["mod"].Value}」把同名内容定义成了另一种类型";

        var patchWithMod = PatchOperationWithMod.Match(line);
        if (patchWithMod.Success)
            return $"「{patchWithMod.Groups["mod"].Value}」对同一处做了{TranslateOperations(patchWithMod.Groups["ops"].Value)}";

        var patchOnly = PatchOperationOnly.Match(line);
        if (patchOnly.Success)
            return $"同一位置还做了{OperationLabel(patchOnly.Groups["op"].Value)}操作";

        var fileOnly = FileOnlyEvidence.Match(line);
        if (fileOnly.Success)
            return $"「{fileOnly.Groups["mod"].Value}」里有对应的定义文件";

        return null;
    }

    private static string TranslateOperations(string raw)
    {
        var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var labels = new List<string>(parts.Length);
        foreach (var part in parts)
            labels.Add(OperationLabel(part));

        return string.Join("、", labels);
    }

    private static string OperationLabel(string operation) => operation switch
    {
        "PatchOperationAdd" => "追加",
        "PatchOperationReplace" => "替换",
        "PatchOperationRemove" => "删除",
        "PatchOperationInsert" => "插入",
        "PatchOperationSetName" => "改名",
        "PatchOperationAttributeSet" => "设置属性",
        "PatchOperationAttributeAdd" => "添加属性",
        "PatchOperationAddModExtension" => "扩充内容",
        "PatchOperationSequence" => "成组修改",
        "PatchOperationConditional" => "条件判断",
        "PatchOperationFindMod" => "按模组判断",
        "PatchOperationTest" => "条件检查",
        _ => "修改"
    };

    private static string PatchKindLabel(string kind) => kind switch
    {
        "Prefix" => "前置处理",
        "Postfix" => "后置处理",
        "Transpiler" => "代码改写",
        "Finalizer" => "收尾处理",
        _ => "补丁"
    };

    private static string ExtractAssemblyName(string signature)
    {
        var head = signature;
        var arrow = head.IndexOf(" -> ", StringComparison.Ordinal);
        if (arrow > 0)
            head = head.Substring(0, arrow);

        var versionMark = head.IndexOf(" (v", StringComparison.Ordinal);
        if (versionMark > 0)
            head = head.Substring(0, versionMark);

        return head.Trim();
    }

    /// <summary>
    /// 把 "System.Void Verse.Pawn::Kill(Verse.DamageInfo)" 这类型号压成界面可读的成员名。
    /// </summary>
    private static string ShortMemberName(string signature)
    {
        var text = (signature ?? string.Empty).Trim();

        var paren = text.IndexOf('(');
        if (paren > 0)
            text = text.Substring(0, paren);

        var generic = text.IndexOf('<');
        if (generic > 0)
            text = text.Substring(0, generic);

        // 返回类型前缀对玩家没有意义，只保留类型.成员
        var space = text.LastIndexOf(' ');
        if (space >= 0 && space + 1 < text.Length)
            text = text.Substring(space + 1);

        var doubleColon = text.LastIndexOf("::", StringComparison.Ordinal);
        if (doubleColon >= 0)
            text = text.Substring(doubleColon + 2);

        var parts = text.Split('.');
        if (parts.Length >= 3)
            text = parts[parts.Length - 2] + "." + parts[parts.Length - 1];

        return text.Trim();
    }
}
