using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ModSorterAgent.Agent;

/// <summary>
/// 最小 LLM 客户端：只走 OpenAI 兼容 /chat/completions。
/// Player2 也复用这条路径——它同样是 Bearer 鉴权，但由服务端选定模型，故不下发 model 字段。
/// 只负责"发提示词、拿回文本"，结构化解析交给 <see cref="AgentInvestigator"/>。
/// </summary>
public sealed class AgentClient : IDisposable
{
    private const int MaxToolRounds = 8;
    private const string SystemPrompt = "你是严谨的 RimWorld 模组兼容性工程师。";
    private const string FinalRoundNudge =
        "工具调用预算已用完。请停止继续读取，立即基于上面对话中已经取得的证据给出最终 JSON 结论。";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private string? _resolvedKey;
    private Player2AuthService.LocalRoute? _player2Route;
    private bool _player2RouteResolved;

    public AgentConfig Config { get; }

    public AgentClient(AgentConfig config, HttpClient? httpClient = null)
    {
        Config = config;
        _ownsHttp = httpClient == null;
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(150) };
    }

    public async Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(3));

        return await CompleteOpenAiAsync(prompt, timeout.Token).ConfigureAwait(false);
    }

    /// <summary>
    /// 带宿主文件工具的对话：模型可以请求读取真实模组文件，由宿主校验路径并执行。
    /// 不支持 tools 的 OpenAI 兼容端点会抛出 <see cref="AgentToolsUnsupportedException"/>，调用方回退到单轮。
    /// </summary>
    public async Task<string> CompleteWithToolsAsync(
        string prompt,
        IReadOnlyList<AgentToolDefinition> tools,
        Func<AgentToolCall, CancellationToken, Task<string>> executeTool,
        CancellationToken cancellationToken)
    {
        if (tools.Count == 0)
            return await CompleteAsync(prompt, cancellationToken).ConfigureAwait(false);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(5));

        return await CompleteOpenAiWithToolsAsync(prompt, tools, executeTool, timeout.Token).ConfigureAwait(false);
    }

    /// <summary>
    /// 同上，但把每一轮的推理正文与工具调用实时回调出去，供 Agent 窗口展示过程。
    /// 没有推理文本的轮次不会触发 onReasoning。
    /// </summary>
    public async Task<string> CompleteWithToolsAsync(
        string prompt,
        IReadOnlyList<AgentToolDefinition> tools,
        Func<AgentToolCall, CancellationToken, Task<string>> executeTool,
        Action<string>? onReasoning,
        Action<AgentToolCall>? onToolCall,
        CancellationToken cancellationToken)
    {
        if (tools.Count == 0)
            return await CompleteAsync(prompt, cancellationToken).ConfigureAwait(false);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(5));

        return await CompleteOpenAiWithToolsAsync(prompt, tools, executeTool, onReasoning, onToolCall, timeout.Token)
            .ConfigureAwait(false);
    }

    private async Task<string> CompleteOpenAiAsync(string prompt, CancellationToken cancellationToken)
    {
        var body = BuildChatBody(new object[]
        {
            new { role = "system", content = SystemPrompt },
            new { role = "user", content = prompt }
        });

        using var response = await PostJsonAsync(body, cancellationToken)
            .ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            throw new AgentApiException(response.StatusCode, ShortBody(text));

        using var doc = JsonDocument.Parse(text);
        return ExtractOpenAiContent(doc);
    }

    private Task<string> CompleteOpenAiWithToolsAsync(
        string prompt,
        IReadOnlyList<AgentToolDefinition> tools,
        Func<AgentToolCall, CancellationToken, Task<string>> executeTool,
        CancellationToken cancellationToken)
        => CompleteOpenAiWithToolsAsync(prompt, tools, executeTool, null, null, cancellationToken);

    private async Task<string> CompleteOpenAiWithToolsAsync(
        string prompt,
        IReadOnlyList<AgentToolDefinition> tools,
        Func<AgentToolCall, CancellationToken, Task<string>> executeTool,
        Action<string>? onReasoning,
        Action<AgentToolCall>? onToolCall,
        CancellationToken cancellationToken)
    {
        var messages = new List<object>
        {
            new { role = "system", content = SystemPrompt },
            new { role = "user", content = prompt }
        };
        var toolSpecs = tools.Select(t => (object)new
        {
            type = "function",
            function = new
            {
                name = t.Name,
                description = t.Description,
                parameters = JsonNode.Parse(t.ParametersJson)
            }
        }).ToArray();

        for (var round = 0; round <= MaxToolRounds; round++)
        {
            var finalRound = round == MaxToolRounds;
            if (finalRound)
                messages.Add(new { role = "user", content = FinalRoundNudge });

            var body = finalRound
                ? BuildChatBody(messages)
                : BuildChatBody(messages, toolSpecs);

            using var response = await PostJsonAsync(body, cancellationToken)
                .ConfigureAwait(false);
            var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                if (round == 0 && IsToolsUnsupported(response.StatusCode))
                    throw new AgentToolsUnsupportedException(response.StatusCode, ShortBody(text));

                throw new AgentApiException(response.StatusCode, ShortBody(text));
            }

            using var doc = JsonDocument.Parse(text);
            if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                throw new AgentApiException(response.StatusCode, "响应中没有 choices 字段");

            var message = choices[0].GetProperty("message");
            var content = message.TryGetProperty("content", out var contentElement) &&
                          contentElement.ValueKind == JsonValueKind.String
                ? contentElement.GetString() ?? string.Empty
                : string.Empty;

            var hasToolCalls = message.TryGetProperty("tool_calls", out var toolCalls) &&
                               toolCalls.ValueKind == JsonValueKind.Array &&
                               toolCalls.GetArrayLength() > 0;

            // 最终结论是返回给程序解析的内部 JSON 报文（包含 verdict、hidden_risks、patch_plan 等），
            // 严禁作为「模型推理文本」直接在界面上暴露；只有伴随工具调用的自然语言思考才对外广播。
            if (hasToolCalls && !string.IsNullOrWhiteSpace(content) && !IsStructuredJsonPayload(content))
                onReasoning?.Invoke(content);

            if (!hasToolCalls)
                return content;

            if (finalRound)
                throw new AgentToolException("模型在最终轮仍请求工具调用，未能给出结论。");

            var callRecords = new List<object>();
            var pendingCalls = new List<AgentToolCall>();
            foreach (var call in toolCalls.EnumerateArray())
            {
                var id = GetString(call, "id");
                var function = call.TryGetProperty("function", out var fn) ? fn : default;
                var name = GetString(function, "name");
                var arguments = GetString(function, "arguments") ?? "{}";
                callRecords.Add(new
                {
                    id,
                    type = "function",
                    function = new { name, arguments }
                });
                pendingCalls.Add(new AgentToolCall(id, name, arguments));
            }

            // Player2 后端转接 Claude/Vertex 时，若带 tool_calls 的 assistant 消息包含非空正文，
            // 会被后端拆成尾随的 assistant 块，导致后续请求被 Vertex 判定为 assistant message prefill 而报 400。
            // 推理正文已在上方通过 onReasoning 传给界面展示，写回历史记录时 Player2 使用空串。
            var assistantContent = Config.IsPlayer2 ? "" : content;
            messages.Add(new { role = "assistant", content = assistantContent, tool_calls = callRecords });

            foreach (var pending in pendingCalls)
            {
                onToolCall?.Invoke(pending);

                var result = await ExecuteToolSafeAsync(executeTool, pending, cancellationToken)
                    .ConfigureAwait(false);
                messages.Add(new { role = "tool", tool_call_id = pending.Id, content = result });
            }
        }

        throw new AgentToolException("模型没有返回最终结论。");
    }

    /// <summary>
    /// 组装 OpenAI 兼容请求体。模型名为空时不下发 model 字段——
    /// Player2 由服务端选定模型，带上空模型名会被拒。
    /// </summary>
    private Dictionary<string, object?> BuildChatBody(IReadOnlyList<object> messages, object[]? tools = null)
    {
        var body = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["temperature"] = 0.1,
            ["messages"] = messages
        };

        if (!string.IsNullOrWhiteSpace(Config.Model))
            body["model"] = Config.Model;

        if (tools is { Length: > 0 })
        {
            body["tools"] = tools;
            body["tool_choice"] = "auto";
        }

        return body;
    }

    /// <summary>
    /// 解析 Player2 的连接方式：优先本地客户端（连端点带凭据一起拿到），
    /// 否则用配置里的远程地址与网页密钥。结果在本实例内缓存，工具循环每轮不重复探测。
    /// </summary>
    private async Task<Player2AuthService.LocalRoute?> ResolvePlayer2RouteAsync(CancellationToken cancellationToken)
    {
        if (_player2RouteResolved)
            return _player2Route;

        _player2RouteResolved = true;
        _player2Route = await Player2AuthService.TryResolveLocalAsync(cancellationToken).ConfigureAwait(false);
        return _player2Route;
    }

    private async Task<string> ResolveApiKeyAsync(CancellationToken cancellationToken)
    {
        if (_resolvedKey != null)
            return _resolvedKey;

        if (Config.IsPlayer2)
        {
            var route = await ResolvePlayer2RouteAsync(cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(route?.ApiKey))
                return _resolvedKey = route!.ApiKey;

            if (!string.IsNullOrWhiteSpace(Config.ApiKey))
                return _resolvedKey = Config.ApiKey;

            throw new AgentApiException(
                System.Net.HttpStatusCode.Unauthorized,
                "Player2 未提供凭据：本地客户端未运行，设置里也没有网页密钥。可在设置中发起网页鉴权。");
        }

        return _resolvedKey = Config.ApiKey;
    }

    private async Task<HttpResponseMessage> PostJsonAsync(
        object body,
        CancellationToken cancellationToken)
    {
        var apiKey = await ResolveApiKeyAsync(cancellationToken).ConfigureAwait(false);

        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint())
        {
            Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json")
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        // Player2 靠这个头把用量归到对应的客户端标识上，缺了会被当成未登记来源。
        if (Config.IsPlayer2)
            request.Headers.TryAddWithoutValidation("player2-game-key", Player2AuthService.GameClientId);

        return await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<string> ExecuteToolSafeAsync(
        Func<AgentToolCall, CancellationToken, Task<string>> executeTool,
        AgentToolCall call,
        CancellationToken cancellationToken)
    {
        try
        {
            return await executeTool(call, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return "工具执行失败：" + ex.Message;
        }
    }

    private static bool IsToolsUnsupported(HttpStatusCode statusCode)
        => (int)statusCode is 400 or 404 or 405 or 422;

    private static string GetString(JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object &&
           element.TryGetProperty(propertyName, out var value) &&
           value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static string ExtractOpenAiContent(JsonDocument doc)
    {
        if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
            throw new AgentApiException(HttpStatusCode.OK, "响应中没有 choices 字段");

        var message = choices[0].GetProperty("message");
        return message.TryGetProperty("content", out var content)
            ? content.GetString() ?? string.Empty
            : string.Empty;
    }

    private string Endpoint()
    {
        // 本地客户端可用时走它自己的端点，否则用配置地址（Player2 默认远程接口）。
        var baseUrl = (_player2Route?.BaseUrl ?? Config.BaseUrl).TrimEnd('/');
        return baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
            ? $"{baseUrl}/chat/completions"
            : $"{baseUrl}/v1/chat/completions";
    }

    private static string ShortBody(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return "（空响应）";
        var trimmed = body.Trim();
        return trimmed.Length <= 2000 ? trimmed : trimmed[..2000] + "…";
    }

    private static bool IsStructuredJsonPayload(string text)
    {
        var trimmed = text.Trim();
        return (trimmed.StartsWith('{') && trimmed.Contains("\"verdict\"")) ||
               (trimmed.StartsWith("```") && trimmed.Contains("\"verdict\""));
    }

    /// <summary>从模型输出中抓出第一个完整 JSON 对象（容忍 Markdown 围栏与前后废话）。</summary>
    public static string? ExtractJsonObject(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var start = text.IndexOf('{');
        while (start >= 0)
        {
            var end = FindMatchingBrace(text, start);
            if (end > start)
                return text.Substring(start, end - start + 1);

            start = text.IndexOf('{', start + 1);
        }

        return null;
    }

    private static int FindMatchingBrace(string text, int start)
    {
        var depth = 0;
        var inString = false;
        var escaped = false;

        for (var i = start; i < text.Length; i++)
        {
            var ch = text[i];
            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (ch == '\\')
                {
                    escaped = true;
                }
                else if (ch == '"')
                {
                    inString = false;
                }
                continue;
            }

            switch (ch)
            {
                case '"':
                    inString = true;
                    break;
                case '{':
                    depth++;
                    break;
                case '}':
                    depth--;
                    if (depth == 0)
                        return i;
                    break;
            }
        }

        return -1;
    }

    public void Dispose()
    {
        if (_ownsHttp)
            _http.Dispose();
    }
}

public sealed class AgentApiException : Exception
{
    public HttpStatusCode StatusCode { get; }

    public AgentApiException(HttpStatusCode statusCode, string message)
        : base($"模型接口返回 {(int)statusCode}：{message}")
    {
        StatusCode = statusCode;
    }
}

public sealed record AgentToolDefinition(string Name, string Description, string ParametersJson);

public sealed record AgentToolCall(string Id, string Name, string ArgumentsJson);

/// <summary>端点不接受 tools 字段，调用方应回退到单轮对话。</summary>
public sealed class AgentToolsUnsupportedException : Exception
{
    public HttpStatusCode StatusCode { get; }

    public AgentToolsUnsupportedException(HttpStatusCode statusCode, string message)
        : base($"模型端点不支持文件工具（{(int)statusCode}）：{message}")
    {
        StatusCode = statusCode;
    }
}

public sealed class AgentToolException : Exception
{
    public AgentToolException(string message) : base(message)
    {
    }
}
