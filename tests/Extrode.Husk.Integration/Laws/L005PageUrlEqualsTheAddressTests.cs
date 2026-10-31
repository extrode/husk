using OpenQA.Selenium;

namespace Extrode.Husk.Integration.Laws;

/// <summary>L005: after any sequence of in-app navigations (link, goto, goto with replaceState, back,
/// forward, load redirect, cancelled navigation), page.url.href equals location.href.</summary>
[Trait("Law", "L005")]
[Collection(nameof(PackedFeed))]
public sealed class L005PageUrlEqualsTheAddressTests : IDisposable
{
    private const int Seed = 5005;
    private readonly TempProject _project = new();
    private readonly StaticServer _server;

    public L005PageUrlEqualsTheAddressTests()
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

    private static readonly string[] Links = ["to-month", "to-home", "to-query"];
    private static readonly string[] Targets = ["/", "/month", "/query", "/query?year=1447", "?year=7", "/settings/about", "/settings/nonsense"];

    private static string Navigate(IWebDriver driver, Random random, bool cancelling)
    {
        int kind = random.Next(cancelling ? 4 : 6);
        if (kind == 2 && (long)Script(driver, "return history.state?.idx ?? 0;")! == 0)
            kind = 0;
        switch (kind)
        {
            case 0:
                string link = Links[random.Next(Links.Length)];
                driver.FindElement(By.Id(link)).Click();
                return "click " + link;
            case 1:
                string target = Targets[random.Next(Targets.Length)];
                Script(driver, $"window.__goto('{target}');");
                return "goto " + target;
            case 2:
                driver.Navigate().Back();
                return "back";
            case 3:
                driver.Navigate().Forward();
                return "forward";
            case 4:
                string replaced = Targets[random.Next(Targets.Length)];
                Script(driver, $"window.__goto('{replaced}', {{ replaceState: true }});");
                return "goto replaceState " + replaced;
            default:
                driver.FindElement(By.Id("to-bad")).Click();
                return "redirect /settings/nonsense";
        }
    }

    private static (string Address, string Page) Settle(IWebDriver driver)
    {
        var until = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        string? last = null;
        for (int same = 0; same < 5; )
        {
            Thread.Sleep(50);
            string now = (string)Script(driver, "return location.href + '\\n' + (document.querySelector('#href')?.textContent ?? '') + '\\n' + (document.querySelector('#title, #error')?.textContent ?? '');")!;
            same = now == last ? same + 1 : 0;
            last = now;
            if (DateTime.UtcNow > until)
                throw new TimeoutException("never settled: " + now);
        }
        string[] parts = last!.Split('\n');
        return (parts[0], parts[1]);
    }

    [Fact]
    public void After_each_step_of_a_random_sequence_page_url_equals_the_address()
    {
        using var driver = Browsers.Start("chrome");
        var random = new Random(Seed);
        for (int sequence = 0; sequence < 24; sequence++)
        {
            driver.Navigate().GoToUrl(_server.Address.ToString());
            Browsers.Until(driver, "return document.querySelector('#title') ? true : null;");
            var applied = new List<string> { "load /" };
            for (int step = 0; step < 8; step++)
            {
                bool cancelling = random.Next(5) == 0;
                if (cancelling)
                    Script(driver, "window.__cancel = true;");
                string name = Navigate(driver, random, cancelling);
                applied.Add(cancelling ? "cancelled " + name : name);
                var (address, page) = Settle(driver);
                if (cancelling)
                    Script(driver, "window.__cancel = false;");
                Assert.True(address == page,
                    $"seed {Seed}, sequence {sequence}, after {string.Join(", ", applied)}: location.href is {address} and page.url.href is {page}");
            }
        }
    }
}
