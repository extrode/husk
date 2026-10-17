using System.Diagnostics;
using System.Reflection;

namespace Extrode.Husk.Integration;

/// <summary>A throwaway project under the temp directory that imports Pare's and Husk's props and
/// targets by path, so a build tests the repo's targets without packing them.</summary>
public sealed class TempProject : IDisposable
{
    public static readonly string RepoRoot = FindRepoRoot();
    public static readonly string HuskBuildDir = Path.Combine(RepoRoot, "src", "Extrode.Husk.Build", "build");
    public static readonly string PareBuildDir = typeof(TempProject).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
        .Single(a => a.Key == "PareBuildDir").Value!;
    // By path: loading the task type would need the MSBuild assemblies, which only the host ships.
    public static readonly string TaskAssembly = Path.Combine(AppContext.BaseDirectory, "Extrode.Husk.Build.dll");

    public static readonly string VendorDir = Path.Combine(RepoRoot, "third-party", "svelte-5.57.2");
    public static readonly string Fixtures = Path.Combine(RepoRoot, "tests", "fixtures");

    // Where a web project's build writes compiled outputs and Husk's own files.
    public const string PareOut = "obj/Debug/net10.0/pare/";
    public const string HuskOut = "obj/Debug/net10.0/husk/";

    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("husk-build-");

    /// <summary>Replaces PATH for every command this project runs, when set.</summary>
    public string? PathOverride { get; set; }

    public string Dir => _dir.FullName;

    public string P(string relative) => Path.Combine(_dir.FullName, relative);

    // Windows keeps a killed app's .exe locked for a moment after the kill returns.
    public void Dispose()
    {
        var until = DateTime.UtcNow.AddSeconds(10);
        while (true)
        {
            try
            {
                _dir.Delete(recursive: true);
                return;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException && DateTime.UtcNow < until)
            {
                Thread.Sleep(200);
            }
        }
    }

    public void Put(string relative, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(P(relative))!);
        File.WriteAllText(P(relative), text);
    }

    public string Read(string relative) => File.ReadAllText(P(relative));

    /// <summary>Copies a file from tests/fixtures to the same path in this project.</summary>
    public void Fixture(string relative)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(P(relative))!);
        File.Copy(Path.Combine(Fixtures, relative), P(relative), overwrite: true);
    }

    public DateTime Stamp(string relative) => File.GetLastWriteTimeUtc(P(relative));

    /// <summary>Writes app.csproj. Husk's files import only once they exist, so T3's empty web project
    /// builds before the targets are written.</summary>
    public void Project(string properties = "", string items = "", string sdk = "Microsoft.NET.Sdk.Web", string targets = "") => Put("app.csproj", $"""
        <Project Sdk="{sdk}">
          <Import Project="{Path.Combine(PareBuildDir, "Extrode.Pare.Build.props")}" />
          <Import Project="{Path.Combine(HuskBuildDir, "Extrode.Husk.Build.props")}" Condition="Exists('{Path.Combine(HuskBuildDir, "Extrode.Husk.Build.props")}')" />
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <OutputType>Library</OutputType>
            <HuskTaskAssembly>{TaskAssembly}</HuskTaskAssembly>
            <HuskVendorDir>{VendorDir}</HuskVendorDir>
            {properties}
          </PropertyGroup>
          <ItemGroup>{items}</ItemGroup>
          <Import Project="{Path.Combine(PareBuildDir, "Extrode.Pare.Build.targets")}" />
          <Import Project="{Path.Combine(HuskBuildDir, "Extrode.Husk.Build.targets")}" Condition="Exists('{Path.Combine(HuskBuildDir, "Extrode.Husk.Build.targets")}')" />
          {targets}
        </Project>
        """);

    public (int Exit, string Output) Build(params string[] extra) =>
        Run(["build", "-nologo", "-nodeReuse:false", "--disable-build-servers", "-clp:NoSummary", .. extra]);

    public (int Exit, string Output) Run(params string[] args) => Exec("dotnet", args);

    public (int Exit, string Output) Exec(string fileName, params string[] args)
    {
        var start = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = _dir.FullName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var arg in args)
            start.ArgumentList.Add(arg);
        // A test host started by `dotnet test` carries MSBuild variables meant for itself.
        foreach (var key in start.Environment.Keys.Where(k => k.StartsWith("MSBuild", StringComparison.OrdinalIgnoreCase)).ToList())
            start.Environment.Remove(key);
        if (PathOverride is not null)
            start.Environment["PATH"] = PathOverride;
        using var process = Process.Start(start)!;
        var stderr = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        string stdout = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, stdout + stderr.Result);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Husk.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Husk.slnx not found above the test output");
    }
}
