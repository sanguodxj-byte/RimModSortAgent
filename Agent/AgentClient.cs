using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ModSorterAgent.Agent;

/// <summary>
/// 最小 LLM 客户端：OpenAI 兼容 /chat/completions 与 Anthropic /v1/messages。
/// 只负责"发提示词、拿回文本"，结构化解析交给 <see cref="AgentInvestigator"/>。
/// </summary>
public sealed class AgentClient : IDisposable
{
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

    private async Task<string> CompleteOpenAiAsync(string prompt, CancellationToken cancellationToken)
    {
        var body = new
        {
            model = Config.Model,
            temperature = 0.1,
            messages = new object[]
            {
                new { role = "system", content = "你是严谨的 RimWorld 模组兼容性工程师。" },
                new { role = "user", content = prompt }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint())
        {
            Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Config.ApiKey);

        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken)
            .ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            throw new AgentApiException(response.StatusCode, ShortBody(text));

        using var doc = JsonDocument.Parse(text);
        if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
            throw new AgentApiException(response.StatusCode, "响应中没有 choices 字段");

        var message = choices[0].GetProperty("message");
        return message.TryGetProperty("content", out var content)
            ? content.GetString() ?? string.Empty
            : string.Empty;
    }

    private async Task<string> CompleteAnthropicAsync(string prompt, CancellationToken cancellationToken)
    {
        var body = new
        {
            model = Config.Model,
            max_tokens = 8192,
            temperature = 0.1,
            system = "你是严谨的 RimWorld 模组兼容性工程师。",
            messages = new object[] { new { role = "user", content = prompt } }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint())
        {
            Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json")
        };
        request.Headers.TryAddWithoutValidation("x-api-key", Config.ApiKey);
        request.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");

        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken)
            .ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            throw new AgentApiException(response.StatusCode, ShortBody(text));

        using var doc = JsonDocument.Parse(text);
        if (!doc.RootElement.TryGetProperty("content", out var blocks) || blocks.ValueKind != JsonValueKind.Array)
            throw new AgentApiException(response.StatusCode, "响应中没有 content 字段");

        var builder = new StringBuilder();
        foreach (var block in blocks.EnumerateArray())
        {
            if (block.TryGetProperty("type", out var type) && type.GetString() == "text" &&
                block.TryGetProperty("text", out var blockText))
                builder.Append(blockText.GetString());
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
