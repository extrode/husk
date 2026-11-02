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
            // SvelteKit's route announcer holds the page title after a client-side navigation; Husk's router has none.
            // It is off screen and for screen readers, so it is hidden, not compared (docs/architecture/006-saaaat-port.md).
            string now = (string)js.ExecuteScript("document.getElementById('svelte-announcer')?.style.setProperty('display', 'none'); return (document.body.innerText || '').replace(/\\s+/g, ' ').trim();")!;
            stable = now.Length > 0 && now == last ? stable + 1 : 0;
            last = now;
        }
        return last;
    }


    internal const string Record = "HUSK_SAAAAT_RECORD";

    /// <summary>The commit of a saaaat checkout; references are keyed by it.</summary>
    internal static string CommitOf(string checkout)
    {
        var start = new System.Diagnostics.ProcessStartInfo("git", ["-C", checkout, "rev-parse", "HEAD"]) { RedirectStandardOutput = true };
        using var git = System.Diagnostics.Process.Start(start)!;
        string sha = git.StandardOutput.ReadToEnd().Trim();
        git.WaitForExit();
        return git.ExitCode == 0 ? sha : throw new InvalidOperationException($"{checkout} is not a git checkout");
    }

    internal static string ReferenceFolder(string sha) => Path.Combine(TempProject.RepoRoot, "tests", "references", "saaaat", sha);

    internal static string ReferenceFile(string folder, string page, string locale, string browser) =>
        Path.Combine(folder, $"{(page == "/" ? "index" : page.Trim('/').Replace('/', '-'))}.{locale}.{browser}.txt");

    /// <summary>The settled text of every page in every pinned locale, keyed by reference file name.</summary>
    internal static Dictionary<string, string> Capture(string browser, string root)
    {
        var seen = new Dictionary<string, string>();
        foreach (var (locale, numerals) in Locales)
        {
            using var server = new StaticServer(root, spa: true, headScript: Pinned(locale, numerals));
            using var driver = Browsers.Start(browser);
            if (browser != "safari") driver.Manage().Window.Size = new System.Drawing.Size(1280, 900);
            foreach (string page in Pages)
                seen[Path.GetFileName(ReferenceFile("", page, locale, browser))] = TextOf(driver, new Uri(server.Address, page).ToString());
        }
        return seen;
    }

    /// <summary>Spike P1 (spec 003 T3): two recordings of the Vite build per browser, to see what differs.</summary>
    [Theory]
    [MemberData(nameof(Browsers.All), MemberType = typeof(Browsers))]
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
