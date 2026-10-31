using System.Runtime.InteropServices;
using System.IO.Compression;
using System.Reflection;

namespace Extrode.Husk.Integration;

/// <summary>Packs both packages once, with scripts/pack-local.sh, into a feed of its own.</summary>
public sealed class PackedFeed : IDisposable
{
    public readonly TempProject Feed = new();
    /// <summary>One package folder for every project restored from this feed: the ClearScript natives
    /// download once per run, not once per project.</summary>
    public readonly TempProject Packages = new();
    public readonly string Version = $"0.1.0-test.{DateTime.UtcNow.Ticks}";
    public readonly (int Exit, string Output) Pack;

    public PackedFeed()
    {
        var start = new System.Diagnostics.ProcessStartInfo("sh") { RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(Path.Combine(TempProject.RepoRoot, "scripts", "pack-local.sh"));
        start.Environment["HUSK_FEED"] = Feed.Dir;
        start.Environment["HUSK_VERSION"] = Version;
        foreach (var key in start.Environment.Keys.Where(k => k.StartsWith("MSBuild", StringComparison.OrdinalIgnoreCase)).ToList())
            start.Environment.Remove(key);
        using var process = System.Diagnostics.Process.Start(start)!;
        var stderr = process.StandardError.ReadToEndAsync();
        string stdout = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        Pack = (process.ExitCode, stdout + stderr.Result);
        _development = new(StartDevelopment);
    }

    private (TempProject, RunningApp) StartDevelopment()
    {
        var project = Sample();
        try
        {
            var (exit, output) = project.Build();
            Assert.True(exit == 0, output);
            return (project, new RunningApp(project.Dir, project.P("bin/Debug/net10.0/svelte-app.dll")));
        }
        catch
        {
            project.Dispose();
            throw;
        }
    }

    public string Package(string id) => Path.Combine(Feed.Dir, $"{id}.{Version}.nupkg");

    /// <summary>A nuget.config for <paramref name="project"/>: Husk from this feed, the rest
    /// from nuget.org, into the feed's shared package folder, or the project's own when <paramref name="ownPackages"/>
    /// (for a test that changes what is in it).</summary>
    public void NuGetConfig(TempProject project, bool ownPackages = false)
    {
        string packages = ownPackages ? project.P("packages") : Packages.Dir;
        Assert.True(Pack.Exit == 0, Pack.Output);
        project.Put("nuget.config", $"""
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <config><add key="globalPackagesFolder" value="{packages}" /></config>
              <packageSources>
                <clear />
                <add key="husk" value="{Feed.Dir}" />
                <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
              </packageSources>
              <packageSourceMapping>
                <packageSource key="husk"><package pattern="Extrode.Husk*" /></packageSource>
                <packageSource key="nuget.org"><package pattern="*" /></packageSource>
              </packageSourceMapping>
            </configuration>
            """);
    }

    /// <summary>The package folder <paramref name="project"/> restores into.</summary>
    public static string PackageFolder(TempProject project) =>
        System.Text.RegularExpressions.Regex.Match(project.Read("nuget.config"), "globalPackagesFolder\" value=\"([^\"]+)\"").Groups[1].Value;

    public static readonly string PareVersion = typeof(PackedFeed).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
        .Single(a => a.Key == "PareVersion").Value!;

    /// <summary>A web project restoring from this feed, with <paramref name="references"/>
    /// (<c>$(HuskVersion)</c> is this feed's version, <c>$(PareVersion)</c> the Pare it depends on)
    /// and the Styled fixture.</summary>
    public TempProject Consumer(string references, bool ownPackages = false)
    {
        var project = new TempProject();
        NuGetConfig(project, ownPackages);
        project.Put("app.csproj", $"""
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
              </PropertyGroup>
              <ItemGroup>
                {references.Replace("$(HuskVersion)", Version, StringComparison.Ordinal).Replace("$(PareVersion)", PareVersion, StringComparison.Ordinal)}
              </ItemGroup>
            </Project>
            """);
        project.Put("Program.cs", "using Extrode.Husk.AspNetCore;\nvar builder = WebApplication.CreateBuilder(args);\nbuilder.AddHusk();\nvar app = builder.Build();\napp.MapStaticAssets();\napp.Run();\n");
        project.Fixture("wwwroot/Styled.svelte");
        return project;
    }

    /// <summary>samples/svelte-app copied to a temp folder, restoring this feed's build.</summary>
    public TempProject Sample()
    {
        var project = new TempProject();
        string sample = Path.Combine(TempProject.RepoRoot, "samples", "svelte-app");
        foreach (var file in Directory.EnumerateFiles(sample, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(sample, file);
            if (relative.Split(Path.DirectorySeparatorChar)[0] is "bin" or "obj")
                continue;
            Directory.CreateDirectory(Path.GetDirectoryName(project.P(relative))!);
            File.Copy(file, project.P(relative));
        }
        NuGetConfig(project);
        project.Put("svelte-app.csproj", project.Read("svelte-app.csproj").Replace("\"0.1.0-*\"", $"\"{Version}\"", StringComparison.Ordinal));
        return project;
    }

    private readonly Lazy<(TempProject Project, RunningApp App)> _development;

    /// <summary>The sample, built once from this feed and running in Development.</summary>
    public (TempProject Project, RunningApp App) Development => _development.Value;

    public void Dispose()
    {
        if (_development.IsValueCreated)
        {
            _development.Value.App.Dispose();
            _development.Value.Project.Dispose();
        }
        Feed.Dispose();
        Packages.Dispose();
    }
}

[CollectionDefinition(nameof(PackedFeed))]
public sealed class PackedFeedCollection : ICollectionFixture<PackedFeed>;

[Collection(nameof(PackedFeed))]
public sealed class PackageTests(PackedFeed feed)
{
    private List<string> Entries(string id)
    {
        Assert.True(feed.Pack.Exit == 0, feed.Pack.Output);
        using var zip = ZipFile.OpenRead(feed.Package(id));
        return [.. zip.Entries.Select(e => e.FullName)];
    }

    [Fact]
    public void The_build_package_carries_the_compiler_vendored_Svelte_and_licenses_and_depends_on_the_natives()
    {
        var entries = Entries("Extrode.Husk.Build");

        Assert.Contains("NOTICE.md", entries);
        Assert.Contains("build/Extrode.Husk.Build.targets", entries);
        Assert.Contains("tasks/net10.0/Extrode.Husk.Build.dll", entries);
        Assert.Contains("tasks/net10.0/Extrode.Husk.Compiler.dll", entries);
        Assert.Contains("tasks/net10.0/Extrode.Husk.Compiler.deps.json", entries);
        Assert.Contains("third-party/svelte-5.57.3/svelte/compiler/index.js", entries);
        Assert.Contains("third-party/svelte-5.57.3/runtime-imports.json", entries);
        Assert.Contains("tasks/net10.0/ClearScript.V8.dll", entries);
        Assert.DoesNotContain(entries, e => e.Contains("/runtimes/", StringComparison.Ordinal));
        Assert.DoesNotContain(entries, e => Path.GetFileName(e).StartsWith("ClearScriptV8.", StringComparison.Ordinal));
        using (var zip = ZipFile.OpenRead(feed.Package("Extrode.Husk.Build")))
        {
            string nuspec = new StreamReader(zip.GetEntry("Extrode.Husk.Build.nuspec")!.Open()).ReadToEnd();
            foreach (var rid in new[] { "linux-arm64", "linux-x64", "osx-arm64", "osx-x64", "win-arm64", "win-x64" })
                Assert.Contains($"<dependency id=\"Microsoft.ClearScript.V8.Native.{rid}\" version=\"[7.5.1.1]\" exclude=\"Runtime,Compile,Build,Native,Analyzers\" />", nuspec, StringComparison.Ordinal);
            Assert.Single(System.Text.RegularExpressions.Regex.Matches(nuspec, "id=\"Extrode.Pare.Build\""));
            Assert.Contains($"<dependency id=\"Extrode.Pare.Build\" version=\"{PackedFeed.PareVersion}\" exclude=\"Build,Analyzers\" />", nuspec, StringComparison.Ordinal);
        }
        Assert.Equal(
            ["third-party/clearscript-7.5.1.1/License.txt", "third-party/clearscript-7.5.1.1/V8/LICENSE",
             "third-party/clearscript-7.5.1.1/V8/LICENSE.fdlibm", "third-party/clearscript-7.5.1.1/V8/LICENSE.strongtalk", "third-party/clearscript-7.5.1.1/V8/LICENSE.v8",
             "third-party/newtonsoft.json-13.0.3/LICENSE.md",
             "third-party/svelte-5.57.3/clsx/license", "third-party/svelte-5.57.3/esm-env/LICENSE", "third-party/svelte-5.57.3/svelte/LICENSE.md"],
            entries.Where(e => e.StartsWith("third-party/", StringComparison.Ordinal) && Path.GetFileName(e).StartsWith("license", StringComparison.OrdinalIgnoreCase))
                .Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Projects_restored_from_the_feed_share_one_package_folder()
    {
        using var first = new TempProject();
        using var second = new TempProject();
        feed.NuGetConfig(first);
        feed.NuGetConfig(second);

        Assert.Equal(PackedFeed.PackageFolder(first), PackedFeed.PackageFolder(second));
        Assert.DoesNotContain(first.Dir, PackedFeed.PackageFolder(first), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Extrode.Husk.Build")]
    [InlineData("Extrode.Husk.AspNetCore")]
    public void Both_packages_declare_ISL_P_and_carry_the_license_text_and_notice(string id)
    {
        var entries = Entries(id);
        using var zip = ZipFile.OpenRead(feed.Package(id));
        string nuspec = new StreamReader(zip.GetEntry(id + ".nuspec")!.Open()).ReadToEnd();

        Assert.Contains("<license type=\"file\">LICENSE.md</license>", nuspec, StringComparison.Ordinal);
        Assert.Contains("<repository type=\"git\" url=\"https://github.com/extrode/husk\"", nuspec, StringComparison.Ordinal);
        Assert.Contains("LICENSE.md", entries);
        Assert.Contains("README.md", entries);
        using var license = new StreamReader(zip.GetEntry("LICENSE.md")!.Open());
        Assert.StartsWith("# Islamic Software License - Permissive (ISL-P), Version 1.2", license.ReadToEnd(), StringComparison.Ordinal);
        using var notice = new StreamReader(zip.GetEntry("NOTICE.md")!.Open());
        Assert.Contains("LicenseRef-ISL-P-1.2 WITH AdditionRef-ISL-OE-1.2", notice.ReadToEnd(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Extrode.Husk.Build")]
    [InlineData("Extrode.Husk.AspNetCore")]
    public void The_packed_readme_is_the_repo_readme_with_every_link_and_image_absolute(string id)
    {
        using var zip = ZipFile.OpenRead(feed.Package(id));
        string packed = new StreamReader(zip.GetEntry("README.md")!.Open()).ReadToEnd();
        string tag = "v" + feed.Version + "/";

        Assert.DoesNotMatch(@"\]\((?!https://|#)", packed);
        Assert.DoesNotMatch(@"src=""(?!https://)", packed);
        // WriteLinesToFile ends the file with Environment.NewLine, CRLF on Windows.
        string repo = File.ReadAllText(Path.Combine(TempProject.RepoRoot, "README.md")).ReplaceLineEndings("\n");
        packed = packed.ReplaceLineEndings("\n");
        Assert.Equal(repo, packed.Replace("https://raw.githubusercontent.com/extrode/husk/" + tag, "").Replace("https://github.com/extrode/husk/blob/" + tag, ""));
    }

    [Fact]
    public void The_runtime_package_carries_no_V8()
    {
        var entries = Entries("Extrode.Husk.AspNetCore");

        Assert.Contains("lib/net10.0/Extrode.Husk.AspNetCore.dll", entries);
        Assert.DoesNotContain(entries, e => e.Contains("ClearScript", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void A_fresh_project_restores_both_from_the_feed_and_builds_a_component()
    {
        using var project = feed.Consumer("""
            <PackageReference Include="Extrode.Husk.Build" Version="$(HuskVersion)" />
            <PackageReference Include="Extrode.Husk.AspNetCore" Version="$(HuskVersion)" />
            """);

        var (exit, output) = project.Build();
        Assert.True(exit == 0, output);
        Assert.Contains("from 'svelte/internal/client'", project.Read(TempProject.PareOut + "Styled.svelte.js"), StringComparison.Ordinal);
        Assert.NotEmpty(project.Read(TempProject.HuskOut + "husk.css"));
        Assert.True(File.Exists(project.P("bin/Debug/net10.0/Extrode.Husk.AspNetCore.dll")));
        Assert.True(File.Exists(project.P("bin/Debug/net10.0/Extrode.Husk.development.json")));
        Assert.Empty(Directory.GetFiles(project.P("bin"), "*ClearScript*", SearchOption.AllDirectories));
        string ext = OperatingSystem.IsWindows() ? "dll" : OperatingSystem.IsMacOS() ? "dylib" : "so";
        Assert.True(File.Exists(Path.Combine(PackedFeed.PackageFolder(project), $"microsoft.clearscript.v8.native.{HostRid}/7.5.1.1/runtimes/{HostRid}/native/ClearScriptV8.{HostRid}.{ext}")));
    }

    [Fact]
    public void A_missing_native_fails_the_build_with_HUSK003_naming_the_cache_that_holds_the_task()
    {
        using var project = feed.Consumer("""
            <PackageReference Include="Extrode.Husk.Build" Version="$(HuskVersion)" />
            <PackageReference Include="Extrode.Husk.AspNetCore" Version="$(HuskVersion)" />
            """, ownPackages: true);
        var (exit, output) = project.Build();
        Assert.True(exit == 0, output);
        string rid = HostRid;
        string task = Path.Combine("extrode.husk.build", feed.Version.ToLowerInvariant(), "tasks", "net10.0");

        Copy(Path.Combine(PackedFeed.PackageFolder(project), task), project.P("elsewhere/" + task.Replace('\\', '/')));
        (exit, output) = project.Build("-p:HuskTaskAssembly=" + Path.Combine(project.P("elsewhere"), task, "Extrode.Husk.Build.dll"));
        Assert.NotEqual(0, exit);
        Assert.Contains("error HUSK003", output, StringComparison.Ordinal);
        Assert.Contains(Path.Combine(project.P("elsewhere"), "microsoft.clearscript.v8.native." + rid), output, StringComparison.Ordinal);

        Directory.Move(Path.Combine(PackedFeed.PackageFolder(project), $"microsoft.clearscript.v8.native.{rid}/7.5.1.1/runtimes"), project.P("runtimes-moved"));
        (exit, output) = project.Build();
        Assert.NotEqual(0, exit);
        Assert.Contains("error HUSK003", output, StringComparison.Ordinal);
        Assert.Contains($"Microsoft.ClearScript.V8.Native.{rid} 7.5.1.1", output, StringComparison.Ordinal);

        using var app = new RunningApp(project.Dir, project.P("bin/Debug/net10.0/app.dll"));
        project.Put("wwwroot/Styled.svelte", project.Read("wwwroot/Styled.svelte") + "\n<p>saved</p>\n");
        Thread.Sleep(3000);
        Assert.Single(app.Output.Split('\n'), l => l.Contains("Husk's V8 library is not at", StringComparison.Ordinal));
        Assert.DoesNotContain("HUSK100", app.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Husk_that_would_load_a_second_V8_native_in_one_build_process_is_HUSK004_and_the_first_still_builds()
    {
        using var project = feed.Consumer("""
            <PackageReference Include="Extrode.Husk.Build" Version="$(HuskVersion)" />
            <PackageReference Include="Extrode.Husk.AspNetCore" Version="$(HuskVersion)" />
            """);
        var (exit, output) = project.Build();
        Assert.True(exit == 0, output);
        string task = $"extrode.husk.build/{feed.Version.ToLowerInvariant()}/tasks/net10.0";
        string native = $"microsoft.clearscript.v8.native.{HostRid}/7.5.1.1";
        Copy(Path.Combine(PackedFeed.PackageFolder(project), task), project.P("elsewhere/" + task));
        Copy(Path.Combine(PackedFeed.PackageFolder(project), native), project.P("elsewhere/" + native));
        string other = Path.Combine(project.P("elsewhere/" + task), "Extrode.Husk.Build.dll");
        // One process builds Release with the restored Husk, then Debug with the copy.
        project.Put("both.proj", $"""
            <Project>
              <Target Name="Build">
                <MSBuild Projects="app.csproj" Properties="Configuration=Release" />
                <MSBuild Projects="app.csproj" Properties="HuskTaskAssembly={other}" />
              </Target>
            </Project>
            """);

        // Without HUSK004 the second V8 spins at full CPU and the build never ends (2026-10-31, macOS).
        (exit, output) = project.Exec(TimeSpan.FromMinutes(3), "dotnet", "build", "both.proj", "--no-restore", "-nologo", "-m:1", "-nodeReuse:false", "--disable-build-servers", "-clp:NoSummary");

        Assert.True(exit > 0, output);
        Assert.Contains("error HUSK004", output, StringComparison.Ordinal);
        Assert.Contains("dotnet build-server shutdown", output, StringComparison.Ordinal);
        Assert.Contains(Path.GetDirectoryName(other)!, output, StringComparison.Ordinal);
        Assert.True(File.Exists(project.P("obj/Release/net10.0/pare/Styled.svelte.js")));
    }

    [Fact]
    public void Two_Husk_versions_sharing_one_V8_native_build_in_one_process()
    {
        using var project = feed.Consumer("""
            <PackageReference Include="Extrode.Husk.Build" Version="$(HuskVersion)" />
            <PackageReference Include="Extrode.Husk.AspNetCore" Version="$(HuskVersion)" />
            """);
        var (exit, output) = project.Build();
        Assert.True(exit == 0, output);
        string task = $"extrode.husk.build/{feed.Version.ToLowerInvariant()}/tasks/net10.0";
        string upgraded = "extrode.husk.build/9.9.9/tasks/net10.0";
        Copy(Path.Combine(PackedFeed.PackageFolder(project), task), Path.Combine(PackedFeed.PackageFolder(project), upgraded));
        string other = Path.Combine(Path.Combine(PackedFeed.PackageFolder(project), upgraded), "Extrode.Husk.Build.dll");
        project.Put("both.proj", $"""
            <Project>
              <Target Name="Build">
                <MSBuild Projects="app.csproj" Properties="Configuration=Release" />
                <MSBuild Projects="app.csproj" Properties="HuskTaskAssembly={other}" />
                <MSBuild Projects="app.csproj" Properties="Configuration=Staging" />
              </Target>
            </Project>
            """);

        (exit, output) = project.Exec(TimeSpan.FromMinutes(3), "dotnet", "build", "both.proj", "--no-restore", "-nologo", "-m:1", "-nodeReuse:false", "--disable-build-servers", "-clp:NoSummary");

        Assert.True(exit == 0, output);
        Assert.Equal(3, output.Split('\n').Count(l => l.Contains("husk: compiled 1 of", StringComparison.Ordinal)));
    }

    private static string HostRid =>
        (OperatingSystem.IsWindows() ? "win-" : OperatingSystem.IsMacOS() ? "osx-" : "linux-") + RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();

    private static void Copy(string from, string to)
    {
        foreach (string file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(to, Path.GetRelativePath(from, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    [Fact]
    public void A_direct_Pare_Build_reference_at_Husk_s_version_still_builds()
    {
        using var project = feed.Consumer("""
            <PackageReference Include="Extrode.Pare.Build" Version="$(PareVersion)" PrivateAssets="all" />
            <PackageReference Include="Extrode.Husk.Build" Version="$(HuskVersion)" PrivateAssets="all" />
            <PackageReference Include="Extrode.Husk.AspNetCore" Version="$(HuskVersion)" />
            """);

        var (exit, output) = project.Build();
        Assert.True(exit == 0, output);
        Assert.DoesNotContain("MSB4011", output, StringComparison.Ordinal);
        Assert.Contains("from 'svelte/internal/client'", project.Read(TempProject.PareOut + "Styled.svelte.js"), StringComparison.Ordinal);
    }

    [Fact]
    public void A_direct_Pare_Build_older_than_Husk_s_is_NU1605()
    {
        using var project = feed.Consumer("""
            <PackageReference Include="Extrode.Pare.Build" Version="0.7.0" PrivateAssets="all" />
            <PackageReference Include="Extrode.Husk.Build" Version="$(HuskVersion)" PrivateAssets="all" />
            """);
        project.Put("Program.cs", "var app = WebApplication.CreateBuilder(args).Build();\napp.Run();\n");

        var (exit, output) = project.Build();
        Assert.NotEqual(0, exit);
        Assert.Contains("error NU1605", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Leaving_out_Pare_s_build_fails_with_HUSK002()
    {
        using var project = feed.Consumer("""
            <PackageReference Include="Extrode.Pare.Build" Version="$(PareVersion)" ExcludeAssets="build;buildtransitive" />
            <PackageReference Include="Extrode.Husk.Build" Version="$(HuskVersion)" />
            """);
        project.Put("Program.cs", "var app = WebApplication.CreateBuilder(args).Build();\napp.Run();\n");

        var (exit, output) = project.Build();
        Assert.NotEqual(0, exit);
        Assert.Contains("error HUSK002", output, StringComparison.Ordinal);
    }
}

[Collection(nameof(PackedFeed))]
public sealed class SampleBuildTests(PackedFeed feed)
{
    [Fact]
    public void The_sample_builds_from_the_feed_with_one_HUSK200_naming_file_line_and_column()
    {
        using var sample = feed.Sample();

        var (exit, output) = sample.Build();
        Assert.True(exit == 0, output);
        var warnings = output.Split('\n').Where(l => l.Contains(": warning ", StringComparison.Ordinal)).Distinct().ToList();
        Assert.Matches(@"[/\\]wwwroot[/\\]App\.svelte\(39,1\): warning HUSK200: a11y_missing_attribute: ", Assert.Single(warnings));
        foreach (var name in new[] { "App.svelte.js", "Child.svelte.js", "counter.svelte.js", "format.js", "main.js", "index.html" })
            Assert.True(File.Exists(sample.P(TempProject.PareOut + name)), name);
        Assert.Contains("\"/counter.svelte\": \"/counter.svelte.js\"", sample.Read(TempProject.PareOut + "index.html"), StringComparison.Ordinal);
    }

    [Fact]
    public void A_HUSK200_treated_as_an_error_writes_no_output_so_the_next_build_fails_too()
    {
        using var sample = feed.Sample();

        for (int build = 1; build <= 2; build++)
        {
            var (exit, output) = sample.Build("-p:WarningsAsErrors=HUSK200");
            Assert.True(exit != 0, $"build {build}: {output}");
            Assert.Contains("error HUSK200: a11y_missing_attribute", output, StringComparison.Ordinal);
            Assert.False(File.Exists(sample.P(TempProject.PareOut + "App.svelte.js")), $"build {build}");
            Assert.True(File.Exists(sample.P(TempProject.PareOut + "Child.svelte.js")), $"build {build}");
        }
    }
}
