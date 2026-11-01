namespace Extrode.Husk.Integration;

/// <summary>A folder outside the project served as it is (spec 003 R8): the SDK's own items, no Husk item.</summary>
[Collection(nameof(PackedFeed))]
public sealed class StaticFolderTests
{
    [Fact]
    public void A_folder_outside_the_project_is_served_with_its_exclusions_left_out()
    {
        using var project = new TempProject();
        string outside = Path.Combine(Path.GetTempPath(), "husk-static-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(outside, "sub"));
            Directory.CreateDirectory(Path.Combine(outside, "_app"));
            File.WriteAllText(Path.Combine(outside, "logo.svg"), "<svg/>");
            File.WriteAllText(Path.Combine(outside, "sub", "a.mp3"), "x");
            File.WriteAllText(Path.Combine(outside, "_app", "old.js"), "x");
            File.WriteAllText(Path.Combine(outside, "_headers"), "x");
            project.Project(items: $"""
                <Content Include="{outside}/**" Exclude="{outside}/_app/**;{outside}/_headers" Link="wwwroot/%(RecursiveDir)%(Filename)%(Extension)" CopyToPublishDirectory="PreserveNewest" />
                """);

            var (exit, output) = project.Run("publish", "-nologo", "-c", "Release", "-o", "out", "-nodeReuse:false", "--disable-build-servers");

            Assert.True(exit == 0, output);
            Assert.True(File.Exists(project.P("out/wwwroot/logo.svg")), output);
            Assert.True(File.Exists(project.P("out/wwwroot/sub/a.mp3")));
            Assert.False(File.Exists(project.P("out/wwwroot/_app/old.js")));
            Assert.False(File.Exists(project.P("out/wwwroot/_headers")));
        }
        finally
        {
            Directory.Delete(outside, true);
        }
    }
}
