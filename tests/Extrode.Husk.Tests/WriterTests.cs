using System.Text.Json.Nodes;
using Extrode.Husk.Compiler;

namespace Extrode.Husk.Tests;

public sealed class StylesheetWriterTests
{
    [Fact]
    public void Components_join_in_ordinal_path_order_and_empty_css_is_skipped()
    {
        var text = StylesheetWriter.Join([
            ("wwwroot/b/Two.svelte", ".two.svelte-x{color:red}"),
            ("wwwroot/B.svelte", ".big.svelte-y{color:blue}\n"),
            ("wwwroot/a.svelte", ""),
        ]);

        Assert.Equal("/* wwwroot/B.svelte */\n.big.svelte-y{color:blue}\n/* wwwroot/b/Two.svelte */\n.two.svelte-x{color:red}\n", text);
    }

    [Fact]
    public void A_path_cannot_close_the_comment() =>
        Assert.StartsWith("/* wwwroot/a*\\/b.svelte */\n", StylesheetWriter.Join([("wwwroot/a*/b.svelte", ".a{}")]));

    [Fact]
    public void No_styles_is_an_empty_file() => Assert.Equal("", StylesheetWriter.Join([("wwwroot/a.svelte", "")]));
}

public sealed class ImportMapWriterTests
{
    private static readonly RuntimeImports Runtime = RuntimeImports.Load(Path.Combine(RepoPaths.Vendored, "runtime-imports.json"));

    private static JsonObject Imports(bool dev, params ImportMapSource[] sources) =>
        JsonNode.Parse(ImportMapWriter.Write(Runtime, "/_husk/", dev, sources))!["imports"]!.AsObject();

    [Fact]
    public void Runtime_specifiers_map_under_the_base()
    {
        var imports = Imports(dev: true);

        Assert.Equal("/_husk/svelte/src/internal/client/index.js", (string?)imports["svelte/internal/client"]);
        Assert.Equal("/_husk/clsx/dist/clsx.mjs", (string?)imports["clsx"]);
        Assert.Equal(Runtime.Imports.Count, imports.Count);
    }

    [Theory]
    [InlineData(true, "/_husk/esm-env/true.js")]
    [InlineData(false, "/_husk/esm-env/false.js")]
    public void Dev_picks_the_esm_env_development_file(bool dev, string file) =>
        Assert.Equal(file, (string?)Imports(dev)["esm-env/development"]);

    [Fact]
    public void Each_key_form_maps_to_the_output()
    {
        var imports = Imports(true,
            new("/x/Child.svelte", "/x/Child.svelte.js"),
            new("/x/counter.svelte.ts", "/x/counter.svelte.js"),
            new("/x/clock.svelte.js", "/x/clock.svelte.js"),
            new("/x/format.ts", "/x/format.js"));

        Assert.Equal("/x/Child.svelte.js", (string?)imports["/x/Child.svelte"]);
        Assert.Equal("/x/Child.svelte.js", (string?)imports["/x/Child"]);
        Assert.Equal("/x/counter.svelte.js", (string?)imports["/x/counter.svelte.ts"]);
        Assert.Equal("/x/counter.svelte.js", (string?)imports["/x/counter.svelte"]);
        Assert.Equal("/x/clock.svelte.js", (string?)imports["/x/clock.svelte"]);
        Assert.False(imports.ContainsKey("/x/clock.svelte.js"));
        Assert.Equal("/x/format.js", (string?)imports["/x/format.ts"]);
        Assert.Equal("/x/format.js", (string?)imports["/x/format"]);
    }

    [Fact]
    public void A_bare_key_two_sources_share_is_left_out()
    {
        var imports = Imports(true, new("/x/a.svelte", "/x/a.svelte.js"), new("/x/a.ts", "/x/a.js"));

        Assert.False(imports.ContainsKey("/x/a"));
        Assert.Equal("/x/a.svelte.js", (string?)imports["/x/a.svelte"]);
        Assert.Equal("/x/a.js", (string?)imports["/x/a.ts"]);
    }

    [Fact]
    public void The_map_uses_lf_and_can_be_inlined_in_a_script_tag()
    {
        string text = ImportMapWriter.Write(Runtime, "/_husk/", true, [new("/x/</script>.svelte", "/x/</script>.svelte.js")]);

        Assert.DoesNotContain("\r", text);
        Assert.DoesNotContain("<", text);
        Assert.Equal("/x/</script>.svelte.js", (string?)JsonNode.Parse(text)!["imports"]!["/x/</script>.svelte"]);
    }

    [Fact]
    public void Keys_are_in_ordinal_order_whatever_the_input_order()
    {
        ImportMapSource[] sources = [new("/z/Z.svelte", "/z/Z.svelte.js"), new("/a/A.svelte", "/a/A.svelte.js")];

        string forward = ImportMapWriter.Write(Runtime, "/_husk/", true, sources);
        string backward = ImportMapWriter.Write(Runtime, "/_husk/", true, sources.Reverse());

        Assert.Equal(forward, backward);
        var keys = JsonNode.Parse(forward)!["imports"]!.AsObject().Select(e => e.Key).ToList();
        Assert.Equal(keys.Order(StringComparer.Ordinal), keys);
    }
}
