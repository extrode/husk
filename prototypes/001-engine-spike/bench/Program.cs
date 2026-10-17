// Engine comparison for the Svelte compiler: ClearScript V8 (plain, code cache, parallel) and Jint.
// Usage: dotnet run -c Release -- <compiler/index.js> <list.txt> <node.tsv> <mode> [passes] [limit]
//   mode: v8 | v8-cache | v8-parallel | v8-noop | jint
// Prints timings and how many outputs differ from the Node reference.
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Jint;
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;

var (compilerPath, list, nodeTsv, mode) = (args[0], args[1], args[2], args[3]);
var passes = args.Length > 4 ? int.Parse(args[4]) : 3;
var limit = args.Length > 5 ? int.Parse(args[5]) : int.MaxValue;
var files = File.ReadAllLines(list).Where(l => l.Length > 0).Take(limit).ToArray();
var sources = files.Select(File.ReadAllText).ToArray();
var expected = File.ReadAllLines(nodeTsv).Select(l => l.Split('\t')).ToDictionary(p => p[0], p => p[1] + "\t" + p[2]);
var compilerCode = File.ReadAllText(compilerPath);
var root = Environment.CurrentDirectory;
const string Shim = """
    globalThis.__compile = (source, filename, rootDir) => {
      try {
        return "ok\n" + svelte.compile(source, { filename, rootDir, generate: "client" }).js.code;
      } catch (e) {
        return "err\n" + (e.code ?? String(e.message));
      }
    };
    globalThis.__noop = (source, filename, rootDir) => "ok\n" + source.length;
    """;

string Key(string result)
{
    var nl = result.IndexOf('\n');
    var kind = result[..nl];
    var value = result[(nl + 1)..];
    return kind + "\t" + (kind == "ok" ? Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value))) : value);
}

void Report(string[] results, double firstPass, List<double> warm)
{
    warm.Sort();
    var differ = mode == "v8-noop" ? 0 : Enumerable.Range(0, files.Length).Count(i => Key(results[i]) != expected[files[i]]);
    var warmText = warm.Count > 0 ? $"warm best {warm[0]:F0} ms, median {warm[warm.Count / 2]:F0} ms, " : "";
    Console.WriteLine($"{mode}: {files.Length} files, first pass {firstPass:F0} ms, {warmText}{differ} differ from Node");
}

var sw = Stopwatch.StartNew();
switch (mode)
{
    case "v8" or "v8-noop" or "v8-cache":
    {
        byte[]? cache = null;
        if (mode == "v8-cache")
        {
            // Produce the code cache in a throwaway engine, as a previous build would have.
            using var producer = new V8ScriptEngine();
            producer.Compile(new DocumentInfo("svelte-compiler.js"), compilerCode, V8CacheKind.Code, out cache);
            Console.WriteLine($"code cache {cache.Length / 1024} KB");
        }
        sw.Restart();
        using var engine = new V8ScriptEngine();
        var engineMs = sw.Elapsed.TotalMilliseconds;
        sw.Restart();
        if (cache is null)
            engine.Execute(new DocumentInfo("svelte-compiler.js"), compilerCode);
        else
        {
            var script = engine.Compile(new DocumentInfo("svelte-compiler.js"), compilerCode, V8CacheKind.Code, cache, out var accepted);
            engine.Evaluate(script);
            Console.WriteLine($"cache accepted: {accepted}");
        }
        engine.Execute(Shim);
        var fn = (ScriptObject)(mode == "v8-noop" ? engine.Script.__noop : engine.Script.__compile);
        Console.WriteLine($"engine {engineMs:F1} ms, load {sw.Elapsed.TotalMilliseconds:F1} ms");
        var results = new string[files.Length];
        sw.Restart();
        for (var i = 0; i < files.Length; i++) results[i] = (string)fn.Invoke(false, sources[i], files[i], root);
        var first = sw.Elapsed.TotalMilliseconds;
        var warm = new List<double>();
        for (var p = 0; p < passes; p++)
        {
            sw.Restart();
            for (var i = 0; i < files.Length; i++) fn.Invoke(false, sources[i], files[i], root);
            warm.Add(sw.Elapsed.TotalMilliseconds);
        }
        Report(results, first, warm);
        break;
    }
    case "v8-parallel":
    {
        // One engine per worker; each worker takes the next file from a shared counter.
        var workers = Environment.ProcessorCount;
        var results = new string[files.Length];
        double Run(bool keep)
        {
            var next = -1;
            var t = Stopwatch.StartNew();
            var threads = Enumerable.Range(0, workers).Select(_ => new Thread(() =>
            {
                using var engine = new V8ScriptEngine();
                engine.Execute(new DocumentInfo("svelte-compiler.js"), compilerCode);
                engine.Execute(Shim);
                var fn = (ScriptObject)engine.Script.__compile;
                int i;
                while ((i = Interlocked.Increment(ref next)) < files.Length)
                {
                    var r = (string)fn.Invoke(false, sources[i], files[i], root);
                    if (keep) results[i] = r;
                }
            })).ToList();
            threads.ForEach(th => th.Start());
            threads.ForEach(th => th.Join());
            return t.Elapsed.TotalMilliseconds;
        }
        Console.WriteLine($"{workers} workers, each starting its own engine (times include engine start and compiler load)");
        var first = Run(true);
        var warm = new List<double>();
        for (var p = 0; p < passes; p++) warm.Add(Run(false));
        Report(results, first, warm);
        break;
    }
    case "jint":
    {
        var prepared = Engine.PrepareScript(compilerCode);
        sw.Restart();
        var engine = new Engine();
        var engineMs = sw.Elapsed.TotalMilliseconds;
        sw.Restart();
        engine.Execute(prepared);
        engine.Execute(Shim);
        var fn = engine.GetValue("__compile");
        Console.WriteLine($"engine {engineMs:F1} ms, load {sw.Elapsed.TotalMilliseconds:F1} ms (parse done beforehand by PrepareScript)");
        var results = new string[files.Length];
        sw.Restart();
        for (var i = 0; i < files.Length; i++) results[i] = engine.Invoke(fn, sources[i], files[i], root).AsString();
        var first = sw.Elapsed.TotalMilliseconds;
        var warm = new List<double>();
        for (var p = 0; p < passes; p++)
        {
            sw.Restart();
            for (var i = 0; i < files.Length; i++) engine.Invoke(fn, sources[i], files[i], root);
            warm.Add(sw.Elapsed.TotalMilliseconds);
        }
        Report(results, first, warm);
        break;
    }
    default:
        throw new ArgumentException("mode: v8 | v8-cache | v8-parallel | v8-noop | jint");
}
