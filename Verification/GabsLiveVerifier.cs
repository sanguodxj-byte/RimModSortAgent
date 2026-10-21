using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ModSorterAgent.Models;

namespace ModSorterAgent.Verification;

public static class GabsLiveVerifier
{
    /// <summary>GABS 可执行文件：环境变量 GABS_EXE → 应用目录及其祖先下的 GABS 目录 → 用户目录。</summary>
    public static string DefaultGabsExePath { get; } = ResolveGabsExePath();

    public static string DefaultGabsRuntimeJsonPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".gabs", "rimworld", "runtime.json");

    /// <summary>
    /// 玩家日志路径。原版写在配置根目录，Waylith 构建写在 Logs/ 子目录，两处都探一遍。
    /// </summary>
    public static string DefaultPlayerLogPath { get; } = ResolvePlayerLogPath();

    private static string ResolvePlayerLogPath()
    {
        var configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "AppData", "LocalLow", "Ludeon Studios", "RimWorld by Ludeon Studios");

        var candidates = new[]
        {
            Path.Combine(configDir, "Logs", "Player.log"),
            Path.Combine(configDir, "Player.log")
        };

        return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
    }

    private static string ResolveGabsExePath()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable("GABS_EXE");
        if (!string.IsNullOrWhiteSpace(fromEnvironment) && File.Exists(fromEnvironment))
            return fromEnvironment;

        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "GABS", "gabs.exe");
            if (File.Exists(candidate))
                return candidate;
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            foreach (var candidate in new[]
            {
                Path.Combine(userProfile, ".gabs", "gabs.exe"),
                Path.Combine(userProfile, "GABS", "gabs.exe")
            })
            {
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        return Path.Combine(AppContext.BaseDirectory, "gabs.exe");
    }

    // HARD SAFETY RULE: Never invoke games_stop, games_kill, or terminate RimWorld under any circumstance.
    private static readonly HashSet<string> AllowedGabsTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "games_status",
        "games_connect",
        "games_call_tool",
        "games_tool_names",
        "games_get_attention",
        "games_ack_attention"
    };

    public static GabsLiveVerificationSummary VerifyAndRefineFindings(
        IReadOnlyList<ModDescriptor> mods,
        List<PairwiseConflictFinding> findings,
        bool useGabsMcp = true,
        CancellationToken cancellationToken = default)
    {
        var configWarnings = new List<string>();
        var relevantErrors = new List<string>();
        var relevantWarnings = new List<string>();
        var escalatedPairs = new List<string>();

        var gameRunning = CheckGameRunningStatus(out var statusSummary);
        var connected = false;

        cancellationToken.ThrowIfCancellationRequested();

        if (gameRunning && useGabsMcp && File.Exists(DefaultGabsExePath))
        {
            connected = TryQueryGabsBridge(configWarnings, relevantErrors, relevantWarnings, cancellationToken);
        }

        ScanPlayerLogForModErrors(mods, relevantErrors, relevantWarnings, cancellationToken);

        // Cross-reference live errors with Yellow findings: escalate matching Yellow items to Red
        for (var i = 0; i < findings.Count; i++)
        {
            var finding = findings[i];
            if (finding.Grade != ConflictGrade.Yellow)
                continue;

            var modA = mods.FirstOrDefault(m => m.PackageId.Equals(finding.ModAPackageId, StringComparison.OrdinalIgnoreCase));
            var modB = mods.FirstOrDefault(m => m.PackageId.Equals(finding.ModBPackageId, StringComparison.OrdinalIgnoreCase));

            var matchingError = FindMatchingErrorLine(finding, modA, modB, relevantErrors);
            if (matchingError != null)
            {
                var updatedEvidence = finding.Evidence
                    .Concat(new[] { $"[GABS/实机日志命中红字] {matchingError}" })
                    .ToArray();

                findings[i] = finding with
                {
                    Grade = ConflictGrade.Red,
                    Summary = $"{finding.Summary}（经 GABS 常驻实机日志确认为明确报错冲突）",
                    Evidence = updatedEvidence
                };

                escalatedPairs.Add($"{finding.ModAName} <-> {finding.ModBName} ({finding.TargetSignature})");
            }
        }

        return new GabsLiveVerificationSummary(
            Connected: connected || gameRunning,
            GameStatus: statusSummary,
            ConfigurationWarnings: configWarnings.Distinct().Take(25).ToArray(),
            RelevantLogErrors: relevantErrors.Distinct().Take(30).ToArray(),
            RelevantLogWarnings: relevantWarnings.Distinct().Take(25).ToArray(),
            EscalatedModPairs: escalatedPairs.Distinct().ToArray())
        {
            GameRunning = gameRunning
        };
    }

    private static bool CheckGameRunningStatus(out string statusSummary)
    {
        try
        {
            if (File.Exists(DefaultGabsRuntimeJsonPath))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(DefaultGabsRuntimeJsonPath));
                var root = doc.RootElement;
                var status = root.TryGetProperty("status", out var s) ? s.GetString() : "unknown";
                var pid = root.TryGetProperty("gamePid", out var p) && p.TryGetInt32(out var pidVal) ? pidVal : 0;

                if (status == "running" && pid > 0)
                {
                    try
                    {
                        using var proc = Process.GetProcessById(pid);
                        if (!proc.HasExited)
                        {
                            statusSummary = $"RimWorld 常驻运行中 (PID={pid}, GABS status=running)";
                            return true;
                        }
                    }
                    catch
                    {
                        // Process lookup failed; fallback below
                    }
                }
            }

            var procs = Process.GetProcessesByName("RimWorldWin64");
            if (procs.Length > 0)
            {
                statusSummary = $"RimWorld 常驻运行中 (PID={procs[0].Id})";
                return true;
            }
        }
        catch
        {
            // Ignore process check exceptions
        }

        statusSummary = "静态代码与离线日志核验";
        return false;
    }

    private static bool TryQueryGabsBridge(
        List<string> configWarnings,
        List<string> relevantErrors,
        List<string> relevantWarnings,
        CancellationToken cancellationToken)
    {
        try
        {
            var psi = new ProcessStartInfo(DefaultGabsExePath, "server")
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
                return false;

            try
            {
                SendJsonRpc(proc.StandardInput, new
                {
                    jsonrpc = "2.0",
                    id = 1,
                    method = "initialize",
                    @params = new
                    {
                        protocolVersion = "2024-11-05",
                        capabilities = new { },
                        clientInfo = new { name = "ModSorterAgent", version = "1.0.0" }
                    }
                });

                _ = ReadJsonRpcResponseWithTimeout(proc.StandardOutput, TimeSpan.FromSeconds(5));

                SendJsonRpc(proc.StandardInput, new
                {
                    jsonrpc = "2.0",
                    method = "notifications/initialized",
                    @params = new { }
                });

                cancellationToken.ThrowIfCancellationRequested();

                // Connect to already running game (read-only attach, never stop/kill)
                CallAllowedGabsTool(proc, 2, "games_connect", new
                {
                    gameId = "rimworld",
                    forceTakeover = true,
                    timeout = 8
                });

                var modConfigResp = CallAllowedGabsTool(proc, 3, "games_call_tool", new
                {
                    gameId = "rimworld",
                    tool = "rimworld/get_mod_configuration_status",
                    arguments = new { },
                    timeout = 8
                });

                if (!string.IsNullOrWhiteSpace(modConfigResp))
                {
                    ExtractModConfigIssues(modConfigResp, configWarnings);
                }

                var logsResp = CallAllowedGabsTool(proc, 4, "games_call_tool", new
                {
                    gameId = "rimworld",
                    tool = "rimbridge/list_logs",
                    arguments = new
                    {
                        limit = 100,
                        minimumLevel = "warning"
                    },
                    timeout = 8
                });

                if (!string.IsNullOrWhiteSpace(logsResp))
                {
                    ExtractBridgeLogs(logsResp, relevantErrors, relevantWarnings);
                }

                return true;
            }
            finally
            {
                try
                {
                    proc.StandardInput.Close();
                    if (!proc.WaitForExit(1500))
                        proc.Kill(); // Only closes the short-lived gabs.exe MCP stdio adapter, NEVER RimWorldWin64
                }
                catch
                {
                    // Ignore cleanup error on stdio adapter
                }
            }
        }
        catch
        {
            return false;
        }
    }

    private static string? CallAllowedGabsTool(Process proc, int requestId, string toolName, object arguments)
    {
        if (!AllowedGabsTools.Contains(toolName))
            throw new InvalidOperationException($"GABS safety violation: tool '{toolName}' is not in the read-only allowlist.");

        SendJsonRpc(proc.StandardInput, new
        {
            jsonrpc = "2.0",
            id = requestId,
            method = "tools/call",
            @params = new
            {
                name = toolName,
                arguments
            }
        });

        return ReadJsonRpcResponseWithTimeout(proc.StandardOutput, TimeSpan.FromSeconds(10));
    }

    private static void SendJsonRpc(StreamWriter writer, object payload)
    {
        var json = JsonSerializer.Serialize(payload);
        writer.WriteLine(json);
        writer.Flush();
    }

    private static string? ReadJsonRpcResponseWithTimeout(StreamReader reader, TimeSpan timeout)
    {
        var task = Task.Run(reader.ReadLine);
        return task.Wait(timeout) ? task.Result : null;
    }

    private static void ExtractModConfigIssues(string rawJson, List<string> configWarnings)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var text = doc.RootElement.ToString();
            if (text.Contains("orderingIssues", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("warnings", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var line in text.Split('\n'))
                {
                    var trimmed = line.Trim();
                    if (trimmed.Contains("warning", StringComparison.OrdinalIgnoreCase) ||
                        trimmed.Contains("issue", StringComparison.OrdinalIgnoreCase) ||
                        trimmed.Contains("missing", StringComparison.OrdinalIgnoreCase))
                    {
                        configWarnings.Add(trimmed);
                    }
                }
            }
        }
        catch
        {
            // Ignore malformed JSON snippet
        }
    }

    private static void ExtractBridgeLogs(string rawJson, List<string> errors, List<string> warnings)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var rawText = doc.RootElement.ToString();
            foreach (var line in rawText.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.Contains("\"level\":\"error\"", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.Contains("Exception", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.Contains("XML error", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(trimmed.Length > 240 ? trimmed[..240] : trimmed);
                }
                else if (trimmed.Contains("\"level\":\"warning\"", StringComparison.OrdinalIgnoreCase))
                {
                    warnings.Add(trimmed.Length > 240 ? trimmed[..240] : trimmed);
                }
            }
        }
        catch
        {
            // Ignore parse error
        }
    }

    private static void ScanPlayerLogForModErrors(
        IReadOnlyList<ModDescriptor> mods,
        List<string> relevantErrors,
        List<string> relevantWarnings,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(DefaultPlayerLogPath))
            return;

        try
        {
            using var fs = new FileStream(DefaultPlayerLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fs, Encoding.UTF8);

            var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var mod in mods)
            {
                if (mod.IsOfficialCoreOrDlc)
                    continue;

                keywords.Add(mod.PackageId);
                foreach (var asm in mod.AssemblyAnalyses)
                {
                    if (!string.IsNullOrWhiteSpace(asm.AssemblyName))
                        keywords.Add(asm.AssemblyName);
                }
            }

            var keywordList = keywords.Where(k => k.Length >= 4).ToArray();

            string? line;
            var inspected = 0;
            while ((line = reader.ReadLine()) != null && inspected < 60000)
            {
                if ((inspected++ & 0x3FF) == 0)
                    cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var isErrorLine = line.Contains("XML error", StringComparison.OrdinalIgnoreCase) ||
                                  line.Contains("PatchOperation", StringComparison.OrdinalIgnoreCase) ||
                                  line.Contains("HarmonyException", StringComparison.OrdinalIgnoreCase) ||
                                  line.Contains("Could not resolve cross-reference", StringComparison.OrdinalIgnoreCase) ||
                                  line.Contains("Exception in", StringComparison.OrdinalIgnoreCase) ||
                                  line.Contains("Error while", StringComparison.OrdinalIgnoreCase);

                if (!isErrorLine)
                    continue;

                if (keywordList.Any(k => line.Contains(k, StringComparison.OrdinalIgnoreCase)) ||
                    line.Contains("XML error", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("HarmonyException", StringComparison.OrdinalIgnoreCase))
                {
                    relevantErrors.Add(line.Trim());
                }
                else if (line.Contains("Warning", StringComparison.OrdinalIgnoreCase))
                {
                    relevantWarnings.Add(line.Trim());
                }
            }
        }
        catch
        {
            // Ignore log read lock issues
        }
    }

    private static string? FindMatchingErrorLine(
        PairwiseConflictFinding finding,
        ModDescriptor? modA,
        ModDescriptor? modB,
        IReadOnlyList<string> errorLines)
    {
        if (errorLines.Count == 0)
            return null;

        var tokensA = BuildModSearchTokens(modA);
        var tokensB = BuildModSearchTokens(modB);

        foreach (var err in errorLines)
        {
            var hitsTarget = !string.IsNullOrWhiteSpace(finding.TargetSignature) &&
                             err.Contains(finding.TargetSignature, StringComparison.OrdinalIgnoreCase);
            var hitsA = tokensA.Any(t => err.Contains(t, StringComparison.OrdinalIgnoreCase));
            var hitsB = tokensB.Any(t => err.Contains(t, StringComparison.OrdinalIgnoreCase));

            if ((hitsTarget && (hitsA || hitsB)) || (hitsA && hitsB))
            {
                return err;
            }
        }

        return null;
    }

    private static List<string> BuildModSearchTokens(ModDescriptor? mod)
    {
        var list = new List<string>();
        if (mod == null)
            return list;

        list.Add(mod.PackageId);
        foreach (var asm in mod.AssemblyAnalyses)
        {
            if (!string.IsNullOrWhiteSpace(asm.AssemblyName))
                list.Add(asm.AssemblyName);
        }
        return list.Where(s => s.Length >= 4).ToList();
    }
}
