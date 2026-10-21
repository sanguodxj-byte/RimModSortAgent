using System.Text;
using System.Xml.Linq;
using DecompilerServer.Services;
using ModSorterAgent.Models;
using ModSorterAgent.Reporting;

namespace ModSorterAgent.Agent;

/// <summary>
/// 把一条冲突还原成"真实代码案例"：反编译双方补丁方法、读取目标方法源码、
/// 抽取 XML Patch 原文。Agent 的结论必须建立在证据上，而不是只看报告摘要。
/// </summary>
public sealed class AgentCaseBuilder : IDisposable
{
    private readonly IReadOnlyList<ModDescriptor> _mods;
    private readonly string _gameDir;
    private readonly string[] _searchDirs;
    private readonly Dictionary<string, Lazy<AssemblyHarmonyAnalysisResult?>> _assemblyCache =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _xmlFileCache = new(StringComparer.OrdinalIgnoreCase);

    private AssemblyContextManager? _gameContext;
    private MemberResolver? _gameResolver;
    private DecompilerService? _gameDecompiler;

    public AgentCaseBuilder(IReadOnlyList<ModDescriptor> mods, string gameDir)
    {
        _mods = mods;
        _gameDir = gameDir;

        var dirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { gameDir };
        foreach (var mod in mods)
        {
            foreach (var asm in mod.AssemblyPaths)
            {
                var dir = Path.GetDirectoryName(asm);
                if (!string.IsNullOrWhiteSpace(dir))
                    dirs.Add(dir);
            }
        }

        _searchDirs = dirs.ToArray();
    }

    public AgentCaseFile Build(PlayerConflict conflict, CancellationToken cancellationToken = default)
    {
        var modA = FindMod(conflict.ModAPackageId);
        var modB = FindMod(conflict.ModBPackageId);
        var samples = new List<AgentCodeSample>();
        var targets = conflict.Findings
            .Select(f => f.TargetSignature)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var target = targets.FirstOrDefault() ?? string.Empty;

        var isHarmony = conflict.Category.Contains("Harmony", StringComparison.OrdinalIgnoreCase) ||
                        conflict.Category.Contains("Prefix", StringComparison.OrdinalIgnoreCase) ||
                        conflict.Category.Contains("Transpiler", StringComparison.OrdinalIgnoreCase);

        if (isHarmony)
        {
            AddHarmonySamples(conflict, targets, modA, modB, "A", samples, cancellationToken);
            AddHarmonySamples(conflict, targets, modB, modA, "B", samples, cancellationToken);

            var decompiledTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var targetSignature in targets)
            {
                if (!decompiledTargets.Add(targetSignature))
                    continue;

                var targetCode = DecompileTargetMethod(targetSignature);
                if (string.IsNullOrWhiteSpace(targetCode))
                    continue;

                samples.Add(new AgentCodeSample(
                    "RimWorld / 目标方法原始实现",
                    ShortMember(targetSignature),
                    targetCode!));
            }
        }
        else if (conflict.Category is "ExplicitIncompatibility" or "MissingDependency" or
                 "CyclicLoadOrderConstraint" or "SoftConstraintCycleDropped")
        {
            AddDeclarationSamples(conflict, modA, "A", samples);
            AddDeclarationSamples(conflict, modB, "B", samples);
        }
        else
        {
            AddXmlSamples(conflict, targets, modA, "A", samples, cancellationToken);
            AddXmlSamples(conflict, targets, modB, "B", samples, cancellationToken);
        }

        var prompt = BuildPrompt(conflict, modA, modB, samples);

        return new AgentCaseFile(
            ConflictKey: conflict.Key,
            Category: conflict.Category,
            Target: target,
            ModAName: conflict.ModAName,
            ModAPackageId: conflict.ModAPackageId,
            ModBName: conflict.ModBName,
            ModBPackageId: conflict.ModBPackageId,
            PromptText: prompt,
            Samples: samples,
            Summary: BuildEvidenceSummary(conflict, samples, isHarmony));
    }

    private ModDescriptor? FindMod(string packageId)
        => _mods.FirstOrDefault(m => m.PackageId.Equals(packageId, StringComparison.OrdinalIgnoreCase));

    private void AddHarmonySamples(
        PlayerConflict conflict,
        IReadOnlyList<string> targets,
        ModDescriptor? mod,
        ModDescriptor? otherMod,
        string side,
        List<AgentCodeSample> samples,
        CancellationToken cancellationToken)
    {
        if (mod == null || mod.AssemblyPaths.Count == 0)
            return;

        var targetTokens = BuildTargetTokens(targets);
        var otherTokens = BuildModTokens(otherMod);

        foreach (var assemblyPath in mod.AssemblyPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var analysis = GetAssemblyAnalysis(assemblyPath);
            if (analysis == null)
                continue;

            var patches = analysis.Patches
                .Where(p => MatchesConflict(p, targets, targetTokens, otherTokens))
                .OrderByDescending(p => p.Behavior.CanSkipOriginal)
                .ThenByDescending(p => p.PatchKind == "Transpiler")
                .ToList();

            var dynamicSites = analysis.DynamicPatchSites
                .Where(s => MatchesDynamicSite(s, targets, targetTokens, otherTokens))
                .ToList();

            if (patches.Count == 0 && dynamicSites.Count == 0)
                continue;

            foreach (var patch in patches)
            {
                var behavior = new StringBuilder();
                behavior.Append($"{patch.PatchKind} · Priority={patch.Priority}");
                if (patch.Behavior.AlwaysSkipsOriginal) behavior.Append(" · 无条件跳过原方法");
                if (patch.Behavior.ConditionalSkipOriginal) behavior.Append(" · 条件性跳过原方法");
                if (patch.Behavior.ModifiesResult) behavior.Append(" · 修改返回值");
                if (patch.Behavior.HasTargetGuardClause) behavior.Append(" · 带条件守卫");
                if (patch.Behavior.UsesCodeMatcher) behavior.Append(" · 使用 CodeMatcher");

                var patchTarget = JoinTarget(patch);
                if (string.IsNullOrWhiteSpace(patchTarget))
                    patchTarget = patch.DynamicTargetHints.Count > 0
                        ? string.Join(" / ", patch.DynamicTargetHints)
                        : "（动态目标未解析）";

                var code = patch.Behavior.DecompiledSnippet;
                if (string.IsNullOrWhiteSpace(code))
                    code = "（反编译片段不可用，仅能确认补丁签名）";

                samples.Add(new AgentCodeSample(
                    $"{mod.Name} / {analysis.AssemblyName} / {side} 方",
                    $"{patch.PatchClassFullName}.{patch.PatchMethodName} [目标 {patchTarget}]（{behavior}）",
                    code));
            }

            foreach (var site in dynamicSites)
            {
                var hints = site.ReferencedStrings.Count > 0
                    ? string.Join(" / ", site.ReferencedStrings)
                    : "（无字符串线索）";

                var code = site.DecompiledSnippet;
                if (string.IsNullOrWhiteSpace(code))
                    code = "（动态注册方法反编译片段不可用，仅能确认 Harmony.Patch 调用点）";

                samples.Add(new AgentCodeSample(
                    $"{mod.Name} / {analysis.AssemblyName} / {side} 方 · 动态注册",
                    $"{site.CallerTypeFullName}.{site.CallerMethodName} [Harmony.Patch 动态注册 · 引用 {hints}]",
                    code));
            }
        }
    }

    private static string JoinTarget(HarmonyPatchRecord patch)
    {
        if (string.IsNullOrWhiteSpace(patch.TargetTypeFullName) ||
            string.IsNullOrWhiteSpace(patch.TargetMethodName))
            return string.Empty;

        return $"{patch.TargetTypeFullName}.{patch.TargetMethodName}";
    }

    private static bool MatchesConflict(
        HarmonyPatchRecord patch,
        IReadOnlyList<string> targets,
        IReadOnlyList<string> targetTokens,
        IReadOnlyList<string> otherTokens)
    {
        var patchTarget = JoinTarget(patch);
        var patchIdentity = $"{patch.PatchClassFullName}.{patch.PatchMethodName}";

        foreach (var target in targets)
        {
            if (patchTarget.Equals(target, StringComparison.OrdinalIgnoreCase))
                return true;

            if (patchIdentity.Equals(target, StringComparison.OrdinalIgnoreCase))
                return true;

            var shortTarget = ShortMember(target);
            if (shortTarget.Length > 3 && patchTarget.Contains(shortTarget, StringComparison.OrdinalIgnoreCase))
                return true;

            if (patchTarget.Length > 3 && target.Contains(patchTarget, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        if (patch.UsesDynamicTargetMethod)
        {
            if (ContainsAny(patch.DynamicTargetHints, targetTokens))
                return true;

            if (ContainsAny(patch.DynamicTargetHints, otherTokens))
                return true;

            if (targets.Any(t => t.Contains(patch.PatchClassFullName, StringComparison.OrdinalIgnoreCase) &&
                                 t.Contains(patch.PatchMethodName, StringComparison.OrdinalIgnoreCase)))
                return true;
        }

        return false;
    }

    private static bool MatchesDynamicSite(
        DynamicHarmonyPatchSite site,
        IReadOnlyList<string> targets,
        IReadOnlyList<string> targetTokens,
        IReadOnlyList<string> otherTokens)
    {
        var identity = $"{site.CallerTypeFullName}.{site.CallerMethodName}";
        foreach (var target in targets)
        {
            if (identity.Equals(target, StringComparison.OrdinalIgnoreCase))
                return true;

            if (target.Length > 3 && identity.Contains(target, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        if (ContainsAny(site.ReferencedStrings, targetTokens))
            return true;

        if (ContainsAny(site.ReferencedStrings, otherTokens))
            return true;

        if (ContainsAny(site.ReferencedTypes, targetTokens))
            return true;

        return false;
    }

    private static IReadOnlyList<string> BuildTargetTokens(IReadOnlyList<string> targets)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var target in targets)
        {
            if (string.IsNullOrWhiteSpace(target))
                continue;

            if (target.Length >= 4)
                tokens.Add(target);

            var shortName = ShortMember(target);
            if (shortName.Length >= 4)
                tokens.Add(shortName);

            var lastDot = target.LastIndexOf('.');
            if (lastDot > 0 && lastDot < target.Length - 1)
            {
                var method = target[(lastDot + 1)..];
                if (method.Length >= 4)
                    tokens.Add(method);

                var typeName = target[..lastDot];
                var typeDot = typeName.LastIndexOf('.');
                var simpleType = typeDot >= 0 ? typeName[(typeDot + 1)..] : typeName;
                if (simpleType.Length >= 4)
                    tokens.Add(simpleType);
            }
        }

        return tokens.ToList();
    }

    private static IReadOnlyList<string> BuildModTokens(ModDescriptor? mod)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (mod == null)
            return Array.Empty<string>();

        if (mod.Name.Length >= 4)
            tokens.Add(mod.Name);

        foreach (var part in mod.PackageId.Split(new[] { '.', '_', '-' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.Length >= 4)
                tokens.Add(part);
        }

        foreach (var assembly in mod.AssemblyPaths)
        {
            var name = Path.GetFileNameWithoutExtension(assembly);
            if (name.Length >= 4)
                tokens.Add(name);
        }

        var folder = Path.GetFileName(mod.RootDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (folder.Length >= 4)
            tokens.Add(folder);

        return tokens.ToList();
    }

    private static bool ContainsAny(IReadOnlyList<string> haystacks, IReadOnlyList<string> needles)
    {
        foreach (var haystack in haystacks)
        {
            if (string.IsNullOrWhiteSpace(haystack))
                continue;

            foreach (var needle in needles)
            {
                if (needle.Length >= 4 && haystack.Contains(needle, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    private void AddXmlSamples(
        PlayerConflict conflict,
        IReadOnlyList<string> targets,
        ModDescriptor? mod,
        string side,
        List<AgentCodeSample> samples,
        CancellationToken cancellationToken)
    {
        if (mod == null)
            return;

        var defKeys = targets
            .Where(t => !t.Contains('/') && t.Contains(':'))
            .Select(ParseDefKey)
            .Where(k => k != null)
            .Select(k => k!.Value)
            .ToList();

        foreach (var defKey in defKeys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var def = mod.XmlDefs.FirstOrDefault(d =>
                d.DefType.Equals(defKey.DefType, StringComparison.OrdinalIgnoreCase) &&
                (d.DefName ?? d.NameAttribute ?? string.Empty)
                .Equals(defKey.DefName, StringComparison.OrdinalIgnoreCase));

            if (def == null)
                continue;

            var content = ReadXmlDef(def);
            if (string.IsNullOrWhiteSpace(content))
                continue;

            samples.Add(new AgentCodeSample(
                $"{mod.Name} / {side} 方 · {Path.GetFileName(def.SourceFile)}",
                $"{def.DefType} · {defKey.DefName} · 完整定义",
                content!));
        }

        var patchRecords = mod.XmlPatches
            .Where(p => MatchesXmlPatch(p, targets))
            .ToList();

        if (patchRecords.Count == 0 && defKeys.Count == 0)
            patchRecords = mod.XmlPatches.Where(p => p.IsDestructive).Take(3).ToList();

        foreach (var record in patchRecords)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var content = ReadXmlOperation(record.SourceFile, record.XPath);
            if (string.IsNullOrWhiteSpace(content))
                continue;

            samples.Add(new AgentCodeSample(
                $"{mod.Name} / {side} 方 · {Path.GetFileName(record.SourceFile)}",
                $"{record.OperationClass} · {(record.IsDestructive ? "破坏性操作" : "追加操作")} · xpath={record.XPath}",
                content!));
        }
    }

    private static bool MatchesXmlPatch(XmlPatchRecord patch, IReadOnlyList<string> targets)
    {
        if (string.IsNullOrWhiteSpace(patch.XPath))
            return false;

        foreach (var target in targets)
        {
            if (string.IsNullOrWhiteSpace(target))
                continue;

            if (patch.XPath.Equals(target, StringComparison.OrdinalIgnoreCase))
                return true;

            if (patch.XPath.Contains(target, StringComparison.OrdinalIgnoreCase))
                return true;

            if (target.Contains(patch.XPath, StringComparison.OrdinalIgnoreCase))
                return true;

            var defKey = ParseDefKey(target);
            if (defKey != null &&
                patch.XPath.Contains(defKey.Value.DefName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static (string DefType, string DefName)? ParseDefKey(string target)
    {
        var colon = target.IndexOf(':');
        if (colon <= 0 || colon == target.Length - 1)
            return null;

        var defType = target[..colon].Trim();
        var defName = target[(colon + 1)..].Trim();
        if (defType.Length == 0 || defName.Length == 0)
            return null;

        var at = defName.IndexOf('@');
        if (at >= 0)
            defName = defName[(at + 1)..];

        return (defType, defName);
    }

    private void AddDeclarationSamples(
        PlayerConflict conflict,
        ModDescriptor? mod,
        string side,
        List<AgentCodeSample> samples)
    {
        if (mod == null)
            return;

        var builder = new StringBuilder();
        builder.AppendLine($"PackageId: {mod.PackageId}");
        builder.AppendLine($"Name: {mod.Name}");
        AppendList(builder, "supportedVersions", mod.SupportedVersions);
        AppendList(builder, "modDependencies", mod.Dependencies.Select(d => d.PackageId));
        AppendList(builder, "loadAfter", mod.LoadAfter);
        AppendList(builder, "loadBefore", mod.LoadBefore);
        AppendList(builder, "forceLoadAfter", mod.ForceLoadAfter);
        AppendList(builder, "forceLoadBefore", mod.ForceLoadBefore);
        AppendList(builder, "incompatibleWith", mod.IncompatibleWith);
        AppendList(builder, "referencedAssemblies", mod.ReferencedExternalAssemblies);

        var evidence = conflict.Findings
            .SelectMany(f => f.Evidence)
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (evidence.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("冲突证据：");
            foreach (var line in evidence)
                builder.AppendLine("- " + line);
        }

        samples.Add(new AgentCodeSample(
            $"{mod.Name} / About 声明摘要",
            $"{side} 方 · 依赖、加载顺序与互斥声明",
            builder.ToString()));
        AddAboutSample(mod, samples);
    }

    private static void AppendList(StringBuilder builder, string name, IEnumerable<string> values)
    {
        var list = values.Where(v => !string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (list.Count > 0)
            builder.AppendLine($"{name}: {string.Join(", ", list)}");
    }

    private void AddAboutSample(ModDescriptor? mod, List<AgentCodeSample> samples)
    {
        if (mod == null)
            return;

        var about = Path.Combine(mod.RootDirectory, "About", "About.xml");
        if (!File.Exists(about))
            return;

        try
        {
            var content = File.ReadAllText(about);
            samples.Add(new AgentCodeSample($"{mod.Name} / About.xml", "作者声明（依赖与互斥）", content));
        }
        catch
        {
            // 读不到 About 不影响核查
        }
    }

    private AssemblyHarmonyAnalysisResult? GetAssemblyAnalysis(string assemblyPath)
    {
        if (_assemblyCache.TryGetValue(assemblyPath, out var cached))
            return cached.Value;

        cached = new Lazy<AssemblyHarmonyAnalysisResult?>(() =>
        {
            try
            {
                using var ctx = new AssemblyContextManager();
                ctx.LoadAssemblyDirect(assemblyPath, _searchDirs);
                var resolver = new MemberResolver(ctx);
                var decompiler = new DecompilerService(ctx, resolver);
                return HarmonyPatchAnalyzer.AnalyzeAssembly(ctx, resolver, decompiler, includeSnippets: true);
            }
            catch
            {
                return null;
            }
        }, LazyThreadSafetyMode.ExecutionAndPublication);

        _assemblyCache[assemblyPath] = cached;
        return cached.Value;
    }

    private string? DecompileTargetMethod(string target)
    {
        if (string.IsNullOrWhiteSpace(target) || target.Contains('/'))
            return null;

        var lastDot = target.LastIndexOf('.');
        if (lastDot <= 0 || lastDot == target.Length - 1)
            return null;

        var typeName = target[..lastDot];
        var methodName = target[(lastDot + 1)..];

        try
        {
            EnsureGameDecompiler();
            var memberId = $"M:{typeName}.{methodName}";
            var code = _gameDecompiler!.DecompileEntitySnippet(memberId);
            return string.IsNullOrWhiteSpace(code) ? null : code;
        }
        catch
        {
            // 重载 / 泛型方法解析失败时不影响其他证据
            return null;
        }
    }

    private void EnsureGameDecompiler()
    {
        if (_gameDecompiler != null)
            return;

        _gameContext = new AssemblyContextManager();
        _gameContext.LoadAssembly(_gameDir, "Assembly-CSharp.dll", _searchDirs);
        _gameResolver = new MemberResolver(_gameContext);
        _gameDecompiler = new DecompilerService(_gameContext, _gameResolver);
    }

    private string? ReadXmlOperation(string sourceFile, string? xpath)
    {
        var content = ReadXmlFile(sourceFile);
        if (content == null)
            return null;

        if (string.IsNullOrWhiteSpace(xpath))
            return content;

        try
        {
            var doc = XDocument.Parse(content);
            var operation = doc.Descendants()
                .Where(e => e.Name.LocalName.StartsWith("PatchOperation", StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault(e => string.Equals(
                    (string?)e.Attribute("xpath") ?? (string?)e.Attribute("XPath"),
                    xpath, StringComparison.OrdinalIgnoreCase));

            if (operation != null)
                return operation.ToString();
        }
        catch
        {
            // XML 解析失败时回退到整文件片段
        }

        return content;
    }

    private string? ReadXmlDef(XmlDefRecord def)
    {
        var content = ReadXmlFile(def.SourceFile);
        if (content == null)
            return null;

        try
        {
            var doc = XDocument.Parse(content);
            var element = doc.Descendants()
                .Where(e => e.Name.LocalName.Equals(def.DefType, StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault(e =>
                    string.Equals((string?)e.Element("defName"), def.DefName, StringComparison.OrdinalIgnoreCase) ||
                    (def.DefName == null && string.Equals((string?)e.Attribute("Name"), def.NameAttribute, StringComparison.OrdinalIgnoreCase)));

            if (element != null)
                return element.ToString();
        }
        catch
        {
            // XML 解析失败时回退到整文件片段
        }

        return content;
    }

    private string? ReadXmlFile(string sourceFile)
    {
        if (!File.Exists(sourceFile))
            return null;

        if (_xmlFileCache.TryGetValue(sourceFile, out var content))
            return content;

        try
        {
            content = File.ReadAllText(sourceFile);
        }
        catch
        {
            return null;
        }

        _xmlFileCache[sourceFile] = content;
        return content;
    }

    private static string BuildEvidenceSummary(PlayerConflict conflict, List<AgentCodeSample> samples, bool isHarmony)
    {
        var assemblies = samples.Select(s => s.Source)
            .Where(s => s.Contains("真实补丁代码", StringComparison.Ordinal) ||
                        s.Contains("动态注册补丁代码", StringComparison.Ordinal))
            .Count();
        var patchSamples = samples.Count(s => s.Source.Contains("方", StringComparison.Ordinal) &&
                                              (s.Member.Contains("目标", StringComparison.Ordinal) ||
                                               s.Member.Contains("Prefix", StringComparison.Ordinal) ||
                                               s.Member.Contains("Transpiler", StringComparison.Ordinal) ||
                                               s.Member.Contains("Harmony.Patch", StringComparison.Ordinal)));
        var xmlSamples = samples.Count(s =>
            s.Source.Contains("XML Patch", StringComparison.Ordinal) ||
            s.Source.Contains("Def 原文", StringComparison.Ordinal) ||
            s.Source.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ||
            s.Member.Contains("xpath=", StringComparison.OrdinalIgnoreCase));
        var declarationSamples = samples.Count(s => s.Source.Contains("About 声明摘要", StringComparison.Ordinal) ||
                                                    s.Source.EndsWith("About.xml", StringComparison.OrdinalIgnoreCase));
        var targetSource = samples.Any(s => s.Source.Contains("目标方法", StringComparison.Ordinal));

        var parts = new List<string>();
        if (isHarmony)
        {
            if (patchSamples > 0) parts.Add($"反编译 {patchSamples} 个补丁/注册方法");
            if (targetSource) parts.Add("目标方法完整源码");
        }
        else if (declarationSamples > 0)
        {
            parts.Add($"读取 {declarationSamples} 份 About 声明");
        }
        else if (xmlSamples > 0)
        {
            parts.Add($"读取 {xmlSamples} 份 XML 原文");
        }

        if (assemblies > 0) parts.Add($"{assemblies} 个程序集");
        return parts.Count == 0
            ? "未找到可用的代码证据，仅依据静态报告摘要"
            : string.Join(" · ", parts);
    }

    private static string BuildPrompt(
        PlayerConflict conflict,
        ModDescriptor? modA,
        ModDescriptor? modB,
        IReadOnlyList<AgentCodeSample> samples)
    {
        var builder = new StringBuilder();
        builder.AppendLine("你是 RimWorld 模组兼容性工程师。请基于下面给出的真实代码证据做核查，不要臆测不存在的代码。");
        builder.AppendLine();
        builder.AppendLine("## 冲突报告");
        builder.AppendLine($"- 类型：{conflict.CategoryLabel}（{conflict.Category}）");
        builder.AppendLine($"- 级别：{conflict.SeverityLabel}");
        builder.AppendLine($"- 模组 A：{conflict.ModAName}（{conflict.ModAPackageId}）{(modA == null ? " [未安装]" : $" 路径 {modA.RootDirectory}")}");
        builder.AppendLine($"- 模组 B：{conflict.ModBName}（{conflict.ModBPackageId}）{(modB == null ? " [未安装]" : $" 路径 {modB.RootDirectory}")}");
        builder.AppendLine($"- 目标：{conflict.TargetLabel}");
        var targetIdentities = conflict.Findings
            .Select(f => f.TargetSignature)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (targetIdentities.Count > 0)
            builder.AppendLine($"- 冲突标识：{string.Join(" | ", targetIdentities)}");
        builder.AppendLine($"- 静态结论：{conflict.What}");
        builder.AppendLine($"- 证据数量：{samples.Count} 份（均为完整原文，未截断）");

        if (conflict.Category is "DuplicateXmlDefOverwrite" or "DestructiveXmlPatchCollision")
        {
            builder.AppendLine();
            builder.AppendLine("## 判定口径");
            builder.AppendLine("这是扩展模组对原模组内容的覆盖或替换，属于常见且有意的做法，静态重叠本身不算明确冲突。");
            builder.AppendLine("除非代码证据表明某一方的功能会被实际破坏，否则判为 condition_dependent，修复方向优先给出 order；不要自动生成替换游戏数据的补丁。");
        }

        builder.AppendLine();
        builder.AppendLine("## 代码证据");

        foreach (var sample in samples)
        {
            builder.AppendLine($"### {sample.Source}");
            builder.AppendLine(sample.Member);
            builder.AppendLine("```");
            builder.AppendLine(sample.Code);
            builder.AppendLine("```");
        }

        builder.AppendLine("""
## 输出要求
只输出一个 JSON 对象，不要 Markdown 代码围栏，不要额外解释。字段：
{
  "verdict": "real_conflict | condition_dependent | false_positive | insufficient_evidence",
  "confidence": 0.0,
  "headline": "一句话结论（中文，面向玩家）",
  "explanation": "基于代码的具体解释（中文）",
  "hidden_risks": [{"title":"隐患标题","detail":"具体说明","severity":"high|medium|low"}],
  "fix_strategy": "order | disable_one | compat_patch | config | manual",
  "patch_plan": {
    "kind": "harmony | xml | order_only | manual",
    "summary": "补丁做什么",
    "risk_note": "补丁的风险",
    "harmony_code": "完整的 C# 补丁类源码（kind=harmony 时必填，使用 HarmonyLib，命名空间 ModSorterAgentCompat；不要包含 packageId 或游戏外的 using）",
    "files": [{"relative_path":"Patches/Compat.xml","content":"完整 XML 内容（kind=xml 时必填）"}]
  }
}
规则：
1. 只有当代码证据足以证明真实冲突时才给 real_conflict；证据不足必须如实说。
2. hidden_risks 最多 5 条，必须是代码里能看到的具体问题。
3. 如果无法安全自动修复，patch_plan.kind 必须是 manual，并在 summary 里给出人工修复步骤。
4. 补丁代码必须能在 RimWorld 的 Harmony 3 环境下编译（.NET 10，C# 最新语法可用）。
5. 上面给出的每个补丁方法、目标方法和 XML 节点都是完整原文；不要声称内容被截断。
6. 用 Harmony.Patch / Unpatch 动态注册时，补丁类不要加 [HarmonyPatch] 类特性；只有静态声明补丁才用 [HarmonyPatch(typeof(目标类型), nameof(目标方法))]。不要添加占位补丁类或空 Postfix。
7. 编译工程已引用 Assembly-CSharp、0Harmony、Unity 兼容层，以及本冲突双方模组的 DLL，可以直接使用模组类型；但模组可能缺失或改名，跨模组调用仍应反射访问并处理 null。
8. 对截断型 Prefix（无条件 return false 跳过原方法），只调整它的 Priority、或把它移除后以低优先级重新注册，都不能让原方法恢复执行，不算修复。要么用新补丁完整复刻双方行为，要么如实给出 manual 并说明必须牺牲哪一方；禁止把这类无效改动写成 harmony。
9. 补丁必须使用自己唯一的 Harmony ID（如 modsorteragent.compat.bundle），不要复用其他模组的 Harmony ID；需要 Unpatch 其他模组补丁时，必须在 risk_note 中写明会失去哪一方功能。
10. 调用游戏 API 时必须使用证据中实际出现的签名；不要凭记忆猜测重载或可选参数。无法确定签名时改用反射（AccessTools.Method + Invoke），不要直接调用。
11. 复刻被跳过的方法时必须逐条照搬它的完整行为：它调用的每个子方法（例如其他模组的 *Compat.TryInsertModes）、读取的每处设置与默认值（例如 Mod.Settings?.defaultBatchSize）都要保留，禁止用硬编码常量替代模组的可配置默认值。确实无法复刻的部分，必须在 risk_note 中点名玩家会失去哪一项功能；证据不足以复刻时给 manual，不要交出静默缩水版补丁。
12. 抢占型补丁的验收标准是并集：对每一种输入，玩家能看到、能点的功能必须不少于双方原本功能的总和。只复刻其中一方就用高优先级抢占另一方，会让被抢占方的功能凭空消失，属于回归，不算修复。做不到并集就给 manual，并在 summary 里说明必须牺牲哪一方、由玩家如何选择。
""");

        return builder.ToString();
    }

    private static string ShortMember(string target)
    {
        if (string.IsNullOrWhiteSpace(target))
            return string.Empty;
        var parts = target.Split('.');
        return parts.Length >= 2 ? $"{parts[^2]}.{parts[^1]}" : target;
    }

    public void Dispose()
    {
        _gameDecompiler?.Dispose();
        _gameContext?.Dispose();
        _gameContext = null;
        _gameDecompiler = null;
        _gameResolver = null;
    }
}
