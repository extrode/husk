using System.IO.Compression;

namespace Extrode.Husk.Integration;

/// <summary>Packs both packages once, with scripts/pack-local.sh, into a feed of its own.</summary>
public sealed class PackedFeed : IDisposable
{
    public readonly TempProject Feed = new();
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
    }

    public string Package(string id) => Path.Combine(Feed.Dir, $"{id}.{Version}.nupkg");

    public void Dispose() => Feed.Dispose();
}

public sealed class PackageTests(PackedFeed feed) : IClassFixture<PackedFeed>
{
    private List<string> Entries(string id)
    {
        Assert.True(feed.Pack.Exit == 0, feed.Pack.Output);
        using var zip = ZipFile.OpenRead(feed.Package(id));
        return [.. zip.Entries.Select(e => e.FullName)];
    }

    [Fact]
    public void The_build_package_carries_the_compiler_natives_vendored_Svelte_and_licenses()
    {
        var entries = Entries("Extrode.Husk.Build");

        Assert.Contains("NOTICE.md", entries);
        Assert.Contains("build/Extrode.Husk.Build.targets", entries);
        Assert.Contains("tasks/net10.0/Extrode.Husk.Build.dll", entries);
        Assert.Contains("tasks/net10.0/Extrode.Husk.Compiler.dll", entries);
        Assert.Contains("tasks/net10.0/Extrode.Husk.Compiler.deps.json", entries);
        Assert.Contains("third-party/svelte-5.57.2/svelte/compiler/index.js", entries);
        Assert.Contains("third-party/svelte-5.57.2/runtime-imports.json", entries);
        Assert.Equal(
            ["linux-arm64/native/ClearScriptV8.linux-arm64.so", "linux-x64/native/ClearScriptV8.linux-x64.so", "osx-arm64/native/ClearScriptV8.osx-arm64.dylib",
             "osx-x64/native/ClearScriptV8.osx-x64.dylib", "win-arm64/native/ClearScriptV8.win-arm64.dll", "win-x64/native/ClearScriptV8.win-x64.dll"],
            entries.Where(e => e.StartsWith("tasks/net10.0/runtimes/", StringComparison.Ordinal) && !e.EndsWith(".pem", StringComparison.Ordinal) && !e.EndsWith(".sig", StringComparison.Ordinal))
                .Select(e => e["tasks/net10.0/runtimes/".Length..]).Order(StringComparer.Ordinal));
        Assert.Equal(
            ["third-party/clearscript-7.5.1.1/License.txt", "third-party/clearscript-7.5.1.1/V8/LICENSE",
             "third-party/clearscript-7.5.1.1/V8/LICENSE.fdlibm", "third-party/clearscript-7.5.1.1/V8/LICENSE.strongtalk", "third-party/clearscript-7.5.1.1/V8/LICENSE.v8",
             "third-party/newtonsoft.json-13.0.3/LICENSE.md",
             "third-party/svelte-5.57.2/clsx/license", "third-party/svelte-5.57.2/esm-env/LICENSE", "third-party/svelte-5.57.2/svelte/LICENSE.md"],
            entries.Where(e => e.StartsWith("third-party/", StringComparison.Ordinal) && Path.GetFileName(e).StartsWith("license", StringComparison.OrdinalIgnoreCase))
                .Order(StringComparer.Ordinal));
    }

    [Fact]
    public void The_runtime_package_carries_no_V8()
    {
        var entries = Entries("Extrode.Husk.AspNetCore");

        Assert.Contains("lib/net10.0/Extrode.Husk.AspNetCore.dll", entries);
        Assert.DoesNotContain(entries, e => e.Contains("ClearScript", StringComparison.OrdinalIgnoreCase));
    }

    private TempProject Consumer(string references)
    {
        Assert.True(feed.Pack.Exit == 0, feed.Pack.Output);
        var project = new TempProject();
        project.Put("nuget.config", $"""
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <config><add key="globalPackagesFolder" value="{project.P("packages")}" /></config>
              <packageSources>
                <clear />
                <add key="husk" value="{feed.Feed.Dir}" />
                <add key="pare" value="{Path.Combine(TempProject.RepoRoot, "packages")}" />
                <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
              </packageSources>
              <packageSourceMapping>
                <packageSource key="husk"><package pattern="Extrode.Husk*" /></packageSource>
                <packageSource key="pare"><package pattern="Extrode.Pare*" /></packageSource>
                <packageSource key="nuget.org"><package pattern="*" /></packageSource>
              </packageSourceMapping>
            </configuration>
            """);
        project.Put("app.csproj", $"""
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
              </PropertyGroup>
              <ItemGroup>
                {references.Replace("$(HuskVersion)", feed.Version, StringComparison.Ordinal)}
              </ItemGroup>
            </Project>
            """);
        project.Put("Program.cs", "using Extrode.Husk.AspNetCore;\nvar builder = WebApplication.CreateBuilder(args);\nbuilder.AddHusk();\nvar app = builder.Build();\napp.MapStaticAssets();\napp.Run();\n");
        project.Fixture("wwwroot/Styled.svelte");
        return project;
    }

    [Fact]
    public void A_fresh_project_restores_both_from_the_feed_and_builds_a_component()
    {
        using var project = Consumer("""
            <PackageReference Include="Extrode.Pare.Build" Version="0.6.0" />
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
    }

    [Fact]
    public void Without_Pare_the_build_fails_with_HUSK002()
    {
        using var project = Consumer("""<PackageReference Include="Extrode.Husk.Build" Version="$(HuskVersion)" />""");
        project.Put("Program.cs", "var app = WebApplication.CreateBuilder(args).Build();\napp.Run();\n");

        var (exit, output) = project.Build();
        Assert.NotEqual(0, exit);
        Assert.Contains("error HUSK002", output, StringComparison.Ordinal);
    }
}
