using System.Text.Json;

namespace Extrode.Husk.Integration;

public sealed class PageBuildTests
{
    private const string Marked = "<!doctype html>\n<head><script type=\"importmap\" data-husk></script></head>\n<body></body>\n";
    private const string Unmarked = "<!doctype html>\r\n<p>plain é</p>";

    private static TempProject App()
    {
        var project = new TempProject();
        project.Project();
        project.Fixture("wwwroot/Plain.svelte");
        project.Put("wwwroot/index.html", Marked);
        project.Put("wwwroot/docs/plain.html", Unmarked);
        return project;
    }

    private static JsonElement InlinedMap(string html)
    {
        const string Open = "<script type=\"importmap\" data-husk>";
        int start = html.IndexOf(Open, StringComparison.Ordinal) + Open.Length;
        return JsonDocument.Parse(html[start..html.IndexOf("</script>", start, StringComparison.Ordinal)]).RootElement.GetProperty("imports");
    }

    [Fact]
    public void A_new_component_rewrites_the_map_of_an_untouched_page()
    {
        using var project = App();
        Assert.Equal(0, project.Build().Exit);
        Assert.False(InlinedMap(project.Read(TempProject.PareOut + "index.html")).TryGetProperty("/Styled.svelte", out _));
        var source = project.Stamp("wwwroot/index.html");

        project.Fixture("wwwroot/Styled.svelte");
        var (exit, output) = project.Build();

        Assert.True(exit == 0, output);
        Assert.Equal("/Styled.svelte.js", InlinedMap(project.Read(TempProject.PareOut + "index.html")).GetProperty("/Styled.svelte").GetString());
        Assert.Equal(source, project.Stamp("wwwroot/index.html"));
        Assert.Equal(Marked, project.Read("wwwroot/index.html"));
    }

    [Fact]
    public void A_build_with_nothing_changed_rewrites_no_page()
    {
        using var project = App();
        Assert.Equal(0, project.Build().Exit);
        var stamps = new[] { "index.html", "docs/plain.html" }.Select(p => project.Stamp(TempProject.PareOut + p)).ToArray();

        var (exit, output) = project.Build();

        Assert.True(exit == 0, output);
        Assert.Equal(stamps, new[] { "index.html", "docs/plain.html" }.Select(p => project.Stamp(TempProject.PareOut + p)));
    }

    [Theory]
    [InlineData("<PareEmbed>true</PareEmbed>")]
    [InlineData("<PareOutDir>gen</PareOutDir>")]
    public void An_app_not_served_from_obj_keeps_its_pages_as_they_are(string properties)
    {
        using var project = new TempProject();
        project.Project(properties);
        project.Put("wwwroot/index.html", Marked);

        var (exit, output) = project.Run("publish", "-nologo", "-o", "out", "-nodeReuse:false", "--disable-build-servers");

        Assert.True(exit == 0, output);
        Assert.False(File.Exists(project.P(TempProject.PareOut + "index.html")), output);
        Assert.Equal(Marked, project.Read("out/wwwroot/index.html"));
    }

    [Fact]
    public void Husk_targets_imported_before_Pare_still_see_the_outputs()
    {
        using var project = new TempProject();
        project.Project(huskFirst: true);
        project.Fixture("wwwroot/Plain.svelte");
        project.Put("wwwroot/index.html", Marked);

        var (exit, output) = project.Build();

        Assert.True(exit == 0, output);
        Assert.Equal("/Plain.svelte.js", InlinedMap(project.Read(TempProject.PareOut + "index.html")).GetProperty("/Plain.svelte").GetString());
    }

    [Fact]
    public async Task Published_pages_are_served_inlined_or_byte_identical()
    {
        using var project = App();
        var (exit, output) = project.Run("publish", "-nologo", "-c", "Release", "-o", "out", "-nodeReuse:false", "--disable-build-servers");
        Assert.True(exit == 0, output);

        using var server = new StaticServer(project.P("out/wwwroot"));
        using var http = new HttpClient { BaseAddress = server.Address };
        string page = await http.GetStringAsync("index.html", TestContext.Current.CancellationToken);
        byte[] plain = await http.GetByteArrayAsync("docs/plain.html", TestContext.Current.CancellationToken);

        var imports = InlinedMap(page);
        Assert.Equal("/Plain.svelte.js", imports.GetProperty("/Plain.svelte").GetString());
        Assert.Equal("/_husk/esm-env/false.js", imports.GetProperty("esm-env/development").GetString());
        Assert.EndsWith("</script></head>\n<body></body>\n", page);
        Assert.Equal(File.ReadAllBytes(project.P("wwwroot/docs/plain.html")), plain);
    }
}
