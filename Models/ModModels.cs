using System.Text.Json.Serialization;
using DecompilerServer.Services;

namespace ModSorterAgent.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ConflictGrade
{
    Green = 1,
    Yellow = 2,
    Red = 3
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PipelineStage
{
    Idle = 0,
    ResolvingCatalog = 1,
    AnalyzingXml = 2,
    AnalyzingCode = 3,
    GradingConflicts = 4,
    TopologicalSort = 5,
    LiveVerification = 6,
    BuildingReport = 7,
    Completed = 8,
    Cancelled = 9,
    Faulted = 10
}

public sealed record PipelineProgressReport(
    PipelineStage Stage,
    int StageIndex,
    int TotalStages,
    int CurrentItem,
    int TotalItems,
    int Percentage,
    string StatusText,
    long ElapsedMs);

public sealed record ModDependencyItem(
    string PackageId,
    string? DisplayName);

public sealed record XmlDefRecord(
    string DefType,
    string? DefName,
    string? NameAttribute,
    string? ParentName,
    bool IsAbstract,
    string? ThingClass,
    string? DriverClass,
    string? WorkerClass,
    IReadOnlyList<string> CompClasses,
    string ContentSignature,
    string SourceFile);

public sealed record XmlPatchRecord(
    string OperationClass,
    string? XPath,
    bool IsDestructive,
    bool IsConditionalOrModGated,
    IReadOnlyList<string> ReferencedModNamesOrIds,
    string SourceFile);

public sealed record ModDescriptor
{
    public required string PackageId { get; init; }
    public required string Name { get; init; }
    public required string RootDirectory { get; init; }
    public string? Author { get; init; }
    public int OriginalIndex { get; init; }
    public bool IsOfficialCoreOrDlc { get; init; }
    public bool IsWorkshopMod { get; init; }

    /// <summary>是否出现在 ModsConfig.xml 的 activeMods 中。false 表示来自工坊订阅池。</summary>
    public bool IsActiveInModsConfig { get; init; } = true;

    /// <summary>Steam 创意工坊发布 ID（仅订阅池模组有值）。</summary>
    public string? WorkshopPublishedFileId { get; init; }

    public IReadOnlyList<string> SupportedVersions { get; init; } = Array.Empty<string>();
    public IReadOnlyList<ModDependencyItem> Dependencies { get; init; } = Array.Empty<ModDependencyItem>();
    public IReadOnlyList<string> LoadAfter { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> LoadBefore { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ForceLoadAfter { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ForceLoadBefore { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> IncompatibleWith { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ConditionalLoadFolderMods { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ActiveContentDirectories { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> AssemblyPaths { get; init; } = Array.Empty<string>();
    public IReadOnlyList<XmlDefRecord> XmlDefs { get; init; } = Array.Empty<XmlDefRecord>();
    public IReadOnlyList<XmlPatchRecord> XmlPatches { get; init; } = Array.Empty<XmlPatchRecord>();
    public IReadOnlyList<AssemblyHarmonyAnalysisResult> AssemblyAnalyses { get; init; } = Array.Empty<AssemblyHarmonyAnalysisResult>();
    public IReadOnlyList<string> ReferencedExternalAssemblies { get; init; } = Array.Empty<string>();
}

public sealed record OrderingEdge(
    string BeforePackageId,
    string AfterPackageId,
    bool IsHardConstraint,
    int Weight,
    string SourceKind,
    string Reason);

public sealed record DependencyLinkSummary(
    string RelatedPackageId,
    string RelatedModName,
    bool IsHardConstraint,
    int Weight,
    string SourceKind,
    string Reason);

public sealed record PairwiseConflictFinding(
    string ModAPackageId,
    string ModAName,
    string ModBPackageId,
    string ModBName,
    ConflictGrade Grade,
    string Category,
    string TargetSignature,
    string Summary,
    IReadOnlyList<string> Evidence,
    string? RecommendedBeforePackageId,
    string? RecommendedAfterPackageId,
    string? RecommendationReason);

public sealed record ModGradeSummary(
    string PackageId,
    string Name,
    string ArchitectureTierLabel,
    int RecommendedOrderIndex,
    int OriginalOrderIndex,
    ConflictGrade Grade,
    string GradeLabel,
    int RedConflictCount,
    int YellowUncertaintyCount,
    int HarmonyPatchCount,
    int DynamicPatchSiteCount,
    int XmlDefCount,
    int XmlPatchCount,
    IReadOnlyList<string> KeyReasons)
{
    public string? Author { get; init; }
    public string? RootDirectory { get; init; }
    public bool IsOfficialCoreOrDlc { get; init; }
    public bool IsWorkshopMod { get; init; }

    /// <summary>是否属于当前 ModsConfig.xml 的启用列表（false = 工坊订阅池中的未启用项）。</summary>
    public bool IsActive { get; init; } = true;

    /// <summary>Steam 创意工坊发布 ID（可空）。</summary>
    public string? WorkshopId { get; init; }

    public int TierRank { get; init; }
    public IReadOnlyList<string> SupportedVersions { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> AssemblyNames { get; init; } = Array.Empty<string>();
    public IReadOnlyList<DependencyLinkSummary> UpstreamDependencies { get; init; } = Array.Empty<DependencyLinkSummary>();
    public IReadOnlyList<DependencyLinkSummary> DownstreamDependents { get; init; } = Array.Empty<DependencyLinkSummary>();

    /// <summary>
    /// Positive means moved earlier in load order (e.g., Original #10 -> Recommended #4 => +6).
    /// Negative means moved later in load order (e.g., Original #3 -> Recommended #8 => -5).
    /// Zero means position unchanged.
    /// </summary>
    [JsonIgnore]
    public int OrderDelta => OriginalOrderIndex - RecommendedOrderIndex;
}

public sealed record GabsLiveVerificationSummary(    bool Connected,
    string GameStatus,
    IReadOnlyList<string> ConfigurationWarnings,
    IReadOnlyList<string> RelevantLogErrors,
    IReadOnlyList<string> RelevantLogWarnings,
    IReadOnlyList<string> EscalatedModPairs)
{
    /// <summary>
    /// RimWorld 进程当前是否正在运行。游戏退出时会用内存中的模组列表回写 ModsConfig.xml，
    /// 因此运行期写入的排序会被覆盖，界面需在写盘前给出提示。
    /// </summary>
    public bool GameRunning { get; init; }
}

public sealed record ModSortReport(
    DateTime GeneratedAtUtc,
    string TargetGameVersion,
    ConflictGrade OverallGrade,
    string OverallGradeLabel,
    int TotalModsAnalyzed,
    int GreenModCount,
    int YellowModCount,
    int RedModCount,
    IReadOnlyList<string> MissingDependencies,
    IReadOnlyList<string> HardCycleDescriptions,
    IReadOnlyList<ModGradeSummary> SortedMods,
    IReadOnlyList<PairwiseConflictFinding> ConflictFindings,
    IReadOnlyList<OrderingEdge> AppliedOrderingEdges,
    GabsLiveVerificationSummary? GabsVerification)
{
    public int MovedModCount { get; init; }
    public int TotalHarmonyPatchCount { get; init; }
    public int TotalDynamicPatchSiteCount { get; init; }
    public int TotalXmlDefCount { get; init; }
    public int TotalXmlPatchCount { get; init; }
    public long AnalysisDurationMs { get; init; }
    public IReadOnlyList<string> UnresolvedTokens { get; init; } = Array.Empty<string>();

    // ── 工坊订阅检测 ──
    /// <summary>本次分析是否启用了「含工坊订阅」范围。</summary>
    public bool WorkshopScopeEnabled { get; init; }

    /// <summary>Steam 订阅清单中的条目总数（含未下载完成的）。</summary>
    public int WorkshopSubscribedCount { get; init; }

    /// <summary>订阅池中实际纳入排序的模组数（已安装且带 About.xml）。</summary>
    public int WorkshopPoolModCount { get; init; }

    /// <summary>订阅但本地未安装/未下载完成的条目数。</summary>
    public int WorkshopMissingOnDiskCount { get; init; }

    /// <summary>订阅扫描探测到的订阅清单路径。</summary>
    public string? WorkshopManifestPath { get; init; }

    /// <summary>订阅扫描过程中的告警（清单缺失、条目未下载等）。</summary>
    public IReadOnlyList<string> WorkshopWarnings { get; init; } = Array.Empty<string>();

    /// <summary>
    /// 排序结果中相邻两模组违反"架构分层"启发式的次数。用于诊断分层质量，不影响正确性。
    /// </summary>
    public int TierInversionCount { get; init; }
}
