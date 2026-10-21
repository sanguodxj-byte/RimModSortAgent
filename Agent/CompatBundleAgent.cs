using ModSorterAgent.Models;
using ModSorterAgent.Patching;
using ModSorterAgent.Reporting;

namespace ModSorterAgent.Agent;

public sealed record CompatBundleWork(
    IReadOnlyList<CompatPatchEntry> Entries,
    IReadOnlyList<CompatPatchSkip> Skipped,
    IReadOnlyList<AgentReport> Reports);

/// <summary>
/// 合并补丁的 Agent 编排：由内部配置的 Agent 逐项复核全部生效冲突；
/// 作者声明不兼容的冲突仅在玩家选择"两边都保留"后才复核，
/// 产出结论与补丁方案，再统一交给 <see cref="CompatPatchBuilder"/> 打成一个 mod 包。
/// 没有代码证据、Agent 判定误报或只给出顺序方案的冲突会被排除并记录原因。
/// </summary>
public sealed class CompatBundleAgent
{
    private readonly AgentInvestigator _investigator;

    public CompatBundleAgent(AgentInvestigator investigator)
    {
        _investigator = investigator;
    }

    public async Task<CompatBundleWork> ReviewAsync(
        IReadOnlyList<PlayerConflict> conflicts,
        IProgress<(int Done, int Total, string Label)>? progress = null,
        CancellationToken cancellationToken = default,
        Action<AgentReport>? onReport = null,
        IReadOnlySet<string>? approvedAuthorDeclared = null)
    {
        var skipped = new List<CompatPatchSkip>();
        var candidates = new List<PlayerConflict>();

        foreach (var conflict in conflicts)
        {
            if (conflict.Grade == ConflictGrade.Green)
                continue;

            if (CompatPatchEligibility.IsAuthorDeclared(conflict) &&
                approvedAuthorDeclared?.Contains(conflict.Key) != true)
            {
                skipped.Add(new CompatPatchSkip(
                    conflict,
                    "作者在 About.xml 中明确声明互斥，且未选择同时保留，不生成绕过补丁"));
                continue;
            }

            candidates.Add(conflict);
        }

        var entries = new List<CompatPatchEntry>();
        var reports = new List<AgentReport>();

        for (var i = 0; i < candidates.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var conflict = candidates[i];
            progress?.Report((i, candidates.Count, $"核查 {conflict.PairLabel}"));

            AgentReport report;
            try
            {
                report = await _investigator.InvestigateAsync(conflict, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                skipped.Add(new CompatPatchSkip(conflict, "Agent 核查失败：" + ex.Message));
                progress?.Report((i + 1, candidates.Count, $"失败 {conflict.PairLabel}"));
                continue;
            }

            reports.Add(report);
            onReport?.Invoke(report);

            if (CompatPatchEligibility.CanBundle(conflict, report, approvedAuthorDeclared))
                entries.Add(new CompatPatchEntry(conflict, report));
            else
                skipped.Add(new CompatPatchSkip(
                    conflict,
                    CompatPatchEligibility.ExplainNotEligible(conflict, report, approvedAuthorDeclared)));

            progress?.Report((i + 1, candidates.Count, $"完成 {conflict.PairLabel}"));
        }

        return new CompatBundleWork(entries, skipped, reports);
    }

    /// <summary>
    /// 编译合并包；Harmony 编译失败时把真实编译器错误回传给 Agent 修复并重编译。
    /// 只有最终编译成功的方案才会留在补丁包里。
    /// </summary>
    public async Task<CompatBundleBuild> BuildWithRepairAsync(
        CompatPatchBuilder builder,
        CompatBundleWork work,
        Action<string>? status = null,
        CancellationToken cancellationToken = default,
        int maxRepairRounds = 4)
    {
        var patch = await Task.Run(() => builder.Build(work.Entries, work.Skipped), cancellationToken)
            .ConfigureAwait(false);

        for (var round = 1; round <= maxRepairRounds; round++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var failures = patch.Failures
                .Where(f => !string.IsNullOrWhiteSpace(f.CompilerOutput))
                .ToList();
            if (failures.Count == 0 || !_investigator.UsesModel)
                break;

            status?.Invoke($"补丁编译失败，Agent 正在根据编译器错误修复（{round}/{maxRepairRounds}）…");
            var entries = work.Entries.ToList();
            var reports = work.Reports.ToList();
            var changed = false;

            foreach (var failure in failures)
            {
                var index = entries.FindIndex(e =>
                    e.Conflict.Key.Equals(failure.ConflictKey, StringComparison.OrdinalIgnoreCase));
                if (index < 0)
                    continue;

                var entry = entries[index];
                status?.Invoke($"修复编译错误：{entry.Conflict.PairLabel}（{round}/{maxRepairRounds}）");

                AgentReport? repaired;
                try
                {
                    repaired = await _investigator.RepairHarmonyPlanAsync(
                        entry.Conflict,
                        entry.Report,
                        failure.CompilerOutput,
                        cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    status?.Invoke($"修复未完成：{entry.Conflict.PairLabel} · {ex.Message}");
                    continue;
                }

                if (repaired == null ||
                    string.Equals(
                        repaired.PatchPlan?.HarmonyCode,
                        entry.Report.PatchPlan?.HarmonyCode,
                        StringComparison.Ordinal))
                    continue;

                entries[index] = new CompatPatchEntry(entry.Conflict, repaired);
                var reportIndex = reports.FindIndex(r =>
                    r.ConflictKey.Equals(repaired.ConflictKey, StringComparison.OrdinalIgnoreCase));
                if (reportIndex >= 0)
                    reports[reportIndex] = repaired;
                else
                    reports.Add(repaired);
                changed = true;
            }

            if (!changed)
                break;

            work = work with { Entries = entries, Reports = reports };
            patch = await Task.Run(() => builder.Build(work.Entries, work.Skipped), cancellationToken)
                .ConfigureAwait(false);
        }

        return new CompatBundleBuild(patch, work);
    }
}

public sealed record CompatBundleBuild(CompatPatchResult Patch, CompatBundleWork Work);
