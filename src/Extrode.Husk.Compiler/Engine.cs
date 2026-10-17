using System.Diagnostics;
using System.Text.Json;
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;

namespace Extrode.Husk.Compiler;

/// <summary>One V8 engine with the Svelte compiler and Husk's glue loaded. Not thread-safe: the pool
/// hands each to one caller at a time.</summary>
internal sealed class Engine : IDisposable
{
    private readonly V8ScriptEngine _engine;
    private readonly ScriptObject _compile;
    private readonly object _svelte;

    public int Compiles { get; private set; }

    public TimeSpan StartTime { get; }

    public Engine(string compilerSource, string glueSource)
    {
        var sw = Stopwatch.StartNew();
        _engine = new V8ScriptEngine();
        try
        {
            _engine.Execute(new DocumentInfo("svelte/compiler/index.js"), compilerSource);
            _engine.Execute(new DocumentInfo("husk-compile.js"), glueSource);
            _svelte = _engine.Script.svelte;
            _compile = (ScriptObject)_engine.Script.huskCompile;
        }
        catch
        {
            _engine.Dispose();
            throw;
        }
        StartTime = sw.Elapsed;
    }

    public CompileResult Compile(bool module, string source, string filename, bool dev, string? sourcesContent)
    {
        Compiles++;
        var json = (string)_compile.Invoke(false, _svelte, module ? "module" : "component", source, filename, dev, sourcesContent!);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var warnings = root.GetProperty("warnings").EnumerateArray().Select(Diagnostic).ToList();
        var error = root.GetProperty("error");
        if (error.ValueKind != JsonValueKind.Null)
            return new CompileResult(null, null, null, warnings, Diagnostic(error));
        return new CompileResult(root.GetProperty("js").GetString(), root.GetProperty("css").GetString(), root.GetProperty("map").GetString(), warnings, null);
    }

    private static CompileDiagnostic Diagnostic(JsonElement d) =>
        new(d.GetProperty("code").GetString()!, d.GetProperty("message").GetString()!, d.GetProperty("line").GetInt32(), d.GetProperty("column").GetInt32());

    public void Dispose() => _engine.Dispose();
}
