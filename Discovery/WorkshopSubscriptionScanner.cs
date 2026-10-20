using System.Text;

namespace ModSorterAgent.Discovery;

/// <summary>
/// 单个 Steam 创意工坊订阅项。<see cref="PublishedFileId"/> 同时也是
/// <c>steamapps\workshop\content\294100</c> 下的子目录名。
/// </summary>
public sealed record WorkshopSubscription(
    string PublishedFileId,
    string Directory,
    bool IsSubscribed,
    bool IsInstalledOnDisk,
    bool HasAboutFile);

/// <summary>
/// 一次订阅扫描的结果：既包含订阅清单里的条目，也包含磁盘上实际存在的目录，
/// 因此「已订阅但未下载完」「未订阅但残留目录」都能被发现。
/// </summary>
public sealed record WorkshopScanResult(
    string WorkshopContentDirectory,
    string ManifestPath,
    bool ManifestFound,
    IReadOnlyList<WorkshopSubscription> Subscriptions,
    IReadOnlyList<string> SubscribedButNotInstalled,
    IReadOnlyList<string> InstalledButNotSubscribed,
    IReadOnlyList<string> Warnings)
{
    /// <summary>订阅且本地已可读取（目录存在并带有 About.xml）的模组数。</summary>
    public int ResolvableModCount => Subscriptions.Count(s => s.IsSubscribed && s.IsInstalledOnDisk && s.HasAboutFile);

    public int SubscribedCount => Subscriptions.Count(s => s.IsSubscribed);

    public static WorkshopScanResult Empty(string contentDir, string manifestPath) =>
        new(contentDir, manifestPath, false, Array.Empty<WorkshopSubscription>(),
            Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());
}

/// <summary>
/// Steam 创意工坊订阅检测。数据来源优先级：
/// 1. <c>steamapps\workshop\appworkshop_294100.acf</c> 的 <c>WorkshopItemsInstalled</c>（权威订阅清单）；
/// 2. <c>steamapps\appmanifest_294100.acf</c> 中可能存在的同名区块；
/// 3. 两者都缺失时退化为「磁盘目录即订阅」并给出告警。
/// </summary>
public static class WorkshopSubscriptionScanner
{
    public const string RimWorldAppId = "294100";

    public static WorkshopScanResult Scan(string? workshopContentDir = null, string? manifestPath = null)
    {
        var contentDir = string.IsNullOrWhiteSpace(workshopContentDir)
            ? ModListResolver.PathOptions.WorkshopDir
            : workshopContentDir!.Trim();

        var warnings = new List<string>();
        var manifest = string.IsNullOrWhiteSpace(manifestPath)
            ? ResolveManifestPath(contentDir)
            : manifestPath!.Trim();

        if (!Directory.Exists(contentDir))
        {
            warnings.Add($"未找到创意工坊内容目录：{contentDir}");
            return new WorkshopScanResult(contentDir, manifest, false,
                Array.Empty<WorkshopSubscription>(), Array.Empty<string>(), Array.Empty<string>(), warnings);
        }

        var manifestIds = TryReadSubscribedIds(manifest, warnings);
        var manifestFound = manifestIds != null;

        List<string> diskIds;
        try
        {
            diskIds = Directory.EnumerateDirectories(contentDir)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .Where(IsPublishedFileId)
                .ToList();
        }
        catch (Exception ex)
        {
            warnings.Add($"读取创意工坊目录失败：{ex.Message}");
            diskIds = new List<string>();
        }

        var diskSet = new HashSet<string>(diskIds, StringComparer.OrdinalIgnoreCase);

        // 清单缺失时退化为「磁盘目录即订阅」，并明确告知用户结论可信度下降
        var subscribedIds = manifestIds ?? new HashSet<string>(diskSet, StringComparer.OrdinalIgnoreCase);

        if (!manifestFound)
        {
            warnings.Add($"未能读取订阅清单（{manifest}），已退化为「磁盘目录即订阅」，可能与 Steam 实际订阅状态不一致。");
        }

        var subscribedNotInstalled = subscribedIds
            .Where(id => !diskSet.Contains(id))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        var installedNotSubscribed = diskIds
            .Where(id => !subscribedIds.Contains(id))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        var allIds = subscribedIds
            .Concat(diskIds)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        var subscriptions = new List<WorkshopSubscription>(allIds.Count);
        foreach (var id in allIds)
        {
            var dir = Path.Combine(contentDir, id);
            var installed = diskSet.Contains(id) && Directory.Exists(dir);
            var hasAbout = installed && File.Exists(Path.Combine(dir, "About", "About.xml"));
            subscriptions.Add(new WorkshopSubscription(id, dir, subscribedIds.Contains(id), installed, hasAbout));
        }

        if (subscribedNotInstalled.Count > 0)
        {
            warnings.Add($"{subscribedNotInstalled.Count} 项已订阅工坊模组尚未完整下载，本次不参与排序。");
        }

        if (installedNotSubscribed.Count > 0)
        {
            warnings.Add($"{installedNotSubscribed.Count} 项本地残留目录不在订阅清单中，已标注但不计入订阅统计。");
        }

        return new WorkshopScanResult(
            contentDir,
            manifest,
            manifestFound,
            subscriptions,
            subscribedNotInstalled,
            installedNotSubscribed,
            warnings);
    }

    /// <summary>由内容目录反推 <c>appworkshop_&lt;appid&gt;.acf</c> 路径。</summary>
    public static string ResolveManifestPath(string workshopContentDir)
    {
        try
        {
            var contentRoot = Directory.GetParent(workshopContentDir);
            var workshopRoot = contentRoot?.Parent;
            if (workshopRoot != null)
            {
                return Path.Combine(workshopRoot.FullName, $"appworkshop_{RimWorldAppId}.acf");
            }
        }
        catch
        {
            // 路径异常时回落到默认位置
        }

        return @"D:\Steam\steamapps\workshop\appworkshop_294100.acf";
    }

    private static HashSet<string>? TryReadSubscribedIds(string manifestPath, List<string> warnings)
    {
        var candidates = new[]
        {
            manifestPath,
            // 少数安装把订阅区块写在 appmanifest 里，顺带兜底
            Path.Combine(Path.GetDirectoryName(manifestPath) ?? string.Empty, $"appmanifest_{RimWorldAppId}.acf")
        };

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate) || !File.Exists(candidate))
                continue;

            try
            {
                var root = VdfReader.Parse(File.ReadAllText(candidate, Encoding.UTF8));
                var block = VdfReader.FindChild(root, "WorkshopItemsInstalled");
                if (block == null || block.Children.Count == 0)
                    continue;

                var ids = block.Children.Keys
                    .Where(IsPublishedFileId)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                if (ids.Count > 0)
                    return ids;
            }
            catch (Exception ex)
            {
                warnings.Add($"解析订阅清单失败（{Path.GetFileName(candidate)}）：{ex.Message}");
            }
        }

        return null;
    }

    private static bool IsPublishedFileId(string value) =>
        value.Length >= 6 && value.All(char.IsAsciiDigit);

    /// <summary>极简 Valve KeyValues（.acf/.vdf）解析器，只支持嵌套块与字符串值。</summary>
    private sealed class VdfReader
    {
        private readonly List<string> _tokens;
        private int _position;

        private VdfReader(List<string> tokens) => _tokens = tokens;

        public static VdfNode Parse(string text)
        {
            var reader = new VdfReader(Tokenize(text));
            return reader.ParseNode();
        }

        public static VdfNode? FindChild(VdfNode node, string key)
        {
            if (node.Children.TryGetValue(key, out var direct))
                return direct;

            foreach (var child in node.Children.Values)
            {
                var nested = FindChild(child, key);
                if (nested != null)
                    return nested;
            }

            return null;
        }

        private VdfNode ParseNode()
        {
            var node = new VdfNode();

            while (_position < _tokens.Count)
            {
                var token = _tokens[_position];

                if (token == "}")
                {
                    _position++;
                    return node;
                }

                if (token == "{")
                {
                    _position++;
                    continue;
                }

                var key = token;
                _position++;

                if (_position >= _tokens.Count)
                {
                    node.Children[key] = new VdfNode();
                    break;
                }

                if (_tokens[_position] == "{")
                {
                    _position++;
                    node.Children[key] = ParseNode();
                    continue;
                }

                node.Children[key] = new VdfNode { Value = _tokens[_position] };
                _position++;
            }

            return node;
        }

        private static List<string> Tokenize(string text)
        {
            var tokens = new List<string>();
            var i = 0;

            while (i < text.Length)
            {
                var c = text[i];

                if (char.IsWhiteSpace(c))
                {
                    i++;
                    continue;
                }

                if (c == '/' && i + 1 < text.Length && text[i + 1] == '/')
                {
                    while (i < text.Length && text[i] != '\n')
                        i++;
                    continue;
                }

                if (c is '{' or '}')
                {
                    tokens.Add(c.ToString());
                    i++;
                    continue;
                }

                if (c == '"')
                {
                    var sb = new StringBuilder();
                    i++;
                    while (i < text.Length && text[i] != '"')
                    {
                        if (text[i] == '\\' && i + 1 < text.Length)
                        {
                            sb.Append(text[i + 1]);
                            i += 2;
                            continue;
                        }

                        sb.Append(text[i]);
                        i++;
                    }
                    i++; // 跳过结尾引号
                    tokens.Add(sb.ToString());
                    continue;
                }

                var start = i;
                while (i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] != '{' && text[i] != '}')
                    i++;
                tokens.Add(text[start..i]);
            }

            return tokens;
        }
    }

    private sealed class VdfNode
    {
        public Dictionary<string, VdfNode> Children { get; } = new(StringComparer.OrdinalIgnoreCase);
        public string? Value { get; init; }
    }
}
