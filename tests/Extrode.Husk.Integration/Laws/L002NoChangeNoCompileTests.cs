using System.Text.RegularExpressions;

namespace Extrode.Husk.Integration.Laws;

/// <summary>L002: after any sequence of builds, a build with nothing changed compiles nothing and
/// leaves every output, husk.css and importmap.json with its bytes and last-write time, hashed
/// copies included.</summary>
[Trait("Law", "L002")]
public sealed partial class L002NoChangeNoCompileTests
{
    private static Dictionary<string, (DateTime, string)> Snapshot(TempProject project) =>
        new[] { TempProject.PareOut, TempProject.HuskOut }
            .Where(d => Directory.Exists(project.P(d)))
            .SelectMany(d => Directory.EnumerateFiles(project.P(d), "*", SearchOption.AllDirectories))
            .ToDictionary(f => f, f => (File.GetLastWriteTimeUtc(f), Convert.ToHexString(File.ReadAllBytes(f))));

    private static void Later(TempProject project, string relative) =>
        File.SetLastWriteTimeUtc(project.P(relative), DateTime.UtcNow);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void After_each_step_of_a_random_sequence_a_build_with_no_change_compiles_nothing(bool hashNames)
    {
        string property = "-p:HuskHashNames=" + hashNames;
        using var project = new TempProject();
        project.Project();
        foreach (var file in new[] { "Styled.svelte", "Plain.svelte", "clock.svelte.js", "counter.svelte.ts", "format.ts" })
            project.Fixture("wwwroot/" + file);
        var steps = new (string Name, Action Apply)[]
        {
            ("touch Styled", () => Later(project, "wwwroot/Styled.svelte")),
            ("edit counter", () => { File.AppendAllText(project.P("wwwroot/counter.svelte.ts"), "\n// edited\n"); Later(project, "wwwroot/counter.svelte.ts"); }),
            ("add Image", () => project.Fixture("wwwroot/Image.svelte")),
            ("remove Plain", () => File.Delete(project.P("wwwroot/Plain.svelte"))),
            ("edit Styled style", () => { File.AppendAllText(project.P("wwwroot/Styled.svelte"), "\n<style>.extra { color: blue; }</style>\n"); Later(project, "wwwroot/Styled.svelte"); }),
            ("nothing", () => { }),
        };
        var random = new Random(2002);
        var applied = new List<string> { "first build" };

        Assert.Equal(0, project.Build(property).Exit);
        for (int i = 0; i < 6; i++)
        {
            var before = Snapshot(project);
            var (exit, output) = project.Build(property);

            Assert.True(exit == 0, output);
            Assert.True(Compiled(output) == 0, $"after {string.Join(", ", applied)}: a build with nothing changed compiled {Compiled(output)}\n{output}");
            var after = Snapshot(project);
            Assert.Equal(before.Keys.Order(), after.Keys.Order());
            foreach (var (file, state) in before)
                Assert.True(after[file] == state, $"after {string.Join(", ", applied)}: {file} was rewritten by a build with nothing changed");

            var step = steps[random.Next(steps.Length)];
            if (step.Name == "remove Plain" && !File.Exists(project.P("wwwroot/Plain.svelte")))
                step = steps[^1];
            step.Apply();
            applied.Add(step.Name);
            (exit, output) = project.Build(property);
            Assert.True(exit == 0, output);
        }
        if (hashNames)
            Assert.NotEmpty(Directory.EnumerateFiles(project.P(TempProject.HuskOut + "hashed"), "*.js", SearchOption.AllDirectories));
    }

    private static int? Compiled(string output) =>
        CompiledLine().Match(output) is { Success: true } m ? int.Parse(m.Groups[1].Value) : null;

    [GeneratedRegex(@"husk: compiled (\d+) of \d+ sources")]
    private static partial Regex CompiledLine();
}
