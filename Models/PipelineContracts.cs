namespace ModSorterAgent.Models;

/// <summary>
/// 一次完整分析的输入参数。把散落在命令行与界面层里的参数收敛为单一不可变对象，
/// 便于替换数据源、做单元测试与在后台线程安全传递。
/// </summary>
public sealed record PipelineRequest(
    IReadOnlyList<string> ModTokens,
    string TargetGameVersion,
    IReadOnlyList<string> KnownExpansions,
    bool VerifyWithGabs = true,
    IReadOnlyList<string>? CustomSearchRoots = null,
    string? RimWorldInstallDir = null,
    string? ModsConfigPath = null,
    bool IncludeWorkshopSubscriptions = false,
    string? WorkshopContentDirectory = null)
{
    public static PipelineRequest Create(
        IReadOnlyList<string> modTokens,
        string targetGameVersion,
        IReadOnlyList<string>? knownExpansions = null,
        bool verifyWithGabs = true,
        bool includeWorkshopSubscriptions = false)
    {
        return new PipelineRequest(
            ModTokens: modTokens,
            TargetGameVersion: targetGameVersion,
            KnownExpansions: knownExpansions ?? Array.Empty<string>(),
            VerifyWithGabs: verifyWithGabs,
            IncludeWorkshopSubscriptions: includeWorkshopSubscriptions);
    }
}

/// <summary>
/// 分析产物。Markdown 报告与排序结果一次算好，界面层只负责呈现与导出。
/// </summary>
public sealed record PipelineResult(
    ModSortReport Report,
    string MarkdownReport,
    IReadOnlyList<string> KnownExpansions,
    string EffectiveGameVersion,
    IReadOnlyList<ModDescriptor> Mods);

/// <summary>
/// 分析管线契约。界面层只依赖它，不关心内部如何扫描、反编译与排序。
/// </summary>
public interface IModSortPipeline
{
    PipelineResult Run(
        PipelineRequest request,
        IProgress<PipelineProgressReport>? progress = null,
        Action<string>? onStatusUpdate = null,
        CancellationToken cancellationToken = default);
}
