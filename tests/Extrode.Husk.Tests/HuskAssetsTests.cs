using System.Text.Json;
using Extrode.Husk.Build;
using Extrode.Husk.Compiler;
using Microsoft.Build.Utilities;

namespace Extrode.Husk.Tests;

public sealed class HuskAssetsTests : IDisposable
{
    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("husk-assets-");
    private readonly FakeBuildEngine _engine = new();

    public void Dispose() => _dir.Delete(recursive: true);

    private string P(string relative) => Path.Combine(_dir.FullName, relative.Replace('/', Path.DirectorySeparatorChar));

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
        ServedRoot = P("obj/pare"),
        RuntimeImports = Path.Combine(RepoPaths.Vendored, "runtime-imports.json"),
        CompilerAssembly = "/c/Extrode.Husk.Compiler.dll",
        CompilerPath = "/c/index.js",
        ProjectDirectory = _dir.FullName,
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

    [Theory]
    [InlineData("/obj/pare/a.js", "/obj/pare", StringComparison.Ordinal, true)]
    [InlineData("/obj/pare/a.js", "/obj/pare/", StringComparison.Ordinal, true)]
    [InlineData("/obj/Pare/a.js", "/obj/pare", StringComparison.Ordinal, false)]
    [InlineData("/obj/Pare/a.js", "/obj/pare", StringComparison.OrdinalIgnoreCase, true)]
    [InlineData("/obj/pare-old/a.js", "/obj/pare", StringComparison.OrdinalIgnoreCase, false)]
    public void Served_means_inside_the_folder_with_the_platforms_case_rule(string path, string root, StringComparison comparison, bool served) =>
        Assert.Equal(served, HuskAssets.IsServed(path.Replace('/', Path.DirectorySeparatorChar), root.Replace('/', Path.DirectorySeparatorChar), comparison));

    [Fact]
    public void An_unserved_output_is_left_out_of_the_map()
    {
        string path = P("Scripts/tool.js");
        Directory.CreateDirectory(P("Scripts"));
        File.WriteAllText(path, "");
        var unserved = new TaskItem(path, new Dictionary<string, string>
        {
            ["PareSource"] = P("Scripts/tool.ts"),
            ["PareRelativePath"] = "Scripts/tool.js",
            ["PareCompiler"] = "",
        });

        Assert.True(Run("/", unserved, Output("a.js", "a.ts", compiler: "")));

        string map = File.ReadAllText(P("obj/husk/importmap.json"));
        Assert.Contains("\"/a.ts\"", map);
        Assert.DoesNotContain("tool", map);
    }

    [Fact]
    public void The_development_map_lists_each_component_stylesheet()
    {
        Assert.True(Run("/", Output("B.svelte.css", "B.svelte", text: ".b{}"), Output("A.svelte.css", "A.svelte")));

        var map = JsonDocument.Parse(File.ReadAllText(P("obj/husk/Extrode.Husk.development.json"))).RootElement;
        Assert.Equal("/c/Extrode.Husk.Compiler.dll", map.GetProperty("compiler").GetString());
        Assert.Equal("/c/index.js", map.GetProperty("svelte").GetString());
        Assert.Equal(_dir.FullName, map.GetProperty("projectDirectory").GetString());
        Assert.True(map.GetProperty("dev").GetBoolean());
        Assert.Equal(P("obj/husk/husk.css"), map.GetProperty("stylesheet").GetString());
        Assert.Equal(["A.svelte", "B.svelte"], map.GetProperty("styles").EnumerateArray().Select(s => s.GetProperty("name").GetString()));
        Assert.Equal(P("obj/pare/A.svelte.css"), map.GetProperty("styles")[0].GetProperty("css").GetString());
    }

    [Fact]
    public void The_development_map_records_whether_names_are_hashed()
    {
        Assert.True(Run("/", Output("A.svelte.css", "A.svelte")));
        Assert.False(JsonDocument.Parse(File.ReadAllText(P("obj/husk/Extrode.Husk.development.json"))).RootElement.GetProperty("hashNames").GetBoolean());

        Assert.True(RunHashed(Output("A.svelte.js", "A.svelte", text: "a")));
        Assert.True(JsonDocument.Parse(File.ReadAllText(P("obj/husk/Extrode.Husk.development.json"))).RootElement.GetProperty("hashNames").GetBoolean());
    }

    private bool RunHashed(params TaskItem[] outputs)
    {
        string runtime = P("vendor/svelte/src/index-client.js");
        Directory.CreateDirectory(Path.GetDirectoryName(runtime)!);
        File.WriteAllText(runtime, "export * from './internal/client/index.js';\n");
        return new HuskAssets
        {
            BuildEngine = _engine,
            Outputs = outputs,
            OutputDirectory = P("obj/husk"),
            ServedRoot = P("obj/pare"),
            RuntimeImports = Path.Combine(RepoPaths.Vendored, "runtime-imports.json"),
            CompilerAssembly = "/c/Extrode.Husk.Compiler.dll",
            CompilerPath = "/c/index.js",
            ProjectDirectory = _dir.FullName,
            HashNames = true,
            Runtime = [new TaskItem(runtime, new Dictionary<string, string> { ["RelativePath"] = "_husk/svelte/src/index-client.js" })],
        }.Execute();
    }

    [Fact]
    public void Hashed_names_get_byte_identical_copies_a_map_to_them_and_a_graph()
    {
        const string App = "import { mount } from 'svelte';\nimport Child from './Child.svelte';\n";
        Assert.True(RunHashed(Output("x/App.svelte.js", "x/App.svelte", text: App), Output("x/Child.svelte.js", "x/Child.svelte", text: "export default 1;\n")));

        string app = "x/App.svelte." + ModuleGraph.Hash(File.ReadAllBytes(P("obj/pare/x/App.svelte.js"))) + ".js";
        string child = "x/Child.svelte." + ModuleGraph.Hash(File.ReadAllBytes(P("obj/pare/x/Child.svelte.js"))) + ".js";
        string runtime = "_husk/svelte/src/index-client." + ModuleGraph.Hash(File.ReadAllBytes(P("vendor/svelte/src/index-client.js"))) + ".js";
        Assert.Equal(App, File.ReadAllText(P("obj/husk/hashed/" + app)));
        Assert.True(File.Exists(P("obj/husk/hashed/" + runtime)));

        var imports = JsonDocument.Parse(File.ReadAllText(P("obj/husk/importmap.json"))).RootElement.GetProperty("imports");
        Assert.Equal("/" + child, imports.GetProperty("/x/Child.svelte").GetString());
        Assert.Equal("/" + child, imports.GetProperty("/x/Child.svelte.js").GetString());
        Assert.Equal("/" + runtime, imports.GetProperty("svelte").GetString());

        var graph = JsonDocument.Parse(File.ReadAllText(P("obj/husk/modulegraph.json"))).RootElement;
        Assert.Equal(["/" + runtime, "/" + child], graph.GetProperty("/" + app).EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public void A_truncated_copy_is_written_again_and_no_temp_file_is_left()
    {
        Assert.True(RunHashed(Output("A.svelte.js", "A.svelte", text: "export default 1;\n")));
        string a = Directory.GetFiles(P("obj/husk/hashed"), "A.svelte.*.js").Single();
        File.WriteAllText(a, "");

        Assert.True(RunHashed(Output("A.svelte.js", "A.svelte", text: "export default 1;\n")));

        Assert.Equal("export default 1;\n", File.ReadAllText(a));
        Assert.Empty(Directory.GetFiles(P("obj/husk/hashed"), "*.husk-tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public void A_changed_module_replaces_its_copy_and_leaves_the_others_untouched()
    {
        Assert.True(RunHashed(Output("A.svelte.js", "A.svelte", text: "a"), Output("B.svelte.js", "B.svelte", text: "b")));
        string a = Directory.GetFiles(P("obj/husk/hashed"), "A.svelte.*.js").Single();
        string b = Directory.GetFiles(P("obj/husk/hashed"), "B.svelte.*.js").Single();
        var stamp = DateTime.UtcNow.AddDays(-1);
        File.SetLastWriteTimeUtc(b, stamp);

        Assert.True(RunHashed(Output("A.svelte.js", "A.svelte", text: "a2"), Output("B.svelte.js", "B.svelte", text: "b")));

        Assert.False(File.Exists(a));
        Assert.Equal("a2", File.ReadAllText(Directory.GetFiles(P("obj/husk/hashed"), "A.svelte.*.js").Single()));
        Assert.Equal(stamp, File.GetLastWriteTimeUtc(b));
    }

    [Fact]
    public void Aliases_imports_and_the_router_shims_get_import_map_keys()
    {
        string lib = P("src/lib/util.ts");
        var util = new TaskItem(P("obj/pare/app/lib/util.js"), new Dictionary<string, string>
        {
            ["PareSource"] = lib, ["PareRelativePath"] = "app/lib/util.js", ["PareCompiler"] = "",
        });
        Directory.CreateDirectory(P("obj/pare/app/lib"));
        File.WriteAllText(util.ItemSpec, "");

        Assert.True(new HuskAssets
        {
            BuildEngine = _engine,
            Outputs = [util],
            OutputDirectory = P("obj/husk"),
            ServedRoot = P("obj/pare"),
            RuntimeImports = Path.Combine(RepoPaths.Vendored, "runtime-imports.json"),
            CompilerAssembly = "/c/Extrode.Husk.Compiler.dll",
            CompilerPath = "/c/index.js",
            ProjectDirectory = _dir.FullName,
            Aliases = [new TaskItem("$lib", new Dictionary<string, string> { ["Path"] = "src/lib", ["Entry"] = "util.ts" })],
            Imports = [new TaskItem("@capacitor/core", new Dictionary<string, string> { ["Url"] = "https://cdn.example/core.js" })],
            Routing = true,
        }.Execute());

        var imports = JsonDocument.Parse(File.ReadAllText(P("obj/husk/importmap.json"))).RootElement.GetProperty("imports");
        Assert.Equal("/app/lib/util.js", imports.GetProperty("$lib/util.ts").GetString());
        Assert.Equal("/app/lib/util.js", imports.GetProperty("$lib").GetString());
        Assert.Equal("/app/lib/util.js", imports.GetProperty("$lib/util").GetString());
        Assert.Equal("/app/lib/util.js", imports.GetProperty("$lib/util.js").GetString());
        Assert.Equal("https://cdn.example/core.js", imports.GetProperty("@capacitor/core").GetString());
        Assert.Equal("/_husk/app/navigation.js", imports.GetProperty("$app/navigation").GetString());
        Assert.Equal("/_husk/kit.js", imports.GetProperty("@sveltejs/kit").GetString());
    }

    [Fact]
    public void Without_hashed_names_no_copy_is_left()
    {
        Assert.True(RunHashed(Output("A.svelte.js", "A.svelte", text: "a")));

        Assert.True(Run("/", Output("A.svelte.js", "A.svelte", text: "a")));

        Assert.Empty(Directory.GetFiles(P("obj/husk/hashed"), "*", SearchOption.AllDirectories));
        Assert.Equal("/A.svelte.js", JsonDocument.Parse(File.ReadAllText(P("obj/husk/importmap.json"))).RootElement.GetProperty("imports").GetProperty("/A.svelte").GetString());
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
