using OpenQA.Selenium;

namespace Extrode.Husk.Integration;

/// <summary>The router app in each browser: a fixture mirroring saaaat's route shapes, published and served
/// with a single-page fallback.</summary>
[Collection(nameof(PackedFeed))]
public sealed class RoutesBrowserTests : IDisposable
{
    private readonly TempProject _project = new();
    private readonly StaticServer _server;

    public RoutesBrowserTests()
    {
        _project.Project(items: """
            <HuskComponent Include="src/**/*.svelte" PareServeAt="app" />
            <HuskModule Include="src/**/*.svelte.ts" PareServeAt="app" />
            <TypeScriptSource Include="src/**/*.ts" Exclude="src/**/*.svelte.ts" PareServeAt="app" />
            <HuskRoutes Include="src/routes" />
            <HuskAlias Include="$lib" Path="src/lib" />
            """);
        string fixture = Path.Combine(TempProject.Fixtures, "router-app");
        foreach (string file in Directory.EnumerateFiles(fixture, "*", SearchOption.AllDirectories))
            _project.Put(Path.GetRelativePath(fixture, file).Replace('\\', '/'), File.ReadAllText(file));
        var (exit, output) = _project.Run("publish", "-nologo", "-c", "Release", "-o", "out", "-nodeReuse:false", "--disable-build-servers");
        Assert.True(exit == 0, output);
        _server = new StaticServer(_project.P("out/wwwroot"), spa: true);
    }

    public void Dispose()
    {
        _server.Dispose();
        _project.Dispose();
    }

    private static object? Script(IWebDriver driver, string script) => ((IJavaScriptExecutor)driver).ExecuteScript(script);

    private static string Text(IWebDriver driver, string css) => driver.FindElement(By.CssSelector(css)).Text;

    private IWebDriver Open(string browser, string path)
    {
        var driver = Browsers.Start(browser);
        try
        {
            driver.Navigate().GoToUrl(new Uri(_server.Address, path).ToString());
            Browsers.Until(driver, "return document.querySelector('#title, #error') ? true : null;");
            return driver;
        }
        catch
        {
            driver.Dispose();
            throw;
        }
    }

    private static void Settled(IWebDriver driver, string title) =>
        Browsers.Until(driver, $"return document.querySelector('#title')?.textContent === '{title}' ? true : null;");

    [Theory]
    [MemberData(nameof(Browsers.All), MemberType = typeof(Browsers))]
    public void A_link_navigates_in_place_and_back_returns(string browser)
    {
        using var driver = Open(browser, "/");
        Script(driver, "window.marker = 'kept';");

        driver.FindElement(By.Id("to-month")).Click();
        Settled(driver, "month");
        Assert.Equal("kept", Script(driver, "return window.marker;"));
        Assert.EndsWith("/month", driver.Url);
        Assert.StartsWith("/month ", Text(driver, "#where"));

        driver.Navigate().Back();
        Settled(driver, "home");
        Assert.Equal("kept", Script(driver, "return window.marker;"));
        Assert.StartsWith("/ ", Text(driver, "#where"));
    }

    [Theory]
    [MemberData(nameof(Browsers.All), MemberType = typeof(Browsers))]
    public void A_cancelled_back_after_a_reload_returns_to_the_page_on_screen(string browser)
    {
        using var driver = Open(browser, "/");
        driver.FindElement(By.Id("to-month")).Click();
        Settled(driver, "month");
        driver.Navigate().Refresh();
        Settled(driver, "month");
        Script(driver, "window.__cancel = true;");

        driver.Navigate().Back();
        Thread.Sleep(1500);

        Assert.EndsWith("/month", driver.Url);
        Assert.Equal("month", Text(driver, "#title"));
    }

    [Theory]
    [MemberData(nameof(Browsers.All), MemberType = typeof(Browsers))]
    public void A_refreshed_address_and_a_trailing_slash_reach_their_page(string browser)
    {
        using var driver = Open(browser, "/month/");
        Assert.Equal("month", Text(driver, "#title"));
        Assert.Equal("true", Text(driver, "#where").Split(' ')[^1]);
    }

    [Theory]
    [MemberData(nameof(Browsers.All), MemberType = typeof(Browsers))]
    public void A_load_redirect_lands_on_its_target(string browser)
    {
        using var driver = Open(browser, "/settings/nonsense");
        Settled(driver, "settings appearance");
        Assert.EndsWith("/settings/appearance", driver.Url);
    }

    [Theory]
    [MemberData(nameof(Browsers.All), MemberType = typeof(Browsers))]
    public void An_unknown_address_shows_the_error_page_with_status_404(string browser)
    {
        using var driver = Open(browser, "/no/such/page");
        Assert.Equal("404", Text(driver, "#error"));
    }

    [Theory]
    [MemberData(nameof(Browsers.All), MemberType = typeof(Browsers))]
    public void Goto_with_replaceState_updates_page_and_leaves_history_alone(string browser)
    {
        using var driver = Open(browser, "/query");
        long before = (long)Script(driver, "return history.length;")!;

        driver.FindElement(By.Id("set")).Click();
        Browsers.Until(driver, "return document.querySelector('#year').textContent === '1447' ? true : null;");

        Assert.Equal(before, (long)Script(driver, "return history.length;")!);
        Assert.EndsWith("/query?year=1447", driver.Url);
    }

    [Theory]
    [MemberData(nameof(Browsers.All), MemberType = typeof(Browsers))]
    public void A_cancelling_beforeNavigate_leaves_address_and_page(string browser)
    {
        using var driver = Open(browser, "/");
        Script(driver, "window.__cancel = true;");

        driver.FindElement(By.Id("to-month")).Click();
        Thread.Sleep(300);

        Assert.Equal("home", Text(driver, "#title"));
        Assert.False(driver.Url.EndsWith("/month", StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(Browsers.All), MemberType = typeof(Browsers))]
    public void A_fragment_link_runs_no_navigation_and_updates_the_address(string browser)
    {
        using var driver = Open(browser, "/month");
        Script(driver, "window.marker = 'kept';");

        driver.FindElement(By.Id("to-skip")).Click();

        Browsers.Until(driver, "return location.hash === '#main' ? true : null;");
        Assert.Equal("kept", Script(driver, "return window.marker;"));
        Assert.Equal("month", Text(driver, "#title"));
    }

    [Fact]
    public async Task A_module_address_with_no_file_is_a_404_and_not_the_page()
    {
        using var http = new HttpClient();
        var response = await http.GetAsync(new Uri(_server.Address, "/_husk/nothing.js"), TestContext.Current.CancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }
}
