using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace ModSorterAgent.Agent;

/// <summary>
/// Player2 凭据获取。两条路：
///
/// ① 本地客户端——桌面客户端在本机跑着只对本机开放的接口，端口写在
///    %APPDATA%\game.player2.client\api.port，向它换取 p2Key，用户什么都不用填；
/// ② 网页鉴权——客户端没开时走 OAuth 设备码流程：申请设备码 → 打开授权页 →
///    轮询换取 p2Key，用户只需在浏览器点一次授权。
///
/// GameClientId 取本工具专属标识：Player2 按它把授权出的密钥绑定到对应账户，
/// 换成别家的 ID 会把授权记到别人账上。
/// </summary>
public static class Player2AuthService
{
    /// <summary>本工具的 Player2 客户端标识，不要替换成其它项目的 ID。</summary>
    public const string GameClientId = "01a05876-8d6b-7376-92ef-0e51b45d130c";

    /// <summary>Player2 远程接口根地址。</summary>
    public const string RemoteBaseUrl = "https://api.player2.game/v1";

    private const string DeviceNewEndpoint = RemoteBaseUrl + "/login/device/new";
    private const string DeviceTokenEndpoint = RemoteBaseUrl + "/login/device/token";
    private const string DeviceGrantType = "urn:ietf:params:oauth:grant-type:device_code";
    private const string LocalPortFileName = "api.port";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static LocalRoute? _cachedLocalRoute;

    /// <summary>本地客户端连接信息。BaseUrl 已含 /v1。</summary>
    public sealed record LocalRoute(string BaseUrl, string ApiKey);

    /// <summary>设备码授权期间展示给用户的信息。</summary>
    public sealed record DeviceCodePrompt(string UserCode, string ApprovalUrl, int ExpiresInSeconds);

    private sealed record DeviceCodeSession(string DeviceCode, int ExpiresInSeconds, int IntervalSeconds);

    /// <summary>
    /// 探测本地客户端并换取 p2Key；客户端不在或换不到时返回 null。
    /// 结果在进程内缓存，避免每次对话都重新探测。
    /// </summary>
    public static async Task<LocalRoute?> TryResolveLocalAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedLocalRoute != null)
            return _cachedLocalRoute;

        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cachedLocalRoute != null)
                return _cachedLocalRoute;

            foreach (var baseUrl in GetCandidateBaseUrls())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!await IsHealthyAsync(baseUrl, cancellationToken).ConfigureAwait(false))
                    continue;

                var key = await RequestLocalKeyAsync(baseUrl, cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                _cachedLocalRoute = new LocalRoute(baseUrl, key);
                return _cachedLocalRoute;
            }

            return null;
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>清掉本地探测缓存，用于「重试连接」这类显式操作。</summary>
    public static void ResetLocalCache() => _cachedLocalRoute = null;

    /// <summary>
    /// 走设备码授权换取 p2Key。拿到设备码后回调 <paramref name="onPromptReady"/>，
    /// 由调用方决定何时打开授权页；随后按服务端给的间隔轮询直到授权完成或过期。
    /// </summary>
    public static async Task<string> AuthorizeViaWebAsync(
        Action<DeviceCodePrompt>? onPromptReady,
        CancellationToken cancellationToken = default)
    {
        var (prompt, session) = await RequestDeviceCodeAsync(cancellationToken).ConfigureAwait(false);
        onPromptReady?.Invoke(prompt);

        var pollInterval = TimeSpan.FromSeconds(Math.Max(3, session.IntervalSeconds));
        var maxSeconds = session.ExpiresInSeconds > 0 ? session.ExpiresInSeconds : 600;
        var deadline = DateTime.UtcNow.AddSeconds(maxSeconds);

        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(pollInterval, cancellationToken).ConfigureAwait(false);

            var key = await PollDeviceTokenAsync(session, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(key))
                return key;
        }

        throw new TimeoutException("授权超时，请重新发起网页鉴权。");
    }

    // ───────────────────────────── 本地客户端 ─────────────────────────────

    private static IEnumerable<string> GetCandidateBaseUrls()
    {
        var urls = new List<string>();

        // 桌面客户端把自己的端口写在这里，优先用它，避免猜端口。
        try
        {
            var portFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "game.player2.client",
                LocalPortFileName);

            if (File.Exists(portFile) &&
                int.TryParse(File.ReadAllText(portFile).Trim(), out var port) && port > 0)
            {
                urls.Add($"http://127.0.0.1:{port}/v1");
                urls.Add($"http://localhost:{port}/v1");
            }
        }
        catch
        {
            // 端口文件读不到就退回默认候选。
        }

        foreach (var port in new[] { 4315, 4316 })
        {
            urls.Add($"http://127.0.0.1:{port}/v1");
            urls.Add($"http://localhost:{port}/v1");
        }

        return urls.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<bool> IsHealthyAsync(string baseUrl, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await Http.GetAsync($"{baseUrl}/health", cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            // 客户端没开、端口被占、被防火墙拦下：换下一个候选。
            return false;
        }
    }

    private static async Task<string?> RequestLocalKeyAsync(string baseUrl, CancellationToken cancellationToken)
    {
        try
        {
            using var content = new StringContent("{}", Encoding.UTF8, "application/json");
            using var response = await Http
                .PostAsync($"{baseUrl}/login/web/{GameClientId}", content, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return null;

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(body);

            return doc.RootElement.TryGetProperty("p2Key", out var key) &&
                   key.ValueKind == JsonValueKind.String
                ? key.GetString()
                : null;
        }
        catch
        {
            return null;
        }
    }

    // ───────────────────────────── 设备码授权 ─────────────────────────────

    private static async Task<(DeviceCodePrompt Prompt, DeviceCodeSession Session)> RequestDeviceCodeAsync(
        CancellationToken cancellationToken)
    {
        using var response = await PostJsonAsync(
                DeviceNewEndpoint,
                $"{{\"client_id\":\"{GameClientId}\"}}",
                cancellationToken)
            .ConfigureAwait(false);

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"申请设备码失败（HTTP {(int)response.StatusCode}）。");

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var deviceCode = GetString(root, "deviceCode");
        if (string.IsNullOrWhiteSpace(deviceCode))
            throw new InvalidOperationException("设备码响应缺少 deviceCode。");

        var approvalUrl = GetString(root, "verificationUriComplete");
        if (string.IsNullOrWhiteSpace(approvalUrl))
            approvalUrl = GetString(root, "verificationUri");

        if (string.IsNullOrWhiteSpace(approvalUrl))
            throw new InvalidOperationException("设备码响应缺少授权地址。");

        var expiresIn = root.TryGetProperty("expiresIn", out var exp) && exp.TryGetInt32(out var seconds)
            ? seconds
            : 600;

        var interval = root.TryGetProperty("interval", out var itv) && itv.TryGetInt32(out var secs)
            ? secs
            : 5;

        return (
            new DeviceCodePrompt(GetString(root, "userCode"), approvalUrl, expiresIn),
            new DeviceCodeSession(deviceCode, expiresIn, interval));
    }

    private static async Task<string?> PollDeviceTokenAsync(
        DeviceCodeSession session,
        CancellationToken cancellationToken)
    {
        var payload =
            $"{{\"client_id\":\"{GameClientId}\",\"device_code\":\"{session.DeviceCode}\",\"grant_type\":\"{DeviceGrantType}\"}}";

        using var response = await PostJsonAsync(DeviceTokenEndpoint, payload, cancellationToken)
            .ConfigureAwait(false);

        // 400 = 用户还没在网页上点授权，继续等。
        if ((int)response.StatusCode == 400)
            return null;

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"授权轮询失败（HTTP {(int)response.StatusCode}）。");

        using var doc = JsonDocument.Parse(body);
        return GetString(doc.RootElement, "p2Key");
    }

    private static Task<HttpResponseMessage> PostJsonAsync(
        string url,
        string json,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        return Http.SendAsync(request, cancellationToken);
    }

    private static string GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
}
