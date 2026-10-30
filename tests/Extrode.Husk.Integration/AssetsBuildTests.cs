using System.Text.Json;

namespace Extrode.Husk.Integration;

public sealed class AssetsBuildTests
{
    private static TempProject App()
    {
        var project = new TempProject();
        project.Project();
        foreach (var file in new[] { "Styled.svelte", "Plain.svelte", "clock.svelte.js", "counter.svelte.ts", "format.ts" })
            project.Fixture("wwwroot/" + file);
        return project;
    }

    private static List<string> Routes(string endpointsJson) =>
        [.. JsonDocument.Parse(endpointsJson).RootElement.GetProperty("Endpoints").EnumerateArray().Select(e => e.GetProperty("Route").GetString()!)];

    [Fact]
    public void The_stylesheet_import_map_and_runtime_are_served_below_husk()
    {
        using var project = App();

        var (exit, output) = project.Build();

        Assert.True(exit == 0, output);
        var routes = Routes(project.Read("bin/Debug/net10.0/app.staticwebassets.endpoints.json"));
        Assert.Contains("_husk/husk.css", routes);
        Assert.Contains("_husk/importmap.json", routes);
        Assert.Contains("_husk/svelte/src/internal/client/index.js", routes);
        Assert.Contains("_husk/esm-env/index.js", routes);
        Assert.Contains("_husk/clsx/dist/clsx.mjs", routes);
        Assert.DoesNotContain(routes, r => r.Contains("server", StringComparison.Ordinal));
        Assert.Contains("Styled.svelte.js", routes);
        Assert.DoesNotContain("Styled.svelte", routes);

        Assert.StartsWith("/* Styled.svelte */\n", project.Read(TempProject.HuskOut + "husk.css"));
        var imports = JsonDocument.Parse(project.Read(TempProject.HuskOut + "importmap.json")).RootElement.GetProperty("imports");
        Assert.Equal("/Styled.svelte.js", imports.GetProperty("/Styled.svelte").GetString());
        Assert.Equal("/counter.svelte.js", imports.GetProperty("/counter.svelte.ts").GetString());
        Assert.Equal("/format.js", imports.GetProperty("/format.ts").GetString());
        Assert.Equal("/_husk/svelte/src/internal/client/index.js", imports.GetProperty("svelte/internal/client").GetString());
        Assert.Equal("/_husk/esm-env/true.js", imports.GetProperty("esm-env/development").GetString());
    }

    [Fact]
    public void A_style_change_in_one_component_reaches_the_stylesheet_alone()
    {
        using var project = App();
        Assert.Equal(0, project.Build().Exit);
        var plainStamp = project.Stamp(TempProject.PareOut + "Plain.svelte.js");
        var mapStamp = project.Stamp(TempProject.HuskOut + "importmap.json");

        project.Put("wwwroot/Styled.svelte", project.Read("wwwroot/Styled.svelte").Replace("</style>", ".added { color: red; }\n</style>"));
        var (exit, output) = project.Build();

        Assert.True(exit == 0, output);
        Assert.Contains(".added", project.Read(TempProject.HuskOut + "husk.css"));
        Assert.Equal(plainStamp, project.Stamp(TempProject.PareOut + "Plain.svelte.js"));
        Assert.Equal(mapStamp, project.Stamp(TempProject.HuskOut + "importmap.json"));
    }

    [Fact]
    public void The_development_map_reaches_the_build_output_and_not_a_publish()
    {
        using var project = App();

        var (exit, output) = project.Run("publish", "-nologo", "-c", "Release", "-o", "out", "-nodeReuse:false", "--disable-build-servers");

        Assert.True(exit == 0, output);
        Assert.False(File.Exists(project.P("out/Extrode.Husk.development.json")), output);
        Assert.True(File.Exists(project.P("out/wwwroot/_husk/husk.css")), output);
        Assert.True(File.Exists(project.P("out/wwwroot/_husk/husk.css.br")), output);
        Assert.True(File.Exists(project.P("out/wwwroot/_husk/svelte/src/internal/client/index.js.gz")), output);
        Assert.True(File.Exists(project.P("out/wwwroot/Styled.svelte.js.br")), output);
        Assert.Empty(Directory.EnumerateFiles(project.P("out"), "*.svelte", SearchOption.AllDirectories));
        Assert.Empty(Directory.EnumerateFiles(project.P("out"), "*.svelte.ts", SearchOption.AllDirectories));
        var imports = JsonDocument.Parse(File.ReadAllText(project.P("out/wwwroot/_husk/importmap.json"))).RootElement.GetProperty("imports");
        Assert.Matches(@"^/_husk/esm-env/false\.[0-9a-f]{10}\.js$", imports.GetProperty("esm-env/development").GetString());

        Assert.Equal(0, project.Build().Exit);
        var map = JsonDocument.Parse(project.Read("bin/Debug/net10.0/Extrode.Husk.development.json")).RootElement;
        Assert.True(map.GetProperty("dev").GetBoolean());
        Assert.EndsWith("Styled.svelte.css", Assert.Single(map.GetProperty("styles").EnumerateArray(),
            s => s.GetProperty("name").GetString() == "Styled.svelte").GetProperty("css").GetString());
        Assert.True(File.Exists(map.GetProperty("compiler").GetString()));
        Assert.True(File.Exists(map.GetProperty("svelte").GetString()));
    }

    [Theory]
    [InlineData("linux-musl-x64")]
    [InlineData("alpine.3.20-arm64")]
    [InlineData("linux-x86")]
    public void An_unsupported_build_machine_is_HUSK001_naming_it_and_the_six(string rid)
    {
        using var project = App();

        var (exit, output) = project.Build("-p:NETCoreSdkRuntimeIdentifier=" + rid);

        Assert.NotEqual(0, exit);
        Assert.Contains($"error HUSK001: Husk cannot build on {rid}:", output);
        Assert.Contains("win-x64, win-arm64, osx-x64, osx-arm64, linux-x64 and linux-arm64", output);
    }

    [Theory]
    [InlineData("ubuntu.24.04-x64")]
    [InlineData("arch-x64")]
    [InlineData("osx-arm64")]
    public void A_supported_build_machine_passes_the_gate(string rid)
    {
        using var project = App();

        var (exit, output) = project.Build("-p:NETCoreSdkRuntimeIdentifier=" + rid);

        Assert.DoesNotContain("HUSK001", output);
        Assert.True(exit == 0, output);
    }
}
