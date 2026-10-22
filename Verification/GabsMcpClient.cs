using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace ModSorterAgent.Verification;

/// <summary>
/// GABS 的 MCP stdio 客户端。只负责协议层：启动 gabs.exe、完成 initialize 握手、
/// 收发 tools/call，并把结构化结果交回调用方。
/// 所有工具名都要经过 <see cref="LiveToolPolicy"/> 放行，避免越权调用。
/// </summary>
public sealed class GabsMcpClient : IDisposable
{
    private readonly Process _process;
    private readonly StreamWriter _stdin;
    private readonly StreamReader _stdout;
    private readonly StringBuilder _stderrBuffer = new();
    private int _nextId;
    private bool _disposed;

    private GabsMcpClient(Process process)
    {
        _process = process;
        _stdin = process.StandardInput;
        _stdout = process.StandardOutput;

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null)
                return;
            lock (_stderrBuffer)
            {
                // 只保留尾部，避免长跑时无界增长
                if (_stderrBuffer.Length > 32_000)
                    _stderrBuffer.Remove(0, 16_000);
                _stderrBuffer.AppendLine(e.Data);
            }
        };
        process.BeginErrorReadLine();
    }

    /// <summary>
    /// 启动 gabs.exe 的 stdio MCP 服务并完成握手。
    /// </summary>
    public static async Task<GabsMcpClient> StartAsync(
        string gabsExePath,
        string? configDir,
        CancellationToken cancellationToken)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo(gabsExePath, "server")
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardInputEncoding = new UTF8Encoding(false),
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        if (!string.IsNullOrWhiteSpace(configDir))
            process.StartInfo.Environment["GABS_CONFIG_DIR"] = configDir;

        process.StartInfo.Environment["GABS_LOG_LEVEL"] = "warn";

        if (!process.Start())
            throw new InvalidOperationException("无法启动 gabs.exe 进程。");

        var client = new GabsMcpClient(process);
        try
        {
            await client.InitializeAsync(cancellationToken).ConfigureAwait(false);
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await SendRequestAsync(
            "initialize",
            new
            {
                protocolVersion = "2024-11-05",
                capabilities = new { },
                clientInfo = new { name = "ModSorterAgent", version = "1.0.0" }
            },
            cancellationToken).ConfigureAwait(false);

        await SendNotificationAsync("notifications/initialized", new { }).ConfigureAwait(false);
    }

    /// <summary>
    /// 调用一个 GABS 顶层工具。工具名必须通过 <see cref="LiveToolPolicy"/> 检查。
    /// 返回结构化内容优先，没有时退回文本内容。
    /// </summary>
    public async Task<JsonElement?> CallToolAsync(
        string toolName,
        object arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (!LiveToolPolicy.IsAllowed(toolName))
            throw new InvalidOperationException($"GABS 工具调用被拒绝：{toolName} 不在实机验证白名单内。");

        var result = await SendRequestAsync("tools/call", new { name = toolName, arguments }, cancellationToken)
            .ConfigureAwait(false);

        if (result == null)
            return null;

        return ExtractToolPayload(result.Value);
    }

    /// <summary>
    /// 通过 games_call_tool 调用桥接侧的游戏内工具。
    /// argumentsJson 是该工具的参数对象原文，直接透传，宿主不改写模型给的参数。
    /// </summary>
    public async Task<JsonElement?> CallGameToolAsync(
        string gameId,
        string gameToolName,
        string argumentsJson,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (!LiveToolPolicy.IsAllowed(gameToolName))
            throw new InvalidOperationException($"工具调用被拒绝：{gameToolName} 会破坏玩家资产，禁止由自动流程执行。");

        object arguments;
        if (string.IsNullOrWhiteSpace(argumentsJson))
        {
            arguments = new { };
        }
        else
        {
            try
            {
                using var doc = JsonDocument.Parse(argumentsJson);
                arguments = doc.RootElement.Clone();
            }
            catch (JsonException)
            {
                throw new InvalidOperationException("工具参数不是合法 JSON，已拒绝调用：" + argumentsJson);
            }
        }

        var payload = await CallToolAsync(
            "games_call_tool",
            new
            {
                gameId,
                tool = gameToolName,
                arguments,
                timeout = (int)Math.Ceiling(timeout.TotalSeconds)
            },
            timeout,
            cancellationToken).ConfigureAwait(false);

        return payload;
    }

    /// <summary>
    /// 从 MCP tools/call 的 result 里取出真正的业务负载：
    /// 优先 structuredContent，其次拼接 content[].text，最后原样返回。
    /// 这条路径保证不再出现"把整个 JSON 当一行文本"的老问题。
    /// </summary>
    private static JsonElement? ExtractToolPayload(JsonElement result)
    {
        if (result.ValueKind != JsonValueKind.Object)
            return result;

        if (result.TryGetProperty("structuredContent", out var structured) &&
            structured.ValueKind == JsonValueKind.Object)
        {
            // 桥接工具常在 structuredContent 外面再包一层 { result: ... }
            if (structured.TryGetProperty("result", out var inner))
                return inner;
            return structured;
        }

        if (result.TryGetProperty("content", out var content) &&
            content.ValueKind == JsonValueKind.Array)
        {
            var texts = new List<string>();
            foreach (var item in content.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object &&
                    item.TryGetProperty("text", out var text) &&
                    text.ValueKind == JsonValueKind.String)
                {
                    texts.Add(text.GetString() ?? string.Empty);
                }
            }

            var combined = string.Join("\n", texts);
            if (!string.IsNullOrWhiteSpace(combined))
            {
                // 文本内容是 JSON 时解析成结构，否则当成纯文本返回
                var trimmed = combined.Trim();
                if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
                {
                    try
                    {
                        using var parsed = JsonDocument.Parse(trimmed);
                        return parsed.RootElement.Clone();
                    }
                    catch (JsonException)
                    {
                        // 落到下面的字符串包装
                    }
                }

                using var doc = JsonDocument.Parse(JsonSerializer.Serialize(combined));
                return doc.RootElement.Clone();
            }
        }

        return result;
    }

    private async Task<JsonElement?> SendRequestAsync(
        string method,
        object @params,
        CancellationToken cancellationToken)
    {
        var id = Interlocked.Increment(ref _nextId);
        var payload = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id,
            method,
            @params
        });

        await _stdin.WriteLineAsync(payload.AsMemory(), cancellationToken).ConfigureAwait(false);
        await _stdin.FlushAsync(cancellationToken).ConfigureAwait(false);

        // 逐行读到 id 匹配的响应为止，跳过通知与其他请求的响应
        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await ReadLineWithCancellationAsync(cancellationToken).ConfigureAwait(false);
            if (line == null)
                return null;

            if (string.IsNullOrWhiteSpace(line))
                continue;

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(line);
            }
            catch (JsonException)
            {
                continue;
            }

            using (doc)
            {
                var root = doc.RootElement;
                if (!root.TryGetProperty("id", out var idElement) ||
                    idElement.ValueKind != JsonValueKind.Number ||
                    idElement.GetInt32() != id)
                {
                    continue;
                }

                if (root.TryGetProperty("error", out var error))
                    throw new InvalidOperationException($"GABS 返回错误：{error}");

                if (root.TryGetProperty("result", out var result))
                    return result.Clone();

                return null;
            }
        }

        return null;
    }

    private async Task SendNotificationAsync(string method, object @params)
    {
        var payload = JsonSerializer.Serialize(new { jsonrpc = "2.0", method, @params });
        await _stdin.WriteLineAsync(payload).ConfigureAwait(false);
        await _stdin.FlushAsync().ConfigureAwait(false);
    }

    private async Task<string?> ReadLineWithCancellationAsync(CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var readTask = _stdout.ReadLineAsync(linked.Token).AsTask();

        try
        {
            return await readTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    /// <summary>GABS 自身 stderr 的尾部，诊断启动失败时用。</summary>
    public string GetStderrTail()
    {
        lock (_stderrBuffer)
        {
            var text = _stderrBuffer.ToString().Trim();
            return text.Length <= 2000 ? text : text[^2000..];
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        try { _stdin.Close(); } catch { /* 关闭 stdio 即结束短命适配进程 */ }
        try
        {
            if (!_process.WaitForExit(2000))
                _process.Kill(entireProcessTree: true);
        }
        catch { /* 进程可能已退出 */ }

        try { _process.Dispose(); } catch { /* 忽略 */ }
    }
}
