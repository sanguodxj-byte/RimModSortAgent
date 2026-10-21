using ModSorterAgent.Agent;
using ModSorterAgent.Models;
using ModSorterAgent.Reporting;

namespace ModSorterAgent.Patching;

/// <summary>
/// 兼容补丁的准入规则：作者明确声明互斥的冲突只有在玩家选择"两边都保留"后才复核；
/// 其余必须有真实代码/XML 证据，且 Agent 给出了可自动落地的补丁方案。
/// 所有入选冲突合并进同一个补丁 mod。
/// </summary>
public static class CompatPatchEligibility
{
    public static bool IsAuthorDeclared(PlayerConflict conflict)
        => conflict.Category.Equals("ExplicitIncompatibility", StringComparison.Ordinal);

    public static bool HasCodeEvidence(AgentReport report)
        => report.CodeSources.Count > 0;

    public static bool HasPatchPlan(AgentReport report)
        => report.PatchPlan is { Kind: "harmony" or "xml" };

    private static bool IsApproved(PlayerConflict conflict, IReadOnlySet<string>? approvedAuthorDeclared)
        => !IsAuthorDeclared(conflict) ||
           approvedAuthorDeclared?.Contains(conflict.Key) == true;

    /// <summary>值得让 Agent 继续核查/生成补丁的冲突。</summary>
    public static bool IsReviewable(
        PlayerConflict conflict,
        AgentReport report,
        IReadOnlySet<string>? approvedAuthorDeclared = null)
        => IsApproved(conflict, approvedAuthorDeclared) &&
           conflict.Grade != ConflictGrade.Green &&
           string.IsNullOrWhiteSpace(report.Error) &&
           report.Verdict is AgentVerdictKind.RealConflict or AgentVerdictKind.ConditionDependent &&
           HasCodeEvidence(report);

    /// <summary>可以直接并入合并补丁包的冲突。</summary>
    public static bool CanBundle(
        PlayerConflict conflict,
        AgentReport report,
        IReadOnlySet<string>? approvedAuthorDeclared = null)
        => IsReviewable(conflict, report, approvedAuthorDeclared) && HasPatchPlan(report);

    public static string ExplainNotEligible(
        PlayerConflict conflict,
        AgentReport report,
        IReadOnlySet<string>? approvedAuthorDeclared = null)
    {
        if (!IsApproved(conflict, approvedAuthorDeclared))
            return "作者在 About.xml 中明确声明互斥，且未选择同时保留，不生成绕过补丁";
        if (conflict.Grade == ConflictGrade.Green)
            return "绿色兼容项不需要补丁";
        if (!string.IsNullOrWhiteSpace(report.Error))
            return "Agent 核查未能完成：" + report.Error;
        if (report.Verdict is AgentVerdictKind.FalsePositive or AgentVerdictKind.InsufficientEvidence)
            return "Agent 结论不是可修复的冲突";
        if (!HasCodeEvidence(report))
            return "没有取到代码或 XML 原文证据";
        if (!HasPatchPlan(report))
            return "Agent 只给出了顺序或人工处理方案";
        return "不符合合并补丁的生成条件";
    }
}
