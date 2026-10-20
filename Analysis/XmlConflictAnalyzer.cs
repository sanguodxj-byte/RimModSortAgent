using System.Text.RegularExpressions;
using System.Xml.Linq;
using ModSorterAgent.Models;

namespace ModSorterAgent.Analysis;

public static class XmlConflictAnalyzer
{
    private static readonly HashSet<string> DestructivePatchClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "PatchOperationReplace",
        "PatchOperationRemove",
        "PatchOperationAttributeSet",
        "PatchOperationAttributeRemove"
    };

    private static readonly Regex XPathDefTypeAndPredicateRegex = new(
        @"/(?<defType>[A-Za-z0-9_.*]+)\s*\[(?<predicate>[^\]]+)\]",
        RegexOptions.Compiled);

    private static readonly Regex DefNameClauseRegex = new(
        @"(?:defName|@Name)\s*=\s*""(?<defName>[^""]+)""",
        RegexOptions.Compiled);

    public static ModDescriptor EnrichWithXmlAnalysis(ModDescriptor mod)
    {
        if (mod.IsOfficialCoreOrDlc)
        {
            return mod;
        }

        var defs = new List<XmlDefRecord>();
        var patches = new List<XmlPatchRecord>();

        foreach (var contentDir in mod.ActiveContentDirectories)
        {
            var defsDir = Path.Combine(contentDir, "Defs");
            if (Directory.Exists(defsDir))
            {
                ScanDefsDirectory(defsDir, defs);
            }

            var patchesDir = Path.Combine(contentDir, "Patches");
            if (Directory.Exists(patchesDir))
            {
                ScanPatchesDirectory(patchesDir, patches);
            }
        }

        return mod with
        {
            XmlDefs = defs,
            XmlPatches = patches
        };
    }

    private static void ScanDefsDirectory(string defsDir, List<XmlDefRecord> output)
    {
        IEnumerable<string> xmlFiles;
        try
        {
            xmlFiles = Directory.EnumerateFiles(defsDir, "*.xml", SearchOption.AllDirectories);
        }
        catch
        {
            return;
        }

        foreach (var xmlFile in xmlFiles)
        {
            try
            {
                var doc = XDocument.Load(xmlFile);
                var root = doc.Root;
                if (root == null || !root.Name.LocalName.Equals("Defs", StringComparison.OrdinalIgnoreCase))
                    continue;

                foreach (var defElem in root.Elements())
                {
                    var defType = defElem.Name.LocalName;
                    var defName = defElem.Element("defName")?.Value?.Trim();
                    var nameAttr = defElem.Attribute("Name")?.Value?.Trim();
                    var parentName = defElem.Attribute("ParentName")?.Value?.Trim();
                    var isAbstract = string.Equals(defElem.Attribute("Abstract")?.Value?.Trim(), "True", StringComparison.OrdinalIgnoreCase);

                    var thingClass = defElem.Element("thingClass")?.Value?.Trim();
                    var driverClass = defElem.Element("driverClass")?.Value?.Trim();
                    var workerClass = defElem.Element("workerClass")?.Value?.Trim();

                    var compClasses = new List<string>();
                    var compsNode = defElem.Element("comps");
                    if (compsNode != null)
                    {
                        foreach (var compLi in compsNode.Elements("li"))
                        {
                            var classAttr = compLi.Attribute("Class")?.Value?.Trim();
                            if (!string.IsNullOrWhiteSpace(classAttr))
                                compClasses.Add(classAttr);

                            var compClassElem = compLi.Element("compClass")?.Value?.Trim();
                            if (!string.IsNullOrWhiteSpace(compClassElem))
                                compClasses.Add(compClassElem);
                        }
                    }

                    var contentSig = string.Join("|", defElem.Elements()
                        .Select(e => $"{e.Name.LocalName}={Regex.Replace(e.Value.Trim(), @"\s+", " ")}")
                        .OrderBy(s => s, StringComparer.Ordinal));

                    output.Add(new XmlDefRecord(
                        DefType: defType,
                        DefName: defName,
                        NameAttribute: nameAttr,
                        ParentName: parentName,
                        IsAbstract: isAbstract,
                        ThingClass: thingClass,
                        DriverClass: driverClass,
                        WorkerClass: workerClass,
                        CompClasses: compClasses,
                        ContentSignature: contentSig,
                        SourceFile: xmlFile));
                }
            }
            catch
            {
                // Skip malformed XML files during static scan
            }
        }
    }

    private static void ScanPatchesDirectory(string patchesDir, List<XmlPatchRecord> output)
    {
        IEnumerable<string> xmlFiles;
        try
        {
            xmlFiles = Directory.EnumerateFiles(patchesDir, "*.xml", SearchOption.AllDirectories);
        }
        catch
        {
            return;
        }

        foreach (var xmlFile in xmlFiles)
        {
            try
            {
                var doc = XDocument.Load(xmlFile);
                var root = doc.Root;
                if (root == null || !root.Name.LocalName.Equals("Patch", StringComparison.OrdinalIgnoreCase))
                    continue;

                foreach (var opElem in root.Elements("Operation"))
                {
                    ExtractPatchOperationsRecursive(opElem, xmlFile, isGated: false, new List<string>(), output);
                }
            }
            catch
            {
                // Skip malformed patch XML
            }
        }
    }

    private static void ExtractPatchOperationsRecursive(
        XElement opElement,
        string sourceFile,
        bool isGated,
        List<string> inheritedModRefs,
        List<XmlPatchRecord> output)
    {
        var opClass = opElement.Attribute("Class")?.Value?.Trim() ?? "PatchOperation";
        var shortClass = opClass.Contains('.') ? opClass[(opClass.LastIndexOf('.') + 1)..] : opClass;

        var currentModRefs = new List<string>(inheritedModRefs);
        var currentGated = isGated;

        if (shortClass.Equals("PatchOperationFindMod", StringComparison.OrdinalIgnoreCase) ||
            shortClass.Contains("FindMod", StringComparison.OrdinalIgnoreCase))
        {
            currentGated = true;
            var modsNode = opElement.Element("mods");
            if (modsNode != null)
            {
                foreach (var li in modsNode.Elements("li"))
                {
                    var val = li.Value.Trim();
                    if (!string.IsNullOrWhiteSpace(val))
                        currentModRefs.Add(val);
                }
            }
        }
        else if (shortClass.Contains("Conditional", StringComparison.OrdinalIgnoreCase) ||
                 shortClass.Contains("ModOption", StringComparison.OrdinalIgnoreCase) ||
                 shortClass.Contains("MayRequire", StringComparison.OrdinalIgnoreCase))
        {
            currentGated = true;
        }

        var xpath = opElement.Element("xpath")?.Value?.Trim();
        if (!string.IsNullOrWhiteSpace(xpath))
        {
            var isDestructive = DestructivePatchClasses.Contains(shortClass);
            output.Add(new XmlPatchRecord(
                OperationClass: shortClass,
                XPath: NormalizeXPath(xpath),
                IsDestructive: isDestructive,
                IsConditionalOrModGated: currentGated,
                ReferencedModNamesOrIds: currentModRefs.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                SourceFile: sourceFile));
        }

        var nestedContainers = new[] { "operations", "match", "nomatch", "caseTrue", "caseFalse" };
        foreach (var containerName in nestedContainers)
        {
            var container = opElement.Element(containerName);
            if (container == null)
                continue;

            foreach (var childLi in container.Elements("li"))
            {
                ExtractPatchOperationsRecursive(childLi, sourceFile, currentGated, currentModRefs, output);
            }
        }
    }

    public static string NormalizeXPath(string rawXPath)
    {
        var collapsed = Regex.Replace(rawXPath.Trim(), @"\s+", " ");
        collapsed = collapsed.Replace("'", "\"");
        return collapsed;
    }

    public static (string? DefType, string? DefName) TryExtractTargetedDefFromXPath(string? normalizedXPath)
    {
        var all = ExtractAllTargetedDefsFromXPath(normalizedXPath);
        return all.Count > 0 ? all[0] : (null, null);
    }

    public static IReadOnlyList<(string DefType, string DefName)> ExtractAllTargetedDefsFromXPath(string? normalizedXPath)
    {
        if (string.IsNullOrWhiteSpace(normalizedXPath))
            return Array.Empty<(string, string)>();

        var results = new List<(string DefType, string DefName)>();
        foreach (Match m in XPathDefTypeAndPredicateRegex.Matches(normalizedXPath))
        {
            var defType = m.Groups["defType"].Value;
            var predicate = m.Groups["predicate"].Value;
            foreach (Match dm in DefNameClauseRegex.Matches(predicate))
            {
                var defName = dm.Groups["defName"].Value;
                if (!string.IsNullOrWhiteSpace(defName))
                {
                    results.Add((defType, defName));
                }
            }
        }

        return results;
    }
}
