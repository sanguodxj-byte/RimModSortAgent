using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ModSorterAgent.Agent;

/// <summary>
/// 最小 LLM 客户端：OpenAI 兼容 /chat/completions 与 Anthropic /v1/messages。
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

        return Config.IsAnthropic
            ? await CompleteAnthropicAsync(prompt, timeout.Token).ConfigureAwait(false)
            : await CompleteOpenAiAsync(prompt, timeout.Token).ConfigureAwait(false);
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

        return Config.IsAnthropic
            ? await CompleteAnthropicWithToolsAsync(prompt, tools, executeTool, timeout.Token).ConfigureAwait(false)
            : await CompleteOpenAiWithToolsAsync(prompt, tools, executeTool, timeout.Token).ConfigureAwait(false);
    }

    private async Task<string> CompleteOpenAiAsync(string prompt, CancellationToken cancellationToken)
    {
        var body = new
        {
            model = Config.Model,
            temperature = 0.1,
            messages = new object[]
            {
                new { role = "system", content = SystemPrompt },
                new { role = "user", content = prompt }
            }
        };

        using var response = await PostJsonAsync(Endpoint(), body, cancellationToken, anthropic: false)
            .ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            throw new AgentApiException(response.StatusCode, ShortBody(text));

        using var doc = JsonDocument.Parse(text);
        return ExtractOpenAiContent(doc);
    }

    private async Task<string> CompleteOpenAiWithToolsAsync(
        string prompt,
        IReadOnlyList<AgentToolDefinition> tools,
        Func<AgentToolCall, CancellationToken, Task<string>> executeTool,
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

            object body = finalRound
                ? new { model = Config.Model, temperature = 0.1, messages }
                : new { model = Config.Model, temperature = 0.1, messages, tools = toolSpecs, tool_choice = "auto" };

            using var response = await PostJsonAsync(Endpoint(), body, cancellationToken, anthropic: false)
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

            if (!message.TryGetProperty("tool_calls", out var toolCalls) ||
                toolCalls.ValueKind != JsonValueKind.Array ||
                toolCalls.GetArrayLength() == 0)
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

            messages.Add(new { role = "assistant", content, tool_calls = callRecords });

            foreach (var pending in pendingCalls)
            {
                var result = await ExecuteToolSafeAsync(executeTool, pending, cancellationToken)
                    .ConfigureAwait(false);
                messages.Add(new { role = "tool", tool_call_id = pending.Id, content = result });
            }
        }

        throw new AgentToolException("模型没有返回最终结论。");
    }

    private async Task<string> CompleteAnthropicAsync(string prompt, CancellationToken cancellationToken)
    {
        var body = new
        {
            model = Config.Model,
            max_tokens = 8192,
            temperature = 0.1,
            system = SystemPrompt,
            messages = new object[] { new { role = "user", content = prompt } }
        };

        using var response = await PostJsonAsync(Endpoint(), body, cancellationToken, anthropic: true)
            .ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            throw new AgentApiException(response.StatusCode, ShortBody(text));

        using var doc = JsonDocument.Parse(text);
        return ExtractAnthropicText(doc);
    }

    private async Task<string> CompleteAnthropicWithToolsAsync(
        string prompt,
        IReadOnlyList<AgentToolDefinition> tools,
        Func<AgentToolCall, CancellationToken, Task<string>> executeTool,
        CancellationToken cancellationToken)
    {
        var messages = new List<object> { new { role = "user", content = prompt } };
        var toolSpecs = tools.Select(t => new
        {
            name = t.Name,
            description = t.Description,
            input_schema = JsonNode.Parse(t.ParametersJson)
        }).ToArray();

        for (var round = 0; round <= MaxToolRounds; round++)
        {
            var finalRound = round == MaxToolRounds;
            if (finalRound)
                messages.Add(new { role = "user", content = FinalRoundNudge });

            object body = finalRound
                ? new { model = Config.Model, max_tokens = 8192, temperature = 0.1, system = SystemPrompt, messages }
                : new { model = Config.Model, max_tokens = 8192, temperature = 0.1, system = SystemPrompt, messages, tools = toolSpecs };

            using var response = await PostJsonAsync(Endpoint(), body, cancellationToken, anthropic: true)
                .ConfigureAwait(false);
            var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                if (round == 0 && IsToolsUnsupported(response.StatusCode))
                    throw new AgentToolsUnsupportedException(response.StatusCode, ShortBody(text));

                throw new AgentApiException(response.StatusCode, ShortBody(text));
            }

            using var doc = JsonDocument.Parse(text);
            if (!doc.RootElement.TryGetProperty("content", out var blocks) ||
                blocks.ValueKind != JsonValueKind.Array)
                throw new AgentApiException(response.StatusCode, "响应中没有 content 字段");

            var toolUses = blocks.EnumerateArray()
                .Where(b => GetString(b, "type").Equals("tool_use", StringComparison.Ordinal))
                .ToList();

            if (toolUses.Count == 0)
                return JoinAnthropicText(blocks);

            if (finalRound)
                throw new AgentToolException("模型在最终轮仍请求工具调用，未能给出结论。");

            var assistantBlocks = new List<object>();
            var pendingCalls = new List<AgentToolCall>();
            foreach (var block in blocks.EnumerateArray())
            {
                var type = GetString(block, "type");
                if (type == "text")
                {
                    assistantBlocks.Add(new { type = "text", text = GetString(block, "text") });
                }
                else if (type == "tool_use")
                {
                    var id = GetString(block, "id");
                    var name = GetString(block, "name");
                    var input = block.TryGetProperty("input", out var inputElement) ? inputElement : default;
                    var arguments = input.ValueKind == JsonValueKind.Undefined ? "{}" : input.GetRawText();
                    assistantBlocks.Add(new
                    {
                        type = "tool_use",
                        id,
                        name,
                        input = JsonNode.Parse(arguments)
                    });
                    pendingCalls.Add(new AgentToolCall(id, name, arguments));
                }
            }

            messages.Add(new { role = "assistant", content = assistantBlocks });

            var results = new List<object>();
            foreach (var pending in pendingCalls)
            {
                var result = await ExecuteToolSafeAsync(executeTool, pending, cancellationToken)
                    .ConfigureAwait(false);
                results.Add(new { type = "tool_result", tool_use_id = pending.Id, content = result });
            }

            messages.Add(new { role = "user", content = results });
        }

        throw new AgentToolException("模型没有返回最终结论。");
    }

    private async Task<HttpResponseMessage> PostJsonAsync(
        string endpoint,
        object body,
        CancellationToken cancellationToken,
        bool anthropic)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json")
        };

        if (anthropic)
        {
            request.Headers.TryAddWithoutValidation("x-api-key", Config.ApiKey);
            request.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");
        }
        else
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Config.ApiKey);
        }

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

    private static string ExtractAnthropicText(JsonDocument doc)
    {
        if (!doc.RootElement.TryGetProperty("content", out var blocks) || blocks.ValueKind != JsonValueKind.Array)
            throw new AgentApiException(HttpStatusCode.OK, "响应中没有 content 字段");

        return JoinAnthropicText(blocks);
    }

    private static string JoinAnthropicText(JsonElement blocks)
    {
        var builder = new StringBuilder();
        foreach (var block in blocks.EnumerateArray())
        {
            if (GetString(block, "type") == "text")
                builder.Append(GetString(block, "text"));
        }

        return builder.ToString();
    }

    private string Endpoint()
    {
        var baseUrl = Config.BaseUrl.TrimEnd('/');
        if (Config.IsAnthropic)
            return baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
                ? $"{baseUrl}/messages"
                : $"{baseUrl}/v1/messages";

        return baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
            ? $"{baseUrl}/chat/completions"
            : $"{baseUrl}/v1/chat/completions";
    }

    private static string ShortBody(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return "（空响应）";
        var trimmed = body.Trim();
        return trimmed.Length <= 300 ? trimmed : trimmed[..300] + "…";
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
