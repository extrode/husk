using Extrode.Husk.Compiler;

namespace Extrode.Husk.Tests;

public sealed class KitFeaturesTests
{
    private static string[] Messages(string source) => [.. KitFeatures.Scan(source).Select(f => $"{f.Line}:{f.Message}")];

    [Fact]
    public void The_supported_names_are_not_findings() =>
        Assert.Empty(Messages("""
            import { goto, beforeNavigate as before, afterNavigate, preloadCode } from '$app/navigation';
            import { page } from '$app/state';
            import { browser, dev } from '$app/environment';
            import { redirect, error, type Redirect } from '@sveltejs/kit';
            import type { Foo } from '$app/stores';
            import * as nav from '$app/navigation';
            """));

    [Fact]
    public void An_unsupported_name_module_or_default_import_is_named_with_its_line() =>
        Assert.Equal(
        [
            "1:invalidate from $app/navigation is not provided",
            "2:navigating from $app/state is not provided",
            "3:$app/stores is not provided",
            "4:$env/static/public is not provided",
            "6:json from @sveltejs/kit is not provided",
            "8:$app/state has no default export",
        ],
        Messages("""
            import { goto, invalidate } from '$app/navigation';
            import { page, navigating } from '$app/state';
            import { page as p } from '$app/stores';
            import { PUBLIC_X } from '$env/static/public';
            // a comment
            import { json,
              redirect } from '@sveltejs/kit';
            import stuff from '$app/state';
            """));

    [Fact]
    public void A_dynamic_import_of_an_unsupported_module_is_a_finding() =>
        Assert.Equal(["1:$app/forms is not provided"], Messages("const m = await import('$app/forms');"));

    [Fact]
    public void An_unsupported_dynamic_import_of_other_modules_is_left_alone() =>
        Assert.Empty(Messages("const m = await import('./x.js'); import y from 'svelte';"));

    [Fact]
    public void The_kit_options_the_app_may_keep_are_accepted_and_others_named()
    {
        const string Config = """
            const config = {
              preprocess: vitePreprocess(),
              kit: {
                adapter: adapter({ fallback: 'index.html', files: { x: 1 } }),
                alias: { $lib: 'src/lib' },
                paths: { relative: false },
                csp: { mode: 'hash' },
                version: { name: 'v1' },
                'serviceWorker': { register: false },
                // prerender: ignored in a comment
                files
              },
            };
            """;

        Assert.Equal(["the kit option version is not supported", "the kit option serviceWorker is not supported", "the kit option files is not supported"],
            KitFeatures.ScanConfig(Config).Select(f => f.Message));
        Assert.Equal([8, 9, 11], KitFeatures.ScanConfig(Config).Select(f => f.Line));
    }

    [Fact]
    public void A_config_with_no_kit_object_has_no_findings() =>
        Assert.Empty(KitFeatures.ScanConfig("export default { preprocess: [] };"));

    [Fact]
    public void Env_reads_are_rewritten_once()
    {
        const string Source = "const a = import.meta.env.PUBLIC_BACKEND_URL ?? ''; const b = import.meta.env;";
        string once = KitFeatures.RewriteEnv(Source);

        Assert.Equal("const a = globalThis.__huskEnv.PUBLIC_BACKEND_URL ?? ''; const b = globalThis.__huskEnv;", once);
        Assert.Equal(once, KitFeatures.RewriteEnv(once));
        Assert.Equal("import.meta.url", KitFeatures.RewriteEnv("import.meta.url"));
    }

    [Theory]
    [InlineData("a = import.meta.env.X; b = import.meta.env;")]
    [InlineData("import.meta.url; import.meta.environment")]
    public void The_development_rewrite_matches_the_build_rewrite(string source) =>
        Assert.Equal(KitFeatures.RewriteEnv(source), Extrode.Husk.AspNetCore.HuskCompiler.RewriteEnv(source));
}
