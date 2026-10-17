using System.Text.Json;
using Extrode.Husk.Build;
using Microsoft.Build.Utilities;

namespace Extrode.Husk.Tests;

public sealed class HuskAssetsTests : IDisposable
{
    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("husk-assets-");
    private readonly FakeBuildEngine _engine = new();

    public void Dispose() => _dir.Delete(recursive: true);

    private string P(string relative) => Path.Combine(_dir.FullName, relative);

    private TaskItem Output(string relative, string source, string compiler = "husk", string text = "")
    {
        string path = P("obj/pare/" + relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return new TaskItem(path, new Dictionary<string, string>
        {
            ["PareSource"] = P("wwwroot/" + source),
            ["PareRelativePath"] = relative,
            ["PareCompiler"] = compiler,
        });
    }

    private bool Run(string basePath, params TaskItem[] outputs) => new HuskAssets
    {
        BuildEngine = _engine,
        Outputs = outputs,
        BasePath = basePath,
        OutputDirectory = P("obj/husk"),
        RuntimeImports = Path.Combine(RepoPaths.Vendored, "runtime-imports.json"),
        CompilerAssembly = "/c/Extrode.Husk.Compiler.dll",
        CompilerPath = "/c/index.js",
        Dev = true,
    }.Execute();

    [Theory]
    [InlineData("", "/")]
    [InlineData("/", "/")]
    [InlineData("app", "/app/")]
    [InlineData("/app/", "/app/")]
    [InlineData("a\\b", "/a/b/")]
    public void A_base_path_becomes_a_url_folder(string basePath, string url) =>
        Assert.Equal(url, HuskAssets.Url(basePath));

    [Fact]
    public void Sources_below_a_base_path_and_in_folders_map_to_their_outputs()
    {
        Assert.True(Run("app",
            Output("ui/Child.svelte.js", "ui/Child.svelte"),
            Output("ui/Child.svelte.css", "ui/Child.svelte", text: ".a{}"),
            Output("ui/Child.svelte.js.map", "ui/Child.svelte"),
            Output("lib/format.js", "lib/format.ts", compiler: ""),
            Output("lib/format.js.map", "lib/format.ts", compiler: "")));

        var imports = JsonDocument.Parse(File.ReadAllText(P("obj/husk/importmap.json"))).RootElement.GetProperty("imports");
        Assert.Equal("/app/ui/Child.svelte.js", imports.GetProperty("/app/ui/Child.svelte").GetString());
        Assert.Equal("/app/ui/Child.svelte.js", imports.GetProperty("/app/ui/Child").GetString());
        Assert.Equal("/app/lib/format.js", imports.GetProperty("/app/lib/format.ts").GetString());
        Assert.Equal("/app/_husk/svelte/src/internal/client/index.js", imports.GetProperty("svelte/internal/client").GetString());
        Assert.False(imports.TryGetProperty("/app/ui/Child.svelte.js.map", out _));
        Assert.Equal("/* ui/Child.svelte */\n.a{}\n", File.ReadAllText(P("obj/husk/husk.css")));
    }

    [Fact]
    public void The_development_map_lists_each_component_stylesheet()
    {
        Assert.True(Run("/", Output("B.svelte.css", "B.svelte", text: ".b{}"), Output("A.svelte.css", "A.svelte")));

        var map = JsonDocument.Parse(File.ReadAllText(P("obj/husk/Extrode.Husk.development.json"))).RootElement;
        Assert.Equal("/c/Extrode.Husk.Compiler.dll", map.GetProperty("compiler").GetString());
        Assert.Equal("/c/index.js", map.GetProperty("svelte").GetString());
        Assert.True(map.GetProperty("dev").GetBoolean());
        Assert.Equal(P("obj/husk/husk.css"), map.GetProperty("stylesheet").GetString());
        Assert.Equal(["A.svelte", "B.svelte"], map.GetProperty("styles").EnumerateArray().Select(s => s.GetProperty("name").GetString()));
        Assert.Equal(P("obj/pare/A.svelte.css"), map.GetProperty("styles")[0].GetProperty("css").GetString());
    }

    [Fact]
    public void Unchanged_text_is_not_rewritten()
    {
        string path = P("same.txt");
        File.WriteAllText(path, "x");
        var stamp = DateTime.UtcNow.AddDays(-1);
        File.SetLastWriteTimeUtc(path, stamp);

        HuskAssets.WriteIfChanged(path, "x");
        Assert.Equal(stamp, File.GetLastWriteTimeUtc(path));

        HuskAssets.WriteIfChanged(path, "y");
        Assert.Equal("y", File.ReadAllText(path));
    }
}
