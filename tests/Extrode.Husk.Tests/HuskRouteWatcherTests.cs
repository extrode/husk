using System.Net;
using Extrode.Husk.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Extrode.Husk.Tests;

public sealed class HuskRouteWatcherTests : IDisposable
{
    private readonly string _root = Path.Combine(RepoPaths.Root, "tmp", "route-watcher-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly string _routes;
    private readonly string _served;
    private readonly string _map;
    private int _builds;

    public HuskRouteWatcherTests()
    {
        _routes = Path.Combine(_root, "src", "routes");
        _served = Path.Combine(_root, "served");
        Directory.CreateDirectory(Path.Combine(_routes, "month"));
        Directory.CreateDirectory(_served);
        File.WriteAllText(Path.Combine(_routes, "+page.svelte"), "<h1>home</h1>");
        File.WriteAllText(Path.Combine(_routes, "month", "+page.svelte"), "<h1>month</h1>");
        _map = Path.Combine(_root, "map.json");
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private void WriteMap(bool routing = true, string? routesRoot = null, string importMap = "importmap.json") =>
        File.WriteAllText(_map, System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["compiler"] = "c", ["svelte"] = "s", ["projectDirectory"] = _root, ["dev"] = true, ["stylesheet"] = "husk.css",
            ["importMap"] = importMap, ["styles"] = Array.Empty<object>(), ["routing"] = routing,
            ["routesRoot"] = routesRoot ?? _routes, ["project"] = Path.Combine(_root, "app.csproj"), ["configuration"] = "Debug",
            ["targetFramework"] = "net10.0", ["servedRoot"] = _served, ["basePath"] = "/app",
        }));

    private HuskRouteWatcher Watcher(bool ok = true, ILogger? logger = null)
    {
        WriteMap();
        return new HuskRouteWatcher(_map, logger ?? NullLogger.Instance, (_, _) =>
        {
            Interlocked.Increment(ref _builds);
            return Task.FromResult((ok, ok ? "" : "error HUSK300 boom"));
        });
    }

    private static async Task Within(Func<bool> done)
    {
        var until = DateTime.UtcNow.AddSeconds(10);
        while (!done() && DateTime.UtcNow < until)
            await Task.Delay(25, TestContext.Current.CancellationToken);
    }

    private static Task Quiet() => Task.Delay(HuskRouteWatcher.Settle * 4, TestContext.Current.CancellationToken);

    [Fact]
    public void The_map_reads_the_route_fields_and_an_older_map_without_them_still_loads()
    {
        WriteMap();
        var map = HuskDevelopmentMap.Read(_map)!;
        Assert.Equal((_routes, "net10.0", "Debug", _served, "/app"), (map.RoutesRoot, map.TargetFramework, map.Configuration, map.ServedRoot, map.BasePath));

        var old = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(_map))!.AsObject();
        foreach (string key in new[] { "routesRoot", "project", "configuration", "targetFramework", "servedRoot", "basePath" })
            old.Remove(key);
        File.WriteAllText(_map, old.ToJsonString());
        var loaded = HuskDevelopmentMap.Read(_map)!;
        Assert.Equal(("", "/"), (loaded.RoutesRoot, loaded.BasePath));
    }

    [Fact]
    public async Task An_added_route_file_builds_once_and_a_burst_of_changes_builds_once()
    {
        using var watcher = Watcher();
        await watcher.StartAsync(TestContext.Current.CancellationToken);

        Directory.CreateDirectory(Path.Combine(_routes, "added"));
        File.WriteAllText(Path.Combine(_routes, "added", "+page.svelte"), "<h1>a</h1>");
        File.WriteAllText(Path.Combine(_routes, "added", "+page.ts"), "export const load = () => ({});");
        await Within(() => _builds > 0);
        await Quiet();

        Assert.Equal(1, _builds);
        await watcher.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_good_build_is_followed_by_a_touch_of_the_import_map_and_the_shell()
    {
        string importMap = Path.Combine(_root, "husk", "importmap.json");
        string shell = Path.Combine(_root, "husk", "app", "index.html");
        Directory.CreateDirectory(Path.GetDirectoryName(shell)!);
        File.WriteAllText(importMap, "{}");
        File.WriteAllText(shell, "<html>");
        var old = DateTime.UtcNow.AddHours(-1);
        File.SetLastWriteTimeUtc(importMap, old);
        File.SetLastWriteTimeUtc(shell, old);
        WriteMap(importMap: importMap);
        using var watcher = new HuskRouteWatcher(_map, NullLogger.Instance, (_, _) => Task.FromResult((true, "")));
        await watcher.StartAsync(TestContext.Current.CancellationToken);

        File.WriteAllText(Path.Combine(_routes, "new.svelte"), "x");
        await Within(() => File.GetLastWriteTimeUtc(shell) > old && File.GetLastWriteTimeUtc(importMap) > old);

        Assert.True(File.GetLastWriteTimeUtc(shell) > old, "the shell was not touched");
        Assert.True(File.GetLastWriteTimeUtc(importMap) > old, "the import map was not touched");
        await watcher.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_removed_route_file_builds()
    {
        using var watcher = Watcher();
        await watcher.StartAsync(TestContext.Current.CancellationToken);

        File.Delete(Path.Combine(_routes, "month", "+page.svelte"));
        await Within(() => _builds > 0);

        Assert.True(_builds >= 1);
        await watcher.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_save_of_a_file_that_was_there_at_start_builds_nothing()
    {
        using var watcher = Watcher();
        await watcher.StartAsync(TestContext.Current.CancellationToken);

        File.WriteAllText(Path.Combine(_routes, "+page.svelte"), "<h1>saved</h1>");
        await Quiet();

        Assert.Equal(0, _builds);
        await watcher.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task An_editor_swap_file_builds_nothing()
    {
        using var watcher = Watcher();
        await watcher.StartAsync(TestContext.Current.CancellationToken);

        File.WriteAllText(Path.Combine(_routes, ".+page.svelte.swp"), "x");
        File.WriteAllText(Path.Combine(_routes, "+page.svelte~"), "x");
        await Quiet();

        Assert.Equal(0, _builds);
        await watcher.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_map_without_routing_starts_no_watcher()
    {
        WriteMap(routing: false);
        using var watcher = new HuskRouteWatcher(_map, NullLogger.Instance, (_, _) =>
        {
            Interlocked.Increment(ref _builds);
            return Task.FromResult((true, ""));
        });
        await watcher.StartAsync(TestContext.Current.CancellationToken);

        File.WriteAllText(Path.Combine(_routes, "new.svelte"), "x");
        await Quiet();

        Assert.Equal(0, _builds);
        await watcher.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_failed_build_is_logged_with_its_output_and_the_next_change_builds_again()
    {
        var logger = new ListLogger();
        using var watcher = Watcher(ok: false, logger);
        await watcher.StartAsync(TestContext.Current.CancellationToken);

        File.WriteAllText(Path.Combine(_routes, "one.svelte"), "x");
        await Within(() => logger.Errors.Count > 0);
        File.WriteAllText(Path.Combine(_routes, "two.svelte"), "x");
        await Within(() => logger.Errors.Count > 1);

        Assert.Equal(2, _builds);
        Assert.Contains("HUSK300 boom", logger.Errors[0]);
        await watcher.StopAsync(TestContext.Current.CancellationToken);
    }

    private async Task<(HttpStatusCode Status, string Body)> Get(string path)
    {
        WriteMap();
        using var watcher = new HuskRouteWatcher(_map, NullLogger.Instance, (_, _) => Task.FromResult((true, "")));
        await watcher.StartAsync(TestContext.Current.CancellationToken);
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseSetting("urls", "http://127.0.0.1:0");
        builder.Services.AddSingleton<IStartupFilter>(watcher);
        await using var app = builder.Build();
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var http = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };
        using var response = await http.GetAsync(path, TestContext.Current.CancellationToken);
        await watcher.StopAsync(TestContext.Current.CancellationToken);
        return (response.StatusCode, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_404_under_the_base_path_is_answered_from_the_served_folder()
    {
        Directory.CreateDirectory(Path.Combine(_served, "routes", "fifth"));
        File.WriteAllText(Path.Combine(_served, "routes", "fifth", "+page.svelte.js"), "export default 5;");

        Assert.Equal((HttpStatusCode.OK, "export default 5;"), await Get("/app/routes/fifth/+page.svelte.js"));
    }

    [Theory]
    [InlineData("/app/routes/none.js")]
    [InlineData("/other/routes/fifth.js")]
    [InlineData("/app/../outside.txt")]
    [InlineData("/app/%2e%2e/outside.txt")]
    public async Task A_404_for_a_missing_file_or_a_path_outside_the_served_folder_stays_a_404(string path)
    {
        File.WriteAllText(Path.Combine(_root, "outside.txt"), "secret");
        Directory.CreateDirectory(Path.Combine(_served, "routes"));
        File.WriteAllText(Path.Combine(_served, "routes", "fifth.js"), "x");

        Assert.Equal(HttpStatusCode.NotFound, (await Get(path)).Status);
    }

    private sealed class ListLogger : ILogger
    {
        public List<string> Errors { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Error)
                lock (Errors)
                    Errors.Add(formatter(state, exception));
        }
    }
}
