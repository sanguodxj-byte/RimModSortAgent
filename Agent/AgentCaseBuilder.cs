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
    private const int MaxSnippetChars = 7000;
    private const int MaxXmlChars = 6000;
    private const int MaxPromptChars = 24000;

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
        var sources = new List<string>();

        var target = conflict.Findings.FirstOrDefault()?.TargetSignature ?? string.Empty;
        var isHarmony = conflict.Category.Contains("Harmony", StringComparison.OrdinalIgnoreCase) ||
                        conflict.Category.Contains("Prefix", StringComparison.OrdinalIgnoreCase) ||
                        conflict.Category.Contains("Transpiler", StringComparison.OrdinalIgnoreCase);

        if (isHarmony)
        {
            AddHarmonySamples(conflict, modA, "A", samples, sources, cancellationToken);
            AddHarmonySamples(conflict, modB, "B", samples, sources, cancellationToken);

            var targetCode = DecompileTargetMethod(target);
            if (!string.IsNullOrWhiteSpace(targetCode))
            {
                samples.Add(new AgentCodeSample("RimWorld / 目标方法原始实现", ShortMember(target), targetCode!));
                sources.Add("目标方法源码");
            }
        }
        else
        {
            AddXmlSamples(conflict, modA, "A", samples, sources, cancellationToken);
            AddXmlSamples(conflict, modB, "B", samples, sources, cancellationToken);
        }

        if (conflict.Category.Equals("ExplicitIncompatibility", StringComparison.OrdinalIgnoreCase))
        {
            AddAboutSample(modA, samples);
            AddAboutSample(modB, samples);
        }

        var prompt = BuildPrompt(conflict, modA, modB, samples);
        var memorySources = sources
            .Concat(samples.Select(s => s.Source))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

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
        ModDescriptor? mod,
        string side,
        List<AgentCodeSample> samples,
        List<string> sources,
        CancellationToken cancellationToken)
    {
        if (mod == null || mod.AssemblyPaths.Count == 0)
            return;

        var target = conflict.Findings.FirstOrDefault()?.TargetSignature ?? string.Empty;
        var targetMethod = ShortMember(target);

        foreach (var assemblyPath in mod.AssemblyPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var analysis = GetAssemblyAnalysis(assemblyPath);
            if (analysis == null)
                continue;

            var patches = analysis.Patches
                .Where(p => MatchesTarget(p, target, targetMethod))
                .OrderByDescending(p => p.Behavior.CanSkipOriginal)
                .ThenByDescending(p => p.PatchKind == "Transpiler")
                .Take(5)
                .ToList();

            if (patches.Count == 0)
                continue;

            sources.Add($"{mod.Name} / {analysis.AssemblyName}（真实补丁代码）");

            foreach (var patch in patches)
            {
                var behavior = new StringBuilder();
                behavior.Append($"{patch.PatchKind} · Priority={patch.Priority}");
                if (patch.Behavior.AlwaysSkipsOriginal) behavior.Append(" · 无条件跳过原方法");
                if (patch.Behavior.ConditionalSkipOriginal) behavior.Append(" · 条件性跳过原方法");
                if (patch.Behavior.ModifiesResult) behavior.Append(" · 修改返回值");
                if (patch.Behavior.HasTargetGuardClause) behavior.Append(" · 带条件守卫");
                if (patch.Behavior.UsesCodeMatcher) behavior.Append(" · 使用 CodeMatcher");

                var code = Trim(patch.Behavior.DecompiledSnippet, MaxSnippetChars);
                if (string.IsNullOrWhiteSpace(code))
                    code = "（反编译片段不可用，仅能确认补丁签名）";

                samples.Add(new AgentCodeSample(
                    $"{mod.Name} / {analysis.AssemblyName} / {side} 方",
                    $"{patch.PatchClassFullName}.{patch.PatchMethodName} [目标 {patch.TargetTypeFullName}.{patch.TargetMethodName}]（{behavior}）",
                    code));
            }
        }
    }

    private static bool MatchesTarget(HarmonyPatchRecord patch, string target, string targetMethod)
    {
        var patchTarget = $"{patch.TargetTypeFullName}.{patch.TargetMethodName}";
        if (patchTarget.Equals(target, StringComparison.OrdinalIgnoreCase))
            return true;

        if (targetMethod.Length > 3 && patchTarget.Contains(targetMethod, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private void AddXmlSamples(
        PlayerConflict conflict,
        ModDescriptor? mod,
        string side,
        List<AgentCodeSample> samples,
        List<string> sources,
        CancellationToken cancellationToken)
    {
        if (mod == null)
            return;

        var targets = conflict.Findings.Select(f => f.TargetSignature).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var patchRecords = mod.XmlPatches
            .Where(p => targets.Any(t => p.XPath != null && t.Contains(p.XPath, StringComparison.OrdinalIgnoreCase)) ||
                        targets.Any(t => p.XPath != null && p.XPath.Contains(DefKeyOf(t), StringComparison.OrdinalIgnoreCase)))
            .Take(6)
            .ToList();

        if (patchRecords.Count == 0)
            patchRecords = mod.XmlPatches.Where(p => p.IsDestructive).Take(3).ToList();

        foreach (var record in patchRecords)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var content = ReadXmlOperation(record.SourceFile, record.XPath);
            if (string.IsNullOrWhiteSpace(content))
                continue;

            sources.Add($"{mod.Name} / XML Patch 原文");
            samples.Add(new AgentCodeSample(
                $"{mod.Name} / {side} 方 · {Path.GetFileName(record.SourceFile)}",
                $"{record.OperationClass} · {(record.IsDestructive ? "破坏性操作" : "追加操作")} · xpath={record.XPath}",
                Trim(content, MaxXmlChars)));
        }
    }

    private static string DefKeyOf(string target)
    {
        var match = System.Text.RegularExpressions.Regex.Match(target, "defName=\"([^\"]+)\"");
        return match.Success ? match.Groups[1].Value : target;
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
            var content = Trim(File.ReadAllText(about), 4000);
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
            if (string.IsNullOrWhiteSpace(code))
                return null;

            return Trim(code, MaxSnippetChars);
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
        if (!File.Exists(sourceFile))
            return null;

        if (!_xmlFileCache.TryGetValue(sourceFile, out var content))
        {
            try
            {
                content = File.ReadAllText(sourceFile);
            }
            catch
            {
                return null;
            }

            _xmlFileCache[sourceFile] = content;
        }

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

    private static string BuildEvidenceSummary(PlayerConflict conflict, List<AgentCodeSample> samples, bool isHarmony)
    {
        var assemblies = samples.Select(s => s.Source).Where(s => s.Contains(".dll", StringComparison.OrdinalIgnoreCase)).Count();
        var patchSamples = samples.Count(s => s.Member.Contains("目标", StringComparison.Ordinal) || s.Member.Contains("Prefix", StringComparison.Ordinal) || s.Member.Contains("Transpiler", StringComparison.Ordinal));
        var xmlSamples = samples.Count(s => s.Member.Contains("xpath=", StringComparison.OrdinalIgnoreCase));
        var targetSource = samples.Any(s => s.Source.Contains("目标方法", StringComparison.Ordinal));

        var parts = new List<string>();
        if (isHarmony)
        {
            if (patchSamples > 0) parts.Add($"反编译 {patchSamples} 个补丁方法");
            if (targetSource) parts.Add("目标方法原始实现");
        }
        else if (xmlSamples > 0)
        {
            parts.Add($"读取 {xmlSamples} 份 XML Patch 原文");
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
        var builder = new StringBuilder(MaxPromptChars);
        builder.AppendLine("你是 RimWorld 模组兼容性工程师。请基于下面给出的真实代码证据做核查，不要臆测不存在的代码。");
        builder.AppendLine();
        builder.AppendLine("## 冲突报告");
        builder.AppendLine($"- 类型：{conflict.CategoryLabel}（{conflict.Category}）");
        builder.AppendLine($"- 级别：{conflict.SeverityLabel}");
        builder.AppendLine($"- 模组 A：{conflict.ModAName}（{conflict.ModAPackageId}）{(modA == null ? " [未安装]" : $" 路径 {modA.RootDirectory}")}");
        builder.AppendLine($"- 模组 B：{conflict.ModBName}（{conflict.ModBPackageId}）{(modB == null ? " [未安装]" : $" 路径 {modB.RootDirectory}")}");
        builder.AppendLine($"- 目标：{conflict.TargetLabel}");
        builder.AppendLine($"- 静态结论：{conflict.What}");

        if (conflict.Category is "DuplicateXmlDefOverwrite" or "DestructiveXmlPatchCollision")
        {
            builder.AppendLine();
            builder.AppendLine("## 判定口径");
            builder.AppendLine("这是扩展模组对原模组内容的覆盖或替换，属于常见且有意的做法，静态重叠本身不算明确冲突。");
            builder.AppendLine("除非代码证据表明某一方的功能会被实际破坏，否则判为 condition_dependent，修复方向优先给出 order；不要自动生成替换游戏数据的补丁。");
        }

        builder.AppendLine();
        builder.AppendLine("## 代码证据");

        var budget = MaxPromptChars - builder.Length;
        foreach (var sample in samples)
        {
            var block = $"### {sample.Source}\n{sample.Member}\n```\n{sample.Code}\n```\n";
            if (block.Length > budget)
                break;
            builder.AppendLine(block);
            budget -= block.Length;
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

    private static string Trim(string? text, int max)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;
        return text.Length <= max ? text : text[..max] + "\n…（已截断）";
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
