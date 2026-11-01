using OpenQA.Selenium;

namespace Extrode.Husk.Integration;

/// <summary>Page text of the saaaat web app, compared with a recorded Vite build (spec 003, R3 to R5). A reference
/// is recorded on a Node machine with scripts/node-reference/saaaat-vite-build.sh; every test here skips when
/// the folder it needs is not named.</summary>
[Trait("Category", "Corpus")]
public sealed class SaaaatParityTests
{
    internal const string ViteBuild = "HUSK_SAAAAT_VITE_BUILD";

    internal static readonly string[] Pages =
    [
        "/", "/today", "/calendar", "/month", "/moon", "/moon/details", "/qibla", "/about/estimated-times", "/about/scholarly-differences",
        "/about/umm-al-qura", "/admin", "/guide/terminology", "/guide/transliteration", "/history", "/location", "/settings/appearance", "/settings",
    ];

    /// <summary>Runs before every script of the page: a frozen clock and the stored state the pages need, so two
    /// builds of the same app show the same text. Set only when absent, so an in-app change survives a reload.</summary>
    internal static string Pinned(string locale, string numerals) => $$"""
        (() => {
          const at = Date.parse('2026-11-01T12:00:00Z'); const Real = Date;
          class Frozen extends Real { constructor(...a) { if (a.length === 0) super(at); else super(...a); } static now() { return at; } }
          window.Date = Frozen;
          const seed = (k, v) => { if (localStorage.getItem(k) === null) localStorage.setItem(k, JSON.stringify(v)); };
          seed('saaaat:v1:tos', '1.0');
          seed('saaaat:v1:locations', [{ id: 'l1', name: 'Mecca', latitude: 21.4225, longitude: 39.8262, timezone: 'Asia/Riyadh', countryCode: 'SA' }]);
          seed('saaaat:v1:activeLocation', 'l1');
          seed('saaaat:v1:settings', { locale: '{{locale}}', numerals: '{{numerals}}', theme: 'mono' });
        })();
        """;

    internal static (string Locale, string Numerals)[] Locales { get; } = [("en", "latn"), ("ar", "arab")];

    /// <summary>The text of a page once it has stopped changing: the same for two reads a second apart.</summary>
    internal static string TextOf(IWebDriver driver, string url)
    {
        driver.Navigate().GoToUrl(url);
        var js = (IJavaScriptExecutor)driver;
        string last = "";
        int stable = 0;
        for (int i = 0; i < 75 && stable < 5; i++)
        {
            Thread.Sleep(200);
            string now = (string)js.ExecuteScript("return (document.body.innerText || '').replace(/\\s+/g, ' ').trim();")!;
            stable = now.Length > 0 && now == last ? stable + 1 : 0;
            last = now;
        }
        return last;
    }

    /// <summary>Spike P1 (spec 003 T3): two recordings of the Vite build per browser, to see what differs.</summary>
    [Theory]
    [InlineData("chrome")]
    [InlineData("firefox")]
    [InlineData("safari")]
    public void Two_recordings_of_the_same_build_agree(string browser)
    {
        string? build = Environment.GetEnvironmentVariable(ViteBuild);
        if (string.IsNullOrEmpty(build))
            Assert.Skip($"{ViteBuild} names no Vite build");
        var runs = new List<Dictionary<string, string>>();
        for (int run = 0; run < 2; run++)
        {
            var seen = new Dictionary<string, string>();
            foreach (var (locale, numerals) in Locales)
            {
                using var server = new StaticServer(build, spa: true, headScript: Pinned(locale, numerals));
                using var driver = Browsers.Start(browser);
                if (browser != "safari") driver.Manage().Window.Size = new System.Drawing.Size(1280, 900);
                foreach (string page in Pages)
                    seen[$"{page}.{locale}"] = TextOf(driver, new Uri(server.Address, page).ToString());
            }
            runs.Add(seen);
        }
        string folder = Path.Combine(TempProject.RepoRoot, "tmp", "parity", browser);
        Directory.CreateDirectory(folder);
        for (int run = 0; run < 2; run++)
            File.WriteAllLines(Path.Combine(folder, $"run{run}.txt"), runs[run].OrderBy(p => p.Key).Select(p => $"## {p.Key}\n{p.Value}\n"));
        var differ = runs[0].Keys.Where(k => runs[0][k] != runs[1][k]).ToList();
        Assert.True(differ.Count == 0, "differ between runs: " + string.Join(", ", differ));
        Assert.DoesNotContain(runs[0], p => p.Value.Length == 0);
    }
}
