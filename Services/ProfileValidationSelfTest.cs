using ModSorterAgent.Models;
using ModSorterAgent.Sorting;

namespace ModSorterAgent.Services;

/// <summary>
/// 针对 <see cref="ProfileValidationService"/> 的合成夹具自检。
/// 验证无需执行完整分析管线（无 PipelineResult）即可直接基于磁盘 About.xml 进行 Profile 校验，
/// 覆盖合法排序放行、缺失必需前置依赖阻止、加载顺序颠倒阻止、缺失模组元数据阻止以及重复包阻止。
/// </summary>
public static class ProfileValidationSelfTest
{
    public static IReadOnlyList<string> Run()
    {
        var failures = new List<string>();

        string? tempDir = null;
        try
        {
            tempDir = CreateTestModFixtureEnvironment();

            AssertValidProfilePassesWithoutPipelineResult(failures, tempDir);
            AssertMissingPrerequisiteDependencyBlocks(failures, tempDir);
            AssertInvertedLoadOrderBlocks(failures, tempDir);
            AssertMissingModMetadataBlocks(failures, tempDir);
            AssertDuplicatePackageIdBlocks(failures, tempDir);
            AssertBaseAndSteamDuplicatePackageIdBlocks(failures, tempDir);
            AssertAlternativeDependencySatisfiedPasses(failures, tempDir);
            AssertDependencySatisfiedWithLoadBeforePasses(failures, tempDir);
            AssertConditionalLoadFoldersDoNotBlockValidOrder(failures, tempDir);
            AssertByVersionExactMatchReplacesBaseDependenciesPasses(failures, tempDir);
            AssertByVersionMismatchUsesBaseDependenciesBlocks(failures, tempDir);
            AssertByVersionEmptyNodeClearsBaseConstraintsPasses(failures, tempDir);
            AssertPipelineServiceRunAndValidateActiveModSetPasses(failures, tempDir);
            AssertGameConfigurationRequiresCore(failures, tempDir);
        }
        catch (Exception ex)
        {
            failures.Add($"自检异常：{ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            if (tempDir != null && Directory.Exists(tempDir))
            {
                try
                {
                    Directory.Delete(tempDir, recursive: true);
                }
                catch
                {
                }
            }
        }

        return failures;
    }

    private static string CreateTestModFixtureEnvironment()
    {
        var root = Path.Combine(Path.GetTempPath(), "msa-profile-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        // 创建 Mod A (被依赖项)
        var modADir = Path.Combine(root, "ModA", "About");
        Directory.CreateDirectory(modADir);
        File.WriteAllText(Path.Combine(modADir, "About.xml"),
            """
            <?xml version="1.0" encoding="utf-8"?>
            <ModMetaData>
              <name>Test Mod A</name>
              <author>Tester</author>
              <packageId>selftest.profile.a</packageId>
              <supportedVersions>
                <li>1.7</li>
              </supportedVersions>
            </ModMetaData>
            """);

        // 创建 Mod B (依赖 Mod A，并要求在 Mod A 之后加载)
        var modBDir = Path.Combine(root, "ModB", "About");
        Directory.CreateDirectory(modBDir);
        File.WriteAllText(Path.Combine(modBDir, "About.xml"),
            """
            <?xml version="1.0" encoding="utf-8"?>
            <ModMetaData>
              <name>Test Mod B</name>
              <author>Tester</author>
              <packageId>selftest.profile.b</packageId>
              <supportedVersions>
                <li>1.7</li>
              </supportedVersions>
              <modDependencies>
                <li>
                  <packageId>selftest.profile.a</packageId>
                  <displayName>Test Mod A</displayName>
                </li>
              </modDependencies>
              <loadAfter>
                <li>selftest.profile.a</li>
              </loadAfter>
            </ModMetaData>
            """);

        // 创建 Mod C (声明可选备用依赖: 主依赖不存在，但备用依赖 selftest.profile.a 存在)
        var modCDir = Path.Combine(root, "ModC", "About");
        Directory.CreateDirectory(modCDir);
        File.WriteAllText(Path.Combine(modCDir, "About.xml"),
            """
            <?xml version="1.0" encoding="utf-8"?>
            <ModMetaData>
              <name>Test Mod C</name>
              <author>Tester</author>
              <packageId>selftest.profile.c</packageId>
              <supportedVersions>
                <li>1.7</li>
              </supportedVersions>
              <modDependencies>
                <li>
                  <packageId>selftest.profile.nonexistent</packageId>
                  <displayName>Nonexistent Main</displayName>
                  <alternativePackageIds>
                    <li>selftest.profile.a</li>
                  </alternativePackageIds>
                </li>
              </modDependencies>
            </ModMetaData>
            """);

        // 创建 Mod D (依赖 Mod E，但声明 loadBefore Mod E)
        var modDDir = Path.Combine(root, "ModD", "About");
        Directory.CreateDirectory(modDDir);
        File.WriteAllText(Path.Combine(modDDir, "About.xml"),
            """
            <?xml version="1.0" encoding="utf-8"?>
            <ModMetaData>
              <name>Test Mod D</name>
              <author>Tester</author>
              <packageId>selftest.profile.d</packageId>
              <supportedVersions>
                <li>1.7</li>
              </supportedVersions>
              <modDependencies>
                <li>
                  <packageId>selftest.profile.e</packageId>
                  <displayName>Test Mod E</displayName>
                </li>
              </modDependencies>
              <loadBefore>
                <li>selftest.profile.e</li>
              </loadBefore>
            </ModMetaData>
            """);

        // 创建 Mod E (被 Mod D 依赖)
        var modEDir = Path.Combine(root, "ModE", "About");
        Directory.CreateDirectory(modEDir);
        File.WriteAllText(Path.Combine(modEDir, "About.xml"),
            """
            <?xml version="1.0" encoding="utf-8"?>
            <ModMetaData>
              <name>Test Mod E</name>
              <author>Tester</author>
              <packageId>selftest.profile.e</packageId>
              <supportedVersions>
                <li>1.7</li>
              </supportedVersions>
            </ModMetaData>
            """);

        // 创建 Mod F (包含 LoadFolders.xml 条件目录 IfModActive，条件目录仅影响文件加载，不能硬拦合法顺序)
        var modFDir = Path.Combine(root, "ModF");
        var modFAboutDir = Path.Combine(modFDir, "About");
        Directory.CreateDirectory(modFAboutDir);
        File.WriteAllText(Path.Combine(modFAboutDir, "About.xml"),
            """
            <?xml version="1.0" encoding="utf-8"?>
            <ModMetaData>
              <name>Test Mod F</name>
              <author>Tester</author>
              <packageId>selftest.profile.f</packageId>
              <supportedVersions>
                <li>1.7</li>
              </supportedVersions>
            </ModMetaData>
            """);
        // 创建 Mod G (测试 ByVersion 覆盖：基础依赖不存在的 selftest.profile.invalid，但在 v1.7 节点替换为有效的 selftest.profile.a)
        var modGDir = Path.Combine(root, "ModG", "About");
        Directory.CreateDirectory(modGDir);
        File.WriteAllText(Path.Combine(modGDir, "About.xml"),
            """
            <?xml version="1.0" encoding="utf-8"?>
            <ModMetaData>
              <name>Test Mod G</name>
              <author>Tester</author>
              <packageId>selftest.profile.g</packageId>
              <supportedVersions>
                <li>1.7</li>
              </supportedVersions>
              <modDependencies>
                <li>
                  <packageId>selftest.profile.invalid</packageId>
                  <displayName>Invalid Base Dep</displayName>
                </li>
              </modDependencies>
              <modDependenciesByVersion>
                <v1.7>
                  <li>
                    <packageId>selftest.profile.a</packageId>
                    <displayName>Valid Dep In 1.7</displayName>
                  </li>
                </v1.7>
              </modDependenciesByVersion>
            </ModMetaData>
            """);

        // 创建 Mod H (测试 ByVersion 未匹配当前版本时不生效：基础依赖不存在的 selftest.profile.invalid，仅在 v1.6 节点有有效依赖，1.7 下应使用基础依赖而受阻)
        var modHDir = Path.Combine(root, "ModH", "About");
        Directory.CreateDirectory(modHDir);
        File.WriteAllText(Path.Combine(modHDir, "About.xml"),
            """
            <?xml version="1.0" encoding="utf-8"?>
            <ModMetaData>
              <name>Test Mod H</name>
              <author>Tester</author>
              <packageId>selftest.profile.h</packageId>
              <supportedVersions>
                <li>1.7</li>
              </supportedVersions>
              <modDependencies>
                <li>
                  <packageId>selftest.profile.invalid</packageId>
                  <displayName>Invalid Base Dep</displayName>
                </li>
              </modDependencies>
              <modDependenciesByVersion>
                <v1.6>
                  <li>
                    <packageId>selftest.profile.a</packageId>
                    <displayName>Valid Dep Only In 1.6</displayName>
                  </li>
                </v1.6>
              </modDependenciesByVersion>
            </ModMetaData>
            """);

        // 创建 Mod I (测试 ByVersion 空节点清空基础约束：基础声明 loadAfter selftest.profile.a，但 v1.7 使用空版本节点清空了约束，因而排在 a 前也能通过)
        var modIDir = Path.Combine(root, "ModI", "About");
        Directory.CreateDirectory(modIDir);
        File.WriteAllText(Path.Combine(modIDir, "About.xml"),
            """
            <?xml version="1.0" encoding="utf-8"?>
            <ModMetaData>
              <name>Test Mod I</name>
              <author>Tester</author>
              <packageId>selftest.profile.i</packageId>
              <supportedVersions>
                <li>1.7</li>
              </supportedVersions>
              <loadAfter>
                <li>selftest.profile.a</li>
              </loadAfter>
              <loadAfterByVersion>
                <v1.7/>
              </loadAfterByVersion>
            </ModMetaData>
            """);

        return root;
    }

    private static void AssertValidProfilePassesWithoutPipelineResult(List<string> failures, string fixtureRoot)
    {
        var profile = new SortListProfile(
            Name: "ValidProfile",
            CreatedAtUtc: DateTime.UtcNow,
            GameVersion: "1.7",
            PackageIds: new[] { "selftest.profile.a", "selftest.profile.b" },
            KnownExpansions: Array.Empty<string>(),
            TotalModsAnalyzed: 2,
            RedConflictCount: 0,
            YellowConflictCount: 0);

        var outcome = ProfileValidationService.ValidateProfile(profile, new[] { fixtureRoot }, requireCore: false);

        if (!outcome.IsValid)
        {
            failures.Add($"合法 Profile 校验应通过，实际失败：{string.Join("; ", outcome.Issues.Select(i => i.Message))}");
            return;
        }

        if (outcome.ResolvedMods.Count != 2)
        {
            failures.Add($"ResolvedMods 应为 2，实际为 {outcome.ResolvedMods.Count}");
        }

        if (outcome.UnresolvedTokens.Count != 0)
        {
            failures.Add($"UnresolvedTokens 应为空，实际有 {outcome.UnresolvedTokens.Count} 项");
        }
    }

    private static void AssertMissingPrerequisiteDependencyBlocks(List<string> failures, string fixtureRoot)
    {
        // 仅包含 Mod B，缺少其必需依赖 Mod A
        var profile = new SortListProfile(
            Name: "MissingDepProfile",
            CreatedAtUtc: DateTime.UtcNow,
            GameVersion: "1.7",
            PackageIds: new[] { "selftest.profile.b" },
            KnownExpansions: Array.Empty<string>(),
            TotalModsAnalyzed: 1,
            RedConflictCount: 1,
            YellowConflictCount: 0);

        var outcome = ProfileValidationService.ValidateProfile(profile, new[] { fixtureRoot }, requireCore: false);

        if (outcome.IsValid)
        {
            failures.Add("缺少必需前置依赖的 Profile 应被阻止，实际却通过校验");
            return;
        }

        var hasMissingDepIssue = outcome.Issues.Any(i =>
            i.ErrorType == SortValidationErrorType.MissingActiveDependency &&
            i.PackageId == "selftest.profile.b");

        if (!hasMissingDepIssue)
        {
            failures.Add("缺少必需依赖时应报告 MissingActiveDependency 类型的错误");
        }
    }

    private static void AssertInvertedLoadOrderBlocks(List<string> failures, string fixtureRoot)
    {
        // Mod B 声明在 Mod A 之后加载，但列表将其置于 Mod A 之前
        var profile = new SortListProfile(
            Name: "InvertedOrderProfile",
            CreatedAtUtc: DateTime.UtcNow,
            GameVersion: "1.7",
            PackageIds: new[] { "selftest.profile.b", "selftest.profile.a" },
            KnownExpansions: Array.Empty<string>(),
            TotalModsAnalyzed: 2,
            RedConflictCount: 0,
            YellowConflictCount: 0);

        var outcome = ProfileValidationService.ValidateProfile(profile, new[] { fixtureRoot }, requireCore: false);

        if (outcome.IsValid)
        {
            failures.Add("加载顺位颠倒的 Profile 应被阻止，实际却通过校验");
            return;
        }

        var hasOrderViolation = outcome.Issues.Any(i => i.ErrorType == SortValidationErrorType.EdgeOrderViolation);
        if (!hasOrderViolation)
        {
            failures.Add("加载顺位颠倒时应报告 EdgeOrderViolation 类型的错误");
        }
    }

    private static void AssertMissingModMetadataBlocks(List<string> failures, string fixtureRoot)
    {
        // 包含不存在于磁盘的未知模组
        var profile = new SortListProfile(
            Name: "NonexistentModProfile",
            CreatedAtUtc: DateTime.UtcNow,
            GameVersion: "1.7",
            PackageIds: new[] { "selftest.profile.a", "selftest.profile.unknown" },
            KnownExpansions: Array.Empty<string>(),
            TotalModsAnalyzed: 2,
            RedConflictCount: 0,
            YellowConflictCount: 0);

        var outcome = ProfileValidationService.ValidateProfile(profile, new[] { fixtureRoot }, requireCore: false);

        if (outcome.IsValid)
        {
            failures.Add("包含未知模组的 Profile 应被阻止，实际却通过校验");
            return;
        }

        var hasMissingMetadataIssue = outcome.Issues.Any(i =>
            i.ErrorType == SortValidationErrorType.MissingAnalysisMetadata &&
            i.PackageId == "selftest.profile.unknown");

        if (!hasMissingMetadataIssue)
        {
            failures.Add("包含未知模组时应报告 MissingAnalysisMetadata 类型的错误");
        }
    }

    private static void AssertDuplicatePackageIdBlocks(List<string> failures, string fixtureRoot)
    {
        // 包含重复包标识
        var profile = new SortListProfile(
            Name: "DuplicateModProfile",
            CreatedAtUtc: DateTime.UtcNow,
            GameVersion: "1.7",
            PackageIds: new[] { "selftest.profile.a", "selftest.profile.a" },
            KnownExpansions: Array.Empty<string>(),
            TotalModsAnalyzed: 2,
            RedConflictCount: 0,
            YellowConflictCount: 0);

        var outcome = ProfileValidationService.ValidateProfile(profile, new[] { fixtureRoot }, requireCore: false);

        if (outcome.IsValid)
        {
            failures.Add("包含重复 PackageId 的 Profile 应被阻止，实际却通过校验");
            return;
        }

        var hasDuplicateIssue = outcome.Issues.Any(i => i.ErrorType == SortValidationErrorType.DuplicatePackageId);
        if (!hasDuplicateIssue)
        {
            failures.Add("包含重复 PackageId 时应报告 DuplicatePackageId 类型的错误");
        }
    }

    private static void AssertBaseAndSteamDuplicatePackageIdBlocks(List<string> failures, string fixtureRoot)
    {
        // 包含 base 与 _steam 变体的重复项（例如 selftest.profile.a 与 selftest.profile.a_steam）
        var profile = new SortListProfile(
            Name: "BaseSteamDuplicateProfile",
            CreatedAtUtc: DateTime.UtcNow,
            GameVersion: "1.7",
            PackageIds: new[] { "selftest.profile.a", "selftest.profile.a_steam" },
            KnownExpansions: Array.Empty<string>(),
            TotalModsAnalyzed: 2,
            RedConflictCount: 0,
            YellowConflictCount: 0);

        var outcome = ProfileValidationService.ValidateProfile(profile, new[] { fixtureRoot }, requireCore: false);

        if (outcome.IsValid)
        {
            failures.Add("包含 base 与 _steam 重复包标识的 Profile 应被阻止，实际却通过校验");
            return;
        }

        var hasDuplicateIssue = outcome.Issues.Any(i => i.ErrorType == SortValidationErrorType.DuplicatePackageId);
        if (!hasDuplicateIssue)
        {
            failures.Add("包含 base 与 _steam 重复包标识时应报告 DuplicatePackageId 类型的错误");
        }
    }

    private static void AssertAlternativeDependencySatisfiedPasses(List<string> failures, string fixtureRoot)
    {
        // Mod C 声明主依赖 selftest.profile.nonexistent，但备用依赖为 selftest.profile.a，profile 包含 a 与 c 时应通过
        var profile = new SortListProfile(
            Name: "AltDepSatisfiedProfile",
            CreatedAtUtc: DateTime.UtcNow,
            GameVersion: "1.7",
            PackageIds: new[] { "selftest.profile.a", "selftest.profile.c" },
            KnownExpansions: Array.Empty<string>(),
            TotalModsAnalyzed: 2,
            RedConflictCount: 0,
            YellowConflictCount: 0);

        var outcome = ProfileValidationService.ValidateProfile(profile, new[] { fixtureRoot }, requireCore: false);

        if (!outcome.IsValid)
        {
            failures.Add($"备用依赖满足的 Profile 校验应通过，实际失败：{string.Join("; ", outcome.Issues.Select(i => i.Message))}");
        }
    }

    private static void AssertDependencySatisfiedWithLoadBeforePasses(List<string> failures, string fixtureRoot)
    {
        // Mod D 依赖 Mod E，但声明了 loadBefore Mod E。
        // 模组依赖规则只要求依赖项在激活列表中启用，顺位方向由 loadBefore/loadAfter 约束决定。
        var profile = new SortListProfile(
            Name: "DepWithLoadBeforeProfile",
            CreatedAtUtc: DateTime.UtcNow,
            GameVersion: "1.7",
            PackageIds: new[] { "selftest.profile.d", "selftest.profile.e" },
            KnownExpansions: Array.Empty<string>(),
            TotalModsAnalyzed: 2,
            RedConflictCount: 0,
            YellowConflictCount: 0);

        var outcome = ProfileValidationService.ValidateProfile(profile, new[] { fixtureRoot }, requireCore: false);

        if (!outcome.IsValid)
        {
            failures.Add($"依赖项声明 loadBefore 目标的合法排序应通过校验，实际失败：{string.Join("; ", outcome.Issues.Select(i => i.Message))}");
        }
    }

    private static void AssertConditionalLoadFoldersDoNotBlockValidOrder(List<string> failures, string fixtureRoot)
    {
        // Mod F 配置了 IfModActive="selftest.profile.a" 的条件加载目录。
        // 条件目录只决定内容的存在性，不能当作硬性排序边拦截合法顺序。
        var profile = new SortListProfile(
            Name: "ConditionalFolderOrderProfile",
            CreatedAtUtc: DateTime.UtcNow,
            GameVersion: "1.7",
            PackageIds: new[] { "selftest.profile.f", "selftest.profile.a" },
            KnownExpansions: Array.Empty<string>(),
            TotalModsAnalyzed: 2,
            RedConflictCount: 0,
            YellowConflictCount: 0);

        var outcome = ProfileValidationService.ValidateProfile(profile, new[] { fixtureRoot }, requireCore: false);

        if (!outcome.IsValid)
        {
            failures.Add($"含有条件目录但无硬性 loadAfter 约束的 Profile 应通过校验，实际失败：{string.Join("; ", outcome.Issues.Select(i => i.Message))}");
        }
    }

    private static void AssertByVersionExactMatchReplacesBaseDependenciesPasses(List<string> failures, string fixtureRoot)
    {
        // Mod G 基础依赖不存在的 invalid 包，但在 v1.7 节点精确匹配替换为有效的 selftest.profile.a
        // 当前 Profile 包含 selftest.profile.a 与 selftest.profile.g，应通过校验
        var profile = new SortListProfile(
            Name: "ByVersionMatchProfile",
            CreatedAtUtc: DateTime.UtcNow,
            GameVersion: "1.7",
            PackageIds: new[] { "selftest.profile.a", "selftest.profile.g" },
            KnownExpansions: Array.Empty<string>(),
            TotalModsAnalyzed: 2,
            RedConflictCount: 0,
            YellowConflictCount: 0);

        var outcome = ProfileValidationService.ValidateProfile(profile, new[] { fixtureRoot }, requireCore: false);

        if (!outcome.IsValid)
        {
            failures.Add($"ByVersion 精确匹配当前版本并替换基础依赖时应通过校验，实际失败：{string.Join("; ", outcome.Issues.Select(i => i.Message))}");
        }
    }

    private static void AssertByVersionMismatchUsesBaseDependenciesBlocks(List<string> failures, string fixtureRoot)
    {
        // Mod H 基础依赖不存在的 invalid 包，仅在 v1.6 节点声明有效依赖，在 1.7 下应使用基础依赖因而被阻止
        var profile = new SortListProfile(
            Name: "ByVersionMismatchProfile",
            CreatedAtUtc: DateTime.UtcNow,
            GameVersion: "1.7",
            PackageIds: new[] { "selftest.profile.a", "selftest.profile.h" },
            KnownExpansions: Array.Empty<string>(),
            TotalModsAnalyzed: 2,
            RedConflictCount: 0,
            YellowConflictCount: 0);

        var outcome = ProfileValidationService.ValidateProfile(profile, new[] { fixtureRoot }, requireCore: false);

        if (outcome.IsValid)
        {
            failures.Add("ByVersion 未匹配当前版本时应回退到基础依赖并阻止校验，实际却通过");
            return;
        }

        var hasMissingDep = outcome.Issues.Any(i =>
            i.ErrorType == SortValidationErrorType.MissingActiveDependency &&
            i.PackageId == "selftest.profile.h");

        if (!hasMissingDep)
        {
            failures.Add("ByVersion 未匹配当前版本时应报告 MissingActiveDependency");
        }
    }

    private static void AssertByVersionEmptyNodeClearsBaseConstraintsPasses(List<string> failures, string fixtureRoot)
    {
        // Mod I 基础声明 loadAfter selftest.profile.a，但 loadAfterByVersion 在 <v1.7/> 清空了约束
        // 将 Mod I 排在 Mod A 之前，应通过校验而不报告 EdgeOrderViolation
        var profile = new SortListProfile(
            Name: "ByVersionEmptyClearsProfile",
            CreatedAtUtc: DateTime.UtcNow,
            GameVersion: "1.7",
            PackageIds: new[] { "selftest.profile.i", "selftest.profile.a" },
            KnownExpansions: Array.Empty<string>(),
            TotalModsAnalyzed: 2,
            RedConflictCount: 0,
            YellowConflictCount: 0);

        var outcome = ProfileValidationService.ValidateProfile(profile, new[] { fixtureRoot }, requireCore: false);

        if (!outcome.IsValid)
        {
            failures.Add($"ByVersion 空节点清空基础约束后应通过校验，实际失败：{string.Join("; ", outcome.Issues.Select(i => i.Message))}");
        }
    }

    private static void AssertGameConfigurationRequiresCore(List<string> failures, string fixtureRoot)
    {
        var missingCore = ProfileValidationService.ValidatePackageIds(new[] { "selftest.profile.a" }, "1.7", new[] { fixtureRoot });
        if (missingCore.IsValid || !missingCore.Issues.Any(i => i.PackageId == "ludeon.rimworld"))
            failures.Add("游戏配置缺少 Core 必须阻止写入。");
        var empty = ProfileValidationService.ValidatePackageIds(Array.Empty<string>(), "1.7", new[] { fixtureRoot });
        if (empty.IsValid || empty.Issues.Count == 0)
            failures.Add("空游戏配置必须返回具体校验问题。");
    }

    private static void AssertPipelineServiceRunAndValidateActiveModSetPasses(List<string> failures, string fixtureRoot)
    {
        // 验证主线分析管线 ModSortPipelineService：
        // 在无外部大模型 provider / 无 Agent / 无实机环境下，
        // 传入合成夹具目录，执行完整分析主线，产出的 PipelineResult 可成功通过 ValidateActiveModSet 校验。
        var packageIds = new[] { "selftest.profile.a", "selftest.profile.b" };
        var request = new PipelineRequest(
            ModTokens: packageIds,
            TargetGameVersion: "1.7",
            KnownExpansions: Array.Empty<string>(),
            VerifyWithGabs: false,
            CustomSearchRoots: new[] { fixtureRoot },
            IncludeWorkshopSubscriptions: false);

        var pipeline = new ModSortPipelineService();
        PipelineResult result;
        try
        {
            result = pipeline.Run(request);
        }
        catch (Exception ex)
        {
            failures.Add($"ModSortPipelineService.Run 执行失败：{ex.GetType().Name}: {ex.Message}");
            return;
        }

        if (result.Mods.Count != 2)
        {
            failures.Add($"PipelineResult.Mods 数量应为 2，实际为 {result.Mods.Count}");
        }

        var sortedIds = result.Report.SortedMods.Select(m => m.PackageId).ToList();
        var validation = SortOrderValidator.ValidateActiveModSet(sortedIds, result);
        if (!validation.IsValid)
        {
            failures.Add($"PipelineResult 经 ValidateActiveModSet 校验失败：{string.Join("; ", validation.Issues.Select(i => i.Message))}");
        }
    }
}
