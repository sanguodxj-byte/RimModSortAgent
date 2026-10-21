using System.Text.Json;

namespace ModSorterAgent.Services;

/// <summary>
/// 应用设置（路径覆盖项），持久化到 exe 旁的 AppSettings.json。
/// 所有字段留空 = 使用自动检测路径（Steam 库与当前用户 LocalLow 目录）。
/// </summary>
public sealed class AppSettings
{
    /// <summary>RimWorld 安装目录（含 RimWorldWin64.exe / Data / Mods）。</summary>
    public string GameInstallDir { get; set; } = string.Empty;

    /// <summary>Steam 创意工坊内容目录（…\steamapps\workshop\content\294100）。</summary>
    public string WorkshopContentDir { get; set; } = string.Empty;

    /// <summary>本地模组工作区目录（含若干未打包 mod 文件夹）。</summary>
    public string LocalModWorkspaceDir { get; set; } = string.Empty;

    /// <summary>ModsConfig.xml 显式覆盖路径（留空 = 自动检测 LocalLow）。</summary>
    public string ModsConfigPath { get; set; } = string.Empty;

    // ── 分析选项 ──
    /// <summary>分析时是否尝试连接 GABS 实机核验（默认开启，失败自动退回离线日志）。</summary>
    public bool VerifyWithGabs { get; set; } = true;

    /// <summary>分析范围是否包含 Steam 工坊订阅池（默认关闭，避免几百项无关模组）。</summary>
    public bool IncludeWorkshopSubscriptions { get; set; }

    // ── Agent ──
    /// <summary>Agent 协议：off / openai / anthropic。</summary>
    public string AgentProvider { get; set; } = "off";

    /// <summary>OpenAI 兼容或 Anthropic 兼容接口地址（留空使用默认）。</summary>
    public string AgentBaseUrl { get; set; } = string.Empty;

    public string AgentModel { get; set; } = string.Empty;

    /// <summary>DPAPI 加密后的 API Key（仅当前 Windows 用户可解密）。</summary>
    public string AgentApiKeyProtected { get; set; } = string.Empty;

    /// <summary>兼容补丁工作区（留空 = %LOCALAPPDATA%\\ModSorterAgent\\patches）。</summary>
    public string AgentPatchOutputDir { get; set; } = string.Empty;
}

/// <summary>
/// 路径自动检测：扫描所有磁盘 / Steam 库，定位 RimWorld 安装目录、创意工坊内容目录与 ModsConfig.xml。
/// 用户显式设置优先，检测只用于补默认值与提供建议。
/// </summary>
public static class PathAutoDetector
{
    private const string AppId = "294100";

    /// <summary>候选 Steam 根目录：Steam 注册表 + 常见安装位置。</summary>
    private static IEnumerable<string> EnumerateSteamRoots()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;
            try
            {
                var full = Path.GetFullPath(path.TrimEnd('\\', '/'));
                seen.Add(full);
            }
            catch
            {
                // 无效路径忽略
            }
        }

        // 注册表：Steam 主程序位置（HKCU 与 HKLM 都查）
        foreach (var hive in new[] { Microsoft.Win32.Registry.CurrentUser, Microsoft.Win32.Registry.LocalMachine })
        {
            try
            {
                foreach (var subKey in new[] { @"SOFTWARE\Valve\Steam", @"SOFTWARE\WOW6432Node\Valve\Steam" })
                {
                    using var key = hive.OpenSubKey(subKey);
                    if (key?.GetValue("InstallPath") is string installPath)
                        Add(installPath);
                }
            }
            catch
            {
                // 注册表不可读忽略
            }
        }

        // 常见盘符位置
        foreach (var drive in DriveReadyRoots())
            Add(Path.Combine(drive, "Steam"));
        foreach (var drive in DriveReadyRoots())
            Add(Path.Combine(drive, "Program Files (x86)", "Steam"));
        foreach (var drive in DriveReadyRoots())
            Add(Path.Combine(drive, "SteamLibrary"));

        return seen;
    }

    private static List<string> DriveReadyRoots()
    {
        var roots = new List<string>();
        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady)
                    roots.Add(drive.RootDirectory.FullName);
            }
        }
        catch
        {
            var systemRoot = Path.GetPathRoot(Environment.SystemDirectory);
            if (!string.IsNullOrWhiteSpace(systemRoot))
                roots.Add(systemRoot!);
        }
        return roots;
    }

    /// <summary>Steam 库列表：注册表 InstallPath 下 libraryfolders.vdf 解析 + 主目录。</summary>
    private static List<string> EnumerateSteamLibraries()
    {
        var libs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in EnumerateSteamRoots())
        {
            libs.Add(root);
            try
            {
                var vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
                if (!File.Exists(vdf))
                    continue;

                foreach (var line in File.ReadLines(vdf))
                {
                    // 形如 "path"		"<Steam 库路径>"
                    var trimmed = line.Trim();
                    if (!trimmed.StartsWith("\"path\"", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var start = trimmed.IndexOf('"', trimmed.IndexOf(':'));
                    if (start < 0)
                        continue;
                    var value = trimmed.Substring(start + 1).TrimEnd('"').Replace("\\\\", "\\");
                    if (value.Length > 0)
                        libs.Add(value);
                }
            }
            catch
            {
                // vdf 解析失败忽略，继续下一个库
            }
        }
        return libs.ToList();
    }

    /// <summary>检测 RimWorld 安装目录（存在 RimWorldWin64.exe / RimWorld.exe / Data\Core 即命中）。</summary>
    public static string? DetectGameInstallDir()
    {
        foreach (var lib in EnumerateSteamLibraries())
        {
            var common = Path.Combine(lib, "steamapps", "common", "RimWorld");
            if (LooksLikeRimWorldInstall(common))
                return common;
        }

        // 非标准命名兜底：扫各库 common 下一级目录
        foreach (var lib in EnumerateSteamLibraries())
        {
            var common = Path.Combine(lib, "steamapps", "common");
            try
            {
                foreach (var dir in Directory.EnumerateDirectories(common))
                {
                    if (LooksLikeRimWorldInstall(dir))
                        return dir;
                }
            }
            catch
            {
                // 目录不可读忽略
            }
        }

        return null;
    }

    /// <summary>检测创意工坊内容目录（…\steamapps\workshop\content\294100，需存在且含数字子目录）。</summary>
    public static string? DetectWorkshopContentDir()
    {
        foreach (var lib in EnumerateSteamLibraries())
        {
            var content = Path.Combine(lib, "steamapps", "workshop", "content", AppId);
            if (LooksLikeWorkshopContent(content))
                return content;
        }

        foreach (var root in EnumerateSteamRoots())
        {
            var content = Path.Combine(root, "steamapps", "workshop", "content", AppId);
            if (LooksLikeWorkshopContent(content))
                return content;
        }

        return null;
    }

    /// <summary>当前用户的标准 ModsConfig.xml 位置（不要求文件已存在）。</summary>
    public static string? GetStandardModsConfigPath()
    {
        try
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrWhiteSpace(userProfile))
                return null;

            return Path.Combine(
                userProfile, "AppData", "LocalLow",
                "Ludeon Studios", "RimWorld by Ludeon Studios", "Config", "ModsConfig.xml");
        }
        catch
        {
            return null;
        }
    }

    /// <summary>检测当前用户是否已有 ModsConfig.xml。</summary>
    public static string? DetectModsConfigPath()
    {
        var candidate = GetStandardModsConfigPath();
        return candidate != null && File.Exists(candidate) ? candidate : null;
    }

    private static bool LooksLikeRimWorldInstall(string dir)
    {
        try
        {
            if (!Directory.Exists(dir))
                return false;

            var hasExe = File.Exists(Path.Combine(dir, "RimWorldWin64.exe")) ||
                         File.Exists(Path.Combine(dir, "RimWorld.exe"));
            var hasData = Directory.Exists(Path.Combine(dir, "Data", "Core"));
            return hasExe || hasData;
        }
        catch
        {
            return false;
        }
    }

    private static bool LooksLikeWorkshopContent(string dir)
    {
        try
        {
            if (!Directory.Exists(dir))
                return false;

            // 只要存在至少一个纯数字命名的模组目录即认定为 294100 内容目录
            return Directory.EnumerateDirectories(dir)
                .Select(Path.GetFileName)
                .Any(name => !string.IsNullOrEmpty(name) && name!.Length > 0 && name!.All(char.IsDigit));
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// 设置存储：JSON 读写（exe 旁 AppSettings.json），损坏时回退到空设置。
/// </summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string StorePath { get; }

    public SettingsStore(string? storePath = null)
    {
        StorePath = storePath ?? Path.Combine(AppContext.BaseDirectory, "AppSettings.json");
        Current = Load();
    }

    public AppSettings Current { get; private set; }

    public void Save(AppSettings settings)
    {
        Current = settings;
        try
        {
            File.WriteAllText(StorePath, JsonSerializer.Serialize(settings, JsonOptions));
        }
        catch
        {
            // 只读目录等场景静默失败：功能退化为本次会话内存态
        }
    }

    private AppSettings Load()
    {
        try
        {
            if (!File.Exists(StorePath))
                return new AppSettings();

            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(StorePath), JsonOptions);
            return settings ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }
}
