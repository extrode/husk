using System.Text.Json;
using System.Text.RegularExpressions;

namespace Extrode.Husk.Integration;

public sealed partial class PageBuildTests
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

    [Fact]
    public void A_page_removed_from_Content_is_not_built_or_published()
    {
        using var project = new TempProject();
        project.Project(items: "<Content Remove=\"wwwroot/draft.html\" />");
        project.Put("wwwroot/index.html", Marked);
        project.Put("wwwroot/draft.html", Unmarked);

        var (exit, output) = project.Run("publish", "-nologo", "-o", "out", "-nodeReuse:false", "--disable-build-servers");

        Assert.True(exit == 0, output);
        Assert.True(File.Exists(project.P("out/wwwroot/index.html")), output);
        Assert.False(File.Exists(project.P("obj/Release/net10.0/pare/draft.html")), output);
        Assert.False(File.Exists(project.P("out/wwwroot/draft.html")), output);
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
        Assert.Matches(@"^/Plain\.svelte\.[0-9a-f]{10}\.js$", imports.GetProperty("/Plain.svelte").GetString());
        Assert.Matches(@"^/_husk/esm-env/false\.[0-9a-f]{10}\.js$", imports.GetProperty("esm-env/development").GetString());
        Assert.EndsWith("</script></head>\n<body></body>\n", page);
        Assert.Equal(File.ReadAllBytes(project.P("wwwroot/docs/plain.html")), plain);
    }

    private const string Entry = "import { mount } from 'svelte';\nimport Plain from './Plain.svelte';\n\nmount(Plain, { target: document.body, props: { label: 'x' } });\n";
    private const string WithEntry = "<!doctype html>\n<head>\n  <script type=\"importmap\" data-husk></script>\n  <script type=\"module\" src=\"/main.js\"></script>\n</head>\n";

    [Fact]
    public async Task A_release_publish_serves_hashed_copies_that_the_map_the_entry_and_the_preloads_name()
    {
        using var project = new TempProject();
        project.Project();
        project.Fixture("wwwroot/Plain.svelte");
        project.Put("wwwroot/main.ts", Entry);
        project.Put("wwwroot/index.html", WithEntry);
        var (exit, output) = project.Run("publish", "-nologo", "-c", "Release", "-o", "out", "-nodeReuse:false", "--disable-build-servers");
        Assert.True(exit == 0, output);

        using var server = new StaticServer(project.P("out/wwwroot"));
        using var http = new HttpClient { BaseAddress = server.Address };
        string page = await http.GetStringAsync("index.html", TestContext.Current.CancellationToken);
        var imports = InlinedMap(page);
        string main = imports.GetProperty("/main.js").GetString()!;
        string plain = imports.GetProperty("/Plain.svelte").GetString()!;
        Assert.Matches(@"^/main\.[0-9a-f]{10}\.js$", main);
        Assert.Contains($"<script type=\"module\" src=\"{main}\"></script>", page);
        Assert.Equal(plain, imports.GetProperty("/Plain.svelte.js").GetString());
        Assert.Equal(plain, imports.GetProperty("/Plain").GetString());
        // The copies are the compiled bytes: only the name differs.
        Assert.Equal(File.ReadAllBytes(project.P("out/wwwroot/Plain.svelte.js")), await http.GetByteArrayAsync(plain.TrimStart('/'), TestContext.Current.CancellationToken));
        Assert.Equal(File.ReadAllBytes(project.P("out/wwwroot/main.js")), await http.GetByteArrayAsync(main.TrimStart('/'), TestContext.Current.CancellationToken));

        var preloads = PreloadLine().Matches(page).Select(m => m.Groups[1].Value).ToList();
        Assert.Equal([main, imports.GetProperty("svelte").GetString()!], preloads[..2]);
        Assert.Contains(plain, preloads);
        Assert.Contains(imports.GetProperty("svelte/internal/client").GetString()!, preloads);
        Assert.All(preloads, p => Assert.Matches(@"\.[0-9a-f]{10}\.m?js$", p));
        foreach (var preload in preloads)
            Assert.True((await http.GetAsync(preload.TrimStart('/'), TestContext.Current.CancellationToken)).IsSuccessStatusCode, preload);
    }

    [Fact]
    public void A_debug_build_keeps_plain_names_and_preloads_nothing()
    {
        using var project = new TempProject();
        project.Project();
        project.Fixture("wwwroot/Plain.svelte");
        project.Put("wwwroot/main.ts", Entry);
        project.Put("wwwroot/index.html", WithEntry);

        var (exit, output) = project.Build();

        Assert.True(exit == 0, output);
        string page = project.Read(TempProject.PareOut + "index.html");
        Assert.Equal("/Plain.svelte.js", InlinedMap(page).GetProperty("/Plain.svelte").GetString());
        Assert.Contains("<script type=\"module\" src=\"/main.js\"></script>", page);
        Assert.DoesNotContain("modulepreload", page);
        Assert.False(Directory.Exists(project.P(TempProject.HuskOut + "hashed")) && Directory.EnumerateFiles(project.P(TempProject.HuskOut + "hashed"), "*", SearchOption.AllDirectories).Any());
    }

    [GeneratedRegex("<link rel=\"modulepreload\" href=\"([^\"]+)\">")]
    private static partial Regex PreloadLine();
}
