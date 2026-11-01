namespace Extrode.Husk.Integration.Laws;

/// <summary>L007: for every unsupported feature Husk detects (HUSK301, HUSK302, HUSK303), the build
/// fails and routes.js and the shell keep the bytes a good build gave them.</summary>
[Trait("Law", "L007")]
public sealed class L007NoPartialRouteTableTests
{
    private const string Routes = TempProject.HuskOut + "app/routes.js";
    private const string Shell = TempProject.HuskOut + "app/index.html";
    private const string Template = TempProject.HuskOut + "app/shell/index.html";

    private static TempProject App()
    {
        var project = new TempProject();
        project.Project(items: """
            <HuskComponent Include="src/**/*.svelte" PareServeAt="app" />
            <HuskModule Include="src/**/*.svelte.ts" PareServeAt="app" />
            <TypeScriptSource Include="src/**/*.ts" Exclude="src/**/*.svelte.ts" PareServeAt="app" />
            <HuskRoutes Include="src/routes" />
            <HuskAlias Include="$lib" Path="src/lib" />
            <HuskEnv Include="PUBLIC_BACKEND_URL" Value="https://api.test" />
            """);
        project.Put("src/routes/+layout.svelte", "<script>let { children } = $props();</script>\n<main>{@render children()}</main>\n");
        project.Put("src/routes/+page.svelte", "<h1>home</h1>\n");
        project.Put("src/routes/+error.svelte", "<p>error</p>\n");
        project.Put("src/routes/settings/[section]/+page.svelte", "<p>section</p>\n");
        project.Put("src/routes/settings/[section]/+page.ts", "export const load = () => ({ ok: 1 });\n");
        return project;
    }

    [Theory]
    [InlineData("HUSK301", "src/routes/files/[...rest]/+page.svelte", "<p>rest</p>\n")]
    [InlineData("HUSK301", "src/routes/api/+server.ts", "export const GET = () => new Response('x');\n")]
    [InlineData("HUSK302", "src/routes/month/+page.svelte", "<script>\n  import { enhance } from '$app/forms';\n</script>\n<form use:enhance></form>\n")]
    [InlineData("HUSK302", "src/routes/month/+page.svelte", "<script>\n  import { page } from '$app/stores';\n</script>\n<p>{$page.url}</p>\n")]
    [InlineData("HUSK303", "svelte.config.js", "export default {\n  kit: {\n    adapter: null,\n    prerender: { entries: [] }\n  }\n};\n")]
    public void An_unsupported_feature_fails_the_build_and_leaves_routes_js_and_the_shell_unchanged(string code, string file, string text)
    {
        using var project = App();
        var (exit, output) = project.Build();
        Assert.True(exit == 0, output);
        byte[] routes = File.ReadAllBytes(project.P(Routes));
        byte[] shell = File.ReadAllBytes(project.P(Shell));
        byte[] template = File.ReadAllBytes(project.P(Template));

        project.Put(file, text);
        (exit, output) = project.Build();

        Assert.True(exit != 0, output);
        Assert.Contains("error " + code, output);
        Assert.True(routes.AsSpan().SequenceEqual(File.ReadAllBytes(project.P(Routes))), $"{code} {file}: routes.js changed\n{output}");
        Assert.True(shell.AsSpan().SequenceEqual(File.ReadAllBytes(project.P(Shell))), $"{code} {file}: the shell changed\n{output}");
        Assert.True(template.AsSpan().SequenceEqual(File.ReadAllBytes(project.P(Template))), $"{code} {file}: the shell template changed\n{output}");
    }
}
