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

    private void WriteMaps(params string[] components) => WriteMaps(components, []);

    private void WriteMaps(string[] components, string[] pages)
    {
        File.WriteAllText(PareMap, JsonSerializer.Serialize(new
        {
            sourceRoot = P("wwwroot"),
            outputRoot = P("obj/pare"),
            sources = Array.Empty<object>(),
            compiled = components.SelectMany(c => ComponentOutputs(c).Select(o => new { source = P($"wwwroot/{c}.svelte"), output = o, compiler = "husk" }))
                .Concat(pages.Select(page => new { source = P($"wwwroot/{page}"), output = P($"obj/pare/{page}"), compiler = "husk-page" })),
        }));
        File.WriteAllText(HuskMap, JsonSerializer.Serialize(new
        {
            compiler = Path.Combine(AppContext.BaseDirectory, "Extrode.Husk.Compiler.dll"),
            svelte = Path.Combine(RepoPaths.Vendored, "svelte", "compiler", "index.js"),
            projectDirectory = _dir.FullName,
            dev = true,
            stylesheet = P("obj/husk/husk.css"),
            importMap = P("obj/husk/importmap.json"),
            styles = components.Select(c => new { name = $"{c}.svelte", css = ComponentOutputs(c)[1] }),
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

    // L003, the Development half (the build half: tests/Extrode.Husk.Integration/Laws/).
    [Fact]
    [Trait("Law", "L003")]
    public async Task L003_a_rejected_save_keeps_the_last_good_outputs_and_stylesheet_through_a_random_sequence()
    {
        string[] broken = ["<p>one</p>\n{#if}\n", "<p\n", "<script>let x = ;</script>\n", "{#each items}\n"];
        Put("wwwroot/Hello.svelte", "<p class=\"h\">h</p>\n<style>.h { color: red; }</style>\n");
        Put("wwwroot/Other.svelte", "<p class=\"o\">o</p>\n<style>.o { color: blue; }</style>\n");
        WriteMaps("Hello", "Other");
        await using var app = (IAsyncDisposable)await Start();
        WaitFor(() => ComponentOutputs("Hello").Concat(ComponentOutputs("Other")).All(File.Exists));
        List<byte[]?> Snapshot() => [.. ComponentOutputs("Hello").Concat(ComponentOutputs("Other")).Append(P("obj/husk/husk.css"))
            .Select(f => File.Exists(f) ? File.ReadAllBytes(f) : null)];
        var random = new Random(3003);
        var good = Snapshot();

        for (int i = 0; i < 8; i++)
        {
            string name = random.Next(2) == 0 ? "Hello" : "Other";
            if (random.Next(3) == 0)
            {
                Put($"wwwroot/{name}.svelte", $"<p class=\"v{i}\">v{i}</p>\n<style>.v{i} {{ color: red; }}</style>\n");
                WaitFor(() => File.ReadAllText(ComponentOutputs(name)[0]).Contains($"v{i}", StringComparison.Ordinal)
                    && File.Exists(P("obj/husk/husk.css")) && File.ReadAllText(P("obj/husk/husk.css")).Contains($".v{i}", StringComparison.Ordinal));
                good = Snapshot();
                continue;
            }
            int errors = _log.Count(LogLevel.Error, "error HUSK100");
            Put($"wwwroot/{name}.svelte", broken[random.Next(broken.Length)]);
            WaitFor(() => _log.Count(LogLevel.Error, "error HUSK100") > errors);
            Thread.Sleep(200);
            var now = Snapshot();
            Assert.True(good.Zip(now).All(p => p.First is null ? p.Second is null : p.Second is not null && p.First.AsSpan().SequenceEqual(p.Second)),
                $"step {i}, {name}: a rejected save changed the last good output");
        }
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
    public async Task A_style_save_changes_only_that_components_block_in_the_stylesheet()
    {
        Put("wwwroot/A.svelte", "<p>a</p>\n<style>p { color: red; }</style>\n");
        Put("wwwroot/B.svelte", "<p>b</p>\n<style>p { color: blue; }</style>\n");
        WriteMaps("A", "B");
        Directory.CreateDirectory(P("obj/husk"));
        await using var app = (IAsyncDisposable)await Start();
        string stylesheet = P("obj/husk/husk.css");
        Put("wwwroot/B.svelte", "<p>b</p>\n<style>p { color: navy; }</style>\n");
        WaitFor(() => File.Exists(stylesheet) && File.ReadAllText(stylesheet).Contains("navy"));
        string before = File.ReadAllText(stylesheet);
        Assert.Contains("red", before);

        Put("wwwroot/A.svelte", "<p>a</p>\n<style>p { color: green; }</style>\n");
        WaitFor(() => File.ReadAllText(stylesheet).Contains("green"));
        string after = File.ReadAllText(stylesheet);
        static string Block(string css, string name) => css[css.IndexOf($"/* {name} */", StringComparison.Ordinal)..];
        Assert.Equal(Block(before, "B.svelte"), Block(after, "B.svelte"));
        Assert.DoesNotContain("red", after);
        WaitFor(() => File.ReadAllText(ComponentOutputs("A")[1]).Contains("green"));
        Assert.Equal(Compiler.StylesheetWriter.Join([("A.svelte", File.ReadAllText(ComponentOutputs("A")[1])), ("B.svelte", File.ReadAllText(ComponentOutputs("B")[1]))]), after);
    }

    [Fact]
    public async Task A_page_save_inlines_the_current_import_map()
    {
        Directory.CreateDirectory(P("obj/husk"));
        File.WriteAllText(P("obj/husk/importmap.json"), "{\"imports\":{\"/A.svelte\":\"/A.svelte.js\"}}\n");
        Directory.CreateDirectory(P("wwwroot"));
        File.WriteAllBytes(P("wwwroot/index.html"),
            [0xEF, 0xBB, 0xBF, .. "<head><script type=\"importmap\" data-husk></script></head>\n"u8]);
        Put("wwwroot/plain.html", "<p>plain</p>\n");
        WriteMaps([], ["index.html", "plain.html"]);
        await using var app = (IAsyncDisposable)await Start();
        Assert.Equal([0xEF, 0xBB, 0xBF, .. "<head><script type=\"importmap\" data-husk>{\"imports\":{\"/A.svelte\":\"/A.svelte.js\"}}</script></head>\n"u8],
            File.ReadAllBytes(P("obj/pare/index.html")));
        Assert.Equal("<p>plain</p>\n", File.ReadAllText(P("obj/pare/plain.html")));

        File.WriteAllText(P("obj/husk/importmap.json"), "{\"imports\":{\"/A.svelte\":\"/A.svelte.js\",\"/B.svelte\":\"/B.svelte.js\"}}\n");
        File.WriteAllBytes(P("wwwroot/index.html"), [0xEF, 0xBB, 0xBF, .. "<head><title>t</title><script type=\"importmap\" data-husk></script></head>\n"u8]);
        Put("wwwroot/plain.html", "<p>plain two</p>\n");
        WaitFor(() => File.ReadAllText(P("obj/pare/index.html")).Contains("B.svelte"));
        Assert.Equal([0xEF, 0xBB, 0xBF, .. "<head><title>t</title><script type=\"importmap\" data-husk>{\"imports\":{\"/A.svelte\":\"/A.svelte.js\",\"/B.svelte\":\"/B.svelte.js\"}}</script></head>\n"u8],
            File.ReadAllBytes(P("obj/pare/index.html")));
        WaitFor(() => File.ReadAllText(P("obj/pare/plain.html")) == "<p>plain two</p>\n");
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
        var compiler = new HuskCompiler(new HuskSession(P("missing.json")));
        var result = await compiler.CompileAsync(P("wwwroot/Hello.svelte"), "<p/>", ComponentOutputs("Hello"), _log.CreateLogger("t"), TestContext.Current.CancellationToken);

        Assert.Null(result);
        Assert.Contains("missing.json is missing", _log.Lines(LogLevel.Error).Single(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_bad_map_is_logged_not_thrown_and_a_rebuilt_map_is_picked_up()
    {
        File.WriteAllText(HuskMap, "{\"compiler\": \"x\"}");
        File.SetLastWriteTimeUtc(HuskMap, DateTime.UtcNow.AddMinutes(-1));
        var compiler = new HuskCompiler(new HuskSession(HuskMap));
        var ct = TestContext.Current.CancellationToken;

        Assert.Null(await compiler.CompileAsync(P("wwwroot/Hello.svelte"), "<p>one</p>", ComponentOutputs("Hello"), _log.CreateLogger("t"), ct));
        Assert.Null(await compiler.CompileAsync(P("wwwroot/Hello.svelte"), "<p>one</p>", ComponentOutputs("Hello"), _log.CreateLogger("t"), ct));
        Assert.Equal(2, _log.Lines(LogLevel.Error).Count());

        WriteMaps();
        var outputs = await compiler.CompileAsync(P("wwwroot/Hello.svelte"), "<p>one</p>", ComponentOutputs("Hello"), _log.CreateLogger("t"), ct);
        Assert.Contains("one", outputs![ComponentOutputs("Hello")[0]]);
    }

    [Fact]
    public async Task A_map_fixed_within_the_same_timestamp_is_still_picked_up()
    {
        var stamp = DateTime.UtcNow.AddMinutes(-1);
        File.WriteAllText(HuskMap, "{\"compiler\": \"x\"");
        File.SetLastWriteTimeUtc(HuskMap, stamp);
        var compiler = new HuskCompiler(new HuskSession(HuskMap));
        var ct = TestContext.Current.CancellationToken;
        Assert.Null(await compiler.CompileAsync(P("wwwroot/Hello.svelte"), "<p>one</p>", ComponentOutputs("Hello"), _log.CreateLogger("t"), ct));

        WriteMaps();
        File.SetLastWriteTimeUtc(HuskMap, stamp);
        var outputs = await compiler.CompileAsync(P("wwwroot/Hello.svelte"), "<p>one</p>", ComponentOutputs("Hello"), _log.CreateLogger("t"), ct);
        Assert.Contains("one", outputs![ComponentOutputs("Hello")[0]]);
    }

    [Fact]
    public async Task An_output_the_compiler_does_not_write_is_HUSK101()
    {
        WriteMaps();
        var compiler = new HuskCompiler(new HuskSession(HuskMap));
        var result = await compiler.CompileAsync(P("wwwroot/Hello.svelte"), "<p/>", [P("obj/pare/Hello.other.js")], _log.CreateLogger("t"), TestContext.Current.CancellationToken);

        Assert.Null(result);
        Assert.Contains("error HUSK101", _log.Lines(LogLevel.Error).Single(), StringComparison.Ordinal);
    }

    [Fact]
    public void AddHusk_twice_registers_each_compiler_once()
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { EnvironmentName = "Development", ContentRootPath = _dir.FullName });
        HuskExtensions.AddHusk(builder, HuskMap);
        builder.AddHusk();

        Assert.Equal(["husk", "husk-page"], builder.Services.Where(s => s.ServiceType == typeof(IPareCompiler))
            .Select(s => ((IPareCompiler)s.ImplementationInstance!).Name));
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
