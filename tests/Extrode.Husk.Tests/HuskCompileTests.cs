using Extrode.Husk.Compiler;
using Extrode.Husk.Build;
using Microsoft.Build.Utilities;

namespace Extrode.Husk.Tests;

public sealed class HuskCompileTests : IDisposable
{
    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("husk-task-");
    private readonly FakeBuildEngine _engine = new();

    public void Dispose() => _dir.Delete(recursive: true);

    private string P(string relative) => Path.Combine(_dir.FullName, relative);

    // The fixture copied into the project, and the items Pare would hand the task for it.
    private TaskItem[] Source(string filename)
    {
        string path = P(filename);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.Copy(Path.Combine(RepoPaths.Fixtures, filename), path, overwrite: true);
        string[] extensions = filename.EndsWith(".svelte", StringComparison.Ordinal) ? [".svelte.js", ".svelte.css", ".svelte.js.map"] : [".js", ".js.map"];
        string name = Path.GetFileNameWithoutExtension(path);
        return [.. extensions.Select(e => new TaskItem(path, new Dictionary<string, string>
        {
            ["PareOutputExtension"] = e,
            ["PareOutputPath"] = P("obj/pare/" + name + e),
        }))];
    }

    private bool Run(bool dev, params TaskItem[][] sources) => new HuskCompile
    {
        BuildEngine = _engine,
        Sources = [.. sources.SelectMany(s => s)],
        ProjectDirectory = _dir.FullName,
        CompilerPath = ExpectedRow.CompilerPath,
        Dev = dev,
    }.Execute();

    [Fact]
    public void A_component_writes_its_three_outputs_as_node_does()
    {
        var row = ExpectedRow.Find("wwwroot/Styled.svelte", false);

        Assert.True(Run(false, Source(row.Filename)));

        string js = File.ReadAllText(P("obj/pare/Styled.svelte.js"));
        // Svelte's js.code has no trailing newline; WithMapUrl adds one before the comment.
        const string MapUrl = "\n//# sourceMappingURL=Styled.svelte.js.map\n";
        Assert.EndsWith(MapUrl, js);
        Assert.Equal(row.Js, ExpectedRow.Sha(js[..^MapUrl.Length]));
        Assert.Equal(row.Css, ExpectedRow.Sha(File.ReadAllText(P("obj/pare/Styled.svelte.css"))));
        Assert.Equal(row.Map, ExpectedRow.Sha(File.ReadAllText(P("obj/pare/Styled.svelte.js.map"))));
        Assert.Contains(_engine.Messages, m => m.StartsWith("husk: compiled 1 of 1 sources", StringComparison.Ordinal));
    }

    [Fact]
    public void A_component_without_style_writes_an_empty_stylesheet()
    {
        Assert.True(Run(false, Source("wwwroot/Plain.svelte")));

        Assert.Equal("", File.ReadAllText(P("obj/pare/Plain.svelte.css")));
    }

    [Theory]
    [InlineData("wwwroot/counter.svelte.ts", "counter.svelte")]
    [InlineData("wwwroot/clock.svelte.js", "clock.svelte")]
    public void A_module_writes_js_and_map_as_node_does(string filename, string name)
    {
        var row = ExpectedRow.Find(filename, true);

        Assert.True(Run(true, Source(filename)));

        string js = File.ReadAllText(P($"obj/pare/{name}.js"));
        string mapUrl = $"\n//# sourceMappingURL={name}.js.map\n";
        Assert.EndsWith(mapUrl, js);
        Assert.Equal(row.Js, ExpectedRow.Sha(js[..^mapUrl.Length]));
        Assert.Equal(row.Map, ExpectedRow.Sha(File.ReadAllText(P($"obj/pare/{name}.js.map"))));
    }

    [Fact]
    public void An_output_that_is_its_own_source_is_husk101_and_the_source_stays()
    {
        string path = P("clock.svelte.js");
        File.Copy(Path.Combine(RepoPaths.Fixtures, "wwwroot/clock.svelte.js"), path);
        string original = File.ReadAllText(path);
        TaskItem[] items =
        [
            new(path, new Dictionary<string, string> { ["PareOutputExtension"] = ".js", ["PareOutputPath"] = path }),
            new(path, new Dictionary<string, string> { ["PareOutputExtension"] = ".js.map", ["PareOutputPath"] = path + ".map" }),
        ];

        Assert.False(Run(true, items));

        Assert.Equal("HUSK101", Assert.Single(_engine.Errors).Code);
        Assert.Equal(original, File.ReadAllText(path));
    }

    [Fact]
    public void An_unwritable_output_is_husk102()
    {
        var items = Source("wwwroot/Plain.svelte");
        Directory.CreateDirectory(P("obj/pare/Plain.svelte.css"));

        Assert.False(Run(false, items));

        Assert.Equal("HUSK102", Assert.Single(_engine.Errors).Code);
    }

    [Fact]
    public void A_missing_source_is_husk102_and_the_rest_compile()
    {
        var plain = Source("wwwroot/Plain.svelte");
        var gone = Source("wwwroot/Styled.svelte");
        File.Delete(P("wwwroot/Styled.svelte"));

        Assert.False(Run(false, plain, gone));

        Assert.Equal("HUSK102", Assert.Single(_engine.Errors).Code);
        Assert.True(File.Exists(P("obj/pare/Plain.svelte.js")));
    }

    [Fact]
    public void A_compiler_that_cannot_start_is_husk100_per_source_not_a_crash()
    {
        Directory.CreateDirectory(P("compiler"));
        File.WriteAllText(P("compiler/index.js"), "this is not javascript (");
        var plain = Source("wwwroot/Plain.svelte");
        var styled = Source("wwwroot/Styled.svelte");

        bool ok = new HuskCompile
        {
            BuildEngine = _engine,
            Sources = [.. plain, .. styled],
            ProjectDirectory = _dir.FullName,
            CompilerPath = P("compiler/index.js"),
        }.Execute();

        Assert.False(ok);
        Assert.Equal([("HUSK100", P("wwwroot/Plain.svelte")), ("HUSK100", P("wwwroot/Styled.svelte"))],
            _engine.Errors.Select(e => (e.Code, e.File)));
        Assert.All(_engine.Errors, e => Assert.StartsWith("the compiler failed: ", e.Message, StringComparison.Ordinal));
        Assert.False(File.Exists(P("obj/pare/Plain.svelte.js")));
    }

    [Fact]
    public void A_rewritten_compiler_file_gets_a_fresh_compiler()
    {
        string compiler = P("compiler/index.js");
        Directory.CreateDirectory(P("compiler"));
        File.Copy(ExpectedRow.CompilerPath, compiler);

        var first = HuskCompile.CompilerFor(compiler);
        Assert.Same(first, HuskCompile.CompilerFor(compiler));
        File.SetLastWriteTimeUtc(compiler, DateTime.UtcNow.AddMinutes(1));

        Assert.NotSame(first, HuskCompile.CompilerFor(compiler));
    }

    [Fact]
    public void A_warning_is_husk200_at_the_position_node_reports()
    {
        Assert.True(Run(false, Source("wwwroot/Image.svelte")));

        var warning = Assert.Single(_engine.Warnings);
        Assert.Equal($"{P("wwwroot/Image.svelte")}(5,1): warning HUSK200: a11y_missing_attribute: `<img>` element should have an alt attribute https://svelte.dev/e/a11y_missing_attribute",
            FakeBuildEngine.Line(warning));
        Assert.Equal("a11y_missing_attribute@5:0", ExpectedRow.Find("wwwroot/Image.svelte", false).Diagnostics);
    }

    [Fact]
    public void An_error_is_husk100_with_its_position_and_the_old_outputs_stay()
    {
        var broken = Source("wwwroot/Broken.svelte");
        Directory.CreateDirectory(P("obj/pare"));
        foreach (var name in new[] { "Broken.svelte.js", "Broken.svelte.css", "Broken.svelte.js.map" })
            File.WriteAllText(P("obj/pare/" + name), "last good " + name);

        Assert.False(Run(false, broken, Source("wwwroot/Plain.svelte")));

        var error = Assert.Single(_engine.Errors);
        Assert.Equal("HUSK100", error.Code);
        Assert.Equal((P("wwwroot/Broken.svelte"), 5, 1), (error.File, error.LineNumber, error.ColumnNumber));
        Assert.StartsWith("block_unclosed: ", error.Message);
        foreach (var name in new[] { "Broken.svelte.js", "Broken.svelte.css", "Broken.svelte.js.map" })
            Assert.Equal("last good " + name, File.ReadAllText(P("obj/pare/" + name)));
        Assert.True(File.Exists(P("obj/pare/Plain.svelte.js")));
    }

    [Fact]
    public void A_component_missing_an_output_item_is_husk101()
    {
        var items = Source("wwwroot/Plain.svelte");

        Assert.False(Run(false, [items[0], items[2]]));

        Assert.Equal("HUSK101", Assert.Single(_engine.Errors).Code);
    }

    [Theory]
    [InlineData("a\n", "a\n//# sourceMappingURL=a.js.map\n")]
    [InlineData("a", "a\n//# sourceMappingURL=a.js.map\n")]
    public void The_map_url_goes_on_its_own_line(string js, string expected) =>
        Assert.Equal(expected, CompiledFiles.WithMapUrl(js, "a.js.map"));
}
