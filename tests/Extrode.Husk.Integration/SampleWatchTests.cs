using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace Extrode.Husk.Integration;

/// <summary>The sample under <c>dotnet watch</c> in its default hot reload mode: a save is compiled
/// in the running app, and dotnet watch sees the new static asset and refreshes the page.</summary>
[Collection(nameof(PackedFeed))]
public sealed partial class SampleWatchTests(PackedFeed feed)
{
    private static object? Script(IWebDriver driver, string script) => ((IJavaScriptExecutor)driver).ExecuteScript(script);

    [Fact]
    public async Task Saves_reload_the_page_with_the_change_and_a_broken_save_keeps_it()
    {
        using var sample = feed.Sample();
        var sinceCopy = Stopwatch.StartNew();
        var start = new ProcessStartInfo("dotnet", ["watch", "--non-interactive", "--no-launch-profile"])
        {
            WorkingDirectory = sample.Dir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var key in start.Environment.Keys.Where(k => k.StartsWith("MSBuild", StringComparison.OrdinalIgnoreCase)).ToList())
            start.Environment.Remove(key);
        start.Environment["ASPNETCORE_URLS"] = "http://127.0.0.1:0";
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        var output = new StringBuilder();
        string Output() { lock (output) return output.ToString(); }
        using var watch = new Process { StartInfo = start };
        watch.OutputDataReceived += (_, e) => { lock (output) output.AppendLine(e.Data); };
        watch.ErrorDataReceived += (_, e) => { lock (output) output.AppendLine(e.Data); };
        watch.Start();
        watch.BeginOutputReadLine();
        watch.BeginErrorReadLine();
        try
        {
            Uri? address = null;
            using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) })
            {
                var deadline = DateTime.UtcNow.AddSeconds(180);
                while (true)
                {
                    Assert.True(DateTime.UtcNow < deadline && !watch.HasExited, Output());
                    address = Listening.Parse(Output());
                    if (address is null)
                    {
                        await Task.Delay(200, TestContext.Current.CancellationToken);
                        continue;
                    }
                    try
                    {
                        using var _ = await http.GetAsync(address, TestContext.Current.CancellationToken);
                        break;
                    }
                    catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !TestContext.Current.CancellationToken.IsCancellationRequested)
                    {
                        await Task.Delay(200, TestContext.Current.CancellationToken);
                    }
                }
            }

            using var driver = (ChromeDriver)Browsers.Start("chrome");
            // An edit before the refresh socket opens never reaches the page, and the refresh
            // script connects after load without saying so.
            driver.ExecuteCdpCommand("Page.addScriptToEvaluateOnNewDocument", new Dictionary<string, object?>
            {
                ["source"] = """
                    const Native = WebSocket;
                    window.WebSocket = class extends Native {
                      constructor(...args) { super(...args); this.addEventListener('open', () => window.huskSocketOpen = true); }
                    };
                    """,
            });
            void Load()
            {
                Browsers.Until(driver, "return window.huskSocketOpen === true && document.querySelector('h1') ? true : null;", TimeSpan.FromSeconds(30));
                Script(driver, "window.huskBeforeSave = true;");
            }
            driver.Navigate().GoToUrl(address!);
            Load();
            Assert.Equal("Husk sample", driver.FindElement(By.TagName("h1")).Text);
            string app = sample.P("wwwroot/App.svelte");

            // On macOS a write to a file created less than about 45 s ago also raises Created. dotnet watch
            // (SDK 10.0.403) then reports "File added" and can exit with "Unexpected true" in
            // HotReloadMSBuildWorkspace.cs line 158. A file a developer saves is older than that.
            var settled = TimeSpan.FromSeconds(45) - sinceCopy.Elapsed;
            if (OperatingSystem.IsMacOS() && settled > TimeSpan.Zero)
                await Task.Delay(settled, TestContext.Current.CancellationToken);

            var markup = Stopwatch.StartNew();
            File.WriteAllText(app, File.ReadAllText(app).Replace("<h1>Husk sample</h1>", "<h1>Husk edited</h1>", StringComparison.Ordinal));
            try { Browsers.Until(driver, "return window.huskBeforeSave === undefined && document.querySelector('h1')?.textContent === 'Husk edited' ? true : null;", TimeSpan.FromSeconds(30)); }
            catch (TimeoutException e) { throw new TimeoutException(e.Message + "\n" + Output(), e); }
            markup.Stop();
            Load();

            var style = Stopwatch.StartNew();
            File.WriteAllText(app, File.ReadAllText(app).Replace("rgb(255, 62, 0)", "rgb(0, 0, 255)", StringComparison.Ordinal));
            Browsers.Until(driver, "const h = document.querySelector('h1'); return window.huskBeforeSave === undefined && h && getComputedStyle(h).color === 'rgb(0, 0, 255)' ? true : null;", TimeSpan.FromSeconds(30));
            style.Stop();
            Load();
            TestContext.Current.SendDiagnosticMessage($"markup save shown in {markup.ElapsedMilliseconds} ms, style save in {style.ElapsedMilliseconds} ms");
            Assert.True(markup.Elapsed < TimeSpan.FromSeconds(3), $"markup save took {markup.ElapsedMilliseconds} ms");
            Assert.True(style.Elapsed < TimeSpan.FromSeconds(3), $"style save took {style.ElapsedMilliseconds} ms");

            var compiles = Compiled().Matches(Output()).Select(m => int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)).ToList();
            Assert.True(compiles.Count >= 2, Output());
            TestContext.Current.SendDiagnosticMessage($"compiles: {string.Join(", ", compiles)} ms");
            Assert.True(compiles[1] <= 250, $"second save compiled in {compiles[1]} ms: {string.Join(", ", compiles)}");

            string before = Output();
            File.WriteAllText(app, File.ReadAllText(app) + "\n{#if}\n");
            var deadline2 = DateTime.UtcNow.AddSeconds(30);
            while (!Output()[before.Length..].Contains("error HUSK100", StringComparison.Ordinal))
            {
                Assert.True(DateTime.UtcNow < deadline2, Output());
                await Task.Delay(50, TestContext.Current.CancellationToken);
            }
            // As long as a good save is given to reload, so a late reload is not missed.
            await Task.Delay(3000, TestContext.Current.CancellationToken);
            Assert.Equal(true, Script(driver, "return window.huskBeforeSave === true;"));
            Assert.Equal("Husk edited", driver.FindElement(By.TagName("h1")).Text);
            Assert.DoesNotContain("Restarting", Output(), StringComparison.Ordinal);
        }
        finally
        {
            watch.Kill(entireProcessTree: true);
            watch.WaitForExit();
        }
    }

    [GeneratedRegex(@"Husk compiled wwwroot[/\\]App\.svelte in (\d+) ms")]
    private static partial Regex Compiled();
}
