using OpenQA.Selenium;

namespace Extrode.Husk.Integration;

/// <summary>The saaaat port project (apps/web-husk on the saaaat branch feat/husk-port, spec 003 T14) in a browser:
/// navigation as in the Vite build (R5), every locale (R7) and the console (R9). Skipped when the port or, for
/// the comparison, the Vite build is absent. HUSK_SAAAAT_PORT names the port project folder, HUSK_SAAAAT_VITE_BUILD
/// a Vite output folder.</summary>
[Trait("Category", "Corpus")]
public sealed class SaaaatPortTests(SaaaatPortTests.Port port) : IClassFixture<SaaaatPortTests.Port>
{
    internal static readonly string[] AllLocales = ["en", "ar", "sq", "bn", "fa", "fr", "ha", "hi", "id", "ja", "kk", "ms", "ps", "ru", "es", "sw", "tr", "ur", "zh"];

    private static readonly string Saaaat = Path.GetFullPath(Path.Combine(TempProject.RepoRoot, "..", "..", "ikhbat.org", "saaaat.com"));

    /// <summary>Publishes the port project once for the class.</summary>
    public sealed class Port : IDisposable
    {
        private TempProject? _scratch;

        public string? Output { get; private set; }

        public Port()
        {
            string? folder = Environment.GetEnvironmentVariable("HUSK_SAAAAT_PORT");
            if (string.IsNullOrEmpty(folder))
                folder = new[] { Path.Combine(Saaaat, ".worktrees", "feat-husk-port", "apps", "web-husk"), Path.Combine(Saaaat, "apps", "web-husk") }
                    .FirstOrDefault(f => File.Exists(Path.Combine(f, "Saaaat.Web.csproj")));
            if (folder is null || !File.Exists(Path.Combine(folder, "Saaaat.Web.csproj")))
                return;
            _scratch = new TempProject();
            var (exit, output) = _scratch.Exec("dotnet", "publish", Path.Combine(folder, "Saaaat.Web.csproj"), "-nologo", "-c", "Release", "-o", _scratch.P("out"), "-nodeReuse:false", "--disable-build-servers");
            if (exit != 0)
                throw new InvalidOperationException("the port project did not publish:\n" + output);
            Output = _scratch.P("out/wwwroot");
        }

        public void Dispose() => _scratch?.Dispose();
    }

    private string PortOutput
    {
        get
        {
            if (port.Output is null)
                Assert.Skip($"no saaaat port project under {Saaaat}");
            return port.Output!;
        }
    }

    private static string? ViteOutput()
    {
        string? folder = Environment.GetEnvironmentVariable(SaaaatParityTests.ViteBuild);
        if (string.IsNullOrEmpty(folder))
            folder = Path.Combine(Saaaat, "apps", "web", "build");
        return Directory.Exists(folder) ? folder : null;
    }

    [Theory]
    [InlineData("chrome")]
    [InlineData("firefox")]
    [InlineData("safari")]
    public void Recording_writes_the_vite_build_text_as_the_reference(string browser)
    {
        if (Environment.GetEnvironmentVariable(SaaaatParityTests.Record) != "1")
            Assert.Skip($"{SaaaatParityTests.Record} is not 1");
        string vite = ViteOutput() ?? throw new InvalidOperationException("no Vite build to record");
        string folder = SaaaatParityTests.ReferenceFolder(SaaaatParityTests.CommitOf(Saaaat));
        Directory.CreateDirectory(folder);
        var seen = SaaaatParityTests.Capture(browser, vite);
        Assert.DoesNotContain(seen, p => p.Value.Length == 0);
        foreach (var (name, text) in seen)
            File.WriteAllText(Path.Combine(folder, name), text + "\n");
    }

    [Theory]
    [InlineData("chrome")]
    [InlineData("firefox")]
    [InlineData("safari")]
    public void Page_text_equals_the_recorded_vite_reference_in_English_and_Arabic(string browser)
    {
        string output = PortOutput;
        if (!Directory.Exists(Saaaat))
            Assert.Skip($"no saaaat checkout at {Saaaat}");
        string sha = SaaaatParityTests.CommitOf(Saaaat);
        string folder = SaaaatParityTests.ReferenceFolder(sha);
        Assert.True(Directory.Exists(folder), $"no reference for saaaat commit {sha}: the checkout moved. Build it with scripts/node-reference/saaaat-vite-build.sh and record with {SaaaatParityTests.Record}=1");
        var differ = new List<string>();
        foreach (var (name, text) in SaaaatParityTests.Capture(browser, output))
        {
            string file = Path.Combine(folder, name);
            Assert.True(File.Exists(file), $"reference {name} is missing for commit {sha}");
            if (File.ReadAllText(file).TrimEnd('\n') != text)
            {
                differ.Add(name);
                string actual = Path.Combine(TempProject.RepoRoot, "tmp", "parity-actual");
                Directory.CreateDirectory(actual);
                File.WriteAllText(Path.Combine(actual, name), text + "\n");
            }
        }
        Assert.True(differ.Count == 0, "port text differs from the Vite reference: " + string.Join(", ", differ));
    }

    private static IWebDriver Driver(string browser)
    {
        var driver = Browsers.Start(browser);
        if (browser != "safari") driver.Manage().Window.Size = new System.Drawing.Size(1280, 900);
        return driver;
    }

    private static string Settled(IWebDriver driver)
    {
        var js = (IJavaScriptExecutor)driver;
        string last = "";
        for (int same = 0, i = 0; same < 5 && i < 75; i++)
        {
            Thread.Sleep(200);
            string now = (string)js.ExecuteScript("return location.pathname + location.search + ' | ' + (document.body.innerText || '').replace(/\\s+/g, ' ').trim().slice(0, 60);")!;
            same = now == last ? same + 1 : 0;
            last = now;
        }
        return last;
    }

    private static List<string> Walk(IWebDriver driver, Uri address)
    {
        var js = (IJavaScriptExecutor)driver;
        var steps = new List<string>();
        driver.Navigate().GoToUrl(new Uri(address, "/today").ToString());
        steps.Add("load /today -> " + Settled(driver));
        js.ExecuteScript("""
            const here = location.pathname;
            const a = [...document.querySelectorAll('a[href^="/"]')].find(x => new URL(x.href).pathname !== here);
            a.click();
            """);
        steps.Add("click a link -> " + Settled(driver));
        driver.Navigate().Back();
        steps.Add("back -> " + Settled(driver));
        driver.Navigate().Forward();
        steps.Add("forward -> " + Settled(driver));
        driver.Navigate().GoToUrl(new Uri(address, "/settings/nonsense").ToString());
        steps.Add("/settings/nonsense -> " + Settled(driver));
        driver.Navigate().GoToUrl(new Uri(address, "/month?year=1447").ToString());
        steps.Add("/month?year=1447 -> " + Settled(driver));
        driver.Navigate().GoToUrl(new Uri(address, "/no/such/page").ToString());
        steps.Add("/no/such/page -> " + Settled(driver));
        return steps;
    }

    [Theory]
    [InlineData("chrome")]
    [InlineData("firefox")]
    [InlineData("safari")]
    public void Navigation_behaves_as_in_the_Vite_build(string browser)
    {
        string? vite = ViteOutput();
        if (vite is null)
            Assert.Skip("no Vite build to compare with");
        string head = SaaaatParityTests.Pinned("en", "latn");
        List<string> expected, actual;
        using (var server = new StaticServer(vite!, spa: true, headScript: head))
        using (var driver = Driver(browser))
            expected = Walk(driver, server.Address);
        using (var server = new StaticServer(PortOutput, spa: true, headScript: head))
        using (var driver = Driver(browser))
            actual = Walk(driver, server.Address);

        Assert.Equal(expected, actual);
        Assert.DoesNotContain(actual, s => s.EndsWith("| ", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("chrome")]
    [InlineData("firefox")]
    [InlineData("safari")]
    public void Every_shipped_locale_opens_the_home_page(string browser)
    {
        using var driver = Driver(browser);
        var texts = new Dictionary<string, string>();
        foreach (string locale in AllLocales)
        {
            string numerals = locale is "ar" or "ur" or "ps" ? "arab" : "latn";
            using var server = new StaticServer(PortOutput, spa: true, headScript: SaaaatParityTests.Pinned(locale, numerals));
            texts[locale] = SaaaatParityTests.TextOf(driver, server.Address.ToString());
        }
        Assert.DoesNotContain(texts, t => t.Value.Length == 0);
        Assert.DoesNotContain(texts, t => t.Key != "en" && t.Value == texts["en"]);
    }

    [Theory]
    [InlineData("chrome")]
    [InlineData("firefox")]
    [InlineData("safari")]
    public void One_in_app_language_switch_changes_the_text(string browser)
    {
        using var server = new StaticServer(PortOutput, spa: true, headScript: SaaaatParityTests.Pinned("en", "latn"));
        using var driver = Driver(browser);
        var js = (IJavaScriptExecutor)driver;
        string before = SaaaatParityTests.TextOf(driver, new Uri(server.Address, "/settings/appearance").ToString());
        js.ExecuteScript("[...document.querySelectorAll('.lang-grid button')].find(b => b.querySelector('.lang-name').textContent.trim() === 'Fran\u00e7ais').click();");
        Thread.Sleep(1000);
        string after = Settled(driver);
        Assert.NotEqual(before, (string)js.ExecuteScript("return (document.body.innerText || '').replace(/\\s+/g, ' ').trim();")!);
        Assert.Equal("true", (string)js.ExecuteScript("return [...document.querySelectorAll('.lang-grid button')].find(b => b.querySelector('.lang-name').textContent.trim() === 'Fran\u00e7ais').getAttribute('aria-current');")!);
        Assert.False(after.EndsWith("| ", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("chrome")]
    [InlineData("firefox")]
    [InlineData("safari")]
    public void Every_page_renders_and_Chromium_logs_no_console_error(string browser)
    {
        using var server = new StaticServer(PortOutput, spa: true, headScript: SaaaatParityTests.Pinned("en", "latn"));
        using var driver = Driver(browser);
        var problems = new List<string>();
        foreach (string page in SaaaatParityTests.Pages)
        {
            string text = SaaaatParityTests.TextOf(driver, new Uri(server.Address, page).ToString());
            if (text.Length == 0)
                problems.Add($"{page}: blank page");
            // The PWA service worker is outside spec 003, so /sw.js gets the page and Chromium reports an unsupported MIME type.
            // Only Chromium exposes its console to WebDriver; the others are held to the page having rendered.
            if (browser == "chrome")
                problems.AddRange(driver.Manage().Logs.GetLog(LogType.Browser).Where(e => e.Level == LogLevel.Severe).Select(e => e.Message)
                    .Where(m => !m.Contains("manifest.webmanifest", StringComparison.Ordinal) && !m.Contains("[sw]", StringComparison.Ordinal) && !m.Contains("unsupported MIME type", StringComparison.Ordinal))
                    .Select(m => $"{page}: {(m.Length > 300 ? m[..300] : m)}"));
        }
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }
}
