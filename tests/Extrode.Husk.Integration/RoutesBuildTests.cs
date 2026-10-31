namespace Extrode.Husk.Integration;

public sealed class RoutesBuildTests
{
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

    [Fact]
    public void A_routes_folder_builds_into_the_route_table_the_runtime_and_a_shell_page()
    {
        using var project = App();

        var (exit, output) = project.Build();

        Assert.True(exit == 0, output);
        string routes = project.Read(TempProject.HuskOut + "app/routes.js");
        Assert.Contains("/app/routes/settings/[section]/+page.svelte", routes);
        Assert.True(File.Exists(project.P(TempProject.HuskOut + "app/router.js")));
        string shell = project.Read(TempProject.HuskOut + "app/index.html");
        Assert.Contains("id=\"husk-body\"", shell);
        Assert.Contains("\"imports\"", shell);
        string map = project.Read(TempProject.HuskOut + "importmap.json");
        Assert.Contains("\"$app/navigation\"", map);
        Assert.Contains("\"/_husk/Root.svelte\"", map);
    }

    [Fact]
    public void Two_route_folders_with_one_address_fail_the_build_and_write_nothing()
    {
        using var project = App();
        project.Put("src/routes/(a)/x/+page.svelte", "<p>a</p>\n");
        project.Put("src/routes/(b)/x/+page.svelte", "<p>b</p>\n");

        var (exit, output) = project.Build();

        Assert.NotEqual(0, exit);
        Assert.Contains("HUSK300", output);
        Assert.False(File.Exists(project.P(TempProject.HuskOut + "app/routes.js")));
    }

    [Fact]
    public void An_unsupported_import_fails_the_build_with_its_file_and_line_and_writes_nothing()
    {
        using var project = App();
        project.Put("src/routes/month/+page.svelte", "<script>\n  import { page } from '$app/stores';\n</script>\n");

        var (exit, output) = project.Build();

        Assert.NotEqual(0, exit);
        Assert.Contains("month/+page.svelte(2,1): error HUSK302: $app/stores is not provided", output.Replace("\\", "/"));
        Assert.False(File.Exists(project.P(TempProject.HuskOut + "app/routes.js")));
    }

    [Fact]
    public void An_unsupported_kit_option_fails_the_build()
    {
        using var project = App();
        project.Put("svelte.config.js", "export default {\n  kit: {\n    adapter: null,\n    prerender: { entries: [] }\n  }\n};\n");

        var (exit, output) = project.Build();

        Assert.NotEqual(0, exit);
        Assert.Contains("svelte.config.js(4,1): error HUSK303: the kit option prerender is not supported", output);
    }

    [Fact]
    public void Env_reads_in_the_apps_outputs_use_the_page_global_and_a_second_build_changes_nothing()
    {
        using var project = App();
        project.Put("src/lib/util.ts", "export const sections = ['appearance'];\nexport const backend = import.meta.env.PUBLIC_BACKEND_URL ?? '';\n");

        Assert.Equal(0, project.Build().Exit);
        string output = project.Read(TempProject.PareOut + "app/lib/util.js");
        Assert.Contains("globalThis.__huskEnv.PUBLIC_BACKEND_URL", output);
        Assert.DoesNotContain("import.meta.env", output);
        Assert.Contains("\"PUBLIC_BACKEND_URL\": \"https://api.test\"", project.Read(TempProject.HuskOut + "app/index.html"));
        var stamp = project.Stamp(TempProject.PareOut + "app/lib/util.js");

        Assert.Equal(0, project.Build().Exit);

        Assert.Equal(stamp, project.Stamp(TempProject.PareOut + "app/lib/util.js"));
    }
}
