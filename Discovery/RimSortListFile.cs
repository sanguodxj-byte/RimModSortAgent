using System.Text;
using System.Xml.Linq;

namespace ModSorterAgent.Discovery;

/// <summary>
/// RimSort / Mod Manager 的「保存的模组列表」(<c>.rml</c>) 读写。
/// 文件结构：<c>&lt;savedModList&gt;</c> → <c>meta</c>（gameVersion / modIds / modNames / modSteamIds）
/// 与 <c>modList</c>（ids / names）。其中 <c>meta/modIds</c> 是有序的权威列表。
/// </summary>
public sealed record RimSortSavedList(
    string SourcePath,
    string? GameVersion,
    IReadOnlyList<string> PackageIds,
    IReadOnlyList<string> Names,
    IReadOnlyList<string> SteamIds)
{
    public int Count => PackageIds.Count;

    public string NameAt(int index) =>
        index >= 0 && index < Names.Count && !string.IsNullOrWhiteSpace(Names[index])
            ? Names[index]
            : PackageIds[index];
}

public static class RimSortListFile
{
    public const string Extension = ".rml";

    public static bool LooksLikeSavedModList(string path, string? headContent = null)
    {
        if (path.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.IsNullOrWhiteSpace(headContent))
            return false;

        return headContent.Contains("<savedModList", StringComparison.OrdinalIgnoreCase);
    }

    public static RimSortSavedList Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"未找到模组列表文件: {path}");

        var doc = XDocument.Load(path);
        var root = doc.Root ?? throw new InvalidOperationException("清单格式无效：缺少根节点。");

        var meta = root.Element("meta");
        var modList = root.Element("modList");

        var ids = ReadList(meta?.Element("modIds")) ?? ReadList(modList?.Element("ids"));
        if (ids == null || ids.Count == 0)
            throw new InvalidOperationException("清单格式无效：未找到 meta/modIds 列表。");

        var names = ReadList(meta?.Element("modNames")) ?? ReadList(modList?.Element("names")) ?? new List<string>();
        var steamIds = ReadList(meta?.Element("modSteamIds")) ?? new List<string>();
        var version = meta?.Element("gameVersion")?.Value?.Trim();

        return new RimSortSavedList(path, version, ids, names, steamIds);
    }

    /// <summary>
    /// 写出 RimSort 可识别的 <c>.rml</c>。steamIds 缺省写 0，RimSort 会按本地数据库重新匹配。
    /// </summary>
    public static void Save(
        string path,
        string gameVersion,
        IReadOnlyList<(string PackageId, string Name, string? SteamId)> mods)
    {
        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("savedModList",
                new XElement("meta",
                    new XElement("gameVersion", gameVersion),
                    BuildList("modIds", mods.Select(m => m.PackageId)),
                    BuildList("modSteamIds", mods.Select(m => string.IsNullOrWhiteSpace(m.SteamId) ? "0" : m.SteamId!)),
                    BuildList("modNames", mods.Select(m => m.Name))),
                new XElement("modList",
                    BuildList("ids", mods.Select(m => m.PackageId)),
                    BuildList("names", mods.Select(m => m.Name)))));

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        doc.Save(path, SaveOptions.None);
    }

    private static XElement BuildList(string tag, IEnumerable<string> values) =>
        new(tag, values.Select(v => new XElement("li", v)));

    private static List<string>? ReadList(XElement? parent)
    {
        if (parent == null)
            return null;

        var items = parent.Elements("li")
            .Select(e => (e.Value ?? string.Empty).Trim())
            .Where(v => v.Length > 0)
            .ToList();

        return items.Count > 0 ? items : null;
    }
}
