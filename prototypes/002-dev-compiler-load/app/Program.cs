using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Loader;

// Usage: LoadApp <compiler dir> <svelte compiler/index.js> <watch dir>
// Loads DevCompiler.dll into its own load context, compiles every .svelte in the watch dir on
// start and on each save, and logs the time per compile.
var compilerDir = Path.GetFullPath(args[0]);
var sveltePath = Path.GetFullPath(args[1]);
var watchDir = Path.GetFullPath(args[2]);

#if WITH_CLEARSCRIPT
using (var own = new Microsoft.ClearScript.V8.V8ScriptEngine())
    Console.Error.WriteLine($"app: own ClearScript says {own.Evaluate("1 + 1")}, from {typeof(Microsoft.ClearScript.V8.V8ScriptEngine).Assembly.Location}");
#endif

var compile = Loader.Load(Path.Combine(compilerDir, "DevCompiler.dll"));

void CompileFile(string path)
{
    var sw = Stopwatch.StartNew();
    var result = compile(sveltePath, File.ReadAllText(path), Path.GetFileName(path));
    var ok = result.StartsWith("ok\n", StringComparison.Ordinal);
    if (ok)
        File.WriteAllText(path + ".js", result[3..]);
    Console.Error.WriteLine($"app: {(ok ? "compiled" : "failed")} {Path.GetFileName(path)} in {sw.Elapsed.TotalMilliseconds:F1} ms{(ok ? "" : ": " + result[4..])}");
}

foreach (var f in Directory.GetFiles(watchDir, "*.svelte"))
    CompileFile(f);

using var watcher = new FileSystemWatcher(watchDir, "*.svelte") { EnableRaisingEvents = true };
watcher.Changed += (_, e) => { Thread.Sleep(20); CompileFile(e.FullPath); };

var app = WebApplication.CreateBuilder(args.Skip(3).ToArray()).Build();
app.MapGet("/", () => $"default context has ClearScript: {AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "ClearScript.V8" && AssemblyLoadContext.GetLoadContext(a) == AssemblyLoadContext.Default)}");
app.Run();

static class Loader
{
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Development only, never in a published app.")]
    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Development only, never in a published app.")]
    public static Func<string, string, string, string> Load(string assemblyPath)
    {
        var sw = Stopwatch.StartNew();
        var context = new CompilerContext(assemblyPath);
        var assembly = context.LoadFromAssemblyPath(assemblyPath);
        var method = assembly.GetType("DevCompiler.Compiler")!.GetMethod("Compile")!;
        Console.Error.WriteLine($"app: loaded {assembly.GetName().Name} into '{context.Name}' in {sw.ElapsedMilliseconds} ms");
        return method.CreateDelegate<Func<string, string, string, string>>();
    }
}

[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Development only, never in a published app.")]
sealed class CompilerContext(string assemblyPath) : AssemblyLoadContext("husk-compiler")
{
    readonly AssemblyDependencyResolver _resolver = new(assemblyPath);

    // V8 aborts the process when two ClearScript copies each load their native library, so an
    // app that already has ClearScript shares its copy (null defers to the default context).
    protected override Assembly? Load(AssemblyName name)
    {
        if (name.Name!.StartsWith("ClearScript", StringComparison.Ordinal) && SharesClearScript)
            return null;
        return _resolver.ResolveAssemblyToPath(name) is { } path ? LoadFromAssemblyPath(path) : null;
    }

    static bool SharesClearScript =>
        Default.Assemblies.Any(a => a.GetName().Name == "ClearScript.V8")
        || File.Exists(Path.Combine(AppContext.BaseDirectory, "ClearScript.V8.dll"));

    protected override IntPtr LoadUnmanagedDll(string name) =>
        _resolver.ResolveUnmanagedDllToPath(name) is { } path ? LoadUnmanagedDllFromPath(path) : IntPtr.Zero;
}
