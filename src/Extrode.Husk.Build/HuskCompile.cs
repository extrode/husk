using System.Diagnostics;
using Extrode.Husk.Compiler;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Extrode.Husk.Build;

/// <summary>Compiles Svelte sources. <see cref="Sources"/> are Pare's <c>PareCompileSource</c> items
/// for compiler <c>husk</c>, one per output (<c>.svelte.js</c>, <c>.svelte.css</c>,
/// <c>.svelte.js.map</c> for a component; <c>.js</c> and <c>.js.map</c> for a module), each with the
/// <c>PareOutputPath</c> Pare computed. A source that fails to compile logs HUSK100 and its outputs
/// are left as they were; every compiler warning is HUSK200, and a source whose HUSK200 the build
/// treats as an error is left as they were too. HUSK101 is an output item missing or
/// naming the source itself; HUSK102 a source that cannot be read or an output that cannot be
/// written.</summary>
public sealed class HuskCompile : Microsoft.Build.Utilities.Task
{
    [Required] public ITaskItem[] Sources { get; set; } = [];

    [Required] public string ProjectDirectory { get; set; } = "";

    /// <summary>The vendored <c>svelte/compiler/index.js</c>.</summary>
    [Required] public string CompilerPath { get; set; } = "";

    public bool Dev { get; set; }

    /// <summary>When set, the build is incremental: a source is compiled only when an output is
    /// missing or older than the source or one of these (the task, the compiler, the targets, the
    /// HuskDev stamp). Decided per source over all its outputs, not by MSBuild per item, which would
    /// hand the task some of a source's outputs and not the others.</summary>
    public ITaskItem[] Inputs { get; set; } = [];

    // Not under TaskHostFactory (Visual Studio's MSBuild), whose host exits after the task.
    internal static SvelteCompiler CompilerFor(string compilerPath) => SvelteCompiler.Shared(compilerPath);

    // A batch that throws (an engine that cannot start, a host error mid-compile) is compiled again
    // one source at a time, so the source behind it is named and the others still compile. Null is
    // a source that failed this way, already logged.
    private IReadOnlyList<CompileResult?> Compile(List<CompileInput> inputs)
    {
        try
        {
            return CompilerFor(CompilerPath).CompileAll(inputs, Dev);
        }
        catch (Exception)
        {
            return [.. inputs.Select(CompileOne)];
        }
    }

    private CompileResult? CompileOne(CompileInput input)
    {
        try
        {
            return CompilerFor(CompilerPath).Compile(input, Dev);
        }
        catch (Exception e)
        {
            Log.LogError(null, "HUSK100", null, Path.GetFullPath(Path.Combine(ProjectDirectory, input.Filename)), 0, 0, 0, 0, "the compiler failed: " + e.Message);
            return null;
        }
    }

    public override bool Execute()
    {
        var sw = Stopwatch.StartNew();
        var sources = new List<(string Path, Dictionary<string, string> Outputs)>();
        foreach (var g in Sources.GroupBy(s => s.GetMetadata("FullPath"), StringComparer.Ordinal))
        {
            if (g.GroupBy(o => o.GetMetadata("PareOutputExtension"), StringComparer.Ordinal).FirstOrDefault(e => e.Count() > 1) is { } twice)
            {
                Log.LogError(null, "HUSK101", null, g.Key, 0, 0, 0, 0,
                    $"is listed twice, with outputs {string.Join(" and ", twice.Select(o => o.GetMetadata("PareOutputPath")))}. Remove one item, e.g. <HuskComponent Remove=\"...\" /> before the Include that sets its metadata.");
                continue;
            }
            sources.Add((g.Key, g.ToDictionary(o => o.GetMetadata("PareOutputExtension"), o => o.GetMetadata("PareOutputPath"), StringComparer.Ordinal)));
        }
        var newestInput = Inputs.Length == 0 ? (DateTime?)null : Inputs.Select(i => File.GetLastWriteTimeUtc(i.ItemSpec)).Max();
        var readable = new List<(string Path, Dictionary<string, string> Outputs)>();
        var inputs = new List<CompileInput>();
        foreach (var source in sources)
        {
            if (newestInput is { } newest && UpToDate(source.Path, source.Outputs.Values, newest))
                continue;
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
        var results = inputs.Count == 0 ? [] : Compile(inputs);

        int compiled = 0;
        for (int i = 0; i < readable.Count; i++)
        {
            var (path, outputs) = readable[i];
            var result = results[i];
            if (result is null)
                continue;
            foreach (var warning in result.Warnings)
                Log.LogWarning(null, "HUSK200", null, path, warning.Line, warning.Column + 1, 0, 0, Message(warning));
            if (!result.Succeeded)
            {
                // Line 0: Svelte gave no position (an internal error), so none is printed.
                var error = result.Error!;
                Log.LogError(null, "HUSK100", null, path, error.Line, error.Line == 0 ? 0 : error.Column + 1, 0, 0, Message(error));
                continue;
            }
            // A warning the build treats as an error fails it like HUSK100: outputs written now would
            // be up to date, and the next build would skip the source and pass.
            if (result.Warnings.Count > 0 && BuildEngine is IBuildEngine8 engine && engine.ShouldTreatWarningAsError("HUSK200"))
                continue;
            if (!Write(path, outputs, result))
                continue;
            compiled++;
        }
        Log.LogMessage(MessageImportance.High, $"husk: compiled {compiled} of {sources.Count} sources in {sw.ElapsedMilliseconds} ms");
        return !Log.HasLoggedErrors;
    }

    // File.GetLastWriteTimeUtc gives 1601 for a missing file, older than any input.
    private static bool UpToDate(string source, IEnumerable<string> outputs, DateTime newestInput)
    {
        var newest = File.GetLastWriteTimeUtc(source) is var s && s > newestInput ? s : newestInput;
        return outputs.All(o => File.GetLastWriteTimeUtc(o) >= newest);
    }

    private bool Write(string path, Dictionary<string, string> outputs, CompileResult result)
    {
        var extensions = CompiledFiles.Extensions(path);
        if (!extensions.All(outputs.ContainsKey))
        {
            Log.LogError(null, "HUSK101", null, path, 0, 0, 0, 0,
                $"needs outputs {string.Join(", ", extensions)}; got {string.Join(", ", outputs.Keys)}.");
            return false;
        }
        // Outside a web project Pare places output next to its source, and x.svelte.js compiles to x.svelte.js.
        if (outputs.Values.FirstOrDefault(o => string.Equals(Path.GetFullPath(o), path, StringComparison.OrdinalIgnoreCase)) is { } self)
        {
            Log.LogError(null, "HUSK101", null, path, 0, 0, 0, 0,
                $"output {self} is the source itself. Set the PareOutDir property, or move the source below wwwroot.");
            return false;
        }
        // A failed write leaves that output older than the source, so the next build compiles it again.
        string? temp = null;
        try
        {
            foreach (var (extension, text) in CompiledFiles.Texts(path, result))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outputs[extension])!);
                // Beside the output, then moved over it: an interrupted build leaves the old file, not half a new one.
                temp = outputs[extension] + ".husk-tmp";
                File.WriteAllText(temp, text);
                File.Move(temp, outputs[extension], overwrite: true);
                temp = null;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            if (temp is not null)
                try { File.Delete(temp); } catch (Exception d) when (d is IOException or UnauthorizedAccessException) { }
            Log.LogError(null, "HUSK102", null, path, 0, 0, 0, 0, "cannot write an output: " + e.Message);
            return false;
        }
        return true;
    }

    internal static string Message(CompileDiagnostic d) => $"{d.Code}: {d.Message.ReplaceLineEndings(" ")}";
}
