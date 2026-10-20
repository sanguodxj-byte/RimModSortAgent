namespace ModSorterAgent.Models;

/// <summary>
/// 一份已保存的"排序列表"：某次分析得到的推荐加载顺序快照。
/// 可在启动游戏前一键切换激活，切换（写盘）前会对 ModsConfig.xml 自动备份。
/// </summary>
public sealed record SortListProfile(
    string Name,
    DateTime CreatedAtUtc,
    string GameVersion,
    IReadOnlyList<string> PackageIds,
    IReadOnlyList<string> KnownExpansions,
    int TotalModsAnalyzed,
    int RedConflictCount,
    int YellowConflictCount)
{
    public DateTime CreatedAtLocal => CreatedAtUtc.ToLocalTime();

    public string DisplayName =>
        $"{Name} · {PackageIds.Count} 项 · 红 {RedConflictCount} · {CreatedAtLocal:MM-dd HH:mm}";
}
