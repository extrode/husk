using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.ClearScript.V8;

namespace Extrode.Husk.Compiler;

/// <summary>Finds ClearScript's V8 library under runtimes/&lt;rid&gt;/native beside ClearScript.V8.dll.
/// A host that loads this assembly without its deps.json (an MSBuild task folder, a test output)
/// otherwise probes only flat folders. The rid comes from the library's own name,
/// ClearScriptV8.&lt;rid&gt;.&lt;ext&gt;, so no rid is guessed.</summary>
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

    private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        string? dir = Path.GetDirectoryName(assembly.Location);
        string? path = dir is { Length: > 0 } ? Candidate(dir, libraryName) : null;
        return path is not null && File.Exists(path) && NativeLibrary.TryLoad(path, out var handle) ? handle : IntPtr.Zero;
    }
}
