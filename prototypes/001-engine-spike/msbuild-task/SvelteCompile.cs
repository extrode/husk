using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;

namespace SvelteSpike;

// Compiles each .svelte input to <OutputDirectory>/<name>.js with the Svelte compiler in V8.
public sealed class SvelteCompile : Microsoft.Build.Utilities.Task
{
    [Required] public ITaskItem[] Sources { get; set; } = [];
    [Required] public string CompilerPath { get; set; } = "";
    [Required] public string OutputDirectory { get; set; } = "";
    [Required] public string RootDirectory { get; set; } = "";

    public override bool Execute()
    {
        var sw = Stopwatch.StartNew();
        using var engine = new V8ScriptEngine();
        engine.Execute(new DocumentInfo("svelte-compiler.js"), File.ReadAllText(CompilerPath));
        engine.Execute("""
            globalThis.__compile = (source, filename, rootDir) => {
              try {
                return "ok\n" + svelte.compile(source, { filename, rootDir, generate: "client" }).js.code;
              } catch (e) {
                return "err\n" + (e.position ? e.position.join(":") + " " : "") + (e.code ?? "") + " " + e.message;
              }
            };
            """);
        var compile = (ScriptObject)engine.Script.__compile;
        Log.LogMessage(MessageImportance.High, $"svelte: engine + compiler {sw.ElapsedMilliseconds} ms (process {Environment.ProcessPath})");
        Directory.CreateDirectory(OutputDirectory);
        foreach (var item in Sources)
        {
            var path = item.GetMetadata("FullPath");
            var result = (string)compile.Invoke(false, File.ReadAllText(path), path, RootDirectory);
            var nl = result.IndexOf('\n');
            if (result[..nl] == "err")
            {
                Log.LogError($"{path}: {result[(nl + 1)..]}");
                continue;
            }
            File.WriteAllText(Path.Combine(OutputDirectory, Path.GetFileNameWithoutExtension(path) + ".js"), result[(nl + 1)..]);
        }
        Log.LogMessage(MessageImportance.High, $"svelte: {Sources.Length} components in {sw.ElapsedMilliseconds} ms");
        return !Log.HasLoggedErrors;
    }
}
