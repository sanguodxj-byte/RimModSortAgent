using ModSorterAgent.Models;

namespace ModSorterAgent.Analysis;

/// <summary>
/// <see cref="XmlCrossReferenceAnalyzer"/> 的合成夹具自检。
///
/// 双向断言：
/// · 该报的报——日志里的每条 Def 错误都要归因到正确的引用方/定义方，六种形态各一条
///   （类型不匹配、悬空、未启用模组、值带多余空白、磁盘有定义但游戏没解析到、Config error）；
/// · 不该报的不报——日志没报过的静态引用一条都不能出现（纯静态扫描在真实清单上会多报上百条），
///   label/description 里的同名字符串不能被当成引用点，门控分支里的引用不能归因到写它的模组。
///
/// 夹具里的名字全是 SelfTest/ModOnly/Ghost/Dormant/Clean/NeverInstalled 这类自造名，
/// 不复用线上真实案例，避免照着答案写测试。
/// </summary>
public static class XmlCrossReferenceSelfTest
{
    public static IReadOnlyList<string> Run()
    {
        var failures = new List<string>();
        var root = Path.Combine(Path.GetTempPath(), "msa-xmlref-selftest-" + Guid.NewGuid().ToString("N")[..8]);

        try
        {
            var mods = BuildFixture(root);
            var logErrors = XmlCrossReferenceAnalyzer.ParseGameLog(FakeLog());
            AssertParsing(failures, logErrors);

            var issues = XmlCrossReferenceAnalyzer.Analyze(mods, logErrors);
            AssertAttribution(failures, issues);
        }
        catch (Exception ex)
        {
            failures.Add($"自检异常：{ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            TryDelete(root);
        }

        return failures;
    }

    private static IEnumerable<string> FakeLog() => new[]
    {
        "2026-10-21 21:07:02.201 ERROR [Verse] Could not resolve cross-reference: No Verse.DamageDef named ModOnlyScorch found to give to Verse.DamageMultiplier Verse.DamageMultiplier",
        // 同一条游戏会重复打印多次，只应算一条
        "2026-10-21 21:07:02.201 ERROR [Verse] Could not resolve cross-reference: No Verse.DamageDef named ModOnlyScorch found to give to Verse.DamageMultiplier Verse.DamageMultiplier",
        "2026-10-21 21:07:02.300 ERROR [Verse] Could not resolve cross-reference: No Verse.ThingDef named GhostThing found to give to Some.Mod.ForceWearItem Some.Mod.ForceWearItem",
        "2026-10-21 21:07:02.400 ERROR [Verse] Could not resolve cross-reference: No Verse.ThingDef named DormantThing found to give to Some.Mod.Field",
        "2026-10-21 21:07:02.500 ERROR [Verse] Could not resolve cross-reference: No Verse.ThingDef named GhostThing2 found to give to Some.Mod.Field",
        "2026-10-21 21:07:02.600 ERROR [Verse] Could not resolve cross-reference: No Verse.ThingDef named NeverInstalled found to give to Some.Mod.Field",
        "2026-10-21 21:07:02.700 ERROR [Verse] Could not resolve cross-reference: No Verse.ThingDef named NobodyReferencesThis found to give to Some.Mod.Field",
        // 病因是引用值带前导空格：日志里 "named" 后面是两个空格
        "2026-10-21 21:07:02.750  WARN [Verse] Could not resolve cross-reference: No Verse.SoundDef named  GhostSound found to give to Verse.SubEffecterDef Verse.SubEffecterDef (using undefined sound instead)",
        // 引用它的元素名不以 Def 结尾（自定义 comp 字段），静态扫描按名字形状找不出来
        "2026-10-21 21:07:02.800 ERROR [Verse] Could not resolve cross-reference: No Verse.SoundDef named BurnGhost found to give to Some.Mod.CompProperties_Flame Some.Mod.CompProperties_Flame",
        // 磁盘上确实定义了、类型也对得上，游戏仍然没解析到
        "2026-10-21 21:07:02.900 ERROR [Verse] Could not resolve cross-reference: No Verse.SoundDef named CleanGhost found to give to Verse.SubEffecterDef",
        "2026-10-21 21:07:05.604 ERROR [Verse] Config error in ModB_OwnDef: has multiple damage multipliers for damageDef Bomb",
        "2026-10-21 21:07:06.000 INFO [Verse] 这行不是错误，不该被解析出来"
    };

    private static void AssertParsing(List<string> failures, IReadOnlyList<GameLogDefError> logErrors)
    {
        var cross = logErrors.Where(e => e.Kind == GameLogDefErrorKind.CrossReference).ToList();
        var config = logErrors.Where(e => e.Kind == GameLogDefErrorKind.ConfigError).ToList();

        if (cross.Count != 9)
            failures.Add($"日志解析：跨引用错误应为 9 条（重复行合并后），实际 {cross.Count}");

        if (config.Count != 1 || config[0].Name != "ModB_OwnDef")
            failures.Add($"日志解析：Config error 应解析出 ModB_OwnDef，实际 {string.Join(",", config.Select(c => c.Name))}");

        if (config.Count == 1 && string.IsNullOrEmpty(config[0].Detail))
            failures.Add("日志解析：Config error 没保住原因文本");

        var scorch = cross.FirstOrDefault(e => e.Name == "ModOnlyScorch");
        if (scorch?.ExpectedDefType != "DamageDef")
            failures.Add($"日志解析：ModOnlyScorch 的期望 DefType 应为 DamageDef，实际 {scorch?.ExpectedDefType ?? "null"}");

        var whitespace = cross.FirstOrDefault(e => e.TrimmedName == "GhostSound");
        if (whitespace == null)
            failures.Add("日志解析：没解析出 GhostSound");
        else if (!whitespace.HasStrayWhitespace)
            failures.Add("日志解析：GhostSound 的前导空格被吃掉了，病因丢失");

        if (logErrors.Any(e => e.Name.Contains("不是错误", StringComparison.Ordinal)))
            failures.Add("日志解析：把 INFO 行当成错误解析出来了");
    }

    private static void AssertAttribution(List<string> failures, IReadOnlyList<XmlDefReferenceIssue> issues)
    {
        // ── 类型不匹配：引用方 mod.b，定义方 mod.a ──
        var scorch = issues.Where(i => i.ReferencedName == "ModOnlyScorch").ToList();
        if (scorch.Count != 1)
        {
            failures.Add($"重复日志行应只产出一条、且 mod.a 自己的 <hediff> 同名字段不该被当成引用点，实际 {scorch.Count} 条");
        }
        else
        {
            var issue = scorch[0];
            Expect(failures, issue.Kind == XmlDefIssueKind.TypeMismatch, $"ModOnlyScorch 应判为类型不匹配，实际 {issue.Kind}");
            Expect(failures, issue.ReferencingPackageId == "mod.b", $"引用方应是 mod.b，实际 {issue.ReferencingPackageId}");
            Expect(failures, issue.DefiningPackageId == "mod.a", $"定义方应是 mod.a，实际 {issue.DefiningPackageId ?? "null"}");
            Expect(failures, issue.ExpectedDefType == "DamageDef", $"期望类型应是 DamageDef，实际 {issue.ExpectedDefType}");
            Expect(failures, issue.ActualDefTypes.Contains("HediffDef"), "实际类型应含 HediffDef");
            Expect(failures, issue.ElementName == "damageDef", $"引用字段应是 damageDef，实际 {issue.ElementName}");
        }

        // ── 悬空：谁都没定义 ──
        var ghost = issues.Where(i => i.ReferencedName == "GhostThing").ToList();
        Expect(failures, ghost.Count == 1, $"GhostThing 应恰好一条（label 与 clipPath 里的同名字符串不算引用点），实际 {ghost.Count} 条");
        if (ghost.Count == 1)
        {
            Expect(failures, ghost[0].Kind == XmlDefIssueKind.NotDefined, $"GhostThing 应判为悬空，实际 {ghost[0].Kind}");
            Expect(failures, ghost[0].DefiningPackageId == null, $"GhostThing 不该有定义方，实际 {ghost[0].DefiningPackageId}");
            Expect(failures, ghost[0].ElementName == "apparelDef", $"GhostThing 的引用字段应是 apparelDef，实际 {ghost[0].ElementName}");
            // 取证侧靠 "DefType:DefName" 捞 XML 原文，所以必须报出引用写在哪个 Def 里
            Expect(failures, ghost[0].EnclosingDefType == "ThingDef", $"GhostThing 的归属 Def 类型应是 ThingDef，实际 {ghost[0].EnclosingDefType ?? "null"}");
            Expect(failures, ghost[0].EnclosingDefName == "ModB_OwnDef", $"GhostThing 的归属 Def 名应是 ModB_OwnDef，实际 {ghost[0].EnclosingDefName ?? "null"}");
        }

        // ── 未启用的模组定义了它 ──
        var dormant = issues.FirstOrDefault(i => i.ReferencedName == "DormantThing");
        Expect(failures, dormant != null, "DormantThing 应被报出来");
        if (dormant != null)
        {
            Expect(failures, dormant.Kind == XmlDefIssueKind.NotEnabled, $"DormantThing 应判为「定义方未启用」，实际 {dormant.Kind}");
            Expect(failures, dormant.DefiningPackageId == "mod.dormant", $"定义方应是 mod.dormant，实际 {dormant.DefiningPackageId ?? "null"}");
        }

        // ── 补丁文件里未门控的引用 ──
        var fromPatch = issues.FirstOrDefault(i => i.ReferencedName == "GhostThing2");
        Expect(failures, fromPatch != null, "未门控补丁里的引用也应被归因（GhostThing2）");
        if (fromPatch != null)
            Expect(failures, string.IsNullOrEmpty(fromPatch.EnclosingDefName),
                $"补丁文件里的引用没有归属 Def，实际 {fromPatch.EnclosingDefName}");

        // ── 引用值带多余空白 ──
        var whitespace = issues.FirstOrDefault(i => i.ReferencedName == "GhostSound");
        Expect(failures, whitespace != null, "GhostSound 应被报出来");
        if (whitespace != null)
        {
            Expect(failures, whitespace.Kind == XmlDefIssueKind.StrayWhitespace, $"GhostSound 应判为值带多余空白，实际 {whitespace.Kind}");
            Expect(failures, whitespace.RawReferencedValue != whitespace.ReferencedName, "GhostSound 应保留带空白的原值作为证据");
            Expect(failures, whitespace.ReferencingPackageId == "mod.b", $"GhostSound 的引用方应是 mod.b，实际 {whitespace.ReferencingPackageId}");
        }

        // ── 元素名不以 Def 结尾也要能归因 ──
        var burn = issues.FirstOrDefault(i => i.ReferencedName == "BurnGhost");
        Expect(failures, burn != null, "BurnGhost 应被报出来（引用字段名不以 Def 结尾）");
        if (burn != null)
        {
            Expect(failures, burn.ElementName == "burnSound", $"BurnGhost 的引用字段应是 burnSound，实际 {burn.ElementName}");
            Expect(failures, burn.ReferencingPackageId == "mod.b", $"BurnGhost 的引用方应是 mod.b，实际 {burn.ReferencingPackageId}");
        }

        // ── 磁盘上有定义且类型对得上，游戏仍没解析到 ──
        var clean = issues.FirstOrDefault(i => i.ReferencedName == "CleanGhost");
        Expect(failures, clean != null, "CleanGhost 应被报出来");
        if (clean != null)
            Expect(failures, clean.Kind == XmlDefIssueKind.DefinedButUnresolved, $"CleanGhost 应判为「有定义但未被解析」，实际 {clean.Kind}");

        // ── Config error 归到 Def 的归属方 ──
        var configError = issues.FirstOrDefault(i => i.Kind == XmlDefIssueKind.ConfigError);
        Expect(failures, configError != null, "Config error 应被归因");
        if (configError != null)
        {
            Expect(failures, configError.ReferencingPackageId == "mod.b", $"Config error 的归属方应是 mod.b，实际 {configError.ReferencingPackageId}");
            Expect(failures, configError.ReferencedName == "ModB_OwnDef", $"Config error 的 Def 名应是 ModB_OwnDef，实际 {configError.ReferencedName}");
        }

        // ── 门控分支里的引用：不归因到写它的模组，但要如实报"没能定位引用方" ──
        var never = issues.Where(i => i.ReferencedName == "NeverInstalled").ToList();
        if (never.Any(i => i.ReferencingPackageId == "mod.b"))
            failures.Add("误报：NeverInstalled 只出现在 PatchOperationFindMod 门控分支里，不该归因到 mod.b");
        Expect(failures, never.Count == 1 && never[0].ReferencingPackageId.Length == 0,
            $"NeverInstalled 应恰好一条未归因记录，实际 {never.Count} 条");

        var nobody = issues.Where(i => i.ReferencedName == "NobodyReferencesThis").ToList();
        Expect(failures, nobody.Count == 1 && nobody[0].ReferencingPackageId.Length == 0,
            $"无人引用的日志错误应恰好一条未归因记录，实际 {nobody.Count} 条");

        // ── 最关键的一条：日志没报过的静态引用，一条都不许出现 ──
        if (issues.Any(i => i.ReferencedName == "NoLogLineThing"))
            failures.Add("误报：NoLogLineThing 只存在于 XML、日志没报过，纯静态扫描才会报它");

        if (issues.Any(i => i.ReferencedName == "SelfTestBoom"))
            failures.Add("误报：SelfTestBoom 引用类型正确、日志也没报，不该出现");

        if (issues.Any(i => i.ReferencingPackageId == "mod.dormant"))
            failures.Add("误报：未启用的模组不会被载入，不该出现归因到它的条目");
    }

    private static void Expect(List<string> failures, bool condition, string message)
    {
        if (!condition)
            failures.Add(message);
    }

    private static IReadOnlyList<ModDescriptor> BuildFixture(string root)
    {
        var core = Path.Combine(root, "Core");
        Write(core, "Defs/Damages.xml", """
            <?xml version="1.0" encoding="utf-8"?>
            <Defs>
              <DamageDef><defName>SelfTestBoom</defName><label>boom</label></DamageDef>
              <ThingDef><defName>SelfTestTunic</defName><label>tunic</label></ThingDef>
              <SoundDef><defName>GhostSound</defName><label>ghost</label></SoundDef>
              <SoundDef><defName>CleanGhost</defName><label>clean</label></SoundDef>
            </Defs>
            """);
        Write(core, "Defs/Things.xml", """
            <?xml version="1.0" encoding="utf-8"?>
            <Defs>
              <ThingDef>
                <defName>SelfTestBullet</defName>
                <projectile><damageDef>SelfTestBoom</damageDef></projectile>
              </ThingDef>
            </Defs>
            """);

        var modA = Path.Combine(root, "ModA");
        Write(modA, "Defs/Hediffs.xml", """
            <?xml version="1.0" encoding="utf-8"?>
            <Defs>
              <HediffDef><defName>ModOnlyScorch</defName><label>scorch</label></HediffDef>
              <ThingDef>
                <defName>ModA_Decoy</defName>
                <hediff>ModOnlyScorch</hediff>
              </ThingDef>
            </Defs>
            """);

        var modB = Path.Combine(root, "ModB");
        Write(modB, "Defs/Buildings.xml", """
            <?xml version="1.0" encoding="utf-8"?>
            <Defs>
              <ThingDef>
                <defName>ModB_OwnDef</defName>
                <label>GhostThing</label>
                <description>GhostThing 出现在 label 与 description 里也不该被当成引用点</description>
                <damageMultipliers>
                  <li><damageDef>ModOnlyScorch</damageDef><multiplier>0</multiplier></li>
                  <li><damageDef>SelfTestBoom</damageDef><multiplier>1</multiplier></li>
                </damageMultipliers>
                <apparelDef>GhostThing</apparelDef>
                <clipPath>GhostThing</clipPath>
                <apparelDef>DormantThing</apparelDef>
                <apparelDef>NoLogLineThing</apparelDef>
                <soundDef> GhostSound</soundDef>
                <soundDef>CleanGhost</soundDef>
                <comps>
                  <li Class="Some.Mod.CompProperties_Flame">
                    <burnSound>BurnGhost</burnSound>
                  </li>
                </comps>
              </ThingDef>
            </Defs>
            """);
        Write(modB, "Patches/Patch.xml", """
            <?xml version="1.0" encoding="utf-8"?>
            <Patch>
              <Operation Class="PatchOperationFindMod">
                <mods><li>Some Mod Not Installed</li></mods>
                <match Class="PatchOperationAdd">
                  <xpath>/Defs/ThingDef[defName="SelfTestBullet"]/costList</xpath>
                  <value><thingDef>NeverInstalled</thingDef></value>
                </match>
              </Operation>
              <Operation Class="PatchOperationAdd">
                <xpath>/Defs/ThingDef[defName="SelfTestBullet"]/costList</xpath>
                <value><thingDef>GhostThing2</thingDef></value>
              </Operation>
            </Patch>
            """);

        var dormant = Path.Combine(root, "ModDormant");
        Write(dormant, "Defs/Things.xml", """
            <?xml version="1.0" encoding="utf-8"?>
            <Defs>
              <ThingDef><defName>DormantThing</defName><label>dormant</label></ThingDef>
              <ThingDef>
                <defName>Dormant_Own</defName>
                <apparelDef>GhostThing5</apparelDef>
              </ThingDef>
            </Defs>
            """);

        var descriptors = new List<ModDescriptor>
        {
            Descriptor("ludeon.core", "SelfTest Core", core, official: true, active: true),
            Descriptor("mod.a", "Mod A", modA, official: false, active: true),
            Descriptor("mod.b", "Mod B", modB, official: false, active: true),
            Descriptor("mod.dormant", "Mod Dormant", dormant, official: false, active: false)
        };

        // 与真实管线一致：非官方模组先过 XmlConflictAnalyzer 富化，官方模组保持 XmlDefs 为空
        return descriptors
            .Select(d => d.IsOfficialCoreOrDlc ? d : XmlConflictAnalyzer.EnrichWithXmlAnalysis(d))
            .ToList();
    }

    private static ModDescriptor Descriptor(string packageId, string name, string root, bool official, bool active)
        => new()
        {
            PackageId = packageId,
            Name = name,
            RootDirectory = root,
            IsOfficialCoreOrDlc = official,
            IsActiveInModsConfig = active,
            ActiveContentDirectories = new[] { root }
        };

    private static void Write(string modRoot, string relative, string content)
    {
        var path = Path.Combine(modRoot, relative.Replace('/', Path.DirectorySeparatorChar));
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, content);
    }

    private static void TryDelete(string root)
    {
        try
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
        catch
        {
            // 临时夹具清不掉不影响结论
        }
    }
}
