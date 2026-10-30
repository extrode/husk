using System.Text.Json;
using OpenQA.Selenium;

namespace Extrode.Husk.Integration;

/// <summary>The sample in each browser, running in Development from the packed feed.</summary>
[Collection(nameof(PackedFeed))]
public sealed class SampleBrowserTests(PackedFeed feed)
{
    private static object? Script(IWebDriver driver, string script) => ((IJavaScriptExecutor)driver).ExecuteScript(script);

    private static object? Async(IWebDriver driver, string script) => ((IJavaScriptExecutor)driver).ExecuteAsyncScript(script);

    private static string Text(IWebDriver driver, string css) => driver.FindElement(By.CssSelector(css)).Text;

    // A module script the page runs, so its imports go through the page's import map: Firefox
    // resolves import() in a WebDriver script without it. The module sets window.result.
    private static object Module(IWebDriver driver, string module)
    {
        Script(driver, "window.result = undefined; const s = document.createElement('script'); s.type = 'module'; s.textContent = arguments[0]; document.head.append(s);"
            .Replace("arguments[0]", JsonSerializer.Serialize(module), StringComparison.Ordinal));
        return Browsers.Until(driver, "return window.result === undefined ? null : window.result;");
    }

    private IWebDriver Open(string browser)
    {
        var driver = Browsers.Start(browser);
        try
        {
            driver.Navigate().GoToUrl(feed.Development.App.Address);
            Browsers.Until(driver, "return document.querySelector('#increment') ? true : null;");
            return driver;
        }
        catch
        {
            driver.Dispose();
            throw;
        }
    }

    [Theory]
    [MemberData(nameof(Browsers.All), MemberType = typeof(Browsers))]
    public void The_sample_works_and_its_styles_apply(string browser)
    {
        using var driver = Open(browser);

        Assert.Equal("clicked 0 times", Text(driver, "#increment"));
        driver.FindElement(By.Id("increment")).Click();
        Browsers.Until(driver, "return document.querySelector('#increment').textContent === 'clicked 1 time' ? true : null;");
        Assert.Equal("child sees 1", Text(driver, ".child"));
        Assert.Equal("total 3", Text(driver, "#total"));

        driver.FindElement(By.Id("add")).Click();
        Browsers.Until(driver, "return document.querySelector('li[data-id=\"3\"]') ? true : null;");
        driver.FindElement(By.CssSelector("li[data-id=\"1\"] .remove")).Click();
        Browsers.Until(driver, "return document.querySelector('li[data-id=\"1\"]') ? null : true;");
        Assert.Equal(["2", "3"], driver.FindElements(By.CssSelector("li")).Select(li => li.GetAttribute("data-id")));

        Assert.NotNull(driver.FindElement(By.Id("faded")));
        driver.FindElement(By.Id("toggle")).Click();
        Browsers.Until(driver, "return document.querySelector('#faded') ? null : true;");

        Assert.Equal("rgb(255, 62, 0)", Script(driver, "return getComputedStyle(document.querySelector('h1')).color;"));
        Assert.Equal("rgb(0, 128, 0)", Script(driver, "return getComputedStyle(document.querySelector('.child')).color;"));
        if (browser == "chrome")
            Assert.Empty(driver.Manage().Logs.GetLog(LogType.Browser).Where(e => e.Level == LogLevel.Severe).Select(e => e.Message));
    }

    [Theory]
    [MemberData(nameof(Browsers.All), MemberType = typeof(Browsers))]
    public void In_Development_the_runtime_is_the_dev_build(string browser)
    {
        using var driver = Open(browser);

        Assert.Equal(true, Module(driver, "import { DEV } from 'esm-env'; window.result = DEV;"));
        // A dev build words this warning; a production build logs only its URL.
        string warning = (string)Module(driver, """
            import * as $ from 'svelte/internal/client';
            const warnings = [];
            const warn = console.warn;
            console.warn = (...args) => warnings.push(args.join(' '));
            try { const raw = {}; $.strict_equals($.proxy(raw), raw); } finally { console.warn = warn; }
            window.result = warnings.join('\n');
            """);
        Assert.StartsWith("%c[svelte] state_proxy_equality_mismatch", warning, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Browsers.All), MemberType = typeof(Browsers))]
    public void The_source_map_maps_a_token_back_to_its_svelte_line(string browser)
    {
        using var driver = Open(browser);
        string source = File.ReadAllText(feed.Development.Project.P("wwwroot/App.svelte"));

        var fetched = (IReadOnlyDictionary<string, object>)Async(driver, """
            const done = arguments[0];
            (async () => {
              const js = await (await fetch('/App.svelte.js')).text();
              const name = js.match(/\/\/# sourceMappingURL=(\S+)\s*$/)[1];
              const mapUrl = new URL(name, new URL('/App.svelte.js', location.href)).href;
              const map = await (await fetch(mapUrl)).text();
              return { js, map, mapUrl };
            })().then(done, e => done({ error: String(e) }));
            """)!;
        Assert.False(fetched.ContainsKey("error"), fetched.TryGetValue("error", out var error) ? (string)error : "");
        string js = (string)fetched["js"];
        using var map = JsonDocument.Parse((string)fetched["map"]);
        var root = map.RootElement;

        var origin = feed.Development.App.Address;
        Assert.Equal(new Uri(origin, "/App.svelte.js.map"), new Uri((string)fetched["mapUrl"]));
        Assert.Equal(new Uri(origin, "/App.svelte"), new Uri(new Uri((string)fetched["mapUrl"]), root.GetProperty("sources")[0].GetString()));
        Assert.Equal(source, root.GetProperty("sourcesContent")[0].GetString());

        string token = "clicks(shared.count)";
        int sourceLine = source.Split('\n').Select((l, i) => (l, i)).Single(x => x.l.Contains("{" + token + "}", StringComparison.Ordinal)).i;
        var generated = js.Split('\n').Select((l, i) => (l, i)).First(x => x.l.Contains(token, StringComparison.Ordinal));
        int column = generated.l.IndexOf(token, StringComparison.Ordinal);
        var segment = SourceMap.Decode(root.GetProperty("mappings").GetString()!)[generated.i].Last(s => s.Column <= column);
        Assert.Equal(sourceLine, segment.SourceLine);
    }

    [Theory]
    [MemberData(nameof(Browsers.All), MemberType = typeof(Browsers))]
    public void The_published_sample_works_from_static_files_loading_hashed_modules_only(string browser)
    {
        using var sample = feed.Sample();
        var (exit, output) = sample.Run("publish", "-nologo", "-c", "Release", "-o", "out", "-nodeReuse:false", "--disable-build-servers");
        Assert.True(exit == 0, output);
        using var server = new StaticServer(sample.P("out/wwwroot"));
        using var driver = Browsers.Start(browser);

        driver.Navigate().GoToUrl(server.Address);
        Browsers.Until(driver, "return document.querySelector('#increment') ? true : null;");
        driver.FindElement(By.Id("increment")).Click();
        Browsers.Until(driver, "return document.querySelector('#increment').textContent === 'clicked 1 time' ? true : null;");
        Assert.Equal("child sees 1", Text(driver, ".child"));

        var modules = ((IEnumerable<object>)Script(driver, "return performance.getEntriesByType('resource').map(e => new URL(e.name).pathname).filter(p => /\\.m?js$/.test(p));")!)
            .Cast<string>().ToList();
        Assert.Contains(modules, m => m.StartsWith("/_husk/svelte/", StringComparison.Ordinal));
        Assert.All(modules, m => Assert.Matches(@"\.[0-9a-f]{10}\.m?js$", m));
    }
}

/// <summary>Source map v3 <c>mappings</c>, decoded per generated line.</summary>
internal static class SourceMap
{
    public readonly record struct Segment(int Column, int Source, int SourceLine, int SourceColumn);

    private const string Base64 = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";

    public static List<List<Segment>> Decode(string mappings)
    {
        var lines = new List<List<Segment>>();
        int source = 0, sourceLine = 0, sourceColumn = 0;
        foreach (string line in mappings.Split(';'))
        {
            var segments = new List<Segment>();
            int column = 0;
            foreach (string text in line.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var fields = Vlq(text);
                column += fields[0];
                if (fields.Count >= 4)
                {
                    source += fields[1];
                    sourceLine += fields[2];
                    sourceColumn += fields[3];
                    segments.Add(new Segment(column, source, sourceLine, sourceColumn));
                }
            }
            lines.Add(segments);
        }
        return lines;
    }

    private static List<int> Vlq(string text)
    {
        var values = new List<int>();
        int value = 0, shift = 0;
        foreach (char c in text)
        {
            int digit = Base64.IndexOf(c, StringComparison.Ordinal);
            value += (digit & 31) << shift;
            if ((digit & 32) != 0)
            {
                shift += 5;
                continue;
            }
            values.Add((value & 1) == 1 ? -(value >> 1) : value >> 1);
            value = 0;
            shift = 0;
        }
        return values;
    }
}
