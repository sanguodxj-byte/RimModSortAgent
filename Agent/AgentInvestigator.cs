using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModSorterAgent.Models;
using ModSorterAgent.Reporting;

namespace ModSorterAgent.Agent;

/// <summary>
/// Agent 核查入口：先取证（反编译补丁方法 / 读取 XML 原文 / 反编译目标方法），
/// 再交给模型下结论；没有配置模型时用同一份证据做确定性的本地核查。
/// 结论必须区分"确认冲突 / 条件性 / 误报 / 证据不足"，不允许凭空断言。
/// </summary>
public sealed class AgentInvestigator : IDisposable
{
    private static readonly JsonSerializerOptions PayloadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private const string AgentToolsPrompt = """

## 可用工具（文件 + 反编译）
运行时已提供 read_file / list_files / write_file，以及 list_assemblies / find_members / decompile_method / decompile_type。
上面的证据是完整原文，优先直接使用；但写任何 Harmony 补丁代码前，必须用反编译工具查询真实程序集核对签名，禁止凭记忆猜测 API。
- 原版程序集：assembly="game"
- 模组程序集：assembly=packageId 或 DLL 文件名；不确定就先用 list_assemblies 搜索
- 目标方法、补丁要调用的每个外部方法/字段/属性，都必须用 find_members 或 decompile_method 核实；有重载时用 parameter_count 或 member_id
- 不调用反编译工具就给出的 harmony 方案会被宿主拒绝编译
文件路径必须位于游戏目录或已发现的模组目录内；write_file 只能写入本案例工作区，不会修改游戏或模组目录。
一旦证据足够，立即输出最终 JSON 结论，不要为了反复确认而继续读取。
""";

    private readonly AgentConfig _config;
    private readonly AgentCaseBuilder _caseBuilder;
    private readonly AgentClient? _client;
    private readonly IReadOnlyList<ModDescriptor> _mods;
    private readonly string _gameDir;
    private readonly AgentDecompilerTools _decompilerTools;
    private readonly Action<string>? _trace;
    private readonly ConcurrentDictionary<string, AgentReport> _cache = new(StringComparer.OrdinalIgnoreCase);

    // 反编译上下文与缓存不是线程安全的：取证阶段串行化，模型调用仍可并行。
    private readonly SemaphoreSlim _caseGate = new(1, 1);

    public AgentInvestigator(
        AgentConfig config,
        IReadOnlyList<ModDescriptor> mods,
        string gameDir,
        Action<string>? trace = null)
    {
        _config = config;
        _caseBuilder = new AgentCaseBuilder(mods, gameDir);
        _mods = mods;
        _gameDir = gameDir;
        _trace = trace;
        _decompilerTools = new AgentDecompilerTools(mods, gameDir, trace);

        if (config.IsConfigured)
            _client = new AgentClient(config);
    }

    public bool UsesModel => _client != null;

    public string ModeLabel => _client == null
        ? "本地代码核查"
        : $"模型核查 · {_config.Model}";

    public async Task<AgentReport> InvestigateAsync(
        PlayerConflict conflict,
        CancellationToken cancellationToken = default,
        bool useModel = true)
    {
        var cacheKey = (useModel && _client != null ? "model:" : "local:") + conflict.Key;
        if (_cache.TryGetValue(cacheKey, out var cached))
            return cached;

        AgentCaseFile caseFile;
        await _caseGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            caseFile = await Task.Run(() => _caseBuilder.Build(conflict, cancellationToken), cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _caseGate.Release();
        }

        AgentReport report;
        if (_client != null && useModel)
        {
            try
            {
                report = await InvestigateWithModelAsync(conflict, caseFile, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                report = BuildLocalReport(conflict, caseFile,
                    $"模型调用失败，已回退本地核查：{ex.Message}");
            }
        }
        else
        {
            report = BuildLocalReport(conflict, caseFile, null);
        }

        _cache[cacheKey] = report;
        return report;
    }

    public async Task<IReadOnlyList<AgentReport>> InvestigateAllAsync(
        IReadOnlyList<PlayerConflict> conflicts,
        IProgress<(int Done, int Total, string Label)>? progress = null,
        CancellationToken cancellationToken = default,
        bool useModel = true,
        Action<AgentReport>? onReport = null)
    {
        var reports = new List<AgentReport>(conflicts.Count);
        for (var i = 0; i < conflicts.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report((i, conflicts.Count, $"核查：{conflicts[i].PairLabel}"));
            var report = await InvestigateAsync(conflicts[i], cancellationToken, useModel).ConfigureAwait(false);
            reports.Add(report);
            onReport?.Invoke(report);
            progress?.Report((i + 1, conflicts.Count, $"完成：{conflicts[i].PairLabel}"));
        }

        return reports;
    }

    private async Task<AgentReport> InvestigateWithModelAsync(
        PlayerConflict conflict,
        AgentCaseFile caseFile,
        CancellationToken cancellationToken)
    {
        var workspace = Path.Combine(
            AgentConfigLoader.DefaultCaseOutputDir,
            AgentFileTools.SafeWorkspaceName(conflict.Key));
        var fileTools = new AgentFileTools(
            _mods.Select(m => m.RootDirectory).Append(_gameDir),
            workspace);
        var prompt = caseFile.PromptText + AgentToolsPrompt + AssemblyHints(conflict);
        _decompilerTools.BeginConversation();
        var decompileCallsBefore = _decompilerTools.EvidenceCalls;

        string raw;
        try
        {
            raw = await _client!.CompleteWithToolsAsync(
                prompt,
                BuildToolDefinitions(fileTools),
                BuildToolExecutor(fileTools),
                cancellationToken).ConfigureAwait(false);
        }
        catch (AgentToolsUnsupportedException)
        {
            _trace?.Invoke("investigate: 端点不支持工具调用，Harmony 方案将被拒绝");
            raw = await _client!.CompleteAsync(prompt, cancellationToken).ConfigureAwait(false);
        }

        var json = AgentClient.ExtractJsonObject(raw)
                   ?? throw new InvalidOperationException("模型没有返回可解析的 JSON 对象。");

        var payload = JsonSerializer.Deserialize<LlmPayload>(json, PayloadOptions)
                      ?? throw new InvalidOperationException("模型返回的 JSON 为空。");

        var report = BuildModelReport(conflict, caseFile, raw, payload);
        return EnforceDecompilerEvidence(
            report,
            _decompilerTools.EvidenceCalls > decompileCallsBefore);
    }

    private IReadOnlyList<AgentToolDefinition> BuildToolDefinitions(AgentFileTools fileTools)
        => fileTools.Definitions.Concat(_decompilerTools.Definitions).ToList();

    private string AssemblyHints(PlayerConflict conflict)
        => _decompilerTools.DescribeAssembliesFor(new[] { conflict.ModAPackageId, conflict.ModBPackageId });

    private Func<AgentToolCall, CancellationToken, Task<string>> BuildToolExecutor(AgentFileTools fileTools)
        => async (call, cancellationToken) =>
        {
            _trace?.Invoke($"tool {call.Name} {Shorten(call.ArgumentsJson, 240)}");
            return _decompilerTools.Handles(call.Name)
                ? await _decompilerTools.ExecuteAsync(call, cancellationToken).ConfigureAwait(false)
                : await fileTools.ExecuteAsync(call, cancellationToken).ConfigureAwait(false);
        };

    private static AgentReport EnforceDecompilerEvidence(AgentReport report, bool usedDecompiler)
    {
        if (usedDecompiler || report.PatchPlan is not { Kind: "harmony" } plan)
            return report;

        return report with
        {
            PatchPlan = plan with
            {
                Kind = "manual",
                HarmonyCode = null,
                Summary = (plan.Summary +
                           "\n\n（未调用反编译工具核对真实程序集签名，宿主已拒绝自动编译该补丁。）").Trim()
            }
        };
    }

    private static string Shorten(string? value, int max)
        => string.IsNullOrEmpty(value) || value.Length <= max ? value ?? string.Empty : value[..max] + "…";

    /// <summary>
    /// 把真实编译器的错误连同原补丁与完整证据回传给模型，要求只修编译问题并重新给出可编译代码。
    /// 返回 null 表示模型未给出可用的新方案，调用方应保留原结论。
    /// </summary>
    public async Task<AgentReport?> RepairHarmonyPlanAsync(
        PlayerConflict conflict,
        AgentReport report,
        string compilerOutput,
        CancellationToken cancellationToken = default)
    {
        if (_client == null ||
            report.PatchPlan is not { Kind: "harmony" } plan ||
            string.IsNullOrWhiteSpace(plan.HarmonyCode) ||
            string.IsNullOrWhiteSpace(compilerOutput))
            return null;

        AgentCaseFile caseFile;
        await _caseGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            caseFile = await Task.Run(() => _caseBuilder.Build(conflict, cancellationToken), cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _caseGate.Release();
        }

        var prompt = BuildRepairPrompt(caseFile, plan.HarmonyCode, compilerOutput) + AssemblyHints(conflict);
        var workspace = Path.Combine(
            AgentConfigLoader.DefaultCaseOutputDir,
            AgentFileTools.SafeWorkspaceName(conflict.Key));
        var fileTools = new AgentFileTools(
            _mods.Select(m => m.RootDirectory).Append(_gameDir),
            workspace);
        _decompilerTools.BeginConversation();
        var decompileCallsBefore = _decompilerTools.EvidenceCalls;

        string raw;
        try
        {
            raw = await _client.CompleteWithToolsAsync(
                prompt,
                BuildToolDefinitions(fileTools),
                BuildToolExecutor(fileTools),
                cancellationToken).ConfigureAwait(false);
        }
        catch (AgentToolsUnsupportedException)
        {
            _trace?.Invoke("repair: 端点不支持工具调用，拒绝未核对真实签名的修复");
            return null;
        }

        if (_decompilerTools.EvidenceCalls <= decompileCallsBefore)
        {
            _trace?.Invoke("repair: 本次修复没有调用反编译工具，拒绝结果");
            return null;
        }

        var json = AgentClient.ExtractJsonObject(raw);
        if (json == null)
            return null;

        var payload = JsonSerializer.Deserialize<LlmPayload>(json, PayloadOptions);
        if (payload == null)
            return null;

        var repaired = BuildModelReport(conflict, caseFile, raw, payload);
        if (repaired.PatchPlan is not { Kind: "harmony" } repairedPlan ||
            string.IsNullOrWhiteSpace(repairedPlan.HarmonyCode))
            return null;

        return repaired with
        {
            RawResponse = (report.RawResponse ?? string.Empty) +
                          "\n\n===== 编译修复 =====\n" + raw,
            Error = null
        };
    }

    private AgentReport BuildModelReport(
        PlayerConflict conflict,
        AgentCaseFile caseFile,
        string raw,
        LlmPayload payload)
    {
        var verdict = ParseVerdict(payload.Verdict);
        var confidence = payload.Confidence is >= 0 and <= 1 ? payload.Confidence : 0.5;
        var hiddenRisks = (payload.HiddenRisks ?? new List<LlmRisk>())
            .Where(r => !string.IsNullOrWhiteSpace(r.Title) || !string.IsNullOrWhiteSpace(r.Detail))
            .Take(5)
            .Select(r => new AgentHiddenRisk(
                Clean(r.Title, "隐患"),
                Clean(r.Detail, string.Empty),
                NormalizeSeverity(r.Severity)))
            .ToList();

        var plan = ParsePatchPlan(payload.PatchPlan);

        return new AgentReport(
            ConflictKey: conflict.Key,
            GeneratedAtUtc: DateTime.UtcNow,
            Mode: ModeLabel,
            Verdict: verdict,
            Confidence: confidence,
            Headline: Clean(payload.Headline, conflict.Headline),
            Explanation: Clean(payload.Explanation, conflict.What),
            HiddenRisks: hiddenRisks,
            FixStrategy: NormalizeStrategy(payload.FixStrategy),
            PatchPlan: plan,
            CodeSources: BuildCodeSources(caseFile),
            EvidenceSummary: caseFile.Summary,
            RawResponse: raw,
            Error: null);
    }

    private static string BuildRepairPrompt(
        AgentCaseFile caseFile,
        string harmonyCode,
        string compilerOutput)
    {
        var errors = compilerOutput.Trim();
        if (errors.Length > 12000)
            errors = errors[^12000..];

        return caseFile.PromptText + $"""

## 编译修复
上一次给出的 Harmony 补丁在真实编译器中失败。请只修复编译错误，保持同一冲突结论与修复策略。
编译器输出：
```text
{errors}
```
待修复代码：
```csharp
{harmonyCode}
```
要求：
1. patch_plan.kind 仍为 harmony，harmony_code 必须是完整、可直接编译的 C# 代码；只输出最终的 JSON 对象。
2. 编译工程只引用 Assembly-CSharp、0Harmony 和 Unity 兼容层，没有目标模组程序集；目标模组类型必须用反射访问，并处理类型或成员缺失。
3. ThingWithComps 只有泛型 GetComp<T>()，不存在 GetComp(Type)。按动态 Type 取组件请用 thing.AllComps.FirstOrDefault(c => type.IsInstanceOfType(c))。
4. 无法直接访问的私有成员用反射或 Harmony Traverse，不要引用不存在的 API。
5. 不要声称代码被截断，不要输出 Markdown 围栏。
6. CS1501 / CS1503 / CS1061 表示你调用的游戏 API 与当前 Assembly-CSharp 不符：不得凭记忆猜测重载或可选参数，必须改成证据中出现过的调用形式；不确定时用反射（AccessTools.Method + Invoke），并逐行复查所有游戏 API 调用。
7. 修复前必须先调用 find_members / decompile_method 查询报错涉及的真实程序集与签名；本次修复没有反编译调用记录会被宿主直接拒绝。
""";
    }

    private static AgentVerdictKind ParseVerdict(string? verdict) => verdict?.Trim().ToLowerInvariant() switch
    {
        "real_conflict" => AgentVerdictKind.RealConflict,
        "condition_dependent" => AgentVerdictKind.ConditionDependent,
        "false_positive" => AgentVerdictKind.FalsePositive,
        _ => AgentVerdictKind.InsufficientEvidence
    };

    private static string NormalizeStrategy(string? strategy) => strategy?.Trim().ToLowerInvariant() switch
    {
        "order" => "order",
        "disable_one" => "disable_one",
        "compat_patch" => "compat_patch",
        "config" => "config",
        _ => "manual"
    };

    private static string NormalizeSeverity(string? severity) => severity?.Trim().ToLowerInvariant() switch
    {
        "high" => "high",
        "low" => "low",
        _ => "medium"
    };

    private static AgentPatchPlan? ParsePatchPlan(LlmPatchPlan? plan)
    {
        if (plan == null)
            return null;

        var kind = plan.Kind?.Trim().ToLowerInvariant().Replace('-', '_') ?? string.Empty;
        if (kind is not ("harmony" or "xml" or "order_only" or "manual"))
            kind = "manual";

        var files = (plan.Files ?? new List<LlmPatchFile>())
            .Where(f => !string.IsNullOrWhiteSpace(f.Content))
            .Select(f => new AgentPatchFile(SafeRelativePath(f.RelativePath), f.Content!))
            .Where(f => f.RelativePath.Length > 0)
            .ToList();

        var harmonyCode = plan.HarmonyCode;
        if (kind == "harmony" && string.IsNullOrWhiteSpace(harmonyCode))
            kind = "manual";

        if (kind == "xml" && files.Count == 0)
            kind = "manual";

        return new AgentPatchPlan(
            Kind: kind,
            Summary: Clean(plan.Summary, string.Empty),
            RiskNote: Clean(plan.RiskNote, null),
            HarmonyCode: harmonyCode,
            Files: files);
    }

    private static string SafeRelativePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        var normalized = path.Replace('\\', '/').TrimStart('/');
        if (normalized.Length == 0 || normalized.Contains("..", StringComparison.Ordinal) || normalized.Contains(':'))
            return string.Empty;

        if (!normalized.StartsWith("Patches/", StringComparison.OrdinalIgnoreCase) &&
            !normalized.StartsWith("Defs/", StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        return normalized;
    }

    private static string Clean(string? value, string? fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback ?? string.Empty : value.Trim();

    private static IReadOnlyList<string> BuildCodeSources(AgentCaseFile caseFile)
        => caseFile.Samples
            .Select(s => $"{s.Source} · {s.Member}")
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    // ── 本地确定性核查 ─────────────────────────────────────────────────────

    private AgentReport BuildLocalReport(PlayerConflict conflict, AgentCaseFile caseFile, string? error)
    {
        var joinedEvidence = string.Join("\n", caseFile.Samples.Select(s => s.Member + "\n" + s.Code));
        var hasBlockingEvidence = joinedEvidence.Contains("无条件跳过原方法", StringComparison.Ordinal);
        var hasConditionalGuard = joinedEvidence.Contains("条件性跳过原方法", StringComparison.Ordinal) ||
                                  joinedEvidence.Contains("带条件守卫", StringComparison.Ordinal);
        var hasCodeMatcher = joinedEvidence.Contains("使用 CodeMatcher", StringComparison.Ordinal);
        var hasSnippet = caseFile.Samples.Any(s => s.Code.Length > 80);
        var recommendedAfter = conflict.Findings
            .Select(f => f.RecommendedAfterPackageId)
            .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id));
        var recommendedBefore = conflict.Findings
            .Select(f => f.RecommendedBeforePackageId)
            .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id));

        var (verdict, confidence) = conflict.Category switch
        {
            "HarmonyBlockingPrefixConflict" when hasBlockingEvidence => (AgentVerdictKind.RealConflict, 0.94),
            "HarmonyBlockingPrefixConflict" => (AgentVerdictKind.RealConflict, 0.84),
            "DuplicateXmlDefOverwrite" => (AgentVerdictKind.ConditionDependent, 0.6),
            "DestructiveXmlPatchCollision" => (AgentVerdictKind.ConditionDependent, 0.56),
            "ExplicitIncompatibility" => (AgentVerdictKind.RealConflict, 0.96),
            "MissingDependency" => (AgentVerdictKind.RealConflict, 0.96),
            "CyclicLoadOrderConstraint" => (AgentVerdictKind.RealConflict, 0.9),
            // 游戏自己打印的红字，证据来自运行时而不是静态推断
            "BrokenXmlDefReference" => (AgentVerdictKind.RealConflict, 0.95),
            "XmlDefConfigError" => (AgentVerdictKind.RealConflict, 0.9),
            "ConditionalPrefixOrResultMutationOverlap" => (AgentVerdictKind.ConditionDependent, hasConditionalGuard ? 0.7 : 0.58),
            "MultipleTranspilersSameTarget" => (AgentVerdictKind.ConditionDependent, hasCodeMatcher ? 0.72 : 0.6),
            "ConditionalOrMixedXmlPatchOverlap" => (AgentVerdictKind.ConditionDependent, 0.58),
            "StructuralXmlPatchOverlap" => (AgentVerdictKind.ConditionDependent, 0.52),
            _ => (AgentVerdictKind.InsufficientEvidence, 0.35)
        };

        var headline = verdict switch
        {
            AgentVerdictKind.RealConflict when conflict.Category == "HarmonyBlockingPrefixConflict" && hasBlockingEvidence
                => $"已在代码中确认：「{conflict.ModAName}」完全接管了目标方法，「{conflict.ModBName}」的修改无法执行。",
            AgentVerdictKind.RealConflict => conflict.Headline,
            AgentVerdictKind.ConditionDependent => $"代码层面存在重叠，但是否出问题取决于运行条件：{conflict.Headline}",
            _ => conflict.Headline
        };

        var explanation = BuildLocalExplanation(conflict, caseFile, verdict, hasBlockingEvidence, hasConditionalGuard,
            hasCodeMatcher, hasSnippet);

        var risks = BuildLocalRisks(conflict, hasBlockingEvidence, hasConditionalGuard, hasCodeMatcher);
        var strategy = conflict.Category switch
        {
            "HarmonyBlockingPrefixConflict" => "compat_patch",
            "ConditionalPrefixOrResultMutationOverlap" => "order",
            "MultipleTranspilersSameTarget" => "order",
            "DuplicateXmlDefOverwrite" => "order",
            "DestructiveXmlPatchCollision" => "order",
            "ConditionalOrMixedXmlPatchOverlap" => "order",
            "StructuralXmlPatchOverlap" => "order",
            "ExplicitIncompatibility" => "disable_one",
            "MissingDependency" => "manual",
            "CyclicLoadOrderConstraint" => "disable_one",
            "BrokenXmlDefReference" => "manual",
            "XmlDefConfigError" => "manual",
            _ => "manual"
        };

        var plan = BuildLocalPatchPlan(conflict, strategy, hasBlockingEvidence,
            recommendedAfter, recommendedBefore);

        return new AgentReport(
            ConflictKey: conflict.Key,
            GeneratedAtUtc: DateTime.UtcNow,
            Mode: "本地代码核查",
            Verdict: verdict,
            Confidence: confidence,
            Headline: headline,
            Explanation: explanation,
            HiddenRisks: risks,
            FixStrategy: strategy,
            PatchPlan: plan,
            CodeSources: BuildCodeSources(caseFile),
            EvidenceSummary: caseFile.Summary,
            RawResponse: null,
            Error: error);
    }

    private static string BuildLocalExplanation(
        PlayerConflict conflict,
        AgentCaseFile caseFile,
        AgentVerdictKind verdict,
        bool hasBlockingEvidence,
        bool hasConditionalGuard,
        bool hasCodeMatcher,
        bool hasSnippet)
    {
        var parts = new List<string> { conflict.What };

        if (caseFile.Samples.Count == 0)
        {
            parts.Add("没有取到可直接阅读的补丁代码（可能依赖程序集缺失或补丁由动态方式注册），以下结论仅来自静态扫描。");
        }
        else
        {
            parts.Add($"本次核查读取了 {caseFile.Samples.Count} 份代码证据：{caseFile.Summary}。");

            if (hasBlockingEvidence)
                parts.Add("补丁方法中可以看到明确的无条件 return false / 跳过原方法，这不是静态分析的推测，目标方法的原有逻辑确实不会执行。");
            else if (hasConditionalGuard)
                parts.Add("补丁带有条件守卫，只在特定兵种、状态或配置下才会跳过/改写原方法，因此影响范围是条件性的。");
            else if (hasCodeMatcher)
                parts.Add("补丁使用 CodeMatcher 定位指令。当另一个模组改变了同一段代码结构时，查找可能失败并抛出 Harmony 异常。");
            else if (!hasSnippet)
                parts.Add("补丁签名已确认，但反编译片段不完整，结论以签名与静态扫描为准。");
        }

        if (verdict == AgentVerdictKind.ConditionDependent)
            parts.Add("建议先在游戏内复现具体场景；若问题只在部分条件下出现，优先调整顺序而不是直接禁用。");

        return string.Join(" ", parts);
    }

    private static IReadOnlyList<AgentHiddenRisk> BuildLocalRisks(
        PlayerConflict conflict,
        bool hasBlockingEvidence,
        bool hasConditionalGuard,
        bool hasCodeMatcher)
    {
        var risks = new List<AgentHiddenRisk>();

        switch (conflict.Category)
        {
            case "HarmonyBlockingPrefixConflict":
                risks.Add(new AgentHiddenRisk(
                    "被接管的方法失去合并点",
                    hasBlockingEvidence
                        ? $"「{conflict.ModAName}」无条件跳过原方法并自行实现全部逻辑，后续任何模组对该方法的修改都不会执行。"
                        : $"「{conflict.ModAName}」的 Prefix 具备截断原方法的能力，另一个模组的修改可能被整体跳过。",
                    "high"));
                risks.Add(new AgentHiddenRisk(
                    "上游更新会造成静默偏差",
                    "完整复刻原方法逻辑的模组不会跟随游戏更新自动修正；游戏版本变化后，表现可能与原版产生细微差异。",
                    "medium"));
                risks.Add(new AgentHiddenRisk(
                    "兼容补丁需要同时复刻两边逻辑",
                    "这类冲突无法靠加载顺序解决。合并补丁必须基于当前版本的目标方法源码编写，任一上游模组更新后都需要重新核对。",
                    "medium"));
                break;

            case "MultipleTranspilersSameTarget":
                risks.Add(new AgentHiddenRisk(
                    "改写锚点相互依赖",
                    "两个 Transpiler 都在同一方法内查找指令位置；一方改变结构后，另一方的锚点可能失效。",
                    "high"));
                if (hasCodeMatcher)
                    risks.Add(new AgentHiddenRisk(
                        "CodeMatcher 找不到锚点会抛异常",
                        "使用 CodeMatcher 的补丁在匹配失败时通常会抛出异常，表现为加载后红字或功能整体失效。",
                        "high"));
                break;

            case "ConditionalPrefixOrResultMutationOverlap":
                risks.Add(new AgentHiddenRisk(
                    "结果覆盖顺序不透明",
                    "两个补丁都修改返回值时，最终结果取决于执行顺序与条件分支，静态分析无法证明某一方总是生效。",
                    hasConditionalGuard ? "medium" : "high"));
                risks.Add(new AgentHiddenRisk(
                    "只在特定条件下出错",
                    hasConditionalGuard
                        ? "带条件守卫的补丁只在特定角色/状态下生效，问题可能表现为偶发而不是必现。"
                        : "部分场景正常、部分场景异常，容易误判为游戏本身的随机问题。",
                    "medium"));
                break;

            case "DuplicateXmlDefOverwrite":
                risks.Add(new AgentHiddenRisk(
                    "整段覆盖会丢失字段",
                    "后加载的 def 会整段替换先加载的版本，先加载方新增的字段、列表项会被静默丢弃。",
                    "high"));
                risks.Add(new AgentHiddenRisk(
                    "更新后覆盖关系可能反转",
                    "任一模组更新后 def 内容变化，原本正常的字段可能再次丢失。",
                    "medium"));
                break;

            case "BrokenXmlDefReference":
                risks.Add(new AgentHiddenRisk(
                    "引用被静默丢弃",
                    "游戏解析不到这条引用时会打印红字并按空值继续，物品、音效或伤害数值会缺失，但不会崩溃，容易被忽略。",
                    "high"));
                risks.Add(new AgentHiddenRisk(
                    "顺序调整无效",
                    "跨引用在全部 Def 载入后才统一解析，调整加载顺序不会改变结果；必须改引用本身或补上定义它的模组。",
                    "medium"));
                break;

            case "XmlDefConfigError":
                risks.Add(new AgentHiddenRisk(
                    "出错字段被忽略",
                    "游戏只跳过配置有误的那部分数据，其余照常生效，表现为数值不对而不是功能全失，很难肉眼定位。",
                    "medium"));
                break;

            case "DestructiveXmlPatchCollision":
                risks.Add(new AgentHiddenRisk(
                    "删除/替换会抹掉追加内容",
                    "破坏性 PatchOperation 会清空或替换整个节点，其他模组追加到同一节点的内容会一起消失。",
                    "high"));
                break;

            case "ConditionalOrMixedXmlPatchOverlap":
            case "StructuralXmlPatchOverlap":
                risks.Add(new AgentHiddenRisk(
                    "节点顺序依赖执行顺序",
                    "同一节点上混用追加与替换/定点修改时，最终结构与顺序取决于执行先后，静态分析无法完全确定。",
                    "medium"));
                break;

            case "ExplicitIncompatibility":
                risks.Add(new AgentHiddenRisk(
                    "作者声明的不兼容通常涉及深层假设",
                    "incompatibleWith 往往来自存档结构、全局状态或核心机制的冲突，强行共存可能导致存档损坏。",
                    "high"));
                break;

            case "MissingDependency":
                risks.Add(new AgentHiddenRisk(
                    "缺少前置会直接报错",
                    "游戏在加载 Def 阶段就会因为找不到依赖而报错，模组内容不会正常工作。",
                    "high"));
                risks.Add(new AgentHiddenRisk(
                    "版本不匹配同样会失败",
                    "即使安装了同名前置，版本或分支不匹配仍可能缺少 Def 或程序集。",
                    "medium"));
                break;

            case "CyclicLoadOrderConstraint":
                risks.Add(new AgentHiddenRisk(
                    "排序只能满足一边",
                    "两边声明的加载顺序互相矛盾，当前排序已强制断开一条约束，另一边可能因顺序不对而异常。",
                    "high"));
                break;
        }

        risks.Add(new AgentHiddenRisk(
            "结论基于当前版本与当前加载顺序",
            "更新模组或改动加载顺序后，代码证据可能变化；重新分析并再次核查即可。",
            "low"));

        return risks;
    }

    private static AgentPatchPlan BuildLocalPatchPlan(
        PlayerConflict conflict,
        string strategy,
        bool hasBlockingEvidence,
        string? recommendedAfter,
        string? recommendedBefore)
    {
        var orderHint = BuildOrderHint(conflict, recommendedAfter, recommendedBefore);

        if (strategy == "order" && orderHint != null)
        {
            return new AgentPatchPlan(
                Kind: "order_only",
                Summary: orderHint,
                RiskNote: "调整顺序只能改变覆盖结果，不能消除两边的逻辑重叠；若游戏内仍有异常，需要兼容补丁。",
                HarmonyCode: null,
                Files: Array.Empty<AgentPatchFile>());
        }

        if (conflict.Category == "HarmonyBlockingPrefixConflict")
        {
            return new AgentPatchPlan(
                Kind: "manual",
                Summary:
                    $"二选一：禁用「{conflict.ModAName}」或「{conflict.ModBName}」。若必须共存，需要基于目标方法「{conflict.TargetLabel}」的当前源码合并两边逻辑，写成新的 Harmony Prefix/Transpiler 补丁；配置模型后 Agent 可自动生成并编译该补丁。",
                RiskNote: hasBlockingEvidence
                    ? "目标方法已被完整接管，任何只调整顺序的方案都无效。"
                    : "静态证据显示存在截断型 Prefix，但未能读取完整方法体；编写补丁前必须先核对源码。",
                HarmonyCode: null,
                Files: Array.Empty<AgentPatchFile>());
        }

        var summary = conflict.Category switch
        {
            "ExplicitIncompatibility" =>
                $"禁用「{conflict.ModAName}」或「{conflict.ModBName}」之一；作者声明的互斥不建议绕过。",
            "MissingDependency" =>
                $"安装并启用前置「{conflict.ModBName}」，或暂时禁用「{conflict.ModAName}」。",
            "CyclicLoadOrderConstraint" =>
                "检查两边模组是否有更新修复了 loadAfter/loadBefore 声明；否则禁用其中一个。",
            "DuplicateXmlDefOverwrite" =>
                $"确认要保留「{conflict.TargetLabel}」的哪个版本；需要合并字段时，让 Agent 生成针对该 def 的 XML 合并补丁。",
            "DestructiveXmlPatchCollision" =>
                $"先确认两边的执行顺序；若必须同时保留两边的数据，需要 Agent 基于两份 XML 原文生成合并补丁。",
            "BrokenXmlDefReference" =>
                $"补上或启用定义「{conflict.TargetLabel}」的模组；若引用本身写错（类型不对、值带空白、名字已改），让 Agent 生成一条 XML 补丁改掉它。",
            "XmlDefConfigError" =>
                $"这是「{conflict.ModAName}」自己的数据问题：等作者更新，或让 Agent 生成 XML 补丁修正出错的字段。",
            _ =>
                "先按推荐顺序排列并在游戏内验证；仍有异常时，配置模型让 Agent 基于代码证据生成兼容补丁。"
        };

        return new AgentPatchPlan(
            Kind: "manual",
            Summary: summary + (orderHint != null ? " " + orderHint : string.Empty),
            RiskNote: "本地核查不会自动生成未经核对的代码补丁；配置模型后可自动编写并编译。",
            HarmonyCode: null,
            Files: Array.Empty<AgentPatchFile>());
    }

    private static string? BuildOrderHint(
        PlayerConflict conflict,
        string? recommendedAfter,
        string? recommendedBefore)
    {
        if (!string.IsNullOrWhiteSpace(recommendedAfter))
        {
            var other = recommendedAfter.Equals(conflict.ModAPackageId, StringComparison.OrdinalIgnoreCase)
                ? conflict.ModBName
                : conflict.ModAName;
            var target = recommendedAfter.Equals(conflict.ModAPackageId, StringComparison.OrdinalIgnoreCase)
                ? conflict.ModAName
                : conflict.ModBName;
            return $"在 ModsConfig 中将「{other}」排在「{target}」之后，让需要的一方最后执行。";
        }

        if (!string.IsNullOrWhiteSpace(recommendedBefore))
        {
            var other = recommendedBefore.Equals(conflict.ModAPackageId, StringComparison.OrdinalIgnoreCase)
                ? conflict.ModBName
                : conflict.ModAName;
            var target = recommendedBefore.Equals(conflict.ModAPackageId, StringComparison.OrdinalIgnoreCase)
                ? conflict.ModAName
                : conflict.ModBName;
            return $"在 ModsConfig 中将「{other}」排在「{target}」之前，让需要的一方最后执行。";
        }

        return null;
    }

    public void Dispose()
    {
        _client?.Dispose();
        _decompilerTools.Dispose();
        _caseBuilder.Dispose();
        _caseGate.Dispose();
    }

    // ── 模型响应 DTO ───────────────────────────────────────────────────────

    private sealed class LlmPayload
    {
        [JsonPropertyName("verdict")] public string? Verdict { get; set; }
        [JsonPropertyName("confidence")] public double Confidence { get; set; }
        [JsonPropertyName("headline")] public string? Headline { get; set; }
        [JsonPropertyName("explanation")] public string? Explanation { get; set; }
        [JsonPropertyName("hidden_risks")] public List<LlmRisk>? HiddenRisks { get; set; }
        [JsonPropertyName("fix_strategy")] public string? FixStrategy { get; set; }
        [JsonPropertyName("patch_plan")] public LlmPatchPlan? PatchPlan { get; set; }
    }

    private sealed class LlmRisk
    {
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("detail")] public string? Detail { get; set; }
        [JsonPropertyName("severity")] public string? Severity { get; set; }
    }

    private sealed class LlmPatchPlan
    {
        [JsonPropertyName("kind")] public string? Kind { get; set; }
        [JsonPropertyName("summary")] public string? Summary { get; set; }
        [JsonPropertyName("risk_note")] public string? RiskNote { get; set; }
        [JsonPropertyName("harmony_code")] public string? HarmonyCode { get; set; }
        [JsonPropertyName("files")] public List<LlmPatchFile>? Files { get; set; }
    }

    private sealed class LlmPatchFile
    {
        [JsonPropertyName("relative_path")] public string? RelativePath { get; set; }
        [JsonPropertyName("content")] public string? Content { get; set; }
    }
}
