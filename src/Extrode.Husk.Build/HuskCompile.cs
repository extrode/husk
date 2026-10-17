using System.Diagnostics;
using Extrode.Husk.Compiler;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Extrode.Husk.Build;

/// <summary>Compiles Svelte sources. <see cref="Sources"/> are Pare's <c>PareCompileSource</c> items
/// for compiler <c>husk</c>, one per output (<c>.svelte.js</c>, <c>.svelte.css</c>,
/// <c>.svelte.js.map</c> for a component; <c>.js</c> and <c>.js.map</c> for a module), each with the
/// <c>PareOutputPath</c> Pare computed. A source that fails to compile logs HUSK100 and its outputs
/// are left as they were; every compiler warning is HUSK200. HUSK101 is an output item missing or
/// naming the source itself; HUSK102 a source that cannot be read or an output that cannot be
/// written.</summary>
public sealed class HuskCompile : Microsoft.Build.Utilities.Task
{
    [Required] public ITaskItem[] Sources { get; set; } = [];

    [Required] public string ProjectDirectory { get; set; } = "";

    /// <summary>The vendored <c>svelte/compiler/index.js</c>.</summary>
    [Required] public string CompilerPath { get; set; } = "";

    public bool Dev { get; set; }

    // Engines stay warm across builds in a reused MSBuild node (not in a TaskHostFactory host, which
    // exits after the task), and are released when idle. A compiler file rewritten in place gets a
    // fresh pool: the old one holds the old source.
    private static readonly Dictionary<string, (DateTime Stamp, SvelteCompiler Compiler)> Compilers = new(StringComparer.Ordinal);

    internal static SvelteCompiler CompilerFor(string compilerPath)
    {
        var stamp = File.GetLastWriteTimeUtc(compilerPath);
        lock (Compilers)
        {
            if (Compilers.TryGetValue(compilerPath, out var cached) && cached.Stamp == stamp)
                return cached.Compiler;
            cached.Compiler?.Dispose();
            var compiler = new SvelteCompiler(compilerPath);
            Compilers[compilerPath] = (stamp, compiler);
            return compiler;
        }
    }

    public override bool Execute()
    {
        var sw = Stopwatch.StartNew();
        var sources = Sources
            .GroupBy(s => s.GetMetadata("FullPath"), StringComparer.Ordinal)
            .Select(g => (Path: g.Key, Outputs: g.ToDictionary(o => o.GetMetadata("PareOutputExtension"), o => o.GetMetadata("PareOutputPath"), StringComparer.Ordinal)))
            .ToList();
        var readable = new List<(string Path, Dictionary<string, string> Outputs)>();
        var inputs = new List<CompileInput>();
        foreach (var source in sources)
        {
            try
            {
                inputs.Add(new CompileInput(SvelteCompiler.Filename(ProjectDirectory, source.Path), File.ReadAllText(source.Path)));
                readable.Add(source);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Log.LogError(null, "HUSK102", null, source.Path, 0, 0, 0, 0, "cannot read the source: " + e.Message);
            }
        }
        var results = CompilerFor(CompilerPath).CompileAll(inputs, Dev);

        int compiled = 0;
        for (int i = 0; i < readable.Count; i++)
        {
            var (path, outputs) = readable[i];
            var result = results[i];
            foreach (var warning in result.Warnings)
                Log.LogWarning(null, "HUSK200", null, path, warning.Line, warning.Column + 1, 0, 0, Message(warning));
            if (!result.Succeeded)
            {
                Log.LogError(null, "HUSK100", null, path, result.Error!.Line, result.Error.Column + 1, 0, 0, Message(result.Error));
                continue;
            }
            if (!Write(path, outputs, result))
                continue;
            compiled++;
        }
        Log.LogMessage(MessageImportance.High, $"husk: compiled {compiled} of {sources.Count} sources in {sw.ElapsedMilliseconds} ms");
        return !Log.HasLoggedErrors;
    }

    private bool Write(string path, Dictionary<string, string> outputs, CompileResult result)
    {
        bool component = path.EndsWith(".svelte", StringComparison.Ordinal);
        string jsExtension = component ? ".svelte.js" : ".js";
        if (!outputs.TryGetValue(jsExtension, out var js) || !outputs.TryGetValue(jsExtension + ".map", out var map)
            || (component && !outputs.ContainsKey(".svelte.css")))
        {
            Log.LogError(null, "HUSK101", null, path, 0, 0, 0, 0,
                $"needs outputs {jsExtension}, {jsExtension}.map{(component ? " and .svelte.css" : "")}; got {string.Join(", ", outputs.Keys)}.");
            return false;
        }
        // Outside a web project Pare places output next to its source, and x.svelte.js compiles to x.svelte.js.
        if (outputs.Values.FirstOrDefault(o => string.Equals(Path.GetFullPath(o), path, StringComparison.OrdinalIgnoreCase)) is { } self)
        {
            Log.LogError(null, "HUSK101", null, path, 0, 0, 0, 0,
                $"output {self} is the source itself. Give the item PareOutDir, or move the source below wwwroot.");
            return false;
        }
        // A failed write leaves that output older than the source, so the next build compiles it again.
        try
        {
            foreach (var output in outputs.Values)
                Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            File.WriteAllText(js, WithMapUrl(result.Js!, Path.GetFileName(map)));
            File.WriteAllText(map, result.Map!);
            if (component)
                File.WriteAllText(outputs[".svelte.css"], result.Css!);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.LogError(null, "HUSK102", null, path, 0, 0, 0, 0, "cannot write an output: " + e.Message);
            return false;
        }
        return true;
    }

    /// <summary>The compiled JS with a <c>sourceMappingURL</c> comment naming the map beside it.</summary>
    public static string WithMapUrl(string js, string mapFileName) =>
        js + (js.EndsWith('\n') ? "" : "\n") + "//# sourceMappingURL=" + mapFileName + "\n";

    internal static string Message(CompileDiagnostic d) => $"{d.Code}: {d.Message.ReplaceLineEndings(" ")}";
}
