using ModSorterAgent.Reporting;
using ModSorterAgent.Verification;

namespace ModSorterAgent.Analysis;

/// <summary>
/// 实机工具链的自检：桥接前置诊断、桥接模组的临时启用与还原、
/// 以及对玩家资产的保护边界。不依赖游戏在跑。
/// </summary>
public static class LiveVerificationSelfTest
{
    public static IReadOnlyList<string> Run()
    {
        var failures = new List<string>();

        Check(failures, "桥接前置诊断", PrerequisiteReportsRealState);
        Check(failures, "临时启用可还原", ActivatorRestoresConfig);
        Check(failures, "崩溃残留可复原", StaleStateIsRestored);
        Check(failures, "资产保护边界", PolicyBlocksDestructiveWrites);
        Check(failures, "配置安全写入-原子替换与回读校验", ConfigSafeWriteSucceedsAndVerifies);
        Check(failures, "配置安全写入-无旧文件创建并校验", ConfigSafeWriteCreatesWhenNotExisted);
        Check(failures, "配置安全写入-临时文件校验失败保原件", ConfigPreVerificationFailurePreservesOriginal);
        Check(failures, "配置安全写入-目标文件校验失败自动回滚", ConfigPostVerificationFailureRollsBack);
        Check(failures, "配置安全写入-回滚失败保留备份且异常带路径", ConfigRollbackFailurePreservesBackupAndReportsPath);
        Check(failures, "配置安全写入-目标创建竞争失败不删除他人创建的目标", ConfigTargetCreationRaceFailurePreservesOtherTarget);
        Check(failures, "配置安全写入-非法重复与空模组写入前失败且保原件", ConfigIllegalIdsFailBeforeWriteAndPreservesOriginal);
        Check(failures, "配置安全写入-TryReadActiveMods缺失activeMods或空li不通过", ConfigTryReadActiveModsMalformedXmlReturnsFalse);

        return failures;
    }

    private static void Check(List<string> failures, string name, Func<string?> probe)
    {
        var error = probe();
        if (error != null)
            failures.Add($"{name}：{error}");
    }

    /// <summary>
    /// 桥接模组定位必须与磁盘实况一致：存在就返回目录，不存在就返回 null。
    /// 不做任何版本号判断——版本号不是加载依据。
    /// </summary>
    private static string? PrerequisiteReportsRealState()
    {
        var dir = BridgeModLocator.FindBridgeModDirectory();

        if (dir != null && !Directory.Exists(dir))
            return $"返回的桥接目录不存在：{dir}";

        return null;
    }

    /// <summary>
    /// 临时启用必须能精确还原：启用后含桥接包名，释放后与原始字节完全一致。
    /// </summary>
    private static string? ActivatorRestoresConfig()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ModSorterAgent_Activator_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);

        try
        {
            var configPath = Path.Combine(tempDir, "ModsConfig.xml");
            const string original = """
            <?xml version="1.0" encoding="utf-8"?>
            <ModsConfigData>
              <version>1.7.4982 rev1280</version>
              <activeMods>
                <li>brrainz.harmony</li>
                <li>ludeon.rimworld</li>
                <li>some.other.mod</li>
              </activeMods>
            </ModsConfigData>
            """;
            File.WriteAllText(configPath, original);
            var before = File.ReadAllBytes(configPath);

            using (var activator = BridgeModActivator.Activate(configPath))
            {
                if (activator.WasAlreadyActive)
                    return "原始配置不含桥接模组，却被判定为已启用";

                var activated = File.ReadAllText(configPath);
                if (!activated.Contains(BridgeModLocator.BridgePackageId, StringComparison.Ordinal))
                    return "启用后配置里没有桥接包名";

                var bridgeIndex = activated.IndexOf(BridgeModLocator.BridgePackageId, StringComparison.Ordinal);
                var coreIndex = activated.IndexOf("ludeon.rimworld", StringComparison.Ordinal);
                if (bridgeIndex < coreIndex)
                    return "桥接模组位置过于靠前，排在官方核心之前";
            }

            var after = File.ReadAllBytes(configPath);
            if (!before.SequenceEqual(after))
                return "释放后 ModsConfig.xml 未还原到原始字节";

            return null;
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* 清理失败不影响结论 */ }
        }
    }

    /// <summary>
    /// 上次操作崩溃时备份会留在磁盘上。下次启动必须据此还原，
    /// 否则桥接模组会永久留在玩家的加载列表里。
    /// </summary>
    private static string? StaleStateIsRestored()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ModSorterAgent_Stale_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);

        try
        {
            var configPath = Path.Combine(tempDir, "ModsConfig.xml");
            const string original = """
            <?xml version="1.0" encoding="utf-8"?>
            <ModsConfigData>
              <activeMods>
                <li>brrainz.harmony</li>
                <li>some.other.mod</li>
              </activeMods>
            </ModsConfigData>
            """;
            File.WriteAllText(configPath, original);
            var originalBytes = File.ReadAllBytes(configPath);

            // 模拟「启用后进程被强杀」：配置里留着桥接，备份也在盘上
            var crashed = original.Replace(
                "<li>some.other.mod</li>",
                "<li>some.other.mod</li>\n    <li>" + BridgeModLocator.BridgePackageId + "</li>");
            File.WriteAllText(configPath, crashed);
            File.WriteAllBytes(configPath + ".modsorteragent-livetest", originalBytes);

            BridgeModActivator.RestoreStaleState(configPath);

            var restored = File.ReadAllText(configPath);
            if (restored.Contains(BridgeModLocator.BridgePackageId, StringComparison.Ordinal))
                return "崩溃残留没有被还原，桥接模组仍在加载列表";
            if (File.Exists(configPath + ".modsorteragent-livetest"))
                return "还原后备份文件未清理";

            return null;
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* 清理失败不影响结论 */ }
        }
    }

    /// <summary>
    /// Agent 可以自由操作游戏，但覆盖存档、删除模组这类不可逆动作必须永远拦住。
    /// </summary>
    private static string? PolicyBlocksDestructiveWrites()
    {
        foreach (var allowed in new[]
                 {
                     "rimbridge/list_logs",
                     "rimworld/get_mod_configuration_status",
                     "rimworld/start_debug_game_ready",
                     "rimworld/step_game_ticks",
                     "games_stop"
                 })
        {
            if (!LiveToolPolicy.IsAllowed(allowed))
                return $"{allowed} 应被允许";
        }

        foreach (var blocked in new[]
                 {
                     "rimworld/save_game",
                     "rimworld/delete_save",
                     "rimworld/overwrite_save",
                     "rimworld/delete_mod",
                     "rimworld/uninstall_mod"
                 })
        {
            if (LiveToolPolicy.IsAllowed(blocked))
                return $"{blocked} 会破坏玩家资产，必须拦住";
        }

        return null;
    }

    /// <summary>正常写入流程：备份生成、目标文件内容与顺序严格匹配、无临时文件残留。</summary>
    private static string? ConfigSafeWriteSucceedsAndVerifies()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ModSorterAgent_SafeWrite_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);

        try
        {
            var configPath = Path.Combine(tempDir, "ModsConfig.xml");
            const string original = """
            <?xml version="1.0" encoding="utf-8"?>
            <ModsConfigData>
              <version>1.7.4982 rev1280</version>
              <activeMods>
                <li>brrainz.harmony</li>
                <li>ludeon.rimworld</li>
                <li>mod.alpha</li>
              </activeMods>
            </ModsConfigData>
            """;
            File.WriteAllText(configPath, original);

            var expected = new[] { "brrainz.harmony", "mod.beta", "ludeon.rimworld" };
            var backup = ModSortReportBuilder.ApplyPackageIdsWithBackup(
                configPath,
                "1.7.4982 rev1280",
                expected,
                Array.Empty<string>());

            if (backup == null || !File.Exists(backup))
                return "写入前应生成备份文件，但未生成";

            if (!ModSortReportBuilder.TryReadActiveMods(configPath, out _, out var active))
                return "写入后无法回读 ModsConfig.xml";

            if (!active.SequenceEqual(expected, StringComparer.Ordinal))
                return $"写入后回读 activeMods 不一致：实际 [{string.Join(", ", active)}]";

            var summaries = new[]
            {
                new ModSorterAgent.Models.ModGradeSummary(
                    "brrainz.harmony", "Harmony", "底座", 1, 1,
                    ModSorterAgent.Models.ConflictGrade.Green, "1.绿色",
                    0, 0, 0, 0, 0, 0, 0, Array.Empty<string>()) { IsActive = true },
                new ModSorterAgent.Models.ModGradeSummary(
                    "inactive.mod", "Inactive", "通用", 2, 2,
                    ModSorterAgent.Models.ConflictGrade.Green, "1.绿色",
                    0, 0, 0, 0, 0, 0, 0, Array.Empty<string>()) { IsActive = false },
                new ModSorterAgent.Models.ModGradeSummary(
                    "mod.beta", "Beta", "通用", 3, 3,
                    ModSorterAgent.Models.ConflictGrade.Green, "1.绿色",
                    0, 0, 0, 0, 0, 0, 0, Array.Empty<string>()) { IsActive = true }
            };

            ModSortReportBuilder.ApplyModsConfigWithBackup(
                configPath,
                "1.7.4982 rev1280",
                summaries,
                Array.Empty<string>());

            if (!ModSortReportBuilder.TryReadActiveMods(configPath, out _, out var active2))
                return "ApplyModsConfigWithBackup 后无法回读 ModsConfig.xml";

            var expectedActive = new[] { "brrainz.harmony", "mod.beta" };
            if (!active2.SequenceEqual(expectedActive, StringComparer.Ordinal))
                return $"ApplyModsConfigWithBackup 未正确过滤未启用模组或顺序不一致：实际 [{string.Join(", ", active2)}]";

            var lingering = Directory.EnumerateFiles(tempDir, "*.tmp.*")
                .Concat(Directory.EnumerateFiles(tempDir, "*.rollback.*"))
                .ToList();
            if (lingering.Count > 0)
                return $"写入成功后残留临时文件：{string.Join(", ", lingering.Select(Path.GetFileName))}";

            return null;
        }
        catch (Exception ex)
        {
            return $"写入失败：{ex.GetType().Name}: {ex.Message}";
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    /// <summary>目标文件不存在时的安全写入流程：目标文件正确创建并校验，无备份生成。</summary>
    private static string? ConfigSafeWriteCreatesWhenNotExisted()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ModSorterAgent_SafeWriteNew_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);

        try
        {
            var configPath = Path.Combine(tempDir, "ModsConfig.xml");
            var expected = new[] { "brrainz.harmony", "ludeon.rimworld" };

            var backup = ModSortReportBuilder.ApplyPackageIdsWithBackup(
                configPath,
                "1.7.4982 rev1280",
                expected,
                Array.Empty<string>());

            if (backup != null)
                return "原文件不存在时备份应为 null";

            if (!File.Exists(configPath))
                return "原文件不存在时写入未能创建目标文件";

            if (!ModSortReportBuilder.TryReadActiveMods(configPath, out _, out var active) ||
                !active.SequenceEqual(expected, StringComparer.Ordinal))
                return "新建文件回读 activeMods 与预期不符";

            var lingering = Directory.EnumerateFiles(tempDir, "*.tmp.*")
                .Concat(Directory.EnumerateFiles(tempDir, "*.rollback.*"))
                .ToList();
            if (lingering.Count > 0)
                return $"新建写入后残留临时文件：{string.Join(", ", lingering.Select(Path.GetFileName))}";

            return null;
        }
        catch (Exception ex)
        {
            return $"新建写入失败：{ex.GetType().Name}: {ex.Message}";
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    /// <summary>临时文件校验失败时的防护：原目标文件内容完全保持不变，抛出异常，临时文件清理。</summary>
    private static string? ConfigPreVerificationFailurePreservesOriginal()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ModSorterAgent_PreFail_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);

        try
        {
            var configPath = Path.Combine(tempDir, "ModsConfig.xml");
            const string original = """
            <?xml version="1.0" encoding="utf-8"?>
            <ModsConfigData>
              <version>1.7.4982 rev1280</version>
              <activeMods>
                <li>brrainz.harmony</li>
                <li>ludeon.rimworld</li>
              </activeMods>
            </ModsConfigData>
            """;
            File.WriteAllText(configPath, original);
            var originalBytes = File.ReadAllBytes(configPath);

            var threw = false;
            try
            {
                ModSortReportBuilder.OnBeforeTempFileVerifyForTesting = tempPath =>
                {
                    File.WriteAllText(tempPath, "<ModsConfigData><version>1.7</version><activeMods><li>wrong.mod</li></activeMods></ModsConfigData>");
                };

                ModSortReportBuilder.ApplyPackageIdsWithBackup(
                    configPath,
                    "1.7.4982 rev1280",
                    new[] { "brrainz.harmony", "ludeon.rimworld", "new.mod" },
                    Array.Empty<string>());
            }
            catch (IOException)
            {
                threw = true;
            }
            finally
            {
                ModSortReportBuilder.OnBeforeTempFileVerifyForTesting = null;
            }

            if (!threw)
                return "临时文件校验不匹配时未抛出异常，悄悄报告成功";

            var afterBytes = File.ReadAllBytes(configPath);
            if (!originalBytes.SequenceEqual(afterBytes))
                return "临时文件校验失败时原文件被篡改，未能保留原文件内容";

            var lingering = Directory.EnumerateFiles(tempDir, "*.tmp.*")
                .Concat(Directory.EnumerateFiles(tempDir, "*.rollback.*"))
                .ToList();
            if (lingering.Count > 0)
                return $"临时文件校验失败后未清理临时文件：{string.Join(", ", lingering.Select(Path.GetFileName))}";

            return null;
        }
        catch (Exception ex)
        {
            return $"预检异常未按预期处理：{ex.GetType().Name}: {ex.Message}";
        }
        finally
        {
            ModSortReportBuilder.OnBeforeTempFileVerifyForTesting = null;
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    /// <summary>目标文件校验失败时的防护：原目标文件自动回滚恢复，抛出异常，临时与回滚文件清理。</summary>
    private static string? ConfigPostVerificationFailureRollsBack()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ModSorterAgent_PostFail_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);

        try
        {
            var configPath = Path.Combine(tempDir, "ModsConfig.xml");
            const string original = """
            <?xml version="1.0" encoding="utf-8"?>
            <ModsConfigData>
              <version>1.7.4982 rev1280</version>
              <activeMods>
                <li>brrainz.harmony</li>
                <li>ludeon.rimworld</li>
              </activeMods>
            </ModsConfigData>
            """;
            File.WriteAllText(configPath, original);
            var originalBytes = File.ReadAllBytes(configPath);

            var threw = false;
            try
            {
                ModSortReportBuilder.OnBeforeFinalVerifyForTesting = targetPath =>
                {
                    File.WriteAllText(targetPath, "<ModsConfigData><version>1.7</version><activeMods><li>corrupted.mod</li></activeMods></ModsConfigData>");
                };

                ModSortReportBuilder.ApplyPackageIdsWithBackup(
                    configPath,
                    "1.7.4982 rev1280",
                    new[] { "brrainz.harmony", "ludeon.rimworld", "new.mod" },
                    Array.Empty<string>());
            }
            catch (IOException)
            {
                threw = true;
            }
            finally
            {
                ModSortReportBuilder.OnBeforeFinalVerifyForTesting = null;
            }

            if (!threw)
                return "目标文件回读校验不匹配时未抛出异常，悄悄报告成功";

            var afterBytes = File.ReadAllBytes(configPath);
            if (!originalBytes.SequenceEqual(afterBytes))
                return "目标文件回读校验失败时未能回滚恢复原目标文件";

            var lingering = Directory.EnumerateFiles(tempDir, "*.tmp.*")
                .Concat(Directory.EnumerateFiles(tempDir, "*.rollback.*"))
                .ToList();
            if (lingering.Count > 0)
                return $"回滚恢复后残留临时或回滚文件：{string.Join(", ", lingering.Select(Path.GetFileName))}";

            return null;
        }
        catch (Exception ex)
        {
            return $"终检异常未按预期处理：{ex.GetType().Name}: {ex.Message}";
        }
        finally
        {
            ModSortReportBuilder.OnBeforeFinalVerifyForTesting = null;
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    /// <summary>回滚失败时的防护：保留 rollback 备份文件并在抛出的 IOException 信息中包含其路径，不得谎报成功或删除备份。</summary>
    private static string? ConfigRollbackFailurePreservesBackupAndReportsPath()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ModSorterAgent_RbFail_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);

        try
        {
            var configPath = Path.Combine(tempDir, "ModsConfig.xml");
            const string original = """
            <?xml version="1.0" encoding="utf-8"?>
            <ModsConfigData>
              <version>1.7.4982 rev1280</version>
              <activeMods>
                <li>brrainz.harmony</li>
                <li>ludeon.rimworld</li>
              </activeMods>
            </ModsConfigData>
            """;
            File.WriteAllText(configPath, original);
            var originalBytes = File.ReadAllBytes(configPath);

            string? capturedRollbackPath = null;
            string? caughtMessage = null;
            var threw = false;

            try
            {
                ModSortReportBuilder.OnBeforeFinalVerifyForTesting = targetPath =>
                {
                    File.WriteAllText(targetPath, "<ModsConfigData><version>1.7</version><activeMods><li>corrupted.mod</li></activeMods></ModsConfigData>");
                };

                ModSortReportBuilder.OnBeforeRollbackForTesting = (rollbackPath, targetPath) =>
                {
                    capturedRollbackPath = rollbackPath;
                    throw new IOException("模拟回滚过程发生 I/O 硬件故障");
                };

                ModSortReportBuilder.ApplyPackageIdsWithBackup(
                    configPath,
                    "1.7.4982 rev1280",
                    new[] { "brrainz.harmony", "ludeon.rimworld", "new.mod" },
                    Array.Empty<string>());
            }
            catch (IOException ex)
            {
                threw = true;
                caughtMessage = ex.Message;
            }
            finally
            {
                ModSortReportBuilder.OnBeforeFinalVerifyForTesting = null;
                ModSortReportBuilder.OnBeforeRollbackForTesting = null;
            }

            if (!threw)
                return "回滚失败时未抛出 IOException 异常，谎报写入回滚成功";

            if (string.IsNullOrWhiteSpace(capturedRollbackPath))
                return "未能捕获 rollback 文件路径";

            if (!File.Exists(capturedRollbackPath))
                return $"回滚失败后 rollback 备份文件被误删：{capturedRollbackPath}";

            var rollbackBytes = File.ReadAllBytes(capturedRollbackPath);
            if (!originalBytes.SequenceEqual(rollbackBytes))
                return "保留的 rollback 备份文件内容已损坏或与原文件不符";

            if (!caughtMessage?.Contains(capturedRollbackPath, StringComparison.OrdinalIgnoreCase) ?? true)
                return $"抛出的异常信息未包含可恢复文件的路径：{caughtMessage}";

            return null;
        }
        catch (Exception ex)
        {
            return $"回滚异常测试未按预期完成：{ex.GetType().Name}: {ex.Message}";
        }
        finally
        {
            ModSortReportBuilder.OnBeforeFinalVerifyForTesting = null;
            ModSortReportBuilder.OnBeforeRollbackForTesting = null;
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    /// <summary>
    /// 当原文件原本不存在时，如果在写入过程中（例如临时文件校验前/移动前）第三方并发创建了目标文件，
    /// 写入过程中断或失败时，绝不能将第三方并发创建的目标文件删除。
    /// </summary>
    private static string? ConfigTargetCreationRaceFailurePreservesOtherTarget()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ModSorterAgent_Race_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);

        try
        {
            var configPath = Path.Combine(tempDir, "ModsConfig.xml");
            const string sentinelContent = "SENTINEL_CREATED_BY_ANOTHER_PROCESS";

            var threw = false;
            try
            {
                ModSortReportBuilder.OnBeforeTempFileVerifyForTesting = _ =>
                {
                    // 在临时文件校验阶段向原本不存在的目标路径放置哨兵文件，模拟并发竞争
                    File.WriteAllText(configPath, sentinelContent);
                };

                ModSortReportBuilder.ApplyPackageIdsWithBackup(
                    configPath,
                    "1.7.4982 rev1280",
                    new[] { "brrainz.harmony", "ludeon.rimworld" },
                    Array.Empty<string>());
            }
            catch (IOException)
            {
                threw = true;
            }
            finally
            {
                ModSortReportBuilder.OnBeforeTempFileVerifyForTesting = null;
            }

            if (!threw)
                return "并发竞争异常发生时未按预期抛出异常";

            if (!File.Exists(configPath))
                return "写入失败后误删了他人并发创建的目标文件";

            var currentContent = File.ReadAllText(configPath);
            if (!string.Equals(currentContent, sentinelContent, StringComparison.Ordinal))
                return "他人并发创建的目标文件内容被破坏或篡改";

            var lingering = Directory.EnumerateFiles(tempDir, "*.tmp.*")
                .Concat(Directory.EnumerateFiles(tempDir, "*.rollback.*"))
                .ToList();
            if (lingering.Count > 0)
                return $"竞争失败后残留临时文件：{string.Join(", ", lingering.Select(Path.GetFileName))}";

            return null;
        }
        catch (Exception ex)
        {
            return $"并发竞争测试未按预期完成：{ex.GetType().Name}: {ex.Message}";
        }
        finally
        {
            ModSortReportBuilder.OnBeforeTempFileVerifyForTesting = null;
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    /// <summary>
    /// 非法重复（含大小写或 base/_steam 变体）与空模组标识必须在写入前校验失败，
    /// 并且目标原文件字节严格保持不变，不生成临时文件或损坏备份。
    /// </summary>
    private static string? ConfigIllegalIdsFailBeforeWriteAndPreservesOriginal()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ModSorterAgent_Illegal_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);

        try
        {
            var configPath = Path.Combine(tempDir, "ModsConfig.xml");
            const string original = """
            <?xml version="1.0" encoding="utf-8"?>
            <ModsConfigData>
              <version>1.7.4982 rev1280</version>
              <activeMods>
                <li>brrainz.harmony</li>
                <li>ludeon.rimworld</li>
              </activeMods>
            </ModsConfigData>
            """;
            File.WriteAllText(configPath, original);
            var originalBytes = File.ReadAllBytes(configPath);

            // 1. 包含空项
            var threwEmpty = false;
            try
            {
                ModSortReportBuilder.ApplyPackageIdsWithBackup(
                    configPath,
                    "1.7.4982 rev1280",
                    new[] { "brrainz.harmony", "   ", "ludeon.rimworld" },
                    Array.Empty<string>());
            }
            catch (InvalidOperationException)
            {
                threwEmpty = true;
            }

            if (!threwEmpty)
                return "包含空 PackageId 时未在写入前抛出 InvalidOperationException";

            if (!File.ReadAllBytes(configPath).SequenceEqual(originalBytes))
                return "空 PackageId 校验失败后原文件字节发生变化";

            // 2. 包含重复项（同名大小写重复）
            var threwDuplicateCase = false;
            try
            {
                ModSortReportBuilder.ApplyPackageIdsWithBackup(
                    configPath,
                    "1.7.4982 rev1280",
                    new[] { "brrainz.harmony", "BRRAINZ.HARMONY" },
                    Array.Empty<string>());
            }
            catch (InvalidOperationException)
            {
                threwDuplicateCase = true;
            }

            if (!threwDuplicateCase)
                return "包含大小写重复 PackageId 时未在写入前抛出 InvalidOperationException";

            if (!File.ReadAllBytes(configPath).SequenceEqual(originalBytes))
                return "大小写重复 PackageId 校验失败后原文件字节发生变化";

            // 3. 包含 base 与 _steam 变体重复
            var threwDuplicateSteam = false;
            try
            {
                ModSortReportBuilder.ApplyPackageIdsWithBackup(
                    configPath,
                    "1.7.4982 rev1280",
                    new[] { "brrainz.harmony", "brrainz.harmony_steam" },
                    Array.Empty<string>());
            }
            catch (InvalidOperationException)
            {
                threwDuplicateSteam = true;
            }

            if (!threwDuplicateSteam)
                return "包含 base 与 _steam 变体重复时未在写入前抛出 InvalidOperationException";

            if (!File.ReadAllBytes(configPath).SequenceEqual(originalBytes))
                return "base/_steam 变体重复校验失败后原文件字节发生变化";

            var lingering = Directory.EnumerateFiles(tempDir, "*.tmp.*")
                .Concat(Directory.EnumerateFiles(tempDir, "*.rollback.*"))
                .ToList();
            if (lingering.Count > 0)
                return $"非法项校验失败后残留临时文件：{string.Join(", ", lingering.Select(Path.GetFileName))}";

            return null;
        }
        catch (Exception ex)
        {
            return $"非法项校验测试未按预期完成：{ex.GetType().Name}: {ex.Message}";
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    /// <summary>
    /// TryReadActiveMods 回读校验：缺失 activeMods 根节点或存在空 li 元素时必须返回 false。
    /// </summary>
    private static string? ConfigTryReadActiveModsMalformedXmlReturnsFalse()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ModSorterAgent_TryRead_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);

        try
        {
            // 1. 缺失 activeMods 节点
            var pathNoActiveMods = Path.Combine(tempDir, "NoActiveMods.xml");
            File.WriteAllText(pathNoActiveMods, """
            <?xml version="1.0" encoding="utf-8"?>
            <ModsConfigData>
              <version>1.7.4982 rev1280</version>
            </ModsConfigData>
            """);

            if (ModSortReportBuilder.TryReadActiveMods(pathNoActiveMods, out _, out _))
                return "缺失 activeMods 节点时 TryReadActiveMods 应返回 false，实际返回 true";

            // 2. 含有空的 <li></li>
            var pathEmptyLi = Path.Combine(tempDir, "EmptyLi.xml");
            File.WriteAllText(pathEmptyLi, """
            <?xml version="1.0" encoding="utf-8"?>
            <ModsConfigData>
              <version>1.7.4982 rev1280</version>
              <activeMods>
                <li>brrainz.harmony</li>
                <li>   </li>
              </activeMods>
            </ModsConfigData>
            """);

            if (ModSortReportBuilder.TryReadActiveMods(pathEmptyLi, out _, out _))
                return "含有空白 <li> 元素时 TryReadActiveMods 应返回 false，实际返回 true";

            // 3. 根节点不是 ModsConfigData
            var pathWrongRoot = Path.Combine(tempDir, "WrongRoot.xml");
            File.WriteAllText(pathWrongRoot, """
            <?xml version="1.0" encoding="utf-8"?>
            <InvalidRoot>
              <activeMods>
                <li>brrainz.harmony</li>
              </activeMods>
            </InvalidRoot>
            """);

            if (ModSortReportBuilder.TryReadActiveMods(pathWrongRoot, out _, out _))
                return "根节点错误时 TryReadActiveMods 应返回 false，实际返回 true";

            return null;
        }
        catch (Exception ex)
        {
            return $"TryReadActiveMods 测试未按预期完成：{ex.GetType().Name}: {ex.Message}";
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }
}
