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
    IReadOnlyList<string> Files)
{
    /// <summary>本次合并包实际收录的冲突 Key。</summary>
    public IReadOnlyList<string> IncludedConflictKeys { get; init; } = Array.Empty<string>();

    /// <summary>未并入的冲突及原因，供界面与 README 追溯。</summary>
    public IReadOnlyList<string> SkippedReasons { get; init; } = Array.Empty<string>();

    /// <summary>Harmony 编译失败项及编译器原文，供 Agent 带错误回修。</summary>
    public IReadOnlyList<CompatPatchFailure> Failures { get; init; } = Array.Empty<CompatPatchFailure>();
}

public sealed record CompatPatchFailure(string ConflictKey, string PairLabel, string CompilerOutput);
