using System.Text.Json.Nodes;

namespace Extrode.Husk.Tests;

public sealed class RuntimeImportsTests
{
    // Server and compiler entry points: no browser module to serve.
    private static readonly string[] Excluded = ["./compiler", "./internal/server", "./server"];

    private static readonly JsonObject Imports =
        JsonNode.Parse(File.ReadAllText(Path.Combine(RepoPaths.Vendored, "runtime-imports.json")))!["imports"]!.AsObject();

    private static string? BrowserTarget(JsonNode? target) => target switch
    {
        JsonValue value => value.GetValue<string>(),
        JsonObject map => BrowserTarget(map["browser"] ?? map["default"]),
        _ => null,
    };

    [Fact]
    public void Every_svelte_subpath_a_browser_loads_from_src_has_an_entry()
    {
        var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(RepoPaths.Vendored, "svelte", "package.json")))!;
        var missing = new List<string>();
        foreach (var (subpath, target) in manifest["exports"]!.AsObject())
        {
            string? file = BrowserTarget(target);
            if (file is null || !file.StartsWith("./src/", StringComparison.Ordinal))
                continue;
            if (!Imports.ContainsKey("svelte" + subpath[1..]))
                missing.Add(subpath);
        }

        Assert.Equal(Excluded, missing.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Every_entry_names_a_vendored_module()
    {
        foreach (var (specifier, file) in Imports)
            Assert.True(File.Exists(Path.Combine(RepoPaths.Vendored, (string)file!)), specifier);
    }

    [Theory]
    [InlineData("svelte", "svelte/src/index-client.js")]
    [InlineData("svelte/internal/client", "svelte/src/internal/client/index.js")]
    [InlineData("svelte/transition", "svelte/src/transition/index.js")]
    [InlineData("#client/constants", "svelte/src/internal/client/constants.js")]
    [InlineData("esm-env/development", "esm-env/true.js")]
    [InlineData("esm-env/browser", "esm-env/true.js")]
    [InlineData("clsx", "clsx/dist/clsx.mjs")]
    public void Browser_conditions_pick_the_client_files(string specifier, string file) =>
        Assert.Equal(file, (string?)Imports[specifier]);
}
