using System.Xml.Linq;
using ModSorterAgent.Agent;

namespace ModSorterAgent.Patching;

public sealed record CompatInstallResult(
    bool Success,
    string Message,
    string? InstalledPath,
    string? BackupPath,
    IReadOnlyList<string> Warnings);

/// <summary>
/// 用户确认后才会调用：把已编译的兼容补丁复制到游戏 Mods 目录，并写入 ModsConfig.xml。
/// 修改配置前一定备份；已有同名但非本工具生成的目录时拒绝覆盖。
/// </summary>
public static class CompatPatchInstaller
{
    public static CompatInstallResult Install(
        CompatPatchResult patch,
        string gameDir,
        string modsConfigPath)
    {
        if (!patch.Success || !patch.IsModPackage || string.IsNullOrWhiteSpace(patch.PackageId))
            return new CompatInstallResult(false, "这不是一个可安装的补丁包。", null, null, Array.Empty<string>());

        if (!Directory.Exists(patch.OutputDirectory))
            return new CompatInstallResult(false, "补丁目录不存在：" + patch.OutputDirectory, null, null, Array.Empty<string>());

        var modsDir = Path.Combine(gameDir, "Mods");
        var targetName = Path.GetFileName(patch.OutputDirectory.TrimEnd('\\', '/'));
        var destination = Path.Combine(modsDir, targetName);
        var warnings = new List<string>();

        try
        {
            Directory.CreateDirectory(modsDir);

            if (Directory.Exists(destination))
            {
                var marker = Path.Combine(destination, ".modsorteragent");
                if (!File.Exists(marker))
                    return new CompatInstallResult(
                        false,
                        $"Mods 目录中已存在同名文件夹且不是本工具生成的：{destination}。请手动处理后再安装。",
                        null, null, warnings);

                Directory.Delete(destination, recursive: true);
            }

            CopyDirectory(patch.OutputDirectory, destination);
        }
        catch (Exception ex)
        {
            return new CompatInstallResult(false, "复制补丁到 Mods 目录失败：" + ex.Message, null, null, warnings);
        }

        string? backupPath = null;
        try
        {
            if (string.IsNullOrWhiteSpace(modsConfigPath) || !File.Exists(modsConfigPath))
            {
                warnings.Add("未找到 ModsConfig.xml，补丁已复制到 Mods，但需要手动在游戏内启用。");
            }
            else
            {
                backupPath = modsConfigPath + ".bak-" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                File.Copy(modsConfigPath, backupPath, overwrite: false);

                var doc = XDocument.Load(modsConfigPath);
                var activeMods = doc.Root?.Element("activeMods");
                if (activeMods == null)
                {
                    warnings.Add("ModsConfig.xml 中没有 activeMods，未自动启用补丁。");
                }
                else if (!activeMods.Elements("li").Any(li =>
                             string.Equals(li.Value.Trim(), patch.PackageId, StringComparison.OrdinalIgnoreCase)))
                {
                    activeMods.Add(new XElement("li", patch.PackageId));
                    doc.Save(modsConfigPath);
                }
            }
        }
        catch (Exception ex)
        {
            warnings.Add("写入 ModsConfig.xml 失败：" + ex.Message);
        }

        var message = backupPath != null
            ? $"补丁已安装到 {destination}，ModsConfig.xml 已备份到 {backupPath}。"
            : $"补丁已安装到 {destination}。";

        return new CompatInstallResult(true, message, destination, backupPath, warnings);
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var dir in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            if (dir.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
                dir.EndsWith($"{Path.DirectorySeparatorChar}obj", StringComparison.OrdinalIgnoreCase) ||
                dir.EndsWith($"{Path.DirectorySeparatorChar}bin", StringComparison.OrdinalIgnoreCase))
                continue;

            var relative = Path.GetRelativePath(source, dir);
            Directory.CreateDirectory(Path.Combine(destination, relative));
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            if (relative.Split(Path.DirectorySeparatorChar).Any(part =>
                    part.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
                    part.Equals("bin", StringComparison.OrdinalIgnoreCase)))
                continue;

            var target = Path.Combine(destination, relative);
            var targetDir = Path.GetDirectoryName(target);
            if (!string.IsNullOrWhiteSpace(targetDir))
                Directory.CreateDirectory(targetDir);
            File.Copy(file, target, overwrite: true);
        }
    }
}
