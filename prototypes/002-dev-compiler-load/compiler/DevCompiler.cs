using System.Diagnostics;
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;

namespace DevCompiler;

// One warm engine; Compile returns "ok\n<js>" or "err\n<message>", plus timings on stderr.
public static class Compiler
{
    static V8ScriptEngine? s_engine;
    static ScriptObject? s_compile;
    static readonly object s_lock = new();

    public static string Compile(string compilerPath, string source, string filename)
    {
        lock (s_lock)
        {
            if (s_compile is null)
            {
                var sw = Stopwatch.StartNew();
                s_engine = new V8ScriptEngine();
                s_engine.Execute(new DocumentInfo("svelte-compiler.js"), File.ReadAllText(compilerPath));
                s_engine.Execute("""
                    globalThis.__compile = (source, filename) => {
                      try { return "ok\n" + svelte.compile(source, { filename, generate: "client" }).js.code; }
                      catch (e) { return "err\n" + (e.code ?? "") + " " + e.message; }
                    };
                    """);
                s_compile = (ScriptObject)s_engine.Script.__compile;
                Console.Error.WriteLine($"devcompiler: engine start {sw.ElapsedMilliseconds} ms, ClearScript from {typeof(V8ScriptEngine).Assembly.Location}");
            }
            return (string)s_compile.Invoke(false, source, filename);
        }
    }
}
