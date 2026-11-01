using System.Diagnostics;
using System.Text;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace Extrode.Husk.Integration;

/// <summary>The router app under <c>dotnet watch</c>: saving, adding and removing a route file each
/// reload the browser with the change. dotnet watch never sees a file added after it starts, so the
/// app's own route watcher builds the project again.</summary>
[Collection(nameof(PackedFeed))]
public sealed class RoutesWatchTests(PackedFeed feed)
{
    private static object? Script(IWebDriver driver, string script) => ((IJavaScriptExecutor)driver).ExecuteScript(script);

    private const string Reloaded = "window.huskBeforeSave === undefined";

    [Fact]
    public async Task A_saved_added_and_removed_route_file_each_reload_the_browser_with_the_change()
    {
        using var app = feed.Consumer("""
            <PackageReference Include="Extrode.Husk.Build" Version="$(HuskVersion)" PrivateAssets="all" />
            <PackageReference Include="Extrode.Husk.AspNetCore" Version="$(HuskVersion)" />
            <HuskComponent Include="src/**/*.svelte" PareServeAt="app" />
            <HuskModule Include="src/**/*.svelte.ts" PareServeAt="app" />
            <TypeScriptSource Include="src/**/*.ts" Exclude="src/**/*.svelte.ts" PareServeAt="app" />
            <HuskRoutes Include="src/routes" />
            <HuskAlias Include="$lib" Path="src/lib" />
            """);
        string fixture = Path.Combine(TempProject.Fixtures, "router-app");
        foreach (string file in Directory.EnumerateFiles(fixture, "*", SearchOption.AllDirectories))
            app.Put(Path.GetRelativePath(fixture, file).Replace('\\', '/'), File.ReadAllText(file));
        app.Put("Program.cs", """
            using Extrode.Husk.AspNetCore;
            var builder = WebApplication.CreateBuilder(args);
            builder.AddHusk();
            var app = builder.Build();
            app.UseHuskSpa();
            app.UseRouting();
            app.Use((context, next) =>
            {
                if (context.Request.Path == "/")
                    context.Request.Path = "/index.html";
                return next(context);
            });
            app.MapStaticAssets();
            app.Run();
            """);
        var sinceCopy = Stopwatch.StartNew();

        var start = new ProcessStartInfo("dotnet", ["watch", "--non-interactive", "--no-launch-profile"])
        {
            WorkingDirectory = app.Dir,
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
                var deadline = DateTime.UtcNow.AddSeconds(240);
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
            driver.ExecuteCdpCommand("Page.addScriptToEvaluateOnNewDocument", new Dictionary<string, object?>
            {
                ["source"] = """
                    const Native = WebSocket;
                    window.WebSocket = class extends Native {
                      constructor(...args) { super(...args); this.addEventListener('open', () => window.huskSocketOpen = true); }
                    };
                    """,
            });
            void Mark()
            {
                Browsers.Until(driver, "return window.huskSocketOpen === true && document.querySelector('#title, #error') ? true : null;", TimeSpan.FromSeconds(30));
                Script(driver, "window.huskBeforeSave = true;");
            }
            void Until(string condition, string what)
            {
                try { Browsers.Until(driver, $"return {condition} ? true : null;", TimeSpan.FromSeconds(60)); }
                catch (TimeoutException e) { throw new TimeoutException(what + "\n" + Output(), e); }
            }
            driver.Navigate().GoToUrl(address!);
            Mark();
            Assert.Equal("home", driver.FindElement(By.Id("title")).Text);

            // See SampleWatchTests: a file written less than about 45 s after the copy also raises Created on macOS.
            var settled = TimeSpan.FromSeconds(45) - sinceCopy.Elapsed;
            if (OperatingSystem.IsMacOS() && settled > TimeSpan.Zero)
                await Task.Delay(settled, TestContext.Current.CancellationToken);

            string home = app.P("src/routes/+page.svelte");
            File.WriteAllText(home, File.ReadAllText(home).Replace("home</h1>", "home edited</h1>", StringComparison.Ordinal));
            Until($"{Reloaded} && document.querySelector('#title')?.textContent === 'home edited'", "a saved route file did not reload the page");
            Mark();

            app.Put("src/routes/added/+page.svelte", "<h1 id=\"title\">added</h1>\n");
            Until($"{Reloaded}", "an added route file did not reload the page");
            Mark();
            Until("(document.querySelector('#title')?.textContent === 'added') || (window.__goto && (window.__goto('/added'), false))", "an added route was not reachable");
            Assert.EndsWith("/added", driver.Url);

            Script(driver, "window.huskBeforeSave = true;");
            Directory.Delete(app.P("src/routes/added"), recursive: true);
            Until($"{Reloaded} && document.querySelector('#error')?.textContent === '404'", "a removed route file did not reload the page to a 404");
            Assert.Contains("Husk built the routes again", Output(), StringComparison.Ordinal);
        }
        finally
        {
            watch.Kill(entireProcessTree: true);
            watch.WaitForExit();
        }
    }
}
