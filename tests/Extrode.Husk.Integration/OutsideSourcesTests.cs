namespace Extrode.Husk.Integration;

public sealed class OutsideSourcesTests
{
    [Fact]
    public void Sources_outside_wwwroot_are_placed_by_PareServeAt()
    {
        using var project = new TempProject();
        project.Project(items: """
            <HuskComponent Include="src/**/*.svelte" PareServeAt="app" />
            <HuskModule Include="src/**/*.svelte.ts" PareServeAt="app" />
            """);
        project.Put("src/lib/A.svelte", "<p>a</p>\n");
        project.Put("src/lib/store.svelte.ts", "export const n: number = 1;\n");

        var (exit, output) = project.Build();

        Assert.True(exit == 0, output);
        Assert.True(File.Exists(project.P(TempProject.PareOut + "app/lib/A.svelte.js")), output);
        Assert.True(File.Exists(project.P(TempProject.PareOut + "app/lib/store.svelte.js")), output);
        Assert.Contains("\"/app/lib/A.svelte\"", project.Read(TempProject.HuskOut + "importmap.json"));
    }
}
