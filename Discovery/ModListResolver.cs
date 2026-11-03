using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;
using ModSorterAgent.Models;
using ModSorterAgent.Services;

namespace ModSorterAgent.Discovery;

public sealed class ModListResolver
{
    /// <summary>
    /// 路径解析入口：用户设置（AppSettings.json）→ 自动检测 → 硬编码默认。
    /// 界面层启动时调用一次 <see cref="PathOptions.Reload"/>；直接消费静态属性即可拿到当前生效路径。
    /// </summary>
    public static class PathOptions
    {
        private static readonly SettingsStore Store = new();

        /// <summary>当前生效的 RimWorld 安装目录（含 RimWorldWin64.exe / Data / Mods）。</summary>
        public static string RimWorldInstallDir { get; private set; } = DefaultRimWorldInstallDir;

        /// <summary>当前生效的创意工坊内容目录（…\steamapps\workshop\content\294100）。</summary>
        public static string WorkshopDir { get; private set; } = DefaultWorkshopDir;

        /// <summary>当前生效的本地模组工作区目录。</summary>
        public static string WorkspaceDir { get; private set; } = DefaultWorkspaceDir;

        /// <summary>用户显式设置过的路径（空 = 该项未覆盖，走检测/默认）。</summary>
        public static AppSettings Settings => Store.Current;

        /// <summary>
        /// 重新读取设置并解析生效路径。设置对话框保存后调用；启动时也会调用一次。
        /// 返回本次实际生效的路径三元组（供状态栏展示）。
        /// </summary>
        public static (string InstallDir, string WorkshopDir, string WorkspaceDir) Reload()
        {
            var s = Store.Current;

            var install = !string.IsNullOrWhiteSpace(s.GameInstallDir) && Directory.Exists(s.GameInstallDir)
                ? s.GameInstallDir
                : PathAutoDetector.DetectGameInstallDir() ?? DefaultRimWorldInstallDir;

            var workshop = !string.IsNullOrWhiteSpace(s.WorkshopContentDir) && Directory.Exists(s.WorkshopContentDir)
                ? s.WorkshopContentDir
                : PathAutoDetector.DetectWorkshopContentDir() ?? DefaultWorkshopDir;

            var workspace = !string.IsNullOrWhiteSpace(s.LocalModWorkspaceDir) && Directory.Exists(s.LocalModWorkspaceDir)
                ? s.LocalModWorkspaceDir
                : DefaultWorkspaceDir;

            RimWorldInstallDir = install;
            WorkshopDir = workshop;
            WorkspaceDir = workspace;

            return (install, workshop, workspace);
        }

        /// <summary>把用户设置立即持久化并刷新生效路径。</summary>
        public static void Update(AppSettings settings)
        {
            Store.Save(settings);
            Reload();
        }

        /// <summary>
        /// 进程内纠偏（RimWorld 模组运行时用）：游戏进程自己就握有安装目录与工坊目录，
        /// 采信调用方传来的值并覆盖自动检测结果。只改本进程静态属性，不写盘、不改 AppSettings.json。
        /// </summary>
        public static void ApplyRuntimePaths(string? installDir, string? workshopDir)
        {
            if (!string.IsNullOrWhiteSpace(installDir) &&
                Directory.Exists(Path.Combine(installDir!, "Data")))
            {
                RimWorldInstallDir = installDir!;
            }

            if (!string.IsNullOrWhiteSpace(workshopDir) && Directory.Exists(workshopDir!))
            {
                WorkshopDir = workshopDir!;
            }
        }
    }

    public static string DefaultModsConfigPath => ResolveDefaultModsConfigPath();

    /// <summary>自动检测的 RimWorld 安装目录；未检测到为空，需在设置中指定。</summary>
    public static string DefaultRimWorldInstallDir { get; } =
        PathAutoDetector.DetectGameInstallDir() ?? string.Empty;

    /// <summary>自动检测的创意工坊内容目录；未检测到为空，需在设置中指定。</summary>
    public static string DefaultWorkshopDir { get; } =
        PathAutoDetector.DetectWorkshopContentDir() ?? string.Empty;

    /// <summary>本地模组工作区没有通用位置，留空表示不扫描。</summary>
    public static string DefaultWorkspaceDir => string.Empty;

    private static readonly HashSet<string> OfficialPackageIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "ludeon.rimworld",
        "ludeon.rimworld.royalty",
        "ludeon.rimworld.ideology",
        "ludeon.rimworld.biotech",
        "ludeon.rimworld.anomaly",
        "ludeon.rimworld.odyssey"
    };

    /// <summary>
    /// 官方内容的权威加载次序（Core 最前，DLC 按发布顺序）。
    /// 官方 DLC 之间只用 forceLoadAfter/forceLoadBefore 表达先后、且不带 modDependencies，
    /// 因此不能用"依赖数"推断，必须用这份固定事实。
    /// </summary>
    private static readonly string[] OfficialLoadOrder =
    {
        "ludeon.rimworld",
        "ludeon.rimworld.royalty",
        "ludeon.rimworld.ideology",
        "ludeon.rimworld.biotech",
        "ludeon.rimworld.anomaly",
        "ludeon.rimworld.odyssey"
    };

    /// <summary>
    /// 官方包在权威加载次序中的位次；非官方包返回 -1。
    /// </summary>
    public static int GetOfficialLoadOrderRank(string packageId)
    {
        var norm = StripSteamSuffix(NormalizePackageId(packageId));
        for (var i = 0; i < OfficialLoadOrder.Length; i++)
        {
            if (OfficialLoadOrder[i].Equals(norm, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    /// <summary>官方权威加载次序的条目数，用于给未登记在册的官方包兜底排序。</summary>
    public static int OfficialLoadOrderCount => OfficialLoadOrder.Length;

    private static readonly HashSet<string> ExcludedWorkspaceFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "rimdevtools",
        "GABS",
        "RimWorld",
        "Rimfx"
    };

    private readonly List<ModCandidateEntry> _catalog = new();

    private sealed record ModCandidateEntry(
        string BasePackageId,
        string SteamPackageId,
        string Name,
        string FolderName,
        string RootDirectory,
        bool IsWorkshop,
        bool IsOfficial,
        XDocument AboutDocument);

    public ModListResolver(IEnumerable<string>? customSearchRoots = null)
    {
        BuildCatalog(customSearchRoots);
    }

    private static string ResolveDefaultModsConfigPath()
    {
        // 1. 用户显式设置优先
        var configured = PathOptions.Settings.ModsConfigPath;
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        // 2. 当前用户的 LocalLow 标准位置
        var detected = PathAutoDetector.DetectModsConfigPath();
        if (!string.IsNullOrWhiteSpace(detected))
            return detected;

        // 3. 文件尚不存在时仍返回当前用户的标准位置，供界面提示与后续创建使用。
        return PathAutoDetector.GetStandardModsConfigPath() ?? "ModsConfig.xml";
    }

    public (string GameVersion, List<string> RequestedTokens, List<string> KnownExpansions) ParseModsConfigFile(string modsConfigPath)
    {
        if (!File.Exists(modsConfigPath))
            throw new FileNotFoundException($"未找到 ModsConfig.xml 配置文件: {modsConfigPath}");

        var doc = XDocument.Load(modsConfigPath);
        var root = doc.Root ?? throw new InvalidOperationException("ModsConfig.xml 格式无效：缺少根节点。");

        var versionText = root.Element("version")?.Value?.Trim() ?? "1.7";
        var activeMods = root.Element("activeMods")?
            .Elements("li")
            .Select(e => e.Value.Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToList() ?? new List<string>();

        var knownExpansions = root.Element("knownExpansions")?
            .Elements("li")
            .Select(e => e.Value.Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToList() ?? new List<string>();

        return (versionText, activeMods, knownExpansions);
    }

    public List<string> ParseInputFileTokens(string inputFilePath, out string? detectedVersion)
    {
        detectedVersion = null;
        if (!File.Exists(inputFilePath))
            throw new FileNotFoundException($"未找到导入文件: {inputFilePath}");

        var content = File.ReadAllText(inputFilePath).Trim();

        // RimSort / Mod Manager 的保存列表（.rml）优先按专用格式解析
        if (RimSortListFile.LooksLikeSavedModList(inputFilePath, content))
        {
            var saved = RimSortListFile.Load(inputFilePath);
            detectedVersion = saved.GameVersion;
            return saved.PackageIds.ToList();
        }

        if (content.StartsWith("<", StringComparison.Ordinal))
        {
            var (ver, mods, _) = ParseModsConfigFile(inputFilePath);
            detectedVersion = ver;
            return mods;
        }

        if (content.StartsWith("[", StringComparison.Ordinal))
        {
            var arr = JsonSerializer.Deserialize<List<string>>(content) ?? new List<string>();
            return arr.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList();
        }

        return content
            .Split(new[] { '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("#", StringComparison.Ordinal))
            .ToList();
    }

    public (List<ModDescriptor> ResolvedMods, List<string> UnresolvedTokens) ResolveModList(
        IReadOnlyList<string> requestedTokens,
        string gameVersion = "1.7")
    {
        var (resolved, details) = ResolveModListDetailed(requestedTokens, gameVersion);
        return (resolved, details.Select(d => d.Token).ToList());
    }

    /// <summary>
    /// 同 <see cref="ResolveModList"/>，但把每一条没解析成的 token 连同原因一起返回，
    /// 供调用方登记成错误清单（排查用），而不是静默丢掉。
    /// </summary>
    public (List<ModDescriptor> ResolvedMods, List<UnresolvedModToken> UnresolvedDetails) ResolveModListDetailed(
        IReadOnlyList<string> requestedTokens,
        string gameVersion = "1.7")
    {
        var candidates = new List<(string RequestedToken, ModCandidateEntry Entry, int OriginalIndex)>();
        var unresolved = new List<UnresolvedModToken>();

        for (var i = 0; i < requestedTokens.Count; i++)
        {
            var token = requestedTokens[i].Trim();
            if (string.IsNullOrWhiteSpace(token))
                continue;

            var matched = MatchToken(token);
            if (matched == null)
            {
                unresolved.Add(DiagnoseUnresolved(token));
                continue;
            }

            candidates.Add((token, matched, i));
        }

        var activePackageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in candidates)
        {
            activePackageIds.Add(item.Entry.BasePackageId);
            activePackageIds.Add(item.Entry.SteamPackageId);
            activePackageIds.Add(NormalizePackageId(item.RequestedToken));
        }

        var resolved = new List<ModDescriptor>();
        var seenPackageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ownerOfPackageId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in candidates)
        {
            var effectivePackageId = item.RequestedToken.EndsWith("_steam", StringComparison.OrdinalIgnoreCase)
                ? item.Entry.SteamPackageId
                : item.Entry.BasePackageId;

            // 同一物理模组可能以 base / _steam 两种 token 形态出现（如 RimSort 导出列表混用
            // steam id 与本地 packageId）。两种形态都登记，首个到达的 token 锁定该模组，
            // 避免同一模组以两个不同 PackageId 重复进入排序。
            // 注意：本模组自身的三个 id（effective / base / steam）之间可能相等，
            // 必须先在集合内去重，再向 seenPackageIds 登记，否则自重复会被误判为「已锁定」。
            var idsToRegister = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                effectivePackageId,
                item.Entry.BasePackageId,
                item.Entry.SteamPackageId
            };
            var collidedId = idsToRegister.FirstOrDefault(id => seenPackageIds.Contains(id));
            if (collidedId != null)
            {
                // 同名双份（本地 + 工坊各一份）是真实会发生的，跳过可以，但必须留痕。
                var owner = ownerOfPackageId.TryGetValue(collidedId, out var who) ? who : collidedId;
                unresolved.Add(new UnresolvedModToken(
                    item.RequestedToken,
                    $"与已解析的「{owner}」packageId 相同，未重复纳入排序",
                    "本地与工坊可能各装了一份同名模组，建议只保留其中一份",
                    item.Entry.RootDirectory));
                continue;
            }

            foreach (var id in idsToRegister)
                seenPackageIds.Add(id);

            var descriptor = BuildDescriptor(item.Entry, effectivePackageId, item.OriginalIndex, gameVersion, activePackageIds);
            resolved.Add(descriptor);

            if (!ownerOfPackageId.ContainsKey(item.Entry.BasePackageId))
                ownerOfPackageId[item.Entry.BasePackageId] = descriptor.Name;
        }

        return (resolved, unresolved);
    }

    /// <summary>一条没能解析到物理目录的 token，附带可直接排查的原因与处置建议。</summary>
    public sealed record UnresolvedModToken(
        string Token,
        string Reason,
        string SuggestedFix,
        string? MatchedRootDirectory = null);

    private UnresolvedModToken DiagnoseUnresolved(string token)
    {
        var emptyCatalog = _catalog.Count == 0;
        if (emptyCatalog)
        {
            return new UnresolvedModToken(
                token,
                "模组扫描根全部不存在，任何模组都读不到",
                "检查游戏安装目录与工坊目录：Data / Mods / steamapps\\workshop\\content\\294100 是否都在扫描范围内");
        }

        if (token.IndexOfAny(new[] { '\\', '/' }) >= 0 || token.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
        {
            return new UnresolvedModToken(
                token,
                "给出的路径不是有效的模组目录（目录不存在或缺少 About.xml）",
                "确认该文件夹还在原位置，且内部有 About\\About.xml");
        }

        var byName = _catalog.FirstOrDefault(c =>
            c.Name.Equals(token, StringComparison.OrdinalIgnoreCase) ||
            c.FolderName.Equals(token, StringComparison.OrdinalIgnoreCase));
        if (byName != null)
        {
            return new UnresolvedModToken(
                token,
                $"磁盘上有同名模组「{byName.Name}」，但它 About.xml 里的 packageId 是 {byName.BasePackageId}",
                "确认 ModsConfig 记录的 ID 与 About.xml 里的 packageId 一致（改过 About.xml 或换过来源会不一致）",
                byName.RootDirectory);
        }

        var stripped = StripSteamSuffix(NormalizePackageId(token));
        var similar = _catalog.FirstOrDefault(c =>
            c.BasePackageId.Equals(stripped, StringComparison.OrdinalIgnoreCase) ||
            c.BasePackageId.Contains(stripped, StringComparison.OrdinalIgnoreCase) ||
            stripped.Contains(c.BasePackageId, StringComparison.OrdinalIgnoreCase));
        if (similar != null)
        {
            return new UnresolvedModToken(
                token,
                $"疑似对应「{similar.Name}」（{similar.BasePackageId}），但标识不完全一致",
                "ID 里可能有错字、多余空格或版本后缀差异，核对后修正列表来源",
                similar.RootDirectory);
        }

        return new UnresolvedModToken(
            token,
            "磁盘上找不到对应目录（工坊未下载完成、压缩包解压层级不对，或装在未被扫描的位置）",
            "确认模组已完整下载/解压，且位于 游戏目录\\Mods 或 工坊 294100 下");
    }

    public static string NormalizePackageId(string rawId)
    {
        return rawId.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// 把「已订阅但未启用」的工坊模组解析成待排序描述符。
    /// 与启用列表里的模组一视同仁：同样解析 About / LoadFolders / 程序集，进入全量深度分析。
    /// </summary>
    public (List<ModDescriptor> Mods, List<string> UnresolvedIds) ResolveWorkshopPool(
        IReadOnlyList<string> publishedFileIds,
        string gameVersion,
        IReadOnlyCollection<ModDescriptor> activeMods)
    {
        var activeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var alreadyPresent = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var mod in activeMods)
        {
            activeIds.Add(NormalizePackageId(mod.PackageId));
            activeIds.Add(StripSteamSuffix(mod.PackageId));
            alreadyPresent.Add(NormalizePackageId(mod.PackageId));
            alreadyPresent.Add(StripSteamSuffix(mod.PackageId));
        }

        var workshopEntries = _catalog
            .Where(entry => entry.IsWorkshop)
            .GroupBy(entry => entry.FolderName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var resolved = new List<ModDescriptor>();
        var unresolved = new List<string>();
        var originalIndex = 10_000; // 订阅池没有"原始顺位"，排在启用列表之后作为稳定次序

        foreach (var id in publishedFileIds
                     .Where(id => !string.IsNullOrWhiteSpace(id))
                     .Select(id => id.Trim())
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(id => id, StringComparer.Ordinal))
        {
            if (!workshopEntries.TryGetValue(id, out var entry))
            {
                unresolved.Add(id);
                continue;
            }

            if (alreadyPresent.Contains(entry.BasePackageId) || alreadyPresent.Contains(entry.SteamPackageId))
                continue;

            alreadyPresent.Add(entry.BasePackageId);
            alreadyPresent.Add(entry.SteamPackageId);

            resolved.Add(BuildDescriptor(
                entry,
                entry.BasePackageId,
                originalIndex++,
                gameVersion,
                activeIds,
                isActive: false,
                workshopPublishedFileId: id));
        }

        return (resolved, unresolved);
    }

    public static string StripSteamSuffix(string packageId)
    {
        var norm = NormalizePackageId(packageId);
        return norm.EndsWith("_steam", StringComparison.Ordinal)
            ? norm[..^6]
            : norm;
    }

    /// <summary>
    /// 获取当前扫描到的官方 DLC 扩展包列表（按官方默认顺序排布，不含 Core）。
    /// </summary>
    public List<string> GetInstalledOfficialExpansions()
    {
        var list = new List<string>();
        foreach (var id in OfficialPackageIds)
        {
            if (id.Equals("ludeon.rimworld", StringComparison.OrdinalIgnoreCase))
                continue;
            if (_catalog.Any(c => c.IsOfficial && c.BasePackageId.Equals(id, StringComparison.OrdinalIgnoreCase)))
                list.Add(id);
        }
        return list;
    }

    /// <summary>
    /// 获取本地模组列表 Token：官方 Core 与已安装 DLC + 本地 Mods 目录及工作区的非工坊模组。
    /// </summary>
    public List<string> GetLocalModTokens()
    {
        var tokens = new List<string> { "ludeon.rimworld" };
        foreach (var exp in GetInstalledOfficialExpansions())
        {
            tokens.Add(exp);
        }

        var seenPackageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in tokens)
        {
            seenPackageIds.Add(NormalizePackageId(t));
            seenPackageIds.Add(StripSteamSuffix(t));
        }

        var localEntries = _catalog
            .Where(c => !c.IsWorkshop && !c.IsOfficial)
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var entry in localEntries)
        {
            if (seenPackageIds.Add(entry.BasePackageId))
            {
                tokens.Add(entry.BasePackageId);
            }
        }

        return tokens;
    }

    /// <summary>
    /// 获取创意工坊已订阅模组 Token：官方 Core 与已安装 DLC + Steam 创意工坊已订阅且本地已下载的模组。
    /// </summary>
    public List<string> GetWorkshopSubscribedTokens()
    {
        var tokens = new List<string> { "ludeon.rimworld" };
        foreach (var exp in GetInstalledOfficialExpansions())
        {
            tokens.Add(exp);
        }

        var scan = WorkshopSubscriptionScanner.Scan();
        var targetIds = new HashSet<string>(
            scan.Subscriptions.Where(s => s.IsSubscribed && s.HasAboutFile).Select(s => s.PublishedFileId),
            StringComparer.OrdinalIgnoreCase);

        if (targetIds.Count == 0)
        {
            foreach (var s in scan.Subscriptions.Where(s => s.HasAboutFile))
                targetIds.Add(s.PublishedFileId);
        }

        var seenPackageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in tokens)
        {
            seenPackageIds.Add(NormalizePackageId(t));
            seenPackageIds.Add(StripSteamSuffix(t));
        }

        var workshopEntries = _catalog
            .Where(c => c.IsWorkshop && targetIds.Contains(c.FolderName))
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var entry in workshopEntries)
        {
            if (seenPackageIds.Add(entry.BasePackageId))
            {
                tokens.Add(entry.SteamPackageId);
            }
        }

        return tokens;
    }

    /// <summary>
    /// 启动 RimWorld 游戏进程。优先检查是否已在运行；若未运行，启动可执行文件；若不存在可执行文件，通过 Steam 协议唤起。
    /// </summary>
    public static (bool Success, string Message) LaunchGame()
    {
        try
        {
            var running = Process.GetProcessesByName("RimWorldWin64")
                .Concat(Process.GetProcessesByName("RimWorld"))
                .FirstOrDefault();

            if (running != null)
            {
                return (true, "RimWorld 已经在运行中。");
            }

            var installDir = PathOptions.RimWorldInstallDir;
            var exe64 = Path.Combine(installDir, "RimWorldWin64.exe");
            var exe = Path.Combine(installDir, "RimWorld.exe");
            var targetExe = File.Exists(exe64) ? exe64 : (File.Exists(exe) ? exe : null);

            if (targetExe != null)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = targetExe,
                    WorkingDirectory = installDir,
                    UseShellExecute = true
                });
                return (true, $"已启动游戏：{Path.GetFileName(targetExe)}");
            }
            else
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "steam://rungameid/294100",
                    UseShellExecute = true
                });
                return (true, "已通过 Steam 协议发起启动：steam://rungameid/294100");
            }
        }
        catch (Exception ex)
        {
            return (false, $"启动游戏失败：{ex.Message}");
        }
    }

    private void BuildCatalog(IEnumerable<string>? customSearchRoots)
    {
        var roots = new List<(string Path, bool IsWorkshop, bool IsOfficial)>();

        var dataDir = Path.Combine(PathOptions.RimWorldInstallDir, "Data");
        var modsDir = Path.Combine(PathOptions.RimWorldInstallDir, "Mods");

        if (Directory.Exists(dataDir))
            roots.Add((dataDir, false, true));

        if (Directory.Exists(modsDir))
            roots.Add((modsDir, false, false));

        if (Directory.Exists(PathOptions.WorkshopDir))
            roots.Add((PathOptions.WorkshopDir, true, false));

        if (Directory.Exists(PathOptions.WorkspaceDir))
            roots.Add((PathOptions.WorkspaceDir, false, false));

        if (customSearchRoots != null)
        {
            foreach (var r in customSearchRoots)
            {
                if (Directory.Exists(r))
                    roots.Add((r, false, false));
            }
        }

        var seenDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (rootPath, isWorkshop, isOfficialRoot) in roots)
        {
            IEnumerable<string> subDirs;
            try
            {
                subDirs = Directory.EnumerateDirectories(rootPath);
            }
            catch
            {
                continue;
            }

            foreach (var modDir in subDirs)
            {
                var fullModDir = Path.GetFullPath(modDir);
                if (!seenDirs.Add(fullModDir))
                    continue;

                var folderName = Path.GetFileName(fullModDir);
                if (rootPath.Equals(PathOptions.WorkspaceDir, StringComparison.OrdinalIgnoreCase) &&
                    ExcludedWorkspaceFolders.Contains(folderName))
                {
                    continue;
                }

                var entry = TryReadModCandidate(fullModDir, isWorkshop, isOfficialRoot);
                if (entry != null)
                {
                    _catalog.Add(entry);
                }
            }
        }
    }

    private static ModCandidateEntry? TryReadModCandidate(string modDir, bool isWorkshop, bool isOfficialRoot)
    {
        var aboutPath = Path.Combine(modDir, "About", "About.xml");
        if (!File.Exists(aboutPath))
            return null;

        try
        {
            var doc = XDocument.Load(aboutPath);
            var root = doc.Root;
            if (root == null)
                return null;

            var folderName = Path.GetFileName(modDir);
            var name = GetElementValueIgnoreCase(root, "name") ?? folderName;
            var author = GetElementValueIgnoreCase(root, "author") ?? "unknown";
            var rawPackageId = GetElementValueIgnoreCase(root, "packageId");

            if (string.IsNullOrWhiteSpace(rawPackageId))
            {
                if (isOfficialRoot)
                {
                    rawPackageId = folderName.Equals("Core", StringComparison.OrdinalIgnoreCase)
                        ? "ludeon.rimworld"
                        : $"ludeon.rimworld.{folderName.ToLowerInvariant()}";
                }
                else
                {
                    rawPackageId = $"{SanitizeIdPart(author)}.{SanitizeIdPart(name)}";
                }
            }

            var baseId = StripSteamSuffix(rawPackageId);
            var steamId = $"{baseId}_steam";
            var isOfficial = isOfficialRoot || OfficialPackageIds.Contains(baseId);

            return new ModCandidateEntry(
                BasePackageId: baseId,
                SteamPackageId: steamId,
                Name: name.Trim(),
                FolderName: folderName,
                RootDirectory: modDir,
                IsWorkshop: isWorkshop,
                IsOfficial: isOfficial,
                AboutDocument: doc);
        }
        catch
        {
            return null;
        }
    }

    private ModCandidateEntry? MatchToken(string token)
    {
        if (Directory.Exists(token))
        {
            var fullDir = Path.GetFullPath(token);
            var existing = _catalog.FirstOrDefault(c => c.RootDirectory.Equals(fullDir, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
                return existing;

            var direct = TryReadModCandidate(fullDir, false, false);
            if (direct != null)
            {
                _catalog.Add(direct);
                return direct;
            }
        }

        var normalized = NormalizePackageId(token);
        var wantsSteam = normalized.EndsWith("_steam", StringComparison.Ordinal);
        var stripped = StripSteamSuffix(normalized);

        if (wantsSteam)
        {
            var workshopHit = _catalog.FirstOrDefault(c =>
                c.IsWorkshop && c.BasePackageId.Equals(stripped, StringComparison.OrdinalIgnoreCase));
            if (workshopHit != null)
                return workshopHit;
        }

        var installRoot = PathOptions.RimWorldInstallDir;
        var localNonWorkshopHit = _catalog.FirstOrDefault(c =>
            !c.IsWorkshop &&
            !string.IsNullOrWhiteSpace(installRoot) &&
            c.RootDirectory.StartsWith(installRoot, StringComparison.OrdinalIgnoreCase) &&
            c.BasePackageId.Equals(stripped, StringComparison.OrdinalIgnoreCase));
        if (localNonWorkshopHit != null)
            return localNonWorkshopHit;

        var anyPackageHit = _catalog.FirstOrDefault(c =>
            c.BasePackageId.Equals(stripped, StringComparison.OrdinalIgnoreCase) ||
            c.SteamPackageId.Equals(normalized, StringComparison.OrdinalIgnoreCase));
        if (anyPackageHit != null)
            return anyPackageHit;

        var exactNameHit = _catalog.FirstOrDefault(c =>
            c.Name.Equals(token, StringComparison.OrdinalIgnoreCase) ||
            c.FolderName.Equals(token, StringComparison.OrdinalIgnoreCase));
        if (exactNameHit != null)
            return exactNameHit;

        if (stripped.Length < 4)
            return null;

        // packageId 形态的标识（形如 author.modid）精确匹配失败，说明本机没装这个模组。
        // 绝不能退化成"子串包含"去匹配别的模组：那会把 zh.xxx 顶替成 xxx，凭空打乱用户给定的顺序。
        var looksLikePackageId = stripped.Contains('.') &&
                                 stripped.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '.' or '_' or '-');
        if (looksLikePackageId)
            return null;

        return _catalog
            .Where(c => c.Name.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                        c.BasePackageId.Contains(stripped, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(c => c.BasePackageId.EndsWith($".{stripped}", StringComparison.OrdinalIgnoreCase))
            .ThenBy(c => c.Name.Length)
            .FirstOrDefault();
    }

    private static ModDescriptor BuildDescriptor(
        ModCandidateEntry entry,
        string effectivePackageId,
        int originalIndex,
        string gameVersion,
        HashSet<string> activePackageIds,
        bool discoverAssemblies = true,
        bool isActive = true,
        string? workshopPublishedFileId = null)
    {
        var root = entry.AboutDocument.Root!;
        var author = GetElementValueIgnoreCase(root, "author");
        var supportedVersions = ExtractStringListByTag(root, "supportedVersions");

        var dependencies = ExtractDependencies(root, gameVersion);
        var loadAfter = MergeVersionedStringLists(root, "loadAfter", "loadAfterByVersion", gameVersion);
        var loadBefore = MergeVersionedStringLists(root, "loadBefore", "loadBeforeByVersion", gameVersion);
        var forceLoadAfter = ExtractStringListByTag(root, "forceLoadAfter").Select(NormalizePackageId).ToList();
        var forceLoadBefore = ExtractStringListByTag(root, "forceLoadBefore").Select(NormalizePackageId).ToList();
        var incompatibleWith = MergeVersionedStringLists(root, "incompatibleWith", "incompatibleWithByVersion", gameVersion);

        var contentDirs = ResolveActiveContentDirectories(entry.RootDirectory, gameVersion, activePackageIds, out var conditionalMods);
        var assemblyPaths = discoverAssemblies ? DiscoverAssemblies(contentDirs) : new List<string>();

        return new ModDescriptor
        {
            PackageId = effectivePackageId,
            Name = entry.Name,
            RootDirectory = entry.RootDirectory,
            Author = author,
            OriginalIndex = originalIndex,
            IsOfficialCoreOrDlc = entry.IsOfficial,
            IsWorkshopMod = entry.IsWorkshop,
            IsActiveInModsConfig = isActive,
            WorkshopPublishedFileId = workshopPublishedFileId,
            SupportedVersions = supportedVersions,
            Dependencies = dependencies,
            LoadAfter = loadAfter,
            LoadBefore = loadBefore,
            ForceLoadAfter = forceLoadAfter,
            ForceLoadBefore = forceLoadBefore,
            IncompatibleWith = incompatibleWith,
            ConditionalLoadFolderMods = conditionalMods,
            ActiveContentDirectories = contentDirs,
            AssemblyPaths = assemblyPaths
        };
    }

    private static List<string> ResolveActiveContentDirectories(
        string modRoot,
        string gameVersion,
        HashSet<string> activePackageIds,
        out List<string> matchedConditionalMods)
    {
        var result = new List<string>();
        var condMods = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var loadFoldersPath = Path.Combine(modRoot, "LoadFolders.xml");
        var majorMinor = ExtractMajorMinorVersion(gameVersion);

        if (File.Exists(loadFoldersPath))
        {
            try
            {
                var doc = XDocument.Load(loadFoldersPath);
                var root = doc.Root;
                if (root != null)
                {
                    var versionNode = FindBestVersionElement(root, majorMinor);
                    if (versionNode != null)
                    {
                        foreach (var li in versionNode.Elements("li"))
                        {
                            if (!ShouldIncludeConditionalLoadFolder(li, activePackageIds, condMods))
                                continue;

                            var rel = (li.Value ?? string.Empty).Trim();
                            string fullDir;
                            if (string.IsNullOrEmpty(rel) || rel == "/" || rel == "\\" || rel == ".")
                            {
                                fullDir = modRoot;
                            }
                            else
                            {
                                var cleaned = rel.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
                                fullDir = Path.Combine(modRoot, cleaned);
                            }

                            if (Directory.Exists(fullDir) && !result.Contains(fullDir, StringComparer.OrdinalIgnoreCase))
                            {
                                result.Add(fullDir);
                            }
                        }
                    }
                }
            }
            catch
            {
                // Fallback to default folder discovery below
            }
        }

        if (result.Count == 0)
        {
            result.Add(modRoot);
            var commonDir = Path.Combine(modRoot, "Common");
            if (Directory.Exists(commonDir))
                result.Add(commonDir);

            var candidateVersions = new[] { majorMinor, $"v{majorMinor}", "1.7", "v1.7", "1.6", "v1.6", "1.5", "v1.5" };
            foreach (var verFolder in candidateVersions.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var verDir = Path.Combine(modRoot, verFolder);
                if (Directory.Exists(verDir))
                {
                    result.Add(verDir);
                    break;
                }
            }
        }

        matchedConditionalMods = condMods.ToList();
        return result;
    }

    private static bool ShouldIncludeConditionalLoadFolder(
        XElement li,
        HashSet<string> activePackageIds,
        HashSet<string> matchedConditionalMods)
    {
        var ifActive = li.Attribute("IfModActive")?.Value ?? li.Attribute("ifModActive")?.Value;
        if (!string.IsNullOrWhiteSpace(ifActive))
        {
            var matched = ifActive
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => NormalizePackageId(s.Trim()))
                .Where(id => activePackageIds.Contains(id) ||
                             activePackageIds.Contains(StripSteamSuffix(id)))
                .ToList();
            if (matched.Count == 0)
                return false;

            foreach (var m in matched)
                matchedConditionalMods.Add(m);
        }

        var ifNotActive = li.Attribute("IfModNotActive")?.Value ?? li.Attribute("ifModNotActive")?.Value;
        if (!string.IsNullOrWhiteSpace(ifNotActive))
        {
            var forbiddenAny = ifNotActive
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Any(id => activePackageIds.Contains(NormalizePackageId(id)) ||
                           activePackageIds.Contains(StripSteamSuffix(id)));
            if (forbiddenAny)
                return false;
        }

        return true;
    }

    private static XElement? FindBestVersionElement(XElement loadFoldersRoot, string majorMinor)
    {
        var preferredNames = new[]
        {
            $"v{majorMinor}",
            majorMinor,
            "v1.7",
            "1.7",
            "v1.6",
            "1.6",
            "default",
            "v1.5",
            "1.5"
        };

        foreach (var candidateName in preferredNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var match = loadFoldersRoot.Elements()
                .FirstOrDefault(e => e.Name.LocalName.Equals(candidateName, StringComparison.OrdinalIgnoreCase));
            if (match != null && match.Elements("li").Any())
                return match;
        }

        return loadFoldersRoot.Elements().LastOrDefault(e => e.Elements("li").Any());
    }

    private static List<string> DiscoverAssemblies(IReadOnlyList<string> activeContentDirs)
    {
        var dllByFileName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var contentDir in activeContentDirs)
        {
            var assembliesDir = Path.Combine(contentDir, "Assemblies");
            if (!Directory.Exists(assembliesDir))
                continue;

            foreach (var dllPath in Directory.EnumerateFiles(assembliesDir, "*.dll", SearchOption.TopDirectoryOnly))
            {
                var fileName = Path.GetFileName(dllPath);
                if (fileName.Equals("0Harmony.dll", StringComparison.OrdinalIgnoreCase) &&
                    !IsHarmonyRuntimeHostDirectory(contentDir))
                {
                    continue;
                }

                dllByFileName[fileName] = dllPath;
            }
        }

        return dllByFileName.Values.ToList();
    }

    /// <summary>
    /// 是否为 Harmony 运行时宿主目录：目录段精确等于 Harmony（本地安装名）
    /// 或 2009463077（工坊发布 ID）。不能用子串匹配——
    /// 目录名碰巧含 "Harmony" 的普通模组（如 MyHarmonyExpanded）会被误判为运行时宿主。
    /// </summary>
    private static bool IsHarmonyRuntimeHostDirectory(string contentDir)
    {
        foreach (var segment in contentDir.Split(
                     Path.DirectorySeparatorChar,
                     Path.AltDirectorySeparatorChar))
        {
            if (segment.Equals("Harmony", StringComparison.OrdinalIgnoreCase) ||
                segment.Equals("2009463077", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    internal static List<ModDependencyItem> ExtractDependencies(XElement aboutRoot, string gameVersion)
    {
        var list = new List<ModDependencyItem>();
        var majorMinor = ExtractMajorMinorVersion(gameVersion);

        var baseDeps = GetElementIgnoreCase(aboutRoot, "modDependencies");
        var byVersion = GetElementIgnoreCase(aboutRoot, "modDependenciesByVersion");
        var selected = FindExactVersionElement(byVersion, majorMinor) ?? baseDeps;
        var depNodes = selected?.Elements()
            .Where(e => e.Name.LocalName.Equals("li", StringComparison.OrdinalIgnoreCase))
            ?? Enumerable.Empty<XElement>();

        foreach (var li in depNodes)
        {
            var pkgId = GetElementValueIgnoreCase(li, "packageId");
            if (string.IsNullOrWhiteSpace(pkgId))
                continue;

            var displayName = GetElementValueIgnoreCase(li, "displayName");
            var normalizedId = NormalizePackageId(pkgId);
            var altPackageIds = ExtractStringListByTag(li, "alternativePackageIds")
                .Select(NormalizePackageId)
                .Where(alt => !alt.Equals(normalizedId, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var existing = list.FirstOrDefault(d => d.PackageId.Equals(normalizedId, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                list.Add(new ModDependencyItem(normalizedId, displayName, altPackageIds));
            }
            else
            {
                var mergedAlts = existing.AlternativePackageIds
                    .Concat(altPackageIds)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                var idx = list.IndexOf(existing);
                list[idx] = existing with
                {
                    DisplayName = existing.DisplayName ?? displayName,
                    AlternativePackageIds = mergedAlts
                };
            }
        }

        return list;
    }

    private static List<string> MergeVersionedStringLists(
        XElement aboutRoot,
        string baseTag,
        string versionedTag,
        string gameVersion)
    {
        var selected = FindExactVersionElement(GetElementIgnoreCase(aboutRoot, versionedTag), ExtractMajorMinorVersion(gameVersion))
            ?? GetElementIgnoreCase(aboutRoot, baseTag);
        return (selected?.Elements() ?? Enumerable.Empty<XElement>())
            .Where(e => e.Name.LocalName.Equals("li", StringComparison.OrdinalIgnoreCase))
            .Select(e => e.Value.Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(NormalizePackageId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static XElement? FindExactVersionElement(XElement? root, string version)
    {
        return root?.Elements().FirstOrDefault(e =>
        {
            var key = e.Name.LocalName;
            if (key.StartsWith('v') || key.StartsWith('V'))
                key = key[1..];
            return key.Equals(version, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static List<string> ExtractStringListByTag(XElement parent, string tagName)
    {
        var elem = GetElementIgnoreCase(parent, tagName);
        if (elem == null)
            return new List<string>();

        var liElements = elem.Elements()
            .Where(e => e.Name.LocalName.Equals("li", StringComparison.OrdinalIgnoreCase))
            .Select(e => e.Value.Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToList();

        return liElements;
    }

    private static XElement? GetElementIgnoreCase(XElement parent, string localName)
    {
        return parent.Elements()
            .FirstOrDefault(e => e.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase));
    }

    private static string? GetElementValueIgnoreCase(XElement parent, string localName)
    {
        return GetElementIgnoreCase(parent, localName)?.Value?.Trim();
    }

    private static string ExtractMajorMinorVersion(string gameVersion)
    {
        var cleaned = gameVersion.Trim().TrimStart('v', 'V');
        var parts = cleaned.Split('.', ' ', '-');
        if (parts.Length >= 2 && int.TryParse(parts[0], out _) && int.TryParse(parts[1], out _))
        {
            return $"{parts[0]}.{parts[1]}";
        }
        return "1.7";
    }

    private static string SanitizeIdPart(string raw)
    {
        var chars = raw.Where(c => char.IsLetterOrDigit(c)).ToArray();
        return chars.Length == 0 ? "mod" : new string(chars).ToLowerInvariant();
    }
}
