using System.Text.RegularExpressions;

namespace Extrode.Husk.Integration;

public sealed partial class CompileBuildTests
{
    private static readonly string[] Outputs =
    [
        "Styled.svelte.js", "Styled.svelte.css", "Styled.svelte.js.map",
        "Plain.svelte.js", "Plain.svelte.css", "Plain.svelte.js.map",
        "counter.svelte.js", "counter.svelte.js.map",
        "clock.svelte.js", "clock.svelte.js.map",
    ];

    private static TempProject App(string properties = "")
    {
        var project = new TempProject();
        project.Project(properties);
        foreach (var file in new[] { "Styled.svelte", "Plain.svelte", "clock.svelte.js", "counter.svelte.ts", "format.ts" })
            project.Fixture("wwwroot/" + file);
        return project;
    }

    private static int? Compiled(string output) =>
        CompiledLine().Match(output) is { Success: true } m ? int.Parse(m.Groups[1].Value) : null;

    private static void Touch(TempProject project, string relative) =>
        File.SetLastWriteTimeUtc(project.P(relative), DateTime.UtcNow.AddSeconds(2));

    [Fact]
    public void A_build_writes_every_output_and_a_second_build_compiles_nothing()
    {
        using var project = App();

        var (exit, output) = project.Build();
        Assert.True(exit == 0, output);
        Assert.Equal(4, Compiled(output));
        foreach (var name in Outputs)
            Assert.True(File.Exists(project.P(TempProject.PareOut + name)), name + "\n" + output);
        Assert.Contains("//# sourceMappingURL=Styled.svelte.js.map", project.Read(TempProject.PareOut + "Styled.svelte.js"));
        Assert.Contains(".counter", project.Read(TempProject.PareOut + "Styled.svelte.css"));

        (exit, output) = project.Build();
        Assert.True(exit == 0, output);
        Assert.Equal(0, Compiled(output));
    }

    [Fact]
    public void A_source_newer_than_one_of_its_outputs_recompiles_all_of_them()
    {
        using var project = App();
        Assert.Equal(0, project.Build().Exit);
        var old = DateTime.UtcNow.AddMinutes(-10);
        File.SetLastWriteTimeUtc(project.P(TempProject.PareOut + "Styled.svelte.css"), old);
        File.SetLastWriteTimeUtc(project.P("wwwroot/Styled.svelte"), old.AddMinutes(1));

        var (exit, output) = project.Build();

        Assert.True(exit == 0, output);
        Assert.Equal(1, Compiled(output));
        Assert.True(project.Stamp(TempProject.PareOut + "Styled.svelte.css") > old.AddMinutes(1));
    }

    [Fact]
    public void A_project_referencing_the_app_gets_no_development_map()
    {
        using var app = App();
        using var consumer = new TempProject();
        consumer.Put("ref.csproj", $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
              <ItemGroup><ProjectReference Include="{{app.P("app.csproj")}}" /></ItemGroup>
            </Project>
            """);

        var (exit, output) = consumer.Build();

        Assert.True(exit == 0, output);
        Assert.True(File.Exists(app.P("bin/Debug/net10.0/Extrode.Husk.development.json")), output);
        Assert.True(File.Exists(consumer.P("bin/Debug/net10.0/app.dll")), output);
        Assert.False(File.Exists(consumer.P("bin/Debug/net10.0/Extrode.Husk.development.json")));
    }

    [Fact]
    public void One_changed_source_compiles_one()
    {
        using var project = App();
        Assert.Equal(0, project.Build().Exit);
        var plainStamp = project.Stamp(TempProject.PareOut + "Plain.svelte.js");

        Touch(project, "wwwroot/Styled.svelte");
        var (exit, output) = project.Build();

        Assert.True(exit == 0, output);
        Assert.Equal(1, Compiled(output));
        Assert.Equal(plainStamp, project.Stamp(TempProject.PareOut + "Plain.svelte.js"));
    }

    [Fact]
    public void A_missing_output_recompiles_its_source()
    {
        using var project = App();
        Assert.Equal(0, project.Build().Exit);
        string css = project.Read(TempProject.PareOut + "Styled.svelte.css");

        File.Delete(project.P(TempProject.PareOut + "Styled.svelte.css"));
        var (exit, output) = project.Build();

        Assert.True(exit == 0, output);
        Assert.Equal(css, project.Read(TempProject.PareOut + "Styled.svelte.css"));
    }

    [Fact]
    public void A_newer_compiler_rebuilds_every_source()
    {
        // A copy of the vendored Svelte, so touching it leaves the shared one alone.
        using var project = App($"<HuskVendorDir>vendor/</HuskVendorDir>");
        foreach (var file in Directory.EnumerateFiles(TempProject.VendorDir, "*", SearchOption.AllDirectories))
        {
            string target = project.P(Path.Combine("vendor", Path.GetRelativePath(TempProject.VendorDir, file)));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
        Assert.Equal(0, project.Build().Exit);

        Touch(project, "vendor/svelte/compiler/index.js");
        var (exit, output) = project.Build();

        Assert.True(exit == 0, output);
        Assert.Equal(4, Compiled(output));
    }

    [Fact]
    public void A_compiler_at_another_path_rebuilds_every_source_though_no_file_is_newer()
    {
        using var project = App();
        Assert.Equal(0, project.Build().Exit);
        string upgraded = project.P("upgraded-svelte");
        CopyDirectory(TempProject.VendorDir, upgraded);
        var past = DateTime.UtcNow.AddDays(-30);
        foreach (var file in Directory.EnumerateFiles(upgraded, "*", SearchOption.AllDirectories))
            File.SetLastWriteTimeUtc(file, past);

        var (exit, output) = project.Build($"-p:HuskVendorDir={upgraded}/");

        Assert.True(exit == 0, output);
        Assert.Equal(4, Compiled(output));
    }

    private static void CopyDirectory(string from, string to)
    {
        foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(to, Path.GetRelativePath(from, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    [Fact]
    public void A_changed_HuskDev_rebuilds_every_source()
    {
        using var project = App();
        Assert.Equal(0, project.Build().Exit);
        Assert.Contains("$.FILENAME", project.Read(TempProject.PareOut + "Styled.svelte.js"), StringComparison.Ordinal);

        var (exit, output) = project.Build("-p:HuskDev=false");

        Assert.True(exit == 0, output);
        Assert.Equal(4, Compiled(output));
        Assert.DoesNotContain("$.FILENAME", project.Read(TempProject.PareOut + "Styled.svelte.js"), StringComparison.Ordinal);
    }

    [Fact]
    public void A_compile_error_fails_the_build_and_keeps_the_previous_outputs()
    {
        using var project = App();
        Assert.Equal(0, project.Build().Exit);
        string[] names = ["Styled.svelte.js", "Styled.svelte.css", "Styled.svelte.js.map"];
        var before = names.Select(n => File.ReadAllBytes(project.P(TempProject.PareOut + n))).ToArray();

        project.Put("wwwroot/Styled.svelte", File.ReadAllText(Path.Combine(TempProject.Fixtures, "wwwroot", "Broken.svelte")));
        Touch(project, "wwwroot/Styled.svelte");
        var (exit, output) = project.Build();

        Assert.NotEqual(0, exit);
        Assert.Contains("HUSK100", output);
        Assert.Contains("Styled.svelte(5,1)", output);
        for (int i = 0; i < names.Length; i++)
            Assert.Equal(before[i], File.ReadAllBytes(project.P(TempProject.PareOut + names[i])));
    }

    [Fact]
    public void A_svelte_ts_module_is_not_also_stripped_by_Pare()
    {
        using var project = App();

        var (exit, output) = project.Build();

        Assert.True(exit == 0, output);
        Assert.DoesNotContain("PARE201", output);
        Assert.True(File.Exists(project.P(TempProject.PareOut + "format.js")), output);
        Assert.False(File.Exists(project.P(TempProject.PareOut + "counter.svelte.ts.js")), output);
    }

    [Fact]
    public void A_build_needs_nothing_on_PATH_but_dotnet()
    {
        using var project = App();
        // The resolved folder, not a shared bin such as /opt/homebrew/bin that may hold node too.
        var dotnet = new FileInfo(DotnetPath());
        project.PathOverride = Path.GetDirectoryName(dotnet.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? dotnet.FullName);

        var (exit, output) = project.Build();

        Assert.True(exit == 0, output);
        Assert.Equal(4, Compiled(output));
    }

    private static string DotnetPath()
    {
        string name = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";
        if (Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } host)
            return host;
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            if (File.Exists(Path.Combine(dir, name)))
                return Path.Combine(dir, name);
        throw new InvalidOperationException("dotnet not found on PATH");
    }

    [GeneratedRegex(@"husk: compiled (\d+) of \d+ sources")]
    private static partial Regex CompiledLine();
}
