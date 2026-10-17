// Spike: the Svelte 5 compiler bundle in ClearScript V8. Same output format as ../js/reference.mjs.
// Usage: dotnet run -c Release -- <compiler/index.js> <list.txt> <out.tsv> [passes]
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;

var (compilerPath, list, outPath) = (args[0], args[1], args[2]);
var passes = args.Length > 3 ? int.Parse(args[3]) : 3;
var files = File.ReadAllLines(list).Where(l => l.Length > 0).ToArray();
var sources = files.Select(File.ReadAllText).ToArray();

var sw = Stopwatch.StartNew();
using var engine = new V8ScriptEngine();
Console.WriteLine($"engine {sw.Elapsed.TotalMilliseconds:F1} ms");

sw.Restart();
engine.Execute(new DocumentInfo("svelte-compiler.js"), File.ReadAllText(compilerPath));
engine.Execute("""
    globalThis.__compile = (source, filename, rootDir) => {
      try {
        return "ok\n" + svelte.compile(source, { filename, rootDir, generate: "client" }).js.code;
      } catch (e) {
        return "err\n" + (e.code ?? String(e.message));
      }
    };
    """);
var compile = (ScriptObject)engine.Script.__compile;
Console.WriteLine($"load {sw.Elapsed.TotalMilliseconds:F1} ms");

string CompileOne(int i) => (string)compile.Invoke(false, sources[i], files[i], Environment.CurrentDirectory);

var lines = new List<string>(files.Length);
sw.Restart();
for (var i = 0; i < files.Length; i++)
{
    var result = CompileOne(i);
    var nl = result.IndexOf('\n');
    var kind = result[..nl];
    var value = result[(nl + 1)..];
    if (Environment.GetEnvironmentVariable("SPIKE_DUMP") is { } dump && files[i].EndsWith(dump)) File.WriteAllText("v8-dump.js", value);
    lines.Add($"{files[i]}\t{kind}\t{(kind == "ok" ? Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value))) : value)}");
}
Console.WriteLine($"first pass {sw.Elapsed.TotalMilliseconds:F0} ms");
File.WriteAllText(outPath, string.Join("\n", lines) + "\n");

var times = new List<double>();
for (var p = 0; p < passes; p++)
{
    sw.Restart();
    for (var i = 0; i < files.Length; i++) CompileOne(i);
    times.Add(sw.Elapsed.TotalMilliseconds);
}
times.Sort();
Console.WriteLine($"warm passes best {times[0]:F0} ms, median {times[times.Count / 2]:F0} ms");
Console.WriteLine($"ok {lines.Count(l => l.Contains("\tok\t"))}, err {lines.Count(l => l.Contains("\terr\t"))}");
