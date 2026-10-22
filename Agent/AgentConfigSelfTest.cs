using ModSorterAgent.Services;

namespace ModSorterAgent.Agent;

/// <summary>
/// Agent 提供方解析的合成夹具自检。
///
/// 锁住两条容易静默回归的规则：
/// · Player2 由服务端选定模型，模型名为空也必须算作已配置，否则设置界面填了字却始终退回本地核查；
/// · 默认接口地址按提供方区分，留空时不能退化成 OpenAI 地址。
/// 另外确认 Anthropic 已不再是可识别的提供方。
/// </summary>
public static class AgentConfigSelfTest
{
    public static IReadOnlyList<string> Run()
    {
        var failures = new List<string>();

        try
        {
            AssertPlayer2NeedsNoModel(failures);
            AssertDefaultBaseUrlByProvider(failures);
            AssertOpenAiStillRequiresModel(failures);
            AssertOffIsInert(failures);
            AssertAnthropicIsGone(failures);
        }
        catch (Exception ex)
        {
            failures.Add($"自检异常：{ex.GetType().Name}: {ex.Message}");
        }

        return failures;
    }

    private static void AssertPlayer2NeedsNoModel(List<string> failures)
    {
        // 选中即启用：地址、模型、密钥全空也必须算作已配置，
        // 凭据由本地客户端在调用时换取，换不到再走网页鉴权。
        var bare = Load(provider: "player2", baseUrl: "", model: "", apiKey: "");

        if (!bare.IsConfigured)
            failures.Add("Player2 只选中、什么都不填就该算作已配置，实际判定为未配置。");

        if (!bare.IsPlayer2)
            failures.Add("provider=player2 未被识别为 Player2。");

        if (bare.DisplayName.Contains("· ", StringComparison.Ordinal))
            failures.Add($"Player2 无模型名时不该拼出分隔符，实际显示为「{bare.DisplayName}」。");

        if (!bare.BaseUrl.Equals("https://api.player2.game/v1", StringComparison.OrdinalIgnoreCase))
            failures.Add($"Player2 地址留空时应落到远程接口，实际「{bare.BaseUrl}」。");
    }

    private static void AssertDefaultBaseUrlByProvider(List<string> failures)
    {
        var player2 = Load("player2", baseUrl: "", model: "", apiKey: "k");
        if (!player2.BaseUrl.Equals("https://api.player2.game/v1", StringComparison.OrdinalIgnoreCase))
            failures.Add($"Player2 默认地址应为官方接口，实际「{player2.BaseUrl}」。");

        var openai = Load("openai", baseUrl: "", model: "gpt-4.1", apiKey: "k");
        if (!openai.BaseUrl.Equals("https://api.openai.com/v1", StringComparison.OrdinalIgnoreCase))
            failures.Add($"OpenAI 默认地址应为官方接口，实际「{openai.BaseUrl}」。");
    }

    private static void AssertOpenAiStillRequiresModel(List<string> failures)
    {
        var config = Load("openai", baseUrl: "", model: "", apiKey: "k");

        if (config.IsConfigured)
            failures.Add("OpenAI 兼容接口缺模型名时不该算作已配置。");
    }

    private static void AssertOffIsInert(List<string> failures)
    {
        var config = Load("off", baseUrl: "https://example.invalid", model: "x", apiKey: "y");

        if (config.IsConfigured)
            failures.Add("provider=off 必须始终为未配置（本地核查）。");

        if (config.IsPlayer2)
            failures.Add("provider=off 不该被识别为 Player2。");
    }

    private static void AssertAnthropicIsGone(List<string> failures)
    {
        var config = Load("anthropic", baseUrl: "", model: "claude-x", apiKey: "k");

        if (config.Provider != "off")
            failures.Add($"旧设置里的 anthropic 应迁移为 off，实际「{config.Provider}」。");

        if (config.IsConfigured)
            failures.Add("迁移后的 anthropic 不该算作已配置，否则会对 Anthropic 地址发出 OpenAI 兼容请求。");

        if (config.BaseUrl.Contains("anthropic", StringComparison.OrdinalIgnoreCase))
            failures.Add("Anthropic 支持已移除，不该再解析出 anthropic 默认地址。");
    }

    private static AgentConfig Load(string provider, string baseUrl, string model, string apiKey)
    {
        // 走真实读取路径：环境变量优先级高于设置文件，先清掉避免宿主环境干扰夹具。
        var saved = new Dictionary<string, string?>
        {
            [AgentConfigLoader.EnvProvider] = Environment.GetEnvironmentVariable(AgentConfigLoader.EnvProvider),
            [AgentConfigLoader.EnvBaseUrl] = Environment.GetEnvironmentVariable(AgentConfigLoader.EnvBaseUrl),
            [AgentConfigLoader.EnvModel] = Environment.GetEnvironmentVariable(AgentConfigLoader.EnvModel),
            [AgentConfigLoader.EnvApiKey] = Environment.GetEnvironmentVariable(AgentConfigLoader.EnvApiKey)
        };

        try
        {
            foreach (var name in saved.Keys)
                Environment.SetEnvironmentVariable(name, null);

            return AgentConfigLoader.Load(new AppSettings
            {
                AgentProvider = provider,
                AgentBaseUrl = baseUrl,
                AgentModel = model,
                AgentApiKeyProtected = string.Empty
            }) with
            {
                ApiKey = apiKey
            };
        }
        finally
        {
            foreach (var (name, value) in saved)
                Environment.SetEnvironmentVariable(name, value);
        }
    }
}
