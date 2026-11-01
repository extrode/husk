using OpenQA.Selenium;

namespace Extrode.Husk.Integration;

/// <summary>A JSON module import with an import attribute (spec 003 R11): served, hashed with the modules, edits
/// reach the browser, a missing file fails the build.</summary>
[Collection(nameof(PackedFeed))]
public sealed class JsonImportTests
{
    private static TempProject App(string json = "src/lib/data.json")
    {
        var project = new TempProject();
        project.Project(items: $"""
            <HuskComponent Include="src/**/*.svelte" PareServeAt="app" />
            <TypeScriptSource Include="src/**/*.ts" PareServeAt="app" />
            <HuskRoutes Include="src/routes" />
            <HuskAlias Include="$lib" Path="src/lib" />
            <HuskJson Include="{json}" Root="src" ServeAt="app" />
            """);
        project.Put("src/lib/data.json", "{ \"word\": \"first\" }\n");
        project.Put("src/lib/data.ts", "import data from './data.json' with { type: 'json' };\nexport const word: string = data.word;\n");
        project.Put("src/routes/+page.svelte", "<script>import { word } from '$lib/data.ts';</script>\n<h1>{word}</h1>\n");
        return project;
    }

    [Fact]
    public void A_json_file_that_is_missing_fails_the_build_with_its_path()
    {
        using var project = App("src/lib/missing.json");

        var (exit, output) = project.Build();

        Assert.NotEqual(0, exit);
        Assert.Contains("HUSK104", output);
        Assert.Contains("missing.json", output);
    }

    [Fact]
    public void An_edit_to_the_json_changes_its_hashed_name_and_the_module_graph_does_not_preload_it()
    {
        using var project = App();
        var (exit, output) = project.Build("-p:HuskHashNames=true");
        Assert.True(exit == 0, output);
        string before = project.Read(TempProject.HuskOut + "importmap.json");
        Assert.Contains("/app/lib/data.json\": \"/app/lib/data.", before);

        project.Put("src/lib/data.json", "{ \"word\": \"second\" }\n");
        (exit, output) = project.Build("-p:HuskHashNames=true");
        Assert.True(exit == 0, output);

        Assert.NotEqual(before, project.Read(TempProject.HuskOut + "importmap.json"));
        Assert.DoesNotContain(".json", project.Read(TempProject.HuskOut + "modulegraph.json"));
    }

    [Fact]
    public void A_file_that_is_not_json_fails_the_build()
    {
        using var project = App("src/lib/data.geojson");
        project.Put("src/lib/data.geojson", "{}\n");

        var (exit, output) = project.Build();

        Assert.NotEqual(0, exit);
        Assert.Contains("HUSK104", output);
    }

    [Fact]
    public void A_file_under_wwwroot_fails_the_build_with_a_message_instead_of_an_sdk_crash()
    {
        using var project = App("wwwroot/data/t.json");
        project.Put("wwwroot/data/t.json", "{}\n");
        project.Put("app.csproj", project.Read("app.csproj").Replace("Root=\"src\"", "Root=\"wwwroot\""));

        var (exit, output) = project.Build();

        Assert.NotEqual(0, exit);
        Assert.Contains("HUSK104", output);
        Assert.DoesNotContain("MSB4018", output);
    }

    [Fact]
    public void A_serve_at_that_leaves_the_served_folder_fails_the_build_and_writes_nothing_outside()
    {
        using var project = App();
        project.Put("app.csproj", project.Read("app.csproj").Replace("Root=\"src\" ServeAt=\"app\"", "Root=\"src\" ServeAt=\"../../../..\""));

        var (exit, output) = project.Build();

        Assert.NotEqual(0, exit);
        Assert.Contains("HUSK104", output);
        Assert.Equal("{ \"word\": \"first\" }\n", project.Read("src/lib/data.json"));
    }

    [Theory]
    [InlineData("chrome")]
    [InlineData("firefox")]
    [InlineData("safari")]
    public void The_json_reaches_the_browser_and_an_edit_replaces_it(string browser)
    {
        using var project = App();
        foreach (string word in new[] { "first", "second" })
        {
            project.Put("src/lib/data.json", $"{{ \"word\": \"{word}\" }}\n");
            var (exit, output) = project.Run("publish", "-nologo", "-c", "Release", "-o", "out", "-nodeReuse:false", "--disable-build-servers");
            Assert.True(exit == 0, output);
            using var server = new StaticServer(project.P("out/wwwroot"), spa: true);
            using var driver = Browsers.Start(browser);
            driver.Navigate().GoToUrl(server.Address.ToString());
            Browsers.Until(driver, "return document.querySelector('h1') ? true : null;");
            Assert.Equal(word, driver.FindElement(By.CssSelector("h1")).Text);
        }
    }
}
