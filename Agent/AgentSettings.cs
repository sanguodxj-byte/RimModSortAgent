using System.Runtime.InteropServices;
using System.Text;
using ModSorterAgent.Services;

namespace ModSorterAgent.Agent;

public sealed record AgentConfig(
    string Provider,
    string BaseUrl,
    string Model,
    string ApiKey)
{
    /// <summary>Player2 由服务端选定模型，不需要模型名。</summary>
    public bool IsPlayer2 => Provider.Equals("player2", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Player2 选中即可用：凭据由本地客户端自动换取，换不到时再走网页鉴权，
    /// 因此不要求用户预先填写任何字段；其它提供方仍需地址、模型与密钥齐备。
    /// </summary>
    public bool IsConfigured =>
        IsPlayer2 ||
        (!string.IsNullOrWhiteSpace(BaseUrl) &&
         !string.IsNullOrWhiteSpace(Model) &&
         !string.IsNullOrWhiteSpace(ApiKey));

    public string DisplayName => IsConfigured
        ? string.IsNullOrWhiteSpace(Model) ? Provider : $"{Provider} · {Model}"
        : "本地代码核查";
}

/// <summary>
/// Agent 配置读取：设置文件 → 环境变量覆盖（密钥只允许运行期注入时使用环境变量）。
/// </summary>
public static class AgentConfigLoader
{
    public const string EnvProvider = "MODSORTER_AGENT_PROVIDER";
    public const string EnvBaseUrl = "MODSORTER_AGENT_BASE_URL";
    public const string EnvModel = "MODSORTER_AGENT_MODEL";
    public const string EnvApiKey = "MODSORTER_AGENT_API_KEY";

    public static AgentConfig Load(AppSettings settings)
    {
        var provider = Env(EnvProvider) ?? settings.AgentProvider;
        var baseUrl = Env(EnvBaseUrl) ?? settings.AgentBaseUrl;
        var model = Env(EnvModel) ?? settings.AgentModel;
        var apiKey = Env(EnvApiKey) ?? DpapiProtector.Unprotect(settings.AgentApiKeyProtected);

        provider = string.IsNullOrWhiteSpace(provider) ? "off" : provider.Trim().ToLowerInvariant();

        // Anthropic 地址，表现为一次静默的 401。直接归到本地核查，让用户重新选择。
        if (provider == "anthropic")
            provider = "off";

        if (provider == "off")
            return new AgentConfig("off", string.Empty, string.Empty, string.Empty);

        baseUrl = string.IsNullOrWhiteSpace(baseUrl)
            ? provider switch
            {
                "player2" => "https://api.player2.game/v1",
                _ => "https://api.openai.com/v1"
            }
            : baseUrl.Trim().TrimEnd('/');

        return new AgentConfig(provider, baseUrl, model?.Trim() ?? string.Empty, apiKey ?? string.Empty);
    }

    public static string DefaultPatchOutputDir =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ModSorterAgent",
            "patches");

    public static string DefaultCaseOutputDir =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ModSorterAgent",
            "agent-cases");

    private static string? Env(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

/// <summary>
/// Windows DPAPI：API Key 只以当前用户可解密的形式落盘。
/// 用 P/Invoke 而不是额外 NuGet 包，保证单文件工具零依赖。
/// </summary>
public static class DpapiProtector
{
    private const int CryptProtectUiForbidden = 0x1;

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob
    {
        public int cbData;
        public IntPtr pbData;
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptProtectData(
        ref DataBlob pDataIn,
        string? szDataDescr,
        IntPtr pOptionalEntropy,
        IntPtr pvReserved,
        IntPtr pPromptStruct,
        int dwFlags,
        ref DataBlob pDataOut);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptUnprotectData(
        ref DataBlob pDataIn,
        IntPtr ppszDataDescr,
        IntPtr pOptionalEntropy,
        IntPtr pvReserved,
        IntPtr pPromptStruct,
        int dwFlags,
        ref DataBlob pDataOut);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr hMem);

    public static string Protect(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        try
        {
            var input = Encoding.UTF8.GetBytes(plainText);
            var inBlob = new DataBlob { cbData = input.Length, pbData = Marshal.AllocHGlobal(input.Length) };
            var outBlob = new DataBlob();
            try
            {
                Marshal.Copy(input, 0, inBlob.pbData, input.Length);
                if (!CryptProtectData(ref inBlob, "ModSorterAgent", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                        CryptProtectUiForbidden, ref outBlob))
                    return string.Empty;

                var output = new byte[outBlob.cbData];
                Marshal.Copy(outBlob.pbData, output, 0, outBlob.cbData);
                return Convert.ToBase64String(output);
            }
            finally
            {
                if (inBlob.pbData != IntPtr.Zero)
                    Marshal.FreeHGlobal(inBlob.pbData);
                if (outBlob.pbData != IntPtr.Zero)
                    LocalFree(outBlob.pbData);
            }
        }
        catch
        {
            return string.Empty;
        }
    }

    public static string Unprotect(string protectedBase64)
    {
        if (string.IsNullOrWhiteSpace(protectedBase64))
            return string.Empty;

        try
        {
            var input = Convert.FromBase64String(protectedBase64);
            var inBlob = new DataBlob { cbData = input.Length, pbData = Marshal.AllocHGlobal(input.Length) };
            var outBlob = new DataBlob();
            try
            {
                Marshal.Copy(input, 0, inBlob.pbData, input.Length);
                if (!CryptUnprotectData(ref inBlob, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                        CryptProtectUiForbidden, ref outBlob))
                    return string.Empty;

                var output = new byte[outBlob.cbData];
                Marshal.Copy(outBlob.pbData, output, 0, outBlob.cbData);
                return Encoding.UTF8.GetString(output);
            }
            finally
            {
                if (inBlob.pbData != IntPtr.Zero)
                    Marshal.FreeHGlobal(inBlob.pbData);
                if (outBlob.pbData != IntPtr.Zero)
                    LocalFree(outBlob.pbData);
            }
        }
        catch
        {
            return string.Empty;
        }
    }
}
