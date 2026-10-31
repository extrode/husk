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

    // AppDomain data is process-wide: MSBuild loads each task folder into its own load context, and
    // every Husk version that finds the same native file shares one loaded copy, which works. A
    // second file (another ClearScript version, a Husk outside the NuGet cache) is a second V8 in
    // the process, which spins and dies on a V8 check (2026-10-31).
    internal const string LoadedKey = "Extrode.Husk.V8Native";

    /// <summary>The native file loaded in this process by any Husk, or null.</summary>
    internal static string? Loaded => AppDomain.CurrentDomain.GetData(LoadedKey) as string;

    /// <summary>The native file this copy of Husk would load, in the order <see cref="Resolve"/>
    /// tries; null when none exists or the process has no V8 build.</summary>
    internal static string? Wanted()
    {
        var assembly = typeof(V8ScriptEngine).Assembly;
        string? os = OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : OperatingSystem.IsLinux() ? "linux" : null;
        string? arch = RuntimeInformation.ProcessArchitecture switch { Architecture.X64 => "x64", Architecture.Arm64 => "arm64", _ => null };
        if (os is null || arch is null || Path.GetDirectoryName(assembly.Location) is not { Length: > 0 } dir)
            return null;
        string name = $"ClearScriptV8.{os}-{arch}.{(os == "win" ? "dll" : os == "osx" ? "dylib" : "so")}";
        return Candidates(dir, name, assembly.GetName().Version!).FirstOrDefault(File.Exists) is { } path ? Path.GetFullPath(path) : null;
    }

    /// <summary>True when loading <paramref name="wanted"/> would put a second native beside <paramref name="loaded"/>.</summary>
    internal static bool Conflicts(string? loaded, string? wanted) =>
        loaded is not null && wanted is not null
        && !string.Equals(loaded, wanted, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static IEnumerable<string> Candidates(string dir, string libraryName, Version version) =>
        new[] { Candidate(dir, libraryName), PackageCandidate(dir, libraryName, version) }.OfType<string>();

    private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (Path.GetDirectoryName(assembly.Location) is not { Length: > 0 } dir)
            return IntPtr.Zero;
        foreach (var path in Candidates(dir, libraryName, assembly.GetName().Version!))
            if (File.Exists(path) && NativeLibrary.TryLoad(path, out var handle))
            {
                if (Loaded is null)
                    AppDomain.CurrentDomain.SetData(LoadedKey, Path.GetFullPath(path));
                return handle;
            }
        return IntPtr.Zero;
    }
}
