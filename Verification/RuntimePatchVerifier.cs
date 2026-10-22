using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ModSorterAgent.Models;

namespace ModSorterAgent.Verification;

/// <summary>
/// 运行期 Harmony 补丁链核验。
///
/// 静态分析只能推断"谁可能 patch 谁"，真正的补丁应用顺序只有 Harmony 自己知道。
/// 本核验通过 RuntimePatchProbe companion 模组（<c>rimpatch/*</c> 工具）读取
/// <c>Harmony.GetPatchInfo</c> 的真实结果，与静态预测逐条比对：
///
/// · 静态判为"顺序敏感"但运行期 owner 的 priority 明显不同 → 该冲突实际不受加载顺序影响，降级；
/// · 静态判为"无冲突"但运行期同一方法上有多个 owner 且 priority 相同 → 真实顺序敏感，升为黄色。
///
/// 只读：绝不 patch、不改加载顺序、不写盘。
/// </summary>
public static class RuntimePatchVerifier
{
    public sealed record ChainEntry(
        string Kind,
        int StoredIndex,
        string Owner,
        int Priority,
        string PriorityLabel,
        string PatchMethod,
        string PatchAssembly,
        IReadOnlyList<string> Before,
        IReadOnlyList<string> After);

    public sealed record MethodChain(
        string TargetKey,
        string DeclaringType,
        string MethodName,
        IReadOnlyList<string> Owners,
        IReadOnlyList<ChainEntry> Entries);

    public sealed record VerificationOutcome(
        bool Available,
        string Status,
        int MethodCount,
        int PatchCount,
        IReadOnlyList<MethodChain> Chains,
        IReadOnlyList<string> Notes);

    /// <summary>
    /// 从运行中的游戏读取补丁索引。游戏未运行、桥接不可用、或 probe 模组未加载时返回 Available=false。
    /// </summary>
    public static VerificationOutcome Query(
        string gabsExePath,
        int minOwners = 2,
        CancellationToken cancellationToken = default)
    {
        var notes = new List<string>();

        if (!File.Exists(gabsExePath))
        {
            return Unavailable("未找到 GABS 可执行文件，无法读取运行期补丁链。", notes);
        }

        try
        {
            var psi = new ProcessStartInfo(gabsExePath, "server stdio")
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardInputEncoding = new UTF8Encoding(false),
                StandardOutputEncoding = Encoding.UTF8,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc == null)
                return Unavailable("无法启动 GABS 适配进程。", notes);

            try
            {
                Send(proc, 1, "initialize", new
                {
                    protocolVersion = "2024-11-05",
                    capabilities = new { },
                    clientInfo = new { name = "ModSorterAgent", version = "1.0.0" }
                });
                ReadResponse(proc, 1, TimeSpan.FromSeconds(10));

                Send(proc, 2, "notifications/initialized", new { });

                cancellationToken.ThrowIfCancellationRequested();

                // 只连接已在运行的游戏，绝不 start/stop/kill
                Send(proc, 3, "tools/call", new
                {
                    name = "games_connect",
                    arguments = new { gameId = "rimworld", forceTakeover = true, timeout = 8 }
                });
                var connect = ReadResponse(proc, 3, TimeSpan.FromSeconds(25));
                if (connect == null || !connect.Contains("\"connected\":true", StringComparison.Ordinal))
                {
                    notes.Add(connect == null ? "连接请求无响应。" : "连接响应未包含 connected=true。");
                    return Unavailable("未能连接运行中的游戏桥接。", notes);
                }

                Send(proc, 4, "tools/call", new
                {
                    name = "games_call_tool",
                    arguments = new
                    {
                        gameId = "rimworld",
                        tool = "rimpatch/export_patch_index",
                        arguments = new { minOwners },
                        timeout = 30
                    }
                });
                var exportResp = ReadResponse(proc, 4, TimeSpan.FromSeconds(50));

                if (exportResp == null)
                    return Unavailable("读取补丁索引超时。", notes);

                return ParseExport(exportResp, notes);
            }
            finally
            {
                try
                {
                    proc.StandardInput.Close();
                    if (!proc.WaitForExit(1500))
                        proc.Kill(); // 只结束短命的 gabs.exe 适配进程，绝不触碰 RimWorldWin64
                }
                catch
                {
                    // 适配进程清理失败不影响结论
                }
            }
        }
        catch (Exception ex)
        {
            return Unavailable($"读取运行期补丁链失败：{ex.GetType().Name}: {ex.Message}", notes);
        }
    }

    private static VerificationOutcome ParseExport(string rawJson, List<string> notes)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawJson);

            if (!TryGetToolText(doc.RootElement, out var text))
                return Unavailable("桥接响应里没有工具结果。", notes);

            using var payload = JsonDocument.Parse(text);
            var root = payload.RootElement;

            if (root.TryGetProperty("error", out var err))
            {
                var code = err.TryGetProperty("code", out var c) ? c.GetString() : null;
                if (code == "tool.not_found" || (err.TryGetProperty("message", out var m) &&
                                                 (m.GetString() ?? string.Empty).Contains("rimpatch/", StringComparison.Ordinal)))
                {
                    notes.Add("RuntimePatchProbe 模组未加载：请在模组列表中启用它并重启游戏，之后才能核验运行期补丁链。");
                    return Unavailable("RuntimePatchProbe companion 未加载。", notes);
                }

                return Unavailable($"补丁索引读取失败：{code ?? "unknown"}", notes);
            }

            var chains = new List<MethodChain>();
            var methodCount = 0;
            var patchCount = 0;

            if (root.TryGetProperty("methodCount", out var mc) && mc.TryGetInt32(out var mcVal))
                methodCount = mcVal;
            if (root.TryGetProperty("patchCount", out var pc) && pc.TryGetInt32(out var pcVal))
                patchCount = pcVal;

            if (root.TryGetProperty("methods", out var methods) && methods.ValueKind == JsonValueKind.Array)
            {
                foreach (var method in methods.EnumerateArray())
                {
                    var targetKey = GetString(method, "targetKey");
                    var owners = new List<string>();
                    if (method.TryGetProperty("owners", out var ownersEl) && ownersEl.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var owner in ownersEl.EnumerateArray())
                        {
                            var value = owner.GetString();
                            if (!string.IsNullOrWhiteSpace(value))
                                owners.Add(value);
                        }
                    }

                    chains.Add(new MethodChain(
                        targetKey,
                        GetString(method, "declaringType"),
                        GetString(method, "methodName"),
                        owners,
                        ParseChains(method)));
                }
            }

            var status = chains.Count == 0
                ? "运行期未发现跨模组补丁重叠（无需要核验的冲突点）。"
                : $"已读取 {methodCount} 个跨模组补丁方法、{patchCount} 条补丁。";

            return new VerificationOutcome(true, status, methodCount, patchCount, chains, notes);
        }
        catch (Exception ex)
        {
            var head = rawJson.Length > 300 ? rawJson[..300] : rawJson;
            return Unavailable($"补丁索引解析失败：{ex.GetType().Name}: {ex.Message}｜原始响应开头：{head}", notes);
        }
    }

    private static IReadOnlyList<ChainEntry> ParseChains(JsonElement method)
    {
        var entries = new List<ChainEntry>();
        if (!method.TryGetProperty("chains", out var chains) || chains.ValueKind != JsonValueKind.Object)
            return entries;

        foreach (var kind in new[] { "prefixes", "postfixes", "transpilers", "finalizers" })
        {
            if (!chains.TryGetProperty(kind, out var list) || list.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var item in list.EnumerateArray())
            {
                entries.Add(new ChainEntry(
                    Kind: GetString(item, "kind"),
                    StoredIndex: GetInt(item, "storedIndex"),
                    Owner: GetString(item, "owner"),
                    Priority: GetInt(item, "priority"),
                    PriorityLabel: GetString(item, "priorityLabel"),
                    PatchMethod: GetString(item, "patchMethod"),
                    PatchAssembly: GetString(item, "patchAssembly"),
                    Before: GetStringArray(item, "before"),
                    After: GetStringArray(item, "after")));
            }
        }

        return entries;
    }

    private static bool TryGetToolText(JsonElement root, out string text)
    {
        text = string.Empty;
        if (!root.TryGetProperty("result", out var result))
            return false;

        if (!result.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
            return false;

        foreach (var item in content.EnumerateArray())
        {
            if (item.TryGetProperty("text", out var t))
            {
                text = t.GetString() ?? string.Empty;
                return text.Length > 0;
            }
        }

        return false;
    }

    private static string GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    /// <summary>
    /// 归一化补丁目标键，使静态分析与运行期两侧能 join：
    /// 去掉 Harmony 的 [Getter]/[Setter]/[Constructor] 后缀，统一去前导点、小写比较交给调用方。
    /// </summary>
    private static string NormalizeTargetKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return string.Empty;

        var trimmed = key.Trim().TrimStart('.');
        var bracket = trimmed.IndexOf('[');
        return bracket > 0 ? trimmed[..bracket] : trimmed;
    }

    private static int GetInt(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt32(out var result) ? result : 0;

    private static IReadOnlyList<string> GetStringArray(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();

        var list = new List<string>();
        foreach (var item in value.EnumerateArray())
        {
            var text = item.GetString();
            if (!string.IsNullOrWhiteSpace(text))
                list.Add(text);
        }

        return list;
    }

    private static VerificationOutcome Unavailable(string status, List<string> notes) =>
        new(false, status, 0, 0, Array.Empty<MethodChain>(), notes);

    private static void Send(Process proc, int id, string method, object parameters)
    {
        var json = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id,
            method,
            @params = parameters
        });
        proc.StandardInput.WriteLine(json);
        proc.StandardInput.Flush();
    }

    private static string? ReadLine(Process proc, TimeSpan timeout)
    {
        var task = Task.Run(proc.StandardOutput.ReadLine);
        return task.Wait(timeout) ? task.Result : null;
    }

    /// <summary>
    /// 读取直到出现指定 id 的 JSON-RPC 响应。
    /// GABS 会先推送 notifications/resources/list_changed 之类的通知行，
    /// 只读一行会读到通知而不是结果。
    /// </summary>
    private static string? ReadResponse(Process proc, int expectedId, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
                break;

            var line = ReadLine(proc, remaining);
            if (line == null)
                return null;

            var trimmed = line.Trim();
            if (!trimmed.StartsWith('{'))
                continue; // gabs 的日志行

            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                if (doc.RootElement.TryGetProperty("id", out var idEl) &&
                    idEl.TryGetInt32(out var id) && id == expectedId)
                {
                    return trimmed;
                }
            }
            catch
            {
                // 非 JSON 行，跳过
            }
        }

        return null;
    }

    /// <summary>
    /// 用运行期事实修正定级与处置建议。
    ///
    /// 关键区分：patch 的执行次序由 Harmony priority 与加载顺序共同决定——
    ///   · priority 有并列（或存在 HarmonyBefore/After 声明）→ 次序取决于加载顺序，本工具的排序结果对它有约束力；
    ///   · priority 互不相同 → 次序已被 priority 锁死，重排加载顺序无效，但冲突本身可能依然存在
    ///     （典型：高优先级那方是截断型 Prefix，会整体跳过后续补丁），因此只改写建议、不降级定级。
    ///
    /// 返回被改写的条目数。
    /// </summary>
    public static int RefineFindings(
        VerificationOutcome outcome,
        List<PairwiseConflictFinding> findings)
    {
        if (!outcome.Available || outcome.Chains.Count == 0)
            return 0;

        // 静态键带 [Getter]/[Constructor] 之类的后缀，运行期键是 Type.Method，两边先归一化再 join。
        var chainsByKey = new Dictionary<string, MethodChain>(StringComparer.OrdinalIgnoreCase);
        foreach (var chain in outcome.Chains)
        {
            var key = NormalizeTargetKey(chain.TargetKey);
            if (!chainsByKey.ContainsKey(key))
                chainsByKey[key] = chain;
        }

        var refined = 0;

        for (var i = 0; i < findings.Count; i++)
        {
            var finding = findings[i];
            if (finding.Grade != ConflictGrade.Yellow && finding.Grade != ConflictGrade.Red)
                continue;

            if (!chainsByKey.TryGetValue(NormalizeTargetKey(finding.TargetSignature), out var chain))
                continue;

            // 只处理"同一方法上多个模组"的情形，单 owner 与顺序无关
            if (chain.Owners.Count < 2)
                continue;

            // 按 owner 归并出每个模组在该方法上的优先级（同模组可能有多条同优先级补丁）。
            var byOwner = chain.Entries
                .Where(e => !string.IsNullOrWhiteSpace(e.Owner))
                .GroupBy(e => e.Owner, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Priority).Distinct().ToList(), StringComparer.OrdinalIgnoreCase);

            if (byOwner.Count < 2)
                continue;

            // 关键判据：全链是否所有 owner 的优先级都互不相同。
            // 只要存在并列（两个模组取同一 priority），同优先级内的先后就仍由加载顺序决定，
            // 那种情况下不能降级——否则会误判"顺序无关"而放过真实冲突。
            var allPriorities = byOwner.Values.SelectMany(v => v).ToList();
            var hasTie = allPriorities.Count != allPriorities.Distinct().Count();

            // Harmony 的 before/after 声明会覆盖 priority 排序（PatchSorter 显式建依赖边），
            // 存在这类声明时优先级数字不能唯一确定次序，同样不许降级。
            var hasExplicitOrder = chain.Entries.Any(e => e.Before.Count > 0 || e.After.Count > 0);

            if (hasTie || hasExplicitOrder)
            {
                var reason = hasExplicitOrder
                    ? "存在 HarmonyBefore/HarmonyAfter 显式声明，次序由该声明与 priority 共同决定"
                    : $"priority 存在并列（{string.Join(" / ", byOwner.Select(kv => $"{kv.Key}={string.Join(",", kv.Value)}"))}），同优先级内的先后仍取决于加载顺序";

                findings[i] = finding with
                {
                    Evidence = finding.Evidence
                        .Concat(new[] { $"[运行期核验] {chain.TargetKey} 上 {byOwner.Count} 个模组：{reason}" })
                        .ToArray()
                };
                continue;
            }

            // 优先级互不相同 → 执行次序已由 priority 唯一确定，调加载顺序改变不了结果。
            // 注意这不是"无冲突"：次序固定不等于次序安全。若高优先级那方是截断型 Prefix，
            // Harmony 编译出的 __runOriginal 闸门会让低优先级的补丁整体被跳过，问题依然成立，
            // 只是重排加载顺序救不了它。因此这里只改写处置建议，绝不降级定级。
            var desc = string.Join(" / ", byOwner.Select(kv => $"{kv.Key}={kv.Value.FirstOrDefault()}"));

            findings[i] = finding with
            {
                Evidence = finding.Evidence
                    .Concat(new[]
                    {
                        $"[运行期核验] {chain.TargetKey} 的实际 priority：{desc}；执行次序由 priority 唯一确定，与加载顺序无关"
                    })
                    .ToArray(),
                RecommendationReason = "运行期核验：该次序由 Harmony priority 唯一确定，调整加载顺序不会改变结果；" +
                                       "若需要改变执行先后，只能在补丁侧调整 priority 或做兼容合并"
            };
            refined++;
        }

        return refined;
    }
}
