namespace ModSorterAgent.Sorting;

/// <summary>
/// 把「推荐顺位」安全地合并进「当前启用列表」。
///
/// 存在的意义：排序对象可以是启用列表的子集（工坊订阅 / 本地模组 / 全部已安装），
/// 而写回的目标永远是完整的启用列表。直接拿推荐结果整体替换启用列表，
/// 会把范围外的已启用模组全部关掉——这是不可接受的破坏。
///
/// 三条不变量：
/// 1. 范围外（不在推荐结果里）的已启用项原地不动，既不移动也不关闭；
/// 2. 范围内的已启用项按推荐顺位回填到它们原本占用的槽位，等价于一次置换；
/// 3. 需要新启用的项按推荐顺位插入，且永不插到官方内容之前。
/// </summary>
public static class ActiveModListMerger
{
    public sealed record MergeResult(
        List<string> FinalOrder,
        List<string> NewlyEnabled,
        int PreservedOutsideScope);

    public static MergeResult Merge(
        IReadOnlyList<string> activityOrder,
        IReadOnlyList<string> recommendedIds,
        bool enableNewMods,
        Func<string, bool> isOfficial)
    {
        var oldSet = new HashSet<string>(activityOrder, StringComparer.OrdinalIgnoreCase);
        var recommendedSet = new HashSet<string>(recommendedIds, StringComparer.OrdinalIgnoreCase);

        var reorderQueue = new Queue<string>(recommendedIds.Where(id => oldSet.Contains(id)));
        var reorderSet = new HashSet<string>(reorderQueue, StringComparer.OrdinalIgnoreCase);

        var result = new List<string>(activityOrder.Count + recommendedIds.Count);
        foreach (var id in activityOrder)
            result.Add(reorderSet.Contains(id) ? reorderQueue.Dequeue() : id);

        var preservedOutsideScope = result.Count(id => !recommendedSet.Contains(id));

        var newlyEnabled = new List<string>();
        if (!enableNewMods)
            return new MergeResult(result, newlyEnabled, preservedOutsideScope);

        var lastOfficialIndex = -1;
        for (var i = 0; i < result.Count; i++)
        {
            if (isOfficial(result[i]))
                lastOfficialIndex = i;
        }

        var placedAt = BuildIndexMap(result);
        for (var i = 0; i < recommendedIds.Count; i++)
        {
            var id = recommendedIds[i];
            if (string.IsNullOrWhiteSpace(id) || oldSet.Contains(id) || placedAt.ContainsKey(id))
                continue;

            // 紧跟在推荐顺位里最后一个已就位的前置之后，同时不早于官方内容之后
            var insertAt = lastOfficialIndex + 1;
            for (var j = i - 1; j >= 0; j--)
            {
                if (placedAt.TryGetValue(recommendedIds[j], out var pos))
                {
                    insertAt = Math.Max(insertAt, pos + 1);
                    break;
                }
            }

            insertAt = Math.Min(Math.Max(insertAt, 0), result.Count);
            result.Insert(insertAt, id);
            newlyEnabled.Add(id);
            placedAt = BuildIndexMap(result);
        }

        return new MergeResult(result, newlyEnabled, preservedOutsideScope);
    }

    private static Dictionary<string, int> BuildIndexMap(List<string> list)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < list.Count; i++)
            map[list[i]] = i;
        return map;
    }
}
