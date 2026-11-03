using DecompilerServer.Services;
using ModSorterAgent.Discovery;
using ModSorterAgent.Models;

namespace ModSorterAgent.Analysis;

/// <summary>
/// 负责对模组程序集执行反编译与 Harmony 补丁特征提取。
/// 设计要点：
/// 1) 每个 DLL 使用独立的 <see cref="AssemblyContextManager"/>，因此可以安全地并发分析；
/// 2) 不做任何缓存：每一次排序都把范围内的程序集现场重读一遍，结果只来自本轮磁盘内容；
/// 3) 过滤 BCL / Unity / Harmony 等框架程序集引用，避免"引用噪声"污染依赖图。
/// </summary>
public sealed class CodeConflictAnalyzer
{
    private static readonly string[] IgnoredAssemblyPrefixes =
    {
        "System",
        "Microsoft",
        "mscorlib",
        "netstandard",
        "Mono.",
        "UnityEngine",
        "Unity.",
        "0Harmony",
        "HarmonyLib",
        "Newtonsoft",
        "ICSharpCode",
        "JetBrains",
        "Assembly-CSharp",
        "WindowsBase",
        "PresentationCore",
        "PresentationFramework"
    };

    private readonly string[] _sharedSearchDirectories;

    public CodeConflictAnalyzer(IReadOnlyList<ModDescriptor> allMods, string? rimWorldInstallDir = null)
    {
        var dirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var gameRoot = rimWorldInstallDir ?? ModListResolver.DefaultRimWorldInstallDir;

        var managedDir = Path.Combine(gameRoot, "RimWorldWin64_Data", "Managed");
        if (Directory.Exists(managedDir))
            dirs.Add(managedDir);

        foreach (var mod in allMods)
        {
            foreach (var asmPath in mod.AssemblyPaths)
            {
                var dir = Path.GetDirectoryName(asmPath);
                if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                    dirs.Add(dir);
            }
        }

        _sharedSearchDirectories = dirs.ToArray();
    }

    public sealed record ProgressInfo(int Completed, int Total, string ModName, int PatchCount);

    /// <summary>
    /// 并发分析全部模组。返回与输入等长、顺序一致的描述符列表。
    /// </summary>
    public IReadOnlyList<ModDescriptor> AnalyzeAll(
        IReadOnlyList<ModDescriptor> mods,
        Action<ProgressInfo>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        var results = new ModDescriptor[mods.Count];
        var completed = 0;
        var totalPatchCount = 0;

        for (var i = 0; i < mods.Count; i++)
            results[i] = mods[i];

        var workItems = new List<int>(mods.Count);
        for (var i = 0; i < mods.Count; i++)
        {
            var m = mods[i];
            if (m.IsOfficialCoreOrDlc || m.AssemblyPaths.Count == 0)
                continue;
            workItems.Add(i);
        }

        if (workItems.Count == 0)
        {
            onProgress?.Invoke(new ProgressInfo(mods.Count, Math.Max(1, mods.Count), "无需反编译的模组", 0));
            return results;
        }

        var options = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = Math.Max(2, Math.Min(workItems.Count, Environment.ProcessorCount))
        };

        Parallel.ForEach(workItems, options, idx =>
        {
            options.CancellationToken.ThrowIfCancellationRequested();

            var enriched = EnrichWithCodeAnalysis(mods[idx], options.CancellationToken);
            results[idx] = enriched;

            var patches = enriched.AssemblyAnalyses.Sum(a => a.Patches.Count);
            Interlocked.Add(ref totalPatchCount, patches);

            var done = Interlocked.Increment(ref completed);
            onProgress?.Invoke(new ProgressInfo(done, workItems.Count, enriched.Name, Volatile.Read(ref totalPatchCount)));
        });

        return results;
    }

    public ModDescriptor EnrichWithCodeAnalysis(ModDescriptor mod, CancellationToken cancellationToken = default)
    {
        if (mod.IsOfficialCoreOrDlc || mod.AssemblyPaths.Count == 0)
            return mod;

        var analyses = new List<AssemblyHarmonyAnalysisResult>(mod.AssemblyPaths.Count);
        var externalRefs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var embeddedSpecs = new List<EmbeddedAssemblySpec>(mod.AssemblyPaths.Count);

        foreach (var dllPath in mod.AssemblyPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!File.Exists(dllPath))
                continue;

            var analyzed = AnalyzeSingleAssembly(dllPath);
            if (analyzed == null)
                continue;

            analyses.Add(analyzed.Result);
            foreach (var reference in analyzed.ExternalReferences)
                externalRefs.Add(reference);

            if (analyzed.EmbeddedSpec != null)
                embeddedSpecs.Add(analyzed.EmbeddedSpec);
        }

        return mod with
        {
            AssemblyAnalyses = analyses,
            ReferencedExternalAssemblies = externalRefs
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            EmbeddedAssemblies = embeddedSpecs
        };
    }

    private CachedAssemblyAnalysis? AnalyzeSingleAssembly(string dllPath)
    {
        try
        {
            using var ctx = new AssemblyContextManager();
            ctx.LoadAssemblyDirect(dllPath, _sharedSearchDirectories);
            var resolver = new MemberResolver(ctx);
            var decompiler = new DecompilerService(ctx, resolver);

            var references = new List<string>();
            string asmName = Path.GetFileNameWithoutExtension(dllPath);
            string asmVersion = "1.0.0.0";
            var exportedMembers = new List<string>();
            var externalCalls = new List<string>();

            try
            {
                var metadata = ctx.GetPEFile().Metadata;
                if (metadata.IsAssembly)
                {
                    var def = metadata.GetAssemblyDefinition();
                    asmName = metadata.GetString(def.Name);
                    asmVersion = def.Version.ToString();
                }

                foreach (var handle in metadata.AssemblyReferences)
                {
                    var name = metadata.GetString(metadata.GetAssemblyReference(handle).Name);
                    if (!string.IsNullOrWhiteSpace(name) && !IsIgnoredAssemblyReference(name))
                        references.Add(name);
                }

                foreach (var typeHandle in metadata.TypeDefinitions)
                {
                    var typeDef = metadata.GetTypeDefinition(typeHandle);
                    var vis = typeDef.Attributes & System.Reflection.TypeAttributes.VisibilityMask;
                    if (vis != System.Reflection.TypeAttributes.Public && vis != System.Reflection.TypeAttributes.NestedPublic)
                        continue;

                    var typeName = metadata.GetString(typeDef.Name);
                    var typeNs = metadata.GetString(typeDef.Namespace);
                    var fullTypeName = string.IsNullOrEmpty(typeNs) ? typeName : $"{typeNs}.{typeName}";

                    foreach (var methodHandle in typeDef.GetMethods())
                    {
                        var methodDef = metadata.GetMethodDefinition(methodHandle);
                        var methodAccess = methodDef.Attributes & System.Reflection.MethodAttributes.MemberAccessMask;
                        if (methodAccess != System.Reflection.MethodAttributes.Public)
                            continue;

                        var methodName = metadata.GetString(methodDef.Name);
                        exportedMembers.Add($"{fullTypeName}::{methodName}");
                    }
                }

                foreach (var memberRefHandle in metadata.MemberReferences)
                {
                    var memberRef = metadata.GetMemberReference(memberRefHandle);
                    if (memberRef.GetKind() != System.Reflection.Metadata.MemberReferenceKind.Method)
                        continue;

                    var memberName = metadata.GetString(memberRef.Name);
                    string? declaringTypeName = null;

                    if (memberRef.Parent.Kind == System.Reflection.Metadata.HandleKind.TypeReference)
                    {
                        var typeRef = metadata.GetTypeReference((System.Reflection.Metadata.TypeReferenceHandle)memberRef.Parent);
                        var typeName = metadata.GetString(typeRef.Name);
                        var typeNs = metadata.GetString(typeRef.Namespace);
                        declaringTypeName = string.IsNullOrEmpty(typeNs) ? typeName : $"{typeNs}.{typeName}";
                    }

                    if (!string.IsNullOrWhiteSpace(declaringTypeName))
                    {
                        externalCalls.Add($"{declaringTypeName}::{memberName}");
                    }
                }
            }
            catch
            {
                // 元数据表读取失败时退化为容错模式
            }

            var result = HarmonyPatchAnalyzer.AnalyzeAssembly(ctx, resolver, decompiler, includeSnippets: false);
            var embeddedSpec = new EmbeddedAssemblySpec(
                AssemblyName: asmName,
                AssemblyVersion: asmVersion,
                FilePath: dllPath,
                ExportedMemberSignatures: exportedMembers.Distinct(StringComparer.Ordinal).ToArray(),
                ExternalMethodCalls: externalCalls.Distinct(StringComparer.Ordinal).ToArray());

            return new CachedAssemblyAnalysis(
                result,
                references.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                embeddedSpec);
        }
        catch
        {
            // 原生 DLL / 损坏程序集：静默跳过
            return null;
        }
    }

    private static bool IsIgnoredAssemblyReference(string name)
    {
        foreach (var prefix in IgnoredAssemblyPrefixes)
        {
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private sealed record CachedAssemblyAnalysis(
        AssemblyHarmonyAnalysisResult Result,
        IReadOnlyList<string> ExternalReferences,
        EmbeddedAssemblySpec? EmbeddedSpec);
}
