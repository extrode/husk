using System.Diagnostics;
using Extrode.Husk.Compiler;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Extrode.Husk.Build;

/// <summary>Compiles Svelte sources. <see cref="Sources"/> are Pare's <c>PareCompileSource</c> items
/// for compiler <c>husk</c>, one per output (<c>.svelte.js</c>, <c>.svelte.css</c>,
/// <c>.svelte.js.map</c> for a component; <c>.js</c> and <c>.js.map</c> for a module), each with the
/// <c>PareOutputPath</c> Pare computed. A source that fails to compile logs HUSK100 and its outputs
/// are left as they were; every compiler warning is HUSK200.</summary>
public sealed class HuskCompile : Microsoft.Build.Utilities.Task
{
    [Required] public ITaskItem[] Sources { get; set; } = [];

    [Required] public string ProjectDirectory { get; set; } = "";

    /// <summary>The vendored <c>svelte/compiler/index.js</c>.</summary>
    [Required] public string CompilerPath { get; set; } = "";

    public bool Dev { get; set; }

    // Engines stay warm across builds in a reused MSBuild node, and are released when idle.
    private static readonly Dictionary<string, SvelteCompiler> Compilers = new(StringComparer.Ordinal);

    internal static SvelteCompiler CompilerFor(string compilerPath)
    {
        lock (Compilers)
        {
            if (!Compilers.TryGetValue(compilerPath, out var compiler))
                Compilers[compilerPath] = compiler = new SvelteCompiler(compilerPath);
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
        var inputs = sources.Select(s => new CompileInput(SvelteCompiler.Filename(ProjectDirectory, s.Path), File.ReadAllText(s.Path))).ToList();
        var results = CompilerFor(CompilerPath).CompileAll(inputs, Dev);

        int compiled = 0;
        for (int i = 0; i < sources.Count; i++)
        {
            var (path, outputs) = sources[i];
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
        foreach (var output in outputs.Values)
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        File.WriteAllText(js, WithMapUrl(result.Js!, Path.GetFileName(map)));
        File.WriteAllText(map, result.Map!);
        if (component)
            File.WriteAllText(outputs[".svelte.css"], result.Css!);
        return true;
    }

    /// <summary>The compiled JS with a <c>sourceMappingURL</c> comment naming the map beside it.</summary>
    public static string WithMapUrl(string js, string mapFileName) =>
        js + (js.EndsWith('\n') ? "" : "\n") + "//# sourceMappingURL=" + mapFileName + "\n";

    internal static string Message(CompileDiagnostic d) => $"{d.Code}: {d.Message.ReplaceLineEndings(" ")}";
}
