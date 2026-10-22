using System.Text;
using System.Text.Json;
using ModSorterAgent.Verification;

namespace ModSorterAgent.Agent;

/// <summary>
/// 宿主侧 GABS 工具：模型通过 MCP 直接操作运行中的 RimWorld，自己决定
/// 启动、读日志、推进时间、查模组配置，并据此判断冲突是否在实机复现。
///
/// 与反编译工具同样的定位：宿主只提供能力与边界，判断由模型自己做。
/// 这里不做任何"包名匹配日志文本"之类的推断。
/// </summary>
public sealed class AgentGabsTools : IDisposable
{
    private const string GameId = "rimworld";
    private const int MaxOutputChars = 120_000;

    private readonly string _gabsExePath;
    private readonly Action<string>? _trace;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private GabsMcpClient? _client;
    private BridgeModActivator? _activator;
    private bool _disposed;

    public AgentGabsTools(string gabsExePath, Action<string>? trace = null)
    {
        _gabsExePath = gabsExePath;
        _trace = trace;
    }

    /// <summary>本轮对话里是否真正动用过实机工具，用于要求模型给出实机依据。</summary>
    public int EvidenceCalls { get; private set; }

    public static bool IsAvailable => File.Exists(ResolveGabsExePath());

    public static string ResolveGabsExePath()
        => GabsLiveVerifier.DefaultGabsExePath;

    public IReadOnlyList<AgentToolDefinition> Definitions { get; } = new[]
    {
        new AgentToolDefinition(
            "rimworld_start",
            "启动 RimWorld 并等待 GABP 桥接就绪。会自动临时启用 RimBridgeServer，并确保它在验证结束后被移出加载列表。游戏已在运行时直接复用并尝试连接。",
            """
            {
              "type": "object",
              "properties": {
                "timeout_seconds": { "type": "integer", "description": "等待桥接就绪的总秒数，默认 180" }
              }
            }
            """),
        new AgentToolDefinition(
            "rimworld_status",
            "读取 RimWorld 与桥接的当前状态：是否运行、是否已连上桥接、已发现多少游戏内工具、当前程序状态与 tick。",
            """
            {
              "type": "object",
              "properties": {}
            }
            """),
        new AgentToolDefinition(
            "rimworld_call",
            "调用一个 RimBridgeServer 游戏内工具并返回结构化结果。常见工具：rimbridge/list_logs、rimbridge/get_bridge_status、rimworld/get_mod_configuration_status、rimworld/list_mods、rimworld/list_alerts、rimworld/get_game_info、rimworld/start_debug_game_ready、rimworld/step_game_ticks、rimworld/set_time_speed、rimworld/play_until_letter。",
            """
            {
              "type": "object",
              "properties": {
                "tool": { "type": "string", "description": "游戏内工具名，例如 rimbridge/list_logs" },
                "arguments": { "type": "object", "description": "该工具的参数对象，可为空" },
                "timeout_seconds": { "type": "integer", "description": "调用超时秒数，默认 60" }
              },
              "required": ["tool"]
            }
            """),
        new AgentToolDefinition(
            "rimworld_tools",
            "列出当前桥接可用的游戏内工具名，可用于发现除了常用工具之外还能做什么。",
            """
            {
              "type": "object",
              "properties": {
                "query": { "type": "string", "description": "按名称过滤的关键字，可为空" },
                "limit": { "type": "integer", "description": "返回条数上限，默认 80" }
              }
            }
            """),
        new AgentToolDefinition(
            "rimworld_stop",
            "关闭由本工具启动的 RimWorld 进程，并把 RimBridgeServer 从加载列表中移除。验证结束前应调用它，保证游戏不会因为验证而长期占用。",
            """
            {
              "type": "object",
              "properties": {}
            }
            """)
    };

    public bool Handles(string name)
        => name.StartsWith("rimworld_", StringComparison.OrdinalIgnoreCase);

    public async Task<string> ExecuteAsync(AgentToolCall call, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(
                    string.IsNullOrWhiteSpace(call.ArgumentsJson) ? "{}" : call.ArgumentsJson);
            }
            catch (JsonException ex)
            {
                return "错误：工具参数不是合法 JSON。" + ex.Message;
            }

            using (document)
            {
                var root = document.RootElement;
                var result = call.Name switch
                {
                    "rimworld_start" => await StartAsync(root, cancellationToken).ConfigureAwait(false),
                    "rimworld_status" => await StatusAsync(cancellationToken).ConfigureAwait(false),
                    "rimworld_call" => await CallToolAsync(root, cancellationToken).ConfigureAwait(false),
                    "rimworld_tools" => await ListToolsAsync(root, cancellationToken).ConfigureAwait(false),
                    "rimworld_stop" => await StopAsync(cancellationToken).ConfigureAwait(false),
                    _ => $"错误：未知工具 {call.Name}。"
                };

                return Clamp(result);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return "错误：实机操作失败。" + ex.Message;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<string> StartAsync(JsonElement root, CancellationToken cancellationToken)
    {
        if (!File.Exists(_gabsExePath))
            return $"错误：未找到 gabs.exe（{_gabsExePath}）。实机操作需要 GABS。";

        // 只确认模组在盘上；能否加载不由版本号判断。
        if (BridgeModLocator.FindBridgeModDirectory() == null)
            return $"错误：未找到 {BridgeModLocator.BridgeModName} 模组，实机桥接无法建立。";

        var timeout = Math.Clamp(GetInt(root, "timeout_seconds") ?? 180, 30, 600);

        if (_client == null)
        {
            // 桥接模组只在验证期间启用，Dispose/Stop 时还原。
            _activator ??= BridgeModActivator.Activate();
            _client = await GabsMcpClient.StartAsync(_gabsExePath, null, cancellationToken)
                .ConfigureAwait(false);
        }

        var status = await _client
            .CallToolAsync("games_status", new { gameId = GameId }, TimeSpan.FromSeconds(20), cancellationToken)
            .ConfigureAwait(false);

        var running = ReadStatus(status);
        if (!IsRunning(running))
        {
            _trace?.Invoke("gabs: games_start");
            await _client.CallToolAsync(
                "games_start",
                new { gameId = GameId, timeout = Math.Min(timeout, 240) },
                TimeSpan.FromSeconds(timeout + 60),
                cancellationToken).ConfigureAwait(false);
        }

        var deadline = DateTime.UtcNow.AddSeconds(timeout);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var current = await _client
                .CallToolAsync("games_status", new { gameId = GameId }, TimeSpan.FromSeconds(20), cancellationToken)
                .ConfigureAwait(false);

            if (IsRunning(ReadStatus(current)))
            {
                var connect = await _client
                    .CallToolAsync("games_connect", new { gameId = GameId, timeout = 30 },
                        TimeSpan.FromSeconds(50), cancellationToken)
                    .ConfigureAwait(false);

                if (IsConnected(connect))
                {
                    // 连上不等于取得证据：只有 rimworld_call 的实际返回才算。
                    var tools = await _client
                        .CallToolAsync("games_tool_names", new { gameId = GameId, limit = 5 },
                            TimeSpan.FromSeconds(20), cancellationToken)
                        .ConfigureAwait(false);

                    return "已连接实机桥接。" + Environment.NewLine + DescribeStatus(current, tools);
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
        }

        return "错误：等待桥接就绪超时。" + Environment.NewLine + DescribeStatus(status, null);
    }

    private async Task<string> StatusAsync(CancellationToken cancellationToken)
    {
        if (_client == null)
            return "尚未启动实机会话。请先调用 rimworld_start。";

        var status = await _client
            .CallToolAsync("games_status", new { gameId = GameId }, TimeSpan.FromSeconds(20), cancellationToken)
            .ConfigureAwait(false);

        return DescribeStatus(status, null);
    }

    private async Task<string> CallToolAsync(JsonElement root, CancellationToken cancellationToken)
    {
        if (_client == null)
            return "错误：尚未启动实机会话。请先调用 rimworld_start。";

        var tool = GetString(root, "tool");
        if (string.IsNullOrWhiteSpace(tool))
            return "错误：缺少 tool。";

        var timeout = Math.Clamp(GetInt(root, "timeout_seconds") ?? 60, 5, 600);
        var argumentsJson = root.ValueKind == JsonValueKind.Object &&
                            root.TryGetProperty("arguments", out var args) &&
                            args.ValueKind == JsonValueKind.Object
            ? args.GetRawText()
            : "{}";

        _trace?.Invoke($"gabs: {tool} {Shorten(argumentsJson, 200)}");

        var payload = await _client
            .CallGameToolAsync(GameId, tool, argumentsJson, TimeSpan.FromSeconds(timeout), cancellationToken)
            .ConfigureAwait(false);

        EvidenceCalls++;
        return payload == null
            ? $"工具 {tool} 没有返回内容。"
            : $"工具 {tool} 结果：" + Environment.NewLine + payload.Value.ToString();
    }

    private async Task<string> ListToolsAsync(JsonElement root, CancellationToken cancellationToken)
    {
        if (_client == null)
            return "错误：尚未启动实机会话。请先调用 rimworld_start。";

        var query = GetString(root, "query");
        var limit = Math.Clamp(GetInt(root, "limit") ?? 80, 1, 400);

        var request = new Dictionary<string, object>
        {
            ["gameId"] = GameId,
            ["limit"] = limit
        };
        if (!string.IsNullOrWhiteSpace(query))
            request["query"] = query;

        var payload = await _client
            .CallToolAsync("games_tool_names", request, TimeSpan.FromSeconds(30), cancellationToken)
            .ConfigureAwait(false);

        return payload == null ? "没有可用的游戏内工具。" : payload.Value.ToString();
    }

    private async Task<string> StopAsync(CancellationToken cancellationToken)
    {
        var notes = new List<string>();

        if (_client != null)
        {
            try
            {
                await _client.CallToolAsync(
                    "games_stop",
                    new { gameId = GameId },
                    TimeSpan.FromSeconds(90),
                    cancellationToken).ConfigureAwait(false);
                notes.Add("已关闭 RimWorld。");
            }
            catch (InvalidOperationException ex)
            {
                notes.Add("关闭 RimWorld 失败：" + ex.Message);
            }

            _client.Dispose();
            _client = null;
        }

        // 无论关闭是否成功，桥接模组都必须退出加载列表。
        if (_activator != null)
        {
            _activator.Dispose();
            _activator = null;
            notes.Add("RimBridgeServer 已从加载列表移除。");
        }

        return notes.Count == 0 ? "没有进行中的实机会话。" : string.Join(Environment.NewLine, notes);
    }

    private void ReleaseSession()
    {
        _client?.Dispose();
        _client = null;
        _activator?.Dispose();
        _activator = null;
    }

    private string DescribeStatus(JsonElement? status, JsonElement? tools)
    {
        var builder = new StringBuilder();
        builder.AppendLine("实机状态：");

        if (status != null && status.Value.ValueKind == JsonValueKind.Object)
        {
            AppendIfPresent(builder, status.Value, "status", "运行状态");
            AppendIfPresent(builder, status.Value, "statusDescription", "状态说明");
            AppendIfPresent(builder, status.Value, "phase", "阶段");
            AppendIfPresent(builder, status.Value, "toolCount", "已发现工具数");
            AppendIfPresent(builder, status.Value, "activeProfile", "启动配置");
        }
        else
        {
            builder.AppendLine("  （没有状态数据）");
        }

        if (tools != null && tools.Value.ValueKind == JsonValueKind.Object &&
            tools.Value.TryGetProperty("total", out var total))
        {
            builder.AppendLine($"  桥接工具总数：{total}");
        }

        return builder.ToString().TrimEnd();
    }

    private static void AppendIfPresent(StringBuilder builder, JsonElement obj, string name, string label)
    {
        if (obj.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null)
            builder.AppendLine($"  {label}：{value}");
    }

    private static string? ReadStatus(JsonElement? payload)
    {
        if (payload == null || payload.Value.ValueKind != JsonValueKind.Object)
            return null;

        return payload.Value.TryGetProperty("status", out var status) &&
               status.ValueKind == JsonValueKind.String
            ? status.GetString()
            : null;
    }

    /// <summary>GABS 用 shared-running 表示已有其他会话持有进程，同样代表游戏在跑。</summary>
    private static bool IsRunning(string? status)
        => !string.IsNullOrWhiteSpace(status) &&
           (status.Equals("running", StringComparison.OrdinalIgnoreCase) ||
            status.Equals("shared-running", StringComparison.OrdinalIgnoreCase));

    private static bool IsConnected(JsonElement? payload)
        => payload != null &&
           payload.Value.ValueKind == JsonValueKind.Object &&
           payload.Value.TryGetProperty("connected", out var connected) &&
           connected.ValueKind == JsonValueKind.True;

    private static string Clamp(string text)
    {
        if (text.Length <= MaxOutputChars)
            return text;

        return text[..(MaxOutputChars / 2)] +
               "\n…（输出过长已截断）…\n" +
               text[^(MaxOutputChars / 2)..];
    }

    private static string Shorten(string? value, int max)
        => string.IsNullOrEmpty(value) || value.Length <= max ? value ?? string.Empty : value[..max] + "…";

    private static string GetString(JsonElement root, string name)
        => root.ValueKind == JsonValueKind.Object &&
           root.TryGetProperty(name, out var value) &&
           value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static int? GetInt(JsonElement root, string name)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(name, out var value))
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var number) => number,
            JsonValueKind.String when int.TryParse(value.GetString(), out var number) => number,
            _ => null
        };
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        ReleaseSession();
        _gate.Dispose();
    }
}
