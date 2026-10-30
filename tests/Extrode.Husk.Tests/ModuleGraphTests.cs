using Extrode.Husk.Compiler;

namespace Extrode.Husk.Tests;

public sealed class ModuleGraphTests
{
    [Theory]
    [InlineData("x/Child.svelte.js", "x/Child.svelte.0123456789.js")]
    [InlineData("_husk/clsx/dist/clsx.mjs", "_husk/clsx/dist/clsx.0123456789.mjs")]
    [InlineData("a.b/c", "a.b/c.0123456789")]
    public void The_hash_goes_before_the_last_extension(string path, string hashed) =>
        Assert.Equal(hashed, ModuleGraph.Hashed(path, "0123456789"));

    [Fact]
    public void The_hash_is_ten_hex_digits_of_the_content()
    {
        string hash = ModuleGraph.Hash("a"u8.ToArray());

        Assert.Matches("^[0-9a-f]{10}$", hash);
        Assert.Equal(hash, ModuleGraph.Hash("a"u8.ToArray()));
        Assert.NotEqual(hash, ModuleGraph.Hash("b"u8.ToArray()));
    }

    [Fact]
    public void Static_imports_and_re_exports_are_found_in_order()
    {
        const string Code = """
            import 'svelte/internal/disclose-version';
            import * as $ from "svelte/internal/client";
            import Child, {
              a as b,
            } from './Child.svelte';
            import{c}from"./c.js";
            export * from '../d.js';
            export { e } from './e.js';
            /** @import { T } from './types.js' */
            const lazy = import('./lazy.js');
            if (import.meta.hot) {}
            export const f = 'not-a-module';
            """;

        Assert.Equal(["svelte/internal/disclose-version", "svelte/internal/client", "./Child.svelte", "./c.js", "../d.js", "./e.js"],
            ModuleGraph.Imports(Code));
    }

    [Theory]
    [InlineData("./Child.svelte", "/x/App.svelte.js", "/x/Child.svelte.h.js")]
    [InlineData("../lib/a.js", "/x/App.svelte.js", "/lib/a.js")]
    [InlineData("/x/Child.svelte", "/", "/x/Child.svelte.h.js")]
    [InlineData("svelte", "/x/App.svelte.js", "/_husk/svelte.h.js")]
    public void A_specifier_resolves_against_its_module_then_through_the_map(string specifier, string baseUrl, string url) =>
        Assert.Equal(url, ModuleGraph.Resolve(specifier, baseUrl, new Dictionary<string, string>
        {
            ["/x/Child.svelte"] = "/x/Child.svelte.h.js",
            ["svelte"] = "/_husk/svelte.h.js",
        }));

    [Theory]
    [InlineData("unknown")]
    [InlineData("https://cdn.example/a.js")]
    [InlineData("//cdn.example/a.js")]
    public void A_bare_specifier_the_map_does_not_name_or_a_full_url_resolves_to_nothing(string specifier) =>
        Assert.Null(ModuleGraph.Resolve(specifier, "/", new Dictionary<string, string>()));
}
