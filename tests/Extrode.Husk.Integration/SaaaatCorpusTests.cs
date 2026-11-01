namespace Extrode.Husk.Integration;

/// <summary>The saaaat web app built by Husk alone (spec 002 T16). Not gating: skipped when the sibling checkout
/// is missing.</summary>
[Trait("Category", "Corpus")]
[Collection(nameof(PackedFeed))]
public sealed class SaaaatCorpusTests
{
    private static readonly string Saaaat = Path.GetFullPath(Path.Combine(TempProject.RepoRoot, "..", "..", "ikhbat.org", "saaaat.com"));

    internal static TempProject Build(out string output, out int exit)
    {
        if (!Directory.Exists(Path.Combine(Saaaat, "apps", "web", "src")))
            Assert.Skip($"no saaaat checkout at {Saaaat}");
        string web = Path.Combine(Saaaat, "apps", "web");
        var project = new TempProject();
        project.Project(items: $"""
            <HuskComponent Include="{web}/src/**/*.svelte" PareServeAt="app" />
            <HuskModule Include="{web}/src/**/*.svelte.ts" Exclude="{web}/src/**/*.test.*;{web}/src/test-mocks/**" PareServeAt="app" />
            <TypeScriptSource Include="{web}/src/**/*.ts" Exclude="{web}/src/**/*.svelte.ts;{web}/src/**/*.d.ts;{web}/src/**/*.test.*;{web}/src/test-mocks/**;{web}/src/hooks.server.ts" PareServeAt="app" />
            <HuskModule Include="{Saaaat}/packages/core/src/**/*.svelte.ts" PareServeAt="pkg/core" />
            <TypeScriptSource Include="{Saaaat}/packages/core/src/**/*.ts" Exclude="{Saaaat}/packages/core/src/**/*.svelte.ts;{Saaaat}/packages/core/src/**/*.test.*;{Saaaat}/packages/core/src/**/*.d.ts" PareServeAt="pkg/core" />
            <TypeScriptSource Include="{Saaaat}/packages/i18n/src/**/*.ts" Exclude="{Saaaat}/packages/i18n/src/**/*.test.*;{Saaaat}/packages/i18n/src/**/*.d.ts" PareServeAt="pkg/i18n" />
            <TypeScriptSource Include="{Saaaat}/packages/geo-data/src/**/*.ts" Exclude="{Saaaat}/packages/geo-data/src/**/*.test.*;{Saaaat}/packages/geo-data/src/**/*.d.ts" PareServeAt="pkg/geo-data" />
            <HuskRoutes Include="{web}/src/routes" />
            <HuskShell Include="{web}/src/app.html" />
            <HuskAlias Include="$lib" Path="{web}/src/lib" />
            <HuskAlias Include="@saaaat/core" Path="{Saaaat}/packages/core/src" Entry="index.ts" />
            <HuskAlias Include="@saaaat/i18n" Path="{Saaaat}/packages/i18n/src" Entry="index.ts" />
            <HuskAlias Include="@saaaat/geo-data" Path="{Saaaat}/packages/geo-data/src" Entry="index.ts" />
            <HuskImport Include="@capacitor/core" Url="/vendor/capacitor-core.js" />
            <HuskImport Include="@capacitor/preferences" Url="/vendor/capacitor-preferences.js" />
            <HuskImport Include="@capacitor/local-notifications" Url="/vendor/capacitor-local-notifications.js" />
            <HuskImport Include="$lib/design/utilities.css" Url="/vendor/utilities-css.js" />
            <HuskEnv Include="PUBLIC_BACKEND_URL" Value="" />
            """, properties: $"<HuskHooks>{web}/src/hooks.client.ts</HuskHooks>");
        (exit, output) = project.Build();
        return project;
    }

    [Fact]
    public void The_app_builds()
    {
        using var project = Build(out string output, out int exit);

        TestContext.Current.SendDiagnosticMessage(string.Join("\n", output.Split('\n').Where(l => l.Contains("error", StringComparison.Ordinal)).Distinct().Take(40)));
        Assert.True(exit == 0, string.Join("\n", output.Split('\n').Where(l => l.Contains("error", StringComparison.Ordinal)).Distinct().Take(40)));
    }

    private static void CopyStatic(TempProject project)
    {
        string source = Path.Combine(Saaaat, "apps", "web", "static");
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(source, file).Replace('\\', '/');
            if (relative.StartsWith("_app/", StringComparison.Ordinal) || relative == "_headers")
                continue;
            Directory.CreateDirectory(Path.GetDirectoryName(project.P("wwwroot/" + relative))!);
            File.Copy(file, project.P("wwwroot/" + relative), overwrite: true);
        }
        // A side-effect CSS import has no Husk support yet (work/todo.md): stand in with a module that adds the link.
        Directory.CreateDirectory(project.P("wwwroot/vendor"));
        File.Copy(Path.Combine(Saaaat, "apps", "web", "src", "lib", "design", "utilities.css"), project.P("wwwroot/vendor/utilities.css"), overwrite: true);
        project.Put("wwwroot/vendor/utilities-css.js", "const l = document.createElement('link'); l.rel = 'stylesheet'; l.href = '/vendor/utilities.css'; document.head.append(l);\n");
        foreach (string json in new[] { "umm-al-qura.json", "sighting-record.json" })
        {
            Directory.CreateDirectory(project.P("wwwroot/pkg/core/hijri"));
            File.Copy(Path.Combine(Saaaat, "packages", "core", "src", "hijri", json), project.P("wwwroot/pkg/core/hijri/" + json), overwrite: true);
        }
        // Stand-ins for the Capacitor packages, which the checkout has not installed: the web (non-native) behaviour.
        project.Put("wwwroot/vendor/capacitor-core.js", "export const Capacitor = { isNativePlatform: () => false, getPlatform: () => 'web', isPluginAvailable: () => false };\nexport const registerPlugin = () => ({});\n");
        project.Put("wwwroot/vendor/capacitor-preferences.js", "export const Preferences = { get: async ({ key }) => ({ value: localStorage.getItem(key) }), set: async ({ key, value }) => localStorage.setItem(key, value), remove: async ({ key }) => localStorage.removeItem(key) };\n");
        project.Put("wwwroot/vendor/capacitor-local-notifications.js", "export const LocalNotifications = { checkPermissions: async () => ({ display: 'denied' }), requestPermissions: async () => ({ display: 'denied' }), schedule: async () => ({}), cancel: async () => {}, getPending: async () => ({ notifications: [] }) };\n");
    }

    public static TheoryData<string> Pages =>
    [
        "/", "/today", "/calendar", "/month", "/moon", "/moon/details", "/qibla", "/about/estimated-times", "/about/scholarly-differences",
        "/about/umm-al-qura", "/admin", "/guide/terminology", "/guide/transliteration", "/history", "/location", "/settings/appearance", "/settings",
    ];

    [Theory]
    [InlineData("chrome")]
    [InlineData("firefox")]
    [InlineData("safari")]
    public void Every_page_renders_without_a_console_error(string browser)
    {
        using var project = Build(out string output, out int exit);
        Assert.True(exit == 0, output);
        CopyStatic(project);
        var (publishExit, publishOutput) = project.Run("publish", "-nologo", "-c", "Release", "-o", "out", "-nodeReuse:false", "--disable-build-servers");
        Assert.True(publishExit == 0, publishOutput);
        using var server = new StaticServer(project.P("out/wwwroot"), spa: true);
        using var driver = Browsers.Start(browser);
        if (browser != "safari") driver.Manage().Window.Size = new System.Drawing.Size(1280, 900);
        driver.Navigate().GoToUrl(new Uri(server.Address, "/about/umm-al-qura").ToString());
        ((OpenQA.Selenium.IJavaScriptExecutor)driver).ExecuteScript("""
            localStorage.setItem('saaaat:v1:tos', '"1.0"');
            localStorage.setItem('saaaat:v1:locations', JSON.stringify([{ id: 'l1', name: 'Mecca', latitude: 21.4225, longitude: 39.8262, timezone: 'Asia/Riyadh', countryCode: 'SA' }]));
            localStorage.setItem('saaaat:v1:activeLocation', '"l1"');
            """);
        var problems = new List<string>();
        foreach (string page in new[] { "/", "/today", "/calendar", "/month", "/moon", "/moon/details", "/qibla", "/about/estimated-times", "/about/scholarly-differences",
                     "/about/umm-al-qura", "/admin", "/guide/terminology", "/guide/transliteration", "/history", "/location", "/settings/appearance", "/settings" })
        {
            driver.Navigate().GoToUrl(new Uri(server.Address, page).ToString());
            Thread.Sleep(2500);
            string where = (string)((OpenQA.Selenium.IJavaScriptExecutor)driver).ExecuteScript("return location.pathname + ' | ' + document.body.innerText.slice(0, 80).replace(/\\n/g, ' / ');")!;
            // Only Chromium exposes the console to WebDriver; the others are held to the page having rendered.
            var errors = browser != "chrome" ? [] : driver.Manage().Logs.GetLog(OpenQA.Selenium.LogType.Browser).Where(e => e.Level == OpenQA.Selenium.LogLevel.Severe).Select(e => e.Message).Where(m => !m.Contains("manifest.webmanifest", StringComparison.Ordinal) && !m.Contains("[sw]", StringComparison.Ordinal) && !m.Contains("unsupported MIME type", StringComparison.Ordinal)).ToList();
            if (browser != "chrome" && where.EndsWith("| ", StringComparison.Ordinal))
                errors.Add("blank page");
            problems.Add($"{page} -> {where}" + (errors.Count > 0 ? "\n    " + string.Join("\n    ", errors.Select(e => e.Length > 300 ? e[..300] : e)) : ""));
        }
        var js = (OpenQA.Selenium.IJavaScriptExecutor)driver;
        driver.Navigate().GoToUrl(new Uri(server.Address, "/month").ToString());
        Thread.Sleep(1500);
        driver.Navigate().GoToUrl(new Uri(server.Address, "/settings/nonsense").ToString());
        Thread.Sleep(2500);
        string landed = (string)js.ExecuteScript("return location.pathname + ' history=' + history.length")!;
        js.ExecuteScript("history.back()");
        Thread.Sleep(1500);
        problems.Add($"nonsense -> {landed}; after back -> " + (string)js.ExecuteScript("return location.pathname")!);
        File.WriteAllText(Path.Combine(TempProject.RepoRoot, "tmp", $"saaaat-pages-{browser}.txt"), string.Join("\n", problems));
        Assert.DoesNotContain(problems, p => p.Contains("\n    ", StringComparison.Ordinal));
    }
}
