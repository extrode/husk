using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Extrode.Husk.Integration;

[Collection(nameof(PackedFeed))]
public sealed class SamplePublishTests(PackedFeed feed)
{
    private static string Sha(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private static (int Exit, string Output) Publish(TempProject project) =>
        project.Run("publish", "-nologo", "-c", "Release", "-o", "out", "-nodeReuse:false", "--disable-build-servers");

    [Fact]
    public void The_published_sample_carries_compressed_output_and_runtime_and_no_sources_or_dev_files()
    {
        using var sample = feed.Sample();

        var (exit, output) = Publish(sample);
        Assert.True(exit == 0, output);
        string root = sample.P("out/wwwroot");
        foreach (var file in new[] { "App.svelte.js", "Child.svelte.js", "counter.svelte.js", "format.js", "main.js", "index.html",
                     "_husk/husk.css", "_husk/svelte/src/internal/client/index.js", "_husk/esm-env/false.js" })
        {
            Assert.True(File.Exists(Path.Combine(root, file)), file);
            Assert.True(File.Exists(Path.Combine(root, file + ".br")), file + ".br");
            Assert.True(File.Exists(Path.Combine(root, file + ".gz")), file + ".gz");
        }
        var published = Directory.GetFiles(sample.P("out"), "*", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(sample.P("out"), f)).ToList();
        Assert.DoesNotContain(published.Select(f => f.EndsWith(".br", StringComparison.Ordinal) || f.EndsWith(".gz", StringComparison.Ordinal) ? f[..^3] : f),
            f => f.EndsWith(".svelte", StringComparison.Ordinal) || f.EndsWith(".ts", StringComparison.Ordinal));
        Assert.DoesNotContain(published, f => f.Contains("Extrode.Husk.development", StringComparison.Ordinal) || f.Contains("ClearScript", StringComparison.Ordinal));

        string page = File.ReadAllText(Path.Combine(root, "index.html"));
        int open = page.IndexOf("<script type=\"importmap\" data-husk>", StringComparison.Ordinal);
        Assert.True(open >= 0, page);
        string map = page[(page.IndexOf('>', open) + 1)..page.IndexOf("</script>", open, StringComparison.Ordinal)];
        string development = JsonDocument.Parse(map).RootElement.GetProperty("imports").GetProperty("esm-env/development").GetString()!;
        Assert.Matches(@"^/_husk/esm-env/false\.[0-9a-f]{10}\.js$", development);
        Assert.Equal("export default false;", File.ReadAllText(Path.Combine(root, development.TrimStart('/'))).Trim());
    }

    [Fact]
    public void Publishing_for_another_platform_compiles_on_this_one_and_carries_no_V8()
    {
        using var sample = feed.Consumer("""
            <PackageReference Include="Extrode.Pare.Build" Version="0.7.0" />
            <PackageReference Include="Extrode.Husk.Build" Version="$(HuskVersion)" />
            <PackageReference Include="Extrode.Husk.AspNetCore" Version="$(HuskVersion)" />
            """);
        string rid = OperatingSystem.IsLinux() ? "osx-arm64" : "linux-x64";

        var (exit, output) = sample.Run("publish", "-nologo", "-c", "Release", "-r", rid, "--self-contained", "false", "-o", "out", "-nodeReuse:false", "--disable-build-servers");
        Assert.True(exit == 0, output);
        Assert.True(File.Exists(sample.P("out/wwwroot/Styled.svelte.js")));
        Assert.Empty(Directory.GetFiles(sample.P("out"), "*ClearScript*", SearchOption.AllDirectories));
    }

    [Fact]
    public void Published_modules_are_Node_s_production_output()
    {
        using var project = feed.Consumer("""
            <PackageReference Include="Extrode.Pare.Build" Version="0.7.0" />
            <PackageReference Include="Extrode.Husk.Build" Version="$(HuskVersion)" />
            <PackageReference Include="Extrode.Husk.AspNetCore" Version="$(HuskVersion)" />
            """);
        var expected = File.ReadAllLines(Path.Combine(TempProject.Fixtures, "expected.tsv"))
            .Where(l => l.Length > 0 && l[0] != '#').Select(l => l.Split('\t'))
            .Where(f => f[1] == "false" && f[2] == "ok").ToList();
        foreach (var row in expected)
            project.Fixture(row[0]);
        project.Fixture("wwwroot/format.ts");

        var (exit, output) = Publish(project);
        Assert.True(exit == 0, output);
        Assert.NotEmpty(expected);
        foreach (var row in expected)
        {
            string source = Path.GetFileName(row[0]);
            string name = (source.EndsWith(".svelte", StringComparison.Ordinal) ? source : source[..source.LastIndexOf('.')]) + ".js";
            string js = project.Read("out/wwwroot/" + name);
            string suffix = $"\n//# sourceMappingURL={name}.map\n";
            Assert.EndsWith(suffix, js, StringComparison.Ordinal);
            Assert.True(row[3] == Sha(js[..^suffix.Length]), $"{name}: not Node's dev: false output");
        }
    }
}
