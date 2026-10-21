using System.Text.RegularExpressions;
using System.Xml.Linq;
using ModSorterAgent.Models;

namespace ModSorterAgent.Analysis;

public enum GameLogDefErrorKind
{
    /// <summary>Could not resolve cross-reference: No Verse.DamageDef named X found…</summary>
    CrossReference = 1,

    /// <summary>Config error in MonolynWall: has multiple damage multipliers for damageDef Bomb</summary>
    ConfigError = 2
}

/// <summary>游戏日志里的一条 Def 级错误。Name 保留日志原文（含多余空白），因为空白本身就是病因之一。</summary>
public sealed record GameLogDefError(
    GameLogDefErrorKind Kind,
    string? ExpectedDefType,
    string Name,
    string? Detail,
    string RawLine)
{
    public string TrimmedName => Name.Trim();

    public bool HasStrayWhitespace => !string.Equals(Name, TrimmedName, StringComparison.Ordinal);
}

/// <summary>一条归因到具体模组的 Def 错误。</summary>
public enum XmlDefIssueKind
{
    /// <summary>引用的值带了前后空白，游戏不 trim 于是解析不到。修法是去掉空白。</summary>
    StrayWhitespace = 1,

    /// <summary>名字存在，但只以别的 DefType 存在（例如把 HediffDef 当 DamageDef 引用）。</summary>
    TypeMismatch = 2,

    /// <summary>当前启用清单里没有任何模组定义它。</summary>
    NotDefined = 3,

    /// <summary>有模组定义了它，但那个模组没启用。</summary>
    NotEnabled = 4,

    /// <summary>磁盘上确实定义了、DefType 也对得上，游戏仍然没解析到（未被载入/重名被拒/XML 解析失败）。</summary>
    DefinedButUnresolved = 5,

    /// <summary>游戏直接判定这个 Def 配置有误。</summary>
    ConfigError = 6
}

public sealed record XmlDefReferenceIssue(
    XmlDefIssueKind Kind,
    string ReferencingPackageId,
    string ReferencingModName,
    string ElementName,
    string ReferencedName,
    string RawReferencedValue,
    string? ExpectedDefType,
    IReadOnlyList<string> ActualDefTypes,
    string? DefiningPackageId,
    string? DefiningModName,
    string? EnclosingDefType,
    string? EnclosingDefName,
    string SourceFile,
    string? Detail,
    string RawLogLine);

/// <summary>
/// 把游戏日志里的 Def 错误归因到"谁引用的、谁定义的"。
///
/// 检测由日志负责，静态 XML 只负责归因——这个分工是实测出来的：
/// RimWorld 只对 C# 字段类型真是 Def 的元素做跨引用解析，光看元素名（damageDef/thingDef…）
/// 分不清"真的是 Def 引用"和"只是叫这个名字的字符串列表"，纯静态扫描在真实的 514 模组清单上
/// 报了 171 条，而游戏实际只报了 20 条。所以判据必须来自游戏自己的输出。
///
/// 归因时按日志里的原值（含空白）在启用模组的 XML 里精确定位引用点；
/// 门控分支（PatchOperationFindMod / Conditional / ModOption / MayRequire）里的引用不参与归因，
/// 因为目标模组不在时那些分支根本不会执行。
/// </summary>
public static class XmlCrossReferenceAnalyzer
{
    private const int MaxIssuesPerLogError = 20;

    private static readonly Regex DefNameShape = new(@"^[A-Za-z0-9_.\- ]{1,160}$", RegexOptions.Compiled);

    // "named" 后只吃一个空白符：多出来的那一个空格要留在捕获组里，它本身就是病因
    private static readonly Regex CrossReferenceRegex = new(
        @"Could not resolve cross-reference:\s*No\s+(?<type>[\w.+]*)\s*named\s(?<name>.*?)\s+found",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ConfigErrorRegex = new(
        @"Config error in\s+(?<name>[^:]+?):\s+(?<detail>.+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly string[] GatedOperationMarkers =
    {
        "FindMod", "Conditional", "ModOption", "MayRequire"
    };

    private static readonly string[] PatchOperationContainers =
    {
        "operations", "match", "nomatch", "caseTrue", "caseFalse"
    };

    private static readonly string[] NeverReferenceElements =
    {
        "defName", "label", "description", "Name", "li"
    };

    public static IReadOnlyList<GameLogDefError> ParseGameLog(IEnumerable<string> lines)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var results = new List<GameLogDefError>();

        foreach (var raw in lines)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            var line = raw.Trim();

            var cross = CrossReferenceRegex.Match(line);
            if (cross.Success)
            {
                var name = cross.Groups["name"].Value;
                if (name.Trim().Length == 0 || !seen.Add("C|" + name))
                    continue;

                results.Add(new GameLogDefError(
                    GameLogDefErrorKind.CrossReference,
                    StripNamespace(cross.Groups["type"].Value),
                    name,
                    null,
                    line));
                continue;
            }

            var config = ConfigErrorRegex.Match(line);
            if (config.Success)
            {
                var name = config.Groups["name"].Value.Trim();
                if (!seen.Add("E|" + name))
                    continue;

                results.Add(new GameLogDefError(
                    GameLogDefErrorKind.ConfigError,
                    null,
                    name,
                    config.Groups["detail"].Value.Trim(),
                    line));
            }
        }

        return results;
    }

    public static IReadOnlyList<GameLogDefError> ParseGameLogFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return Array.Empty<GameLogDefError>();

        try
        {
            // 日志可能被游戏进程独占，用共享读打开
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            var lines = new List<string>();
            string? line;
            while ((line = reader.ReadLine()) != null)
                lines.Add(line);
            return ParseGameLog(lines);
        }
        catch
        {
            return Array.Empty<GameLogDefError>();
        }
    }

    public static IReadOnlyList<XmlDefReferenceIssue> Analyze(
        IReadOnlyList<ModDescriptor> mods,
        IReadOnlyList<GameLogDefError> logErrors,
        CancellationToken cancellationToken = default)
    {
        if (logErrors.Count == 0)
            return Array.Empty<XmlDefReferenceIssue>();

        var active = mods.Where(m => m.IsActiveInModsConfig).ToList();
        var inactive = mods.Where(m => !m.IsActiveInModsConfig).ToList();

        var definitions = new Dictionary<string, DefEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var mod in active)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IndexDefinitions(mod, definitions);
        }

        var inactiveDefinitions = new Dictionary<string, DefEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var mod in inactive)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IndexDefinitions(mod, inactiveDefinitions);
        }

        var references = BuildReferenceMap(active, cancellationToken);
        var issues = new List<XmlDefReferenceIssue>();

        foreach (var error in logErrors)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (error.Kind == GameLogDefErrorKind.ConfigError)
            {
                if (definitions.TryGetValue(error.TrimmedName, out var owner) ||
                    inactiveDefinitions.TryGetValue(error.TrimmedName, out owner))
                {
                    issues.Add(new XmlDefReferenceIssue(
                        XmlDefIssueKind.ConfigError,
                        owner.PackageId, owner.ModName, "defName",
                        error.TrimmedName, error.TrimmedName,
                        null, owner.DefTypes,
                        owner.PackageId, owner.ModName,
                        owner.DefTypes.FirstOrDefault(), error.TrimmedName,
                        owner.SourceFile, error.Detail, error.RawLine));
                }

                continue;
            }

            if (!references.TryGetValue(error.Name, out var sites) || sites.Count == 0)
            {
                // 找不到引用点也要报出来，否则玩家看到的红字在工具里完全没有对应项
                issues.Add(Unattributed(error));
                continue;
            }

            definitions.TryGetValue(error.TrimmedName, out var defined);
            inactiveDefinitions.TryGetValue(error.TrimmedName, out var definedInactive);

            foreach (var site in sites.Take(MaxIssuesPerLogError))
            {
                issues.Add(Classify(error, site, defined, definedInactive));
            }
        }

        return issues;
    }

    private static XmlDefReferenceIssue Classify(
        GameLogDefError error,
        ReferenceSite site,
        DefEntry? defined,
        DefEntry? definedInactive)
    {
        XmlDefIssueKind kind;
        if (error.HasStrayWhitespace)
        {
            kind = XmlDefIssueKind.StrayWhitespace;
        }
        else if (defined == null)
        {
            kind = definedInactive != null ? XmlDefIssueKind.NotEnabled : XmlDefIssueKind.NotDefined;
        }
        else if (MatchesExpected(defined, error.ExpectedDefType))
        {
            kind = XmlDefIssueKind.DefinedButUnresolved;
        }
        else
        {
            kind = XmlDefIssueKind.TypeMismatch;
        }

        var definer = kind is XmlDefIssueKind.NotEnabled ? definedInactive : defined;

        return new XmlDefReferenceIssue(
            kind,
            site.Mod.PackageId,
            site.Mod.Name,
            site.ElementName,
            error.TrimmedName,
            error.Name,
            error.ExpectedDefType,
            definer?.DefTypes ?? Array.Empty<string>(),
            definer?.PackageId,
            definer?.ModName,
            site.EnclosingDefType,
            site.EnclosingDefName,
            site.SourceFile,
            error.Detail,
            error.RawLine);
    }

    private static bool MatchesExpected(DefEntry defined, string? expectedDefType)
    {
        if (string.IsNullOrWhiteSpace(expectedDefType))
            return true;

        return defined.DefTypes.Any(t => string.Equals(t, expectedDefType, StringComparison.OrdinalIgnoreCase));
    }

    private static XmlDefReferenceIssue Unattributed(GameLogDefError error)
        => new(
            error.HasStrayWhitespace ? XmlDefIssueKind.StrayWhitespace : XmlDefIssueKind.NotDefined,
            string.Empty, "未能定位到引用方",
            string.Empty,
            error.TrimmedName, error.Name,
            error.ExpectedDefType, Array.Empty<string>(),
            null, null,
            null, null,
            string.Empty, error.Detail, error.RawLine);

    private sealed record DefEntry(
        string PackageId,
        string ModName,
        IReadOnlyList<string> DefTypes,
        string SourceFile,
        bool IsVanilla);

    private sealed record ReferenceSite(
        ModDescriptor Mod,
        string ElementName,
        string SourceFile,
        bool IsDefShapedField,
        string? EnclosingDefType,
        string? EnclosingDefName);

    private static void IndexDefinitions(ModDescriptor mod, Dictionary<string, DefEntry> target)
    {
        if (mod.XmlDefs.Count > 0)
        {
            foreach (var group in mod.XmlDefs
                         .Where(d => !string.IsNullOrWhiteSpace(d.DefName))
                         .GroupBy(d => d.DefName!.Trim(), StringComparer.OrdinalIgnoreCase))
            {
                target.TryAdd(group.Key, new DefEntry(
                    mod.PackageId,
                    mod.Name,
                    group.Select(d => d.DefType).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                    group.First().SourceFile,
                    mod.IsOfficialCoreOrDlc));
            }

            return;
        }

        // 原版 Core/DLC 被 XmlConflictAnalyzer 整个跳过，XmlDefs 恒空，只能自己扫
        foreach (var file in EnumerateXml(mod, "Defs"))
        {
            var doc = TryLoad(file);
            if (doc?.Root == null || !IsDefsRoot(doc.Root))
                continue;

            foreach (var element in doc.Root.Elements())
            {
                var defName = element.Element("defName")?.Value?.Trim();
                if (string.IsNullOrWhiteSpace(defName))
                    continue;

                target.TryAdd(defName, new DefEntry(
                    mod.PackageId,
                    mod.Name,
                    new[] { element.Name.LocalName },
                    file,
                    mod.IsOfficialCoreOrDlc));
            }
        }
    }

    /// <summary>
    /// 「被引用的原值（含空白）→ 引用点」。索引所有叶子元素的文本值，不限定元素名——
    /// 日志已经证明这个名字确实被当成 Def 引用了，静态这边只负责找出它写在哪儿。
    /// </summary>
    private static Dictionary<string, List<ReferenceSite>> BuildReferenceMap(
        IReadOnlyList<ModDescriptor> activeMods,
        CancellationToken cancellationToken)
    {
        var map = new Dictionary<string, List<ReferenceSite>>(StringComparer.Ordinal);

        foreach (var mod in activeMods.Where(m => !m.IsOfficialCoreOrDlc))
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var file in EnumerateXml(mod, "Defs").Concat(EnumerateXml(mod, "Patches")))
            {
                var doc = TryLoad(file);
                if (doc?.Root == null)
                    continue;

                var isPatchFile = doc.Root.Name.LocalName.Equals("Patch", StringComparison.OrdinalIgnoreCase);
                foreach (var site in ExtractSites(doc.Root, isPatchFile, mod, file))
                {
                    if (!map.TryGetValue(site.Value, out var list))
                        map[site.Value] = list = new List<ReferenceSite>();

                    if (!list.Any(s => s.Mod.PackageId == site.Site.Mod.PackageId &&
                                       s.ElementName == site.Site.ElementName &&
                                       s.IsDefShapedField == site.Site.IsDefShapedField &&
                                       s.EnclosingDefName == site.Site.EnclosingDefName))
                        list.Add(site.Site);
                }
            }
        }

        // 同一个名字可能出现在多处：既有真正的 Def 引用（damageDef、apparelDef…），
        // 也有恰好同字的普通文本（clipPath、hediff 指向另一种 Def 等）。
        // 只要存在名字形状像 Def 字段的引用点，就只认它们；一个都没有时才退回全部候选。
        foreach (var key in map.Keys.ToList())
        {
            var sites = map[key];
            var defShaped = sites.Where(s => s.IsDefShapedField).ToList();
            map[key] = (defShaped.Count > 0 ? defShaped : sites)
                .OrderBy(s => s.Mod.Name, StringComparer.Ordinal)
                .ToList();
        }

        return map;
    }

    private static IEnumerable<(string Value, ReferenceSite Site)> ExtractSites(
        XElement root,
        bool isPatchFile,
        ModDescriptor mod,
        string file)
    {
        var results = new List<(string, ReferenceSite)>();

        if (isPatchFile)
        {
            foreach (var operation in root.Elements("Operation"))
                WalkOperation(operation, gated: false, mod, file, results);
            return results;
        }

        foreach (var element in root.Descendants())
            AddSite(element, mod, file, results);

        return results;
    }

    private static void WalkOperation(
        XElement operation,
        bool gated,
        ModDescriptor mod,
        string file,
        List<(string Value, ReferenceSite Site)> results)
    {
        var opClass = operation.Attribute("Class")?.Value?.Trim() ?? string.Empty;
        var shortClass = opClass.Contains('.') ? opClass[(opClass.LastIndexOf('.') + 1)..] : opClass;
        var nowGated = gated || GatedOperationMarkers.Any(m => shortClass.Contains(m, StringComparison.OrdinalIgnoreCase));

        if (!nowGated)
        {
            var value = operation.Element("value");
            if (value != null)
            {
                foreach (var element in value.DescendantsAndSelf())
                    AddSite(element, mod, file, results);
            }
        }

        foreach (var containerName in PatchOperationContainers)
        {
            var container = operation.Element(containerName);
            if (container == null)
                continue;

            foreach (var child in container.Elements())
                WalkOperation(child, nowGated, mod, file, results);
        }
    }

    private static void AddSite(
        XElement element,
        ModDescriptor mod,
        string file,
        List<(string Value, ReferenceSite Site)> results)
    {
        if (element.HasElements)
            return;

        var name = element.Name.LocalName;
        if (NeverReferenceElements.Contains(name, StringComparer.Ordinal))
            return;

        var value = element.Value;
        if (value.Length == 0 || value.Length > 160)
            return;

        // 只在原值两侧最多允许一处空白：日志里的病因就是这种"看着像但差一个空格"的值
        if (!DefNameShape.IsMatch(value) || !value.Trim().Any(char.IsLetter))
            return;

        var defShaped = (name.Length > 3 && name.EndsWith("Def", StringComparison.Ordinal)) ||
                        IsPluralDefListItem(element);

        var (enclosingType, enclosingName) = FindEnclosingDef(element);
        results.Add((value, new ReferenceSite(mod, name, file, defShaped, enclosingType, enclosingName)));
    }

    /// <summary>引用点所在的顶层 Def（补丁文件里的引用没有归属 Def，返回空）。</summary>
    private static (string? DefType, string? DefName) FindEnclosingDef(XElement element)
    {
        for (var node = element.Parent; node != null; node = node.Parent)
        {
            if (node.Parent == null)
                return (null, null);

            if (IsDefsRoot(node.Parent))
            {
                var defName = node.Element("defName")?.Value?.Trim();
                return string.IsNullOrWhiteSpace(defName) ? (null, null) : (node.Name.LocalName, defName);
            }
        }

        return (null, null);
    }

    private static bool IsPluralDefListItem(XElement element)
    {
        if (!element.Name.LocalName.Equals("li", StringComparison.Ordinal))
            return false;

        var parent = element.Parent?.Name.LocalName;
        return parent != null && parent.Length > 4 && parent.EndsWith("Defs", StringComparison.Ordinal);
    }

    private static bool IsDefsRoot(XElement root)
        => root.Name.LocalName.Equals("Defs", StringComparison.OrdinalIgnoreCase);

    private static string StripNamespace(string typeName)
    {
        var value = typeName.Trim();
        var index = value.LastIndexOf('.');
        return index >= 0 && index < value.Length - 1 ? value[(index + 1)..] : value;
    }

    private static IEnumerable<string> EnumerateXml(ModDescriptor mod, string subDirectory)
    {
        var roots = mod.ActiveContentDirectories.Count > 0
            ? mod.ActiveContentDirectories
            : new[] { mod.RootDirectory };

        foreach (var root in roots)
        {
            var dir = Path.Combine(root, subDirectory);
            if (!Directory.Exists(dir))
                continue;

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(dir, "*.xml", SearchOption.AllDirectories);
            }
            catch
            {
                continue;
            }

            foreach (var file in files)
                yield return file;
        }
    }

    private static XDocument? TryLoad(string file)
    {
        try
        {
            return XDocument.Load(file);
        }
        catch
        {
            return null;
        }
    }
}
