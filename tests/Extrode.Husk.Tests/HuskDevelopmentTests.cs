using System.Collections.Concurrent;
using System.Text.Json;
using Extrode.Husk.AspNetCore;
using Extrode.Pare.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Extrode.Husk.Tests;

[CollectionDefinition(nameof(HuskDevelopmentTests), DisableParallelization = true)]
public sealed class HuskDevelopmentCollection;

// Pare reads its development map from the test output folder, so these share it one at a time.
[Collection(nameof(HuskDevelopmentTests))]
public sealed class HuskDevelopmentTests : IDisposable
{
    private static readonly string PareMap = Path.Combine(AppContext.BaseDirectory, "Extrode.Pare.development.json");
    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("husk-dev-");
    private readonly LogSink _log = new();

    private string P(string relative) => Path.Combine(_dir.FullName, relative);
    private string HuskMap => P("Extrode.Husk.development.json");

    public void Dispose()
    {
        File.Delete(PareMap);
        _dir.Delete(recursive: true);
    }

    private void Put(string relative, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(P(relative))!);
        File.WriteAllText(P(relative), text);
    }

    private string[] ComponentOutputs(string name) =>
        [P($"obj/pare/{name}.svelte.js"), P($"obj/pare/{name}.svelte.css"), P($"obj/pare/{name}.svelte.js.map")];

    private void WriteMaps(params string[] components)
    {
        File.WriteAllText(PareMap, JsonSerializer.Serialize(new
        {
            sourceRoot = P("wwwroot"),
            outputRoot = P("obj/pare"),
            sources = Array.Empty<object>(),
            compiled = components.SelectMany(c => ComponentOutputs(c).Select(o => new { source = P($"wwwroot/{c}.svelte"), output = o, compiler = "husk" })),
        }));
        File.WriteAllText(HuskMap, JsonSerializer.Serialize(new
        {
            compiler = Path.Combine(AppContext.BaseDirectory, "Extrode.Husk.Compiler.dll"),
            svelte = Path.Combine(RepoPaths.Vendored, "svelte", "compiler", "index.js"),
            projectDirectory = _dir.FullName,
            dev = true,
            stylesheet = "/_husk/husk.css",
            importMap = "/_husk/importmap.json",
            styles = Array.Empty<object>(),
        }));
    }

    private async Task<IHost> Start(string environment = "Development")
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { EnvironmentName = environment, ContentRootPath = _dir.FullName });
        builder.Logging.ClearProviders().AddProvider(_log);
        HuskExtensions.AddHusk(builder, HuskMap);
        var app = builder.Build();
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private static void WaitFor(Func<bool> condition)
    {
        var until = DateTime.UtcNow.AddSeconds(30);
        while (!condition())
        {
            if (DateTime.UtcNow > until)
                throw new TimeoutException();
            Thread.Sleep(25);
        }
    }

    [Fact]
    public async Task A_save_recompiles_and_logs_the_timing_line()
    {
        Put("wwwroot/Hello.svelte", "<p>one</p>\n<style>p { color: red; }</style>\n");
        WriteMaps("Hello");
        await using var app = (IAsyncDisposable)await Start();

        var (js, css, map) = (ComponentOutputs("Hello")[0], ComponentOutputs("Hello")[1], ComponentOutputs("Hello")[2]);
        Assert.Contains("one", File.ReadAllText(js));
        Assert.EndsWith("\n//# sourceMappingURL=Hello.svelte.js.map\n", File.ReadAllText(js));
        Assert.Contains("color: red", File.ReadAllText(css));
        Assert.StartsWith("{", File.ReadAllText(map));

        Put("wwwroot/Hello.svelte", "<p>two</p>\n");
        WaitFor(() => File.ReadAllText(js).Contains("two"));
        Assert.Equal("", File.ReadAllText(css));
        WaitFor(() => _log.Count(LogLevel.Information, "Husk compiled wwwroot/Hello.svelte in ") >= 2);
        Assert.Matches(@"^Husk compiled wwwroot/Hello\.svelte in \d+ ms$", _log.Lines(LogLevel.Information).First(l => l.StartsWith("Husk compiled", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task A_failing_save_logs_HUSK100_and_keeps_the_old_outputs()
    {
        Put("wwwroot/Hello.svelte", "<p>one</p>\n<style>p { color: red; }</style>\n");
        WriteMaps("Hello");
        await using var app = (IAsyncDisposable)await Start();
        var before = ComponentOutputs("Hello").Select(File.ReadAllBytes).ToList();

        Put("wwwroot/Hello.svelte", "<p>one</p>\n{#if}\n");
        WaitFor(() => _log.Count(LogLevel.Error, "error HUSK100") > 0);
        Assert.Matches(@"^wwwroot/Hello\.svelte\(2,\d+\): error HUSK100: \w+: ", _log.Lines(LogLevel.Error).Single());
        Assert.Equal(before, ComponentOutputs("Hello").Select(File.ReadAllBytes).ToList());
    }

    [Fact]
    public async Task Warnings_are_logged_with_their_position()
    {
        Put("wwwroot/Hello.svelte", "<p>x</p>\n<img src=\"a.png\">\n");
        WriteMaps("Hello");
        await using var app = (IAsyncDisposable)await Start();

        Assert.Matches(@"^wwwroot/Hello\.svelte\(2,1\): warning HUSK200: a11y_missing_attribute: ",
            _log.Lines(LogLevel.Warning).Single(l => l.Contains("HUSK200", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Outside_Development_nothing_is_registered()
    {
        Put("wwwroot/Hello.svelte", "<p>one</p>\n");
        WriteMaps("Hello");
        await using var app = (IAsyncDisposable)await Start("Production");

        Assert.Empty(((IHost)app).Services.GetServices<IPareCompiler>());
        Assert.False(File.Exists(ComponentOutputs("Hello")[0]));
    }

    [Fact]
    public async Task Without_the_development_map_the_compile_is_skipped_with_an_error()
    {
        var compiler = new HuskCompiler(P("missing.json"));
        var result = await compiler.CompileAsync(P("wwwroot/Hello.svelte"), "<p/>", ComponentOutputs("Hello"), _log.CreateLogger("t"), TestContext.Current.CancellationToken);

        Assert.Null(result);
        Assert.Contains("missing.json is missing", _log.Lines(LogLevel.Error).Single(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_bad_map_is_logged_not_thrown_and_a_rebuilt_map_is_picked_up()
    {
        File.WriteAllText(HuskMap, "{\"compiler\": \"x\"}");
        File.SetLastWriteTimeUtc(HuskMap, DateTime.UtcNow.AddMinutes(-1));
        var compiler = new HuskCompiler(HuskMap);
        var ct = TestContext.Current.CancellationToken;

        Assert.Null(await compiler.CompileAsync(P("wwwroot/Hello.svelte"), "<p>one</p>", ComponentOutputs("Hello"), _log.CreateLogger("t"), ct));
        Assert.Null(await compiler.CompileAsync(P("wwwroot/Hello.svelte"), "<p>one</p>", ComponentOutputs("Hello"), _log.CreateLogger("t"), ct));
        Assert.Equal(2, _log.Lines(LogLevel.Error).Count());

        WriteMaps();
        var outputs = await compiler.CompileAsync(P("wwwroot/Hello.svelte"), "<p>one</p>", ComponentOutputs("Hello"), _log.CreateLogger("t"), ct);
        Assert.Contains("one", outputs![ComponentOutputs("Hello")[0]]);
    }

    [Fact]
    public async Task An_output_the_compiler_does_not_write_is_HUSK101()
    {
        WriteMaps();
        var compiler = new HuskCompiler(HuskMap);
        var result = await compiler.CompileAsync(P("wwwroot/Hello.svelte"), "<p/>", [P("obj/pare/Hello.other.js")], _log.CreateLogger("t"), TestContext.Current.CancellationToken);

        Assert.Null(result);
        Assert.Contains("error HUSK101", _log.Lines(LogLevel.Error).Single(), StringComparison.Ordinal);
    }

    [Fact]
    public void AddHusk_twice_registers_one_compiler()
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { EnvironmentName = "Development", ContentRootPath = _dir.FullName });
        HuskExtensions.AddHusk(builder, HuskMap);
        builder.AddHusk();

        Assert.Single(builder.Services, s => s.ServiceType == typeof(IPareCompiler));
    }

    private sealed class LogSink : ILoggerProvider, ILogger
    {
        private readonly ConcurrentQueue<(LogLevel Level, string Text)> _lines = new();

        public ILogger CreateLogger(string categoryName) => this;
        public void Dispose() { }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            _lines.Enqueue((logLevel, formatter(state, exception)));

        public IEnumerable<string> Lines(LogLevel level) => _lines.Where(l => l.Level == level).Select(l => l.Text);
        public int Count(LogLevel level, string contains) => Lines(level).Count(l => l.Contains(contains, StringComparison.Ordinal));
    }
}
