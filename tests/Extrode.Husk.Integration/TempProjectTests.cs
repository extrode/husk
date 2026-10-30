namespace Extrode.Husk.Integration;

public sealed class TempProjectTests
{
    [Fact]
    public void An_empty_web_project_builds_with_the_imports()
    {
        using var project = new TempProject();
        project.Project();

        var (exit, output) = project.Build();

        Assert.True(exit == 0, output);
        Assert.True(File.Exists(project.P("bin/Debug/net10.0/app.dll")), output);
    }
}
