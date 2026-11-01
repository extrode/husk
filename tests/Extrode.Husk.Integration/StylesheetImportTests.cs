using OpenQA.Selenium;

namespace Extrode.Husk.Integration;

/// <summary>A side-effect stylesheet import, <c>import '$lib/app.css'</c> (spec 003 R11): the CSS reaches the page
/// ahead of the components' own, and a listed stylesheet that is missing fails the build.</summary>
[Collection(nameof(PackedFeed))]
public sealed class StylesheetImportTests
{
    private static TempProject App(string stylesheet = "src/lib/app.css")
    {
        var project = new TempProject();
        project.Project(items: $"""
            <HuskComponent Include="src/**/*.svelte" PareServeAt="app" />
            <HuskRoutes Include="src/routes" />
            <HuskAlias Include="$lib" Path="src/lib" />
            <HuskStylesheet Include="{stylesheet}" />
            """);
        project.Put("src/lib/app.css", "h1 { color: rgb(1, 2, 3); } p { color: rgb(9, 9, 9); } em { color: rgb(9, 9, 9); }\n");
        project.Put("src/routes/+layout.svelte", "<script>import '$lib/app.css'; let { children } = $props();</script>\n<main>{@render children()}</main>\n");
        project.Put("src/routes/+page.svelte", "<h1>home</h1><p>text</p>\n<em>e</em>\n<style>:global(em) { color: rgb(5, 6, 7); }</style>\n");
        return project;
    }

    [Fact]
    public void A_stylesheet_that_is_missing_fails_the_build_with_its_path()
    {
        using var project = App("src/lib/missing.css");

        var (exit, output) = project.Build();

        Assert.NotEqual(0, exit);
        Assert.Contains("HUSK103", output);
        Assert.Contains("missing.css", output);
    }

    [Fact]
    public void A_stylesheet_under_no_alias_fails_the_build()
    {
        using var project = App();
        project.Put("loose/other.css", "a { color: red; }\n");
        project.Put("app.csproj", project.Read("app.csproj").Replace("src/lib/app.css", "loose/other.css"));

        var (exit, output) = project.Build();

        Assert.NotEqual(0, exit);
        Assert.Contains("HUSK105", output);
    }

    [Fact]
    public void A_stylesheet_becomes_a_module_the_import_map_resolves()
    {
        using var project = App();

        var (exit, output) = project.Build();

        Assert.True(exit == 0, output);
        string map = project.Read(TempProject.HuskOut + "importmap.json");
        Assert.Contains("\"$lib/app.css\": \"/_husk/css/app.css.", map);
        Assert.Contains("h1 { color: rgb(1, 2, 3); }", string.Join("", Directory.EnumerateFiles(project.P(TempProject.HuskOut + "css")).Select(File.ReadAllText)));
    }

    [Theory]
    [InlineData("chrome")]
    [InlineData("firefox")]
    [InlineData("safari")]
    public void The_imported_styles_apply_and_a_component_style_still_wins_over_them(string browser)
    {
        using var project = App();
        var (exit, output) = project.Run("publish", "-nologo", "-c", "Release", "-o", "out", "-nodeReuse:false", "--disable-build-servers");
        Assert.True(exit == 0, output);
        using var server = new StaticServer(project.P("out/wwwroot"), spa: true);
        using var driver = Browsers.Start(browser);
        driver.Navigate().GoToUrl(server.Address.ToString());
        var js = (IJavaScriptExecutor)driver;
        Browsers.Until(driver, "return document.querySelector('h1') ? true : null;");
        Assert.Equal("rgb(1, 2, 3)", js.ExecuteScript("return getComputedStyle(document.querySelector('h1')).color"));
        Assert.Equal("rgb(9, 9, 9)", js.ExecuteScript("return getComputedStyle(document.querySelector('p')).color"));
        Assert.Equal("rgb(5, 6, 7)", js.ExecuteScript("return getComputedStyle(document.querySelector('em')).color"));
    }
}
