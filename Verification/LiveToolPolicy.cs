namespace ModSorterAgent.Verification;

/// <summary>
/// GABS 与桥接工具的调用边界。
///
/// Agent 是实机操作的全权执行者，不限制它能用哪些能力；这里只拦会不可逆
/// 破坏玩家资产的操作。关闭游戏是允许的：验证必须能自行收尾，
/// 否则游戏会长期占用资源，桥接模组也会一直留在加载列表里。
/// </summary>
public static class LiveToolPolicy
{
    private static readonly HashSet<string> Blocked = new(StringComparer.OrdinalIgnoreCase)
    {
        // 覆盖或删除玩家存档
        "rimworld/save_game",
        "rimworld/delete_save",
        "rimworld/overwrite_save",
        // 删除模组或工作坊内容
        "rimworld/delete_mod",
        "rimworld/uninstall_mod"
    };

    public static bool IsAllowed(string toolName)
        => !string.IsNullOrWhiteSpace(toolName) && !Blocked.Contains(toolName);

    public static bool IsForbidden(string toolName)
        => Blocked.Contains(toolName);
}
