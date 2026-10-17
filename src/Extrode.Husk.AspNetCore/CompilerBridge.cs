using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Logging;

namespace Extrode.Husk.AspNetCore;

/// <summary>Extrode.Husk.Compiler, loaded from the build package's folder into its own load context
/// (docs/architecture/003), and its string-only entry points.</summary>
internal sealed class CompilerBridge
{
    public required Func<string, string, string, string, bool, string> Compile { get; init; }

    /// <summary>The bridge, or null after logging why saves cannot be compiled in this app.</summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Development only, never in a published app.")]
    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Development only, never in a published app.")]
    public static CompilerBridge? Load(string compilerAssembly, ILogger logger)
    {
        if (!File.Exists(compilerAssembly))
        {
            logger.LogError("Husk's compiler is not at {Path}; build the project again to compile on save", compilerAssembly);
            return null;
        }
        if (ClearScriptMismatch(compilerAssembly) is { } mismatch)
        {
            logger.LogWarning("The app has ClearScript.V8 {App} and Husk's compiler needs {Husk}; two copies in one process stop V8, " +
                "so saves are not compiled while the app runs. Build to compile.", mismatch.App, mismatch.Husk);
            return null;
        }
        var context = new CompilerContext(compilerAssembly);
        var type = context.LoadFromAssemblyPath(compilerAssembly).GetType("Extrode.Husk.Compiler.Development", throwOnError: true)!;
        return new CompilerBridge
        {
            Compile = type.GetMethod("Compile")!.CreateDelegate<Func<string, string, string, string, bool, string>>(),
        };
    }

    // V8 aborts the process when two ClearScript copies each load their native library, so an app
    // that has ClearScript shares its copy, which must then be the version Husk's compiler built on.
    private static (Version App, Version Husk)? ClearScriptMismatch(string compilerAssembly)
    {
        if (CompilerContext.AppClearScript() is not { } app)
            return null;
        string husk = Path.Combine(Path.GetDirectoryName(compilerAssembly)!, "ClearScript.V8.dll");
        if (!File.Exists(husk))
            return null;
        var huskVersion = AssemblyName.GetAssemblyName(husk).Version!;
        return app == huskVersion ? null : (app, huskVersion);
    }
}

[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Development only, never in a published app.")]
internal sealed class CompilerContext(string assemblyPath) : AssemblyLoadContext("husk-compiler")
{
    private readonly AssemblyDependencyResolver _resolver = new(assemblyPath);
    private readonly bool _shareClearScript = AppClearScript() is not null;

    /// <summary>The app's own ClearScript.V8 version, loaded or in its folder, or null.</summary>
    public static Version? AppClearScript()
    {
        if (Default.Assemblies.FirstOrDefault(a => a.GetName().Name == "ClearScript.V8") is { } loaded)
            return loaded.GetName().Version;
        string local = Path.Combine(AppContext.BaseDirectory, "ClearScript.V8.dll");
        return File.Exists(local) ? AssemblyName.GetAssemblyName(local).Version : null;
    }

    // Null defers to the default context: the app's ClearScript when it has one.
    protected override Assembly? Load(AssemblyName name)
    {
        if (_shareClearScript && name.Name!.StartsWith("ClearScript", StringComparison.Ordinal))
            return null;
        return _resolver.ResolveAssemblyToPath(name) is { } path ? LoadFromAssemblyPath(path) : null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName) =>
        _resolver.ResolveUnmanagedDllToPath(unmanagedDllName) is { } path ? LoadUnmanagedDllFromPath(path) : IntPtr.Zero;
}
