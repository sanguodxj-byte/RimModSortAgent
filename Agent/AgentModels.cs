using ModSorterAgent.Models;

namespace ModSorterAgent.Agent;

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum AgentVerdictKind
{
    RealConflict,
    ConditionDependent,
    FalsePositive,
    InsufficientEvidence
}

public sealed record AgentHiddenRisk(string Title, string Detail, string Severity);

public sealed record AgentPatchFile(string RelativePath, string Content);

public sealed record AgentPatchPlan(
    string Kind,
    string Summary,
    string? RiskNote,
    string? HarmonyCode,
    IReadOnlyList<AgentPatchFile> Files);

/// <summary>一次 Agent 核查的完整产物。Mode 为 local 或模型名，便于玩家判断结论来源。</summary>
public sealed record AgentReport(
    string ConflictKey,
    DateTime GeneratedAtUtc,
    string Mode,
    AgentVerdictKind Verdict,
    double Confidence,
    string Headline,
    string Explanation,
    IReadOnlyList<AgentHiddenRisk> HiddenRisks,
    string FixStrategy,
    AgentPatchPlan? PatchPlan,
    IReadOnlyList<string> CodeSources,
    string EvidenceSummary,
    string? RawResponse,
    string? Error)
{
    public string VerdictLabel => Verdict switch
    {
        AgentVerdictKind.RealConflict => "确认冲突",
        AgentVerdictKind.ConditionDependent => "条件性冲突",
        AgentVerdictKind.FalsePositive => "静态误报",
        _ => "证据不足"
    };

    public bool CanBuildPatch => PatchPlan is { Kind: "harmony" or "xml" or "order_only" };
}

public sealed record AgentCodeSample(string Source, string Member, string Code);

public sealed record AgentCaseFile(
    string ConflictKey,
    string Category,
    string Target,
    string ModAName,
    string ModAPackageId,
    string ModBName,
    string ModBPackageId,
    string PromptText,
    IReadOnlyList<AgentCodeSample> Samples,
    string Summary);

/// <summary>补丁生成结果。IsModPackage=false 表示只产出了人工方案说明，不可安装。</summary>
public sealed record CompatPatchResult(
    bool Success,
    bool IsModPackage,
    string OutputDirectory,
    string? PackageId,
    string? AssemblyPath,
    string? CompilerOutput,
    string Message,
    IReadOnlyList<string> Files);
