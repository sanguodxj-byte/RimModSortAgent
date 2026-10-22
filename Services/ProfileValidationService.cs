using ModSorterAgent.Discovery;
using ModSorterAgent.Models;
using ModSorterAgent.Sorting;

namespace ModSorterAgent.Services;

/// <summary>
/// 针对排序快照（SortListProfile）或包标识集合的本地轻量校验服务。
/// 仅依赖磁盘上的 About.xml 与 LoadFolders.xml 进行元数据发现，
/// 校验模组存在性、重复项、必需依赖完整性及加载顺位约束，无需完整分析管线。
/// </summary>
public static class ProfileValidationService
{
    public sealed record ValidationOutcome(
        SortValidationResult Result,
        IReadOnlyList<ModDescriptor> ResolvedMods,
        IReadOnlyList<string> UnresolvedTokens)
    {
        public bool IsValid => Result.IsValid;
        public IReadOnlyList<SortValidationIssue> Issues => Result.Issues;
    }

    public static ValidationOutcome ValidateProfile(
        SortListProfile profile,
        IEnumerable<string>? customSearchRoots = null,
        bool requireCore = true)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return ValidatePackageIds(
            profile.PackageIds,
            string.IsNullOrWhiteSpace(profile.GameVersion) ? "1.7" : profile.GameVersion,
            customSearchRoots,
            requireCore);
    }

    public static ValidationOutcome ValidatePackageIds(
        IReadOnlyList<string> packageIds,
        string gameVersion = "1.7",
        IEnumerable<string>? customSearchRoots = null,
        bool requireCore = true)
    {
        var resolver = new ModListResolver(customSearchRoots);
        return ValidatePackageIds(packageIds, gameVersion, resolver, requireCore);
    }

    public static ValidationOutcome ValidatePackageIds(
        IReadOnlyList<string> packageIds,
        string gameVersion,
        ModListResolver resolver,
        bool requireCore = true)
    {
        ArgumentNullException.ThrowIfNull(packageIds);
        ArgumentNullException.ThrowIfNull(resolver);

        var effectiveVersion = string.IsNullOrWhiteSpace(gameVersion) ? "1.7" : gameVersion;
        var (resolvedMods, unresolvedTokens) = resolver.ResolveModList(packageIds, effectiveVersion);

        if (resolvedMods.Count == 0)
        {
            var issues = unresolvedTokens
                .Select(token => new SortValidationIssue(
                    SortValidationErrorType.MissingAnalysisMetadata,
                    $"模组 '{token}' 缺失分析元数据（未在已知模组分析集合中找到），无法校验前置依赖与排序约束。",
                    PackageId: ModListResolver.NormalizePackageId(token)))
                .ToList();

            if (issues.Count == 0)
            {
                issues.Add(new SortValidationIssue(
                    SortValidationErrorType.MissingAnalysisMetadata,
                    "未能解析到任何模组元数据，无法校验排序列表。"));
            }

            return new ValidationOutcome(
                SortValidationResult.Failure(issues),
                resolvedMods,
                unresolvedTokens);
        }

        var validation = SortOrderValidator.ValidateActiveModSet(packageIds, resolvedMods);
        if (requireCore && !packageIds.Any(id => ModListResolver.StripSteamSuffix(id).Equals("ludeon.rimworld", StringComparison.OrdinalIgnoreCase)))
        {
            validation = SortValidationResult.Failure(validation.Issues.Append(new SortValidationIssue(
                SortValidationErrorType.MissingActiveDependency,
                "最终游戏配置缺少 Core (ludeon.rimworld)。",
                PackageId: "ludeon.rimworld")).ToArray());
        }

        if (unresolvedTokens.Count > 0)
        {
            var existingMissingNormIds = new HashSet<string>(
                validation.Issues
                    .Where(i => i.ErrorType == SortValidationErrorType.MissingAnalysisMetadata && !string.IsNullOrWhiteSpace(i.PackageId))
                    .Select(i => ModListResolver.NormalizePackageId(i.PackageId!)),
                StringComparer.OrdinalIgnoreCase);

            var extraIssues = new List<SortValidationIssue>();
            foreach (var token in unresolvedTokens)
            {
                var norm = ModListResolver.NormalizePackageId(token);
                if (!existingMissingNormIds.Contains(norm))
                {
                    extraIssues.Add(new SortValidationIssue(
                        SortValidationErrorType.MissingAnalysisMetadata,
                        $"模组 '{token}' 缺失分析元数据（未在已知模组分析集合中找到），无法校验前置依赖与排序约束。",
                        PackageId: norm));
                }
            }

            if (extraIssues.Count > 0)
            {
                var allIssues = validation.Issues.Concat(extraIssues).ToList();
                validation = SortValidationResult.Failure(allIssues);
            }
        }

        return new ValidationOutcome(
            validation,
            resolvedMods,
            unresolvedTokens);
    }
}
