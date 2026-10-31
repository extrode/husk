using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.ClearScript.V8;

namespace Extrode.Husk.Compiler;

/// <summary>Finds ClearScript's V8 library: under runtimes/&lt;rid&gt;/native beside ClearScript.V8.dll
/// (a test output, a local build), then in the Microsoft.ClearScript.V8.Native.&lt;rid&gt; package that
/// restored beside Extrode.Husk.Build in the NuGet cache. The Husk packages carry no natives. A
/// host that loads this assembly without its deps.json (an MSBuild task folder) otherwise probes
/// only flat folders. The rid comes from the library's own name, ClearScriptV8.&lt;rid&gt;.&lt;ext&gt;,
/// so no rid is guessed.</summary>
internal static class NativeProbe
{
    private static readonly Lock Gate = new();
    private static bool _registered;

    // Under the lock: parallel engine starts must not pass here before the resolver is set.
    public static void Register()
    {
        lock (Gate)
        {
            if (_registered)
                return;
            try
            {
                NativeLibrary.SetDllImportResolver(typeof(V8ScriptEngine).Assembly, Resolve);
            }
            catch (InvalidOperationException)
            {
                // The app set its own resolver for ClearScript; it knows where its native is.
            }
            _registered = true;
        }
    }

    internal static string? Candidate(string assemblyDir, string libraryName)
    {
        var parts = libraryName.Split('.');
        if (parts.Length != 3 || parts[0] != "ClearScriptV8")
            return null;
        return Path.Combine(assemblyDir, "runtimes", parts[1], "native", libraryName);
    }

    // <cache>/extrode.husk.build/<version>/tasks/net10.0 is the assembly's folder; the native package sits
    // in <cache> too. Null when the assembly is not in such a layout.
    // NuGet drops a zero fourth part from a folder name: assembly 7.5.2.0 lives in 7.5.2.
    internal static string PackageFolder(Version v) => v.Revision == 0 ? v.ToString(3) : v.ToString();

    internal static string? PackageCandidate(string assemblyDir, string libraryName, Version clearScriptVersion)
    {
        var parts = libraryName.Split('.');
        if (parts.Length != 3 || parts[0] != "ClearScriptV8")
            return null;
        var tasks = Path.GetDirectoryName(assemblyDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (tasks is null || Path.GetFileName(tasks) != "tasks" || Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(tasks))) is not { } cache)
            return null;
        return Path.Combine(cache, "microsoft.clearscript.v8.native." + parts[1], PackageFolder(clearScriptVersion), "runtimes", parts[1], "native", libraryName);
    }

    private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (Path.GetDirectoryName(assembly.Location) is not { Length: > 0 } dir)
            return IntPtr.Zero;
        foreach (var path in new[] { Candidate(dir, libraryName), PackageCandidate(dir, libraryName, assembly.GetName().Version!) })
            if (path is not null && File.Exists(path) && NativeLibrary.TryLoad(path, out var handle))
                return handle;
        return IntPtr.Zero;
    }
}
