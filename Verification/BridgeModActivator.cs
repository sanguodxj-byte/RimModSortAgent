using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using ModSorterAgent.Discovery;

namespace ModSorterAgent.Verification;

/// <summary>
/// 实机验证期间的桥接模组（RimBridgeServer 与 RuntimePatchProbe）临时启用与精确恢复。
///
/// 桥接模组平时不进入玩家加载列表以避免影响日常启动时间，仅在实机验证开始时
/// 临时写入 ModsConfig.xml 的 activeMods，验证结束后通过快照精确还原原始文件。
/// </summary>
public sealed class BridgeModActivator : IDisposable
{
    private readonly string _modsConfigPath;
    private readonly byte[]? _originalBytes;
    private readonly bool _changed;
    private bool _restored;

    private BridgeModActivator(string modsConfigPath, byte[]? originalBytes, bool changed)
    {
        _modsConfigPath = modsConfigPath;
        _originalBytes = originalBytes;
        _changed = changed;
    }

    public bool WasAlreadyActive { get; private set; }

    /// <summary>
    /// 还原用的备份文件路径。
    /// </summary>
    private static string PendingBackupPath(string modsConfigPath) => modsConfigPath + ".modsorteragent-livetest";

    /// <summary>
    /// 恢复上次实机验证异常中断遗留的临时配置，避免桥接模组长期滞留在加载列表中。
    /// </summary>
    public static void RestoreStaleState(string? modsConfigPath = null)
    {
        var path = string.IsNullOrWhiteSpace(modsConfigPath)
            ? ModListResolver.DefaultModsConfigPath
            : modsConfigPath;

        var backup = PendingBackupPath(path);
        if (!File.Exists(backup))
            return;

        try
        {
            var backupText = File.ReadAllText(backup);
            var currentText = File.Exists(path) ? File.ReadAllText(path) : string.Empty;

            var backupHasBridge = backupText.Contains(BridgeModLocator.BridgePackageId, StringComparison.OrdinalIgnoreCase);
            var currentHasBridge = currentText.Contains(BridgeModLocator.BridgePackageId, StringComparison.OrdinalIgnoreCase);

            var backupHasProbe = backupText.Contains(BridgeModLocator.RuntimePatchProbePackageId, StringComparison.OrdinalIgnoreCase);
            var currentHasProbe = currentText.Contains(BridgeModLocator.RuntimePatchProbePackageId, StringComparison.OrdinalIgnoreCase);

            var backupHasHarmony = backupText.Contains(BridgeModLocator.HarmonyPackageId, StringComparison.OrdinalIgnoreCase);
            var currentHasHarmony = currentText.Contains(BridgeModLocator.HarmonyPackageId, StringComparison.OrdinalIgnoreCase);
            var shouldRestore = (!backupHasBridge && currentHasBridge) || (!backupHasProbe && currentHasProbe) || (!backupHasHarmony && currentHasHarmony);
            if (shouldRestore)
            {
                var original = File.ReadAllBytes(backup);
                if (!TryWriteBytes(path, original))
                    return;
            }

            File.Delete(backup);
        }
        catch
        {
            // 清理失败时忽略，下次启动将再次重试
        }
    }

    /// <summary>
    /// 临时启用桥接模组与探测模组。若两者均已启用则不进行任何写入。
    /// </summary>
    public static BridgeModActivator Activate(string? modsConfigPath = null)
    {
        var path = string.IsNullOrWhiteSpace(modsConfigPath)
            ? ModListResolver.DefaultModsConfigPath
            : modsConfigPath;

        if (!File.Exists(path))
            return new BridgeModActivator(path, null, false);

        var originalBytes = File.ReadAllBytes(path);

        XDocument doc;
        try
        {
            doc = XDocument.Load(path);
        }
        catch
        {
            return new BridgeModActivator(path, originalBytes, false);
        }

        var activeMods = doc.Root?.Element("activeMods");
        if (activeMods == null)
            return new BridgeModActivator(path, originalBytes, false);

        var bridgeItem = activeMods.Elements("li")
            .FirstOrDefault(li => string.Equals(li.Value.Trim(), BridgeModLocator.BridgePackageId, StringComparison.OrdinalIgnoreCase));
        var probeItem = activeMods.Elements("li")
            .FirstOrDefault(li => string.Equals(li.Value.Trim(), BridgeModLocator.RuntimePatchProbePackageId, StringComparison.OrdinalIgnoreCase));

        var hasBridge = bridgeItem != null;
        var hasProbe = probeItem != null;
        var hasHarmony = activeMods.Elements("li").Any(li =>
            li.Value.Trim().Equals(BridgeModLocator.HarmonyPackageId, StringComparison.OrdinalIgnoreCase));

        if (hasBridge && hasProbe && hasHarmony)
        {
            return new BridgeModActivator(path, originalBytes, false) { WasAlreadyActive = true };
        }

        if (!hasHarmony)
            activeMods.AddFirst(new XElement("li", BridgeModLocator.HarmonyPackageId));

        // 确定插入锚点：必须在 Harmony 和官方 DLC 之后，普通内容模组之前
        var items = activeMods.Elements("li").ToList();
        var insertIndex = 0;
        for (var i = 0; i < items.Count; i++)
        {
            var id = items[i].Value.Trim();
            if (id.Equals("brrainz.harmony", StringComparison.OrdinalIgnoreCase) ||
                id.StartsWith("ludeon.rimworld", StringComparison.OrdinalIgnoreCase))
            {
                insertIndex = i + 1;
            }
        }

        if (!hasBridge && !hasProbe)
        {
            var bridgeElement = new XElement("li", BridgeModLocator.BridgePackageId);
            var probeElement = new XElement("li", BridgeModLocator.RuntimePatchProbePackageId);

            if (insertIndex >= items.Count)
            {
                activeMods.Add(bridgeElement);
                activeMods.Add(probeElement);
            }
            else
            {
                items[insertIndex].AddBeforeSelf(bridgeElement);
                bridgeElement.AddAfterSelf(probeElement);
            }
        }
        else if (!hasBridge)
        {
            var bridgeElement = new XElement("li", BridgeModLocator.BridgePackageId);
            probeItem!.AddBeforeSelf(bridgeElement);
        }
        else if (!hasProbe)
        {
            var probeElement = new XElement("li", BridgeModLocator.RuntimePatchProbePackageId);
            bridgeItem!.AddAfterSelf(probeElement);
        }

        try
        {
            File.WriteAllBytes(PendingBackupPath(path), originalBytes);
            doc.Save(path);
            return new BridgeModActivator(path, originalBytes, true);
        }
        catch
        {
            if (TryWriteBytes(path, originalBytes))
                TryDelete(PendingBackupPath(path));
            return new BridgeModActivator(path, originalBytes, false);
        }
    }

    /// <summary>
    /// 将 ModsConfig.xml 还原至启用前的原始字节。
    /// </summary>
    public void Dispose()
    {
        if (_restored || !_changed || _originalBytes == null)
            return;

        _restored = true;
        TryWriteBytes(_modsConfigPath, _originalBytes);

        if (File.Exists(_modsConfigPath))
        {
            try
            {
                var current = File.ReadAllBytes(_modsConfigPath);
                if (current.SequenceEqual(_originalBytes))
                    TryDelete(PendingBackupPath(_modsConfigPath));
            }
            catch
            {
                // 比对失败时保留备份
            }
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // 删除失败不影响主流程
        }
    }

    private static bool TryWriteBytes(string path, byte[] bytes)
    {
        try
        {
            File.WriteAllBytes(path, bytes);
            return File.ReadAllBytes(path).SequenceEqual(bytes);
        }
        catch
        {
            return false;
        }
    }
}
