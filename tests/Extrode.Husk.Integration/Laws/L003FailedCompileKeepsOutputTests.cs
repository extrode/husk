namespace Extrode.Husk.Integration.Laws;

/// <summary>L003, the build half: a build of a source Svelte rejects leaves its last good outputs and
/// husk.css byte for byte. The Development half is in tests/Extrode.Husk.Tests/HuskDevelopmentTests.cs.</summary>
[Trait("Law", "L003")]
public sealed class L003FailedCompileKeepsOutputTests
{
    private static readonly string[] Broken =
    [
        "<p>one</p>\n{#if}\n",
        "<p\n",
        "<script>let x = ;</script>\n",
        "{#each items}\n",
        "<style>.a { color: red; </style>\n<p>x</p>\n",
    ];

    private static List<byte[]?> Snapshot(TempProject project) =>
        [.. new[] { "Styled.svelte.js", "Styled.svelte.css", "Styled.svelte.js.map" }.Select(n => TempProject.PareOut + n)
            .Append(TempProject.HuskOut + "husk.css")
            .Select(f => File.Exists(project.P(f)) ? File.ReadAllBytes(project.P(f)) : null)];

    [Fact]
    public void A_rejected_source_keeps_its_last_good_outputs_through_a_random_sequence()
    {
        using var project = new TempProject();
        project.Project();
        project.Fixture("wwwroot/Styled.svelte");
        project.Fixture("wwwroot/Plain.svelte");
        var random = new Random(3003);

        Assert.Equal(0, project.Build().Exit);
        var good = Snapshot(project);
        for (int i = 0; i < 6; i++)
        {
            string text = random.Next(3) == 0
                ? $"<p class=\"v{i}\">good {i}</p>\n<style>.v{i} {{ color: red; }}</style>\n"
                : Broken[random.Next(Broken.Length)];
            File.WriteAllText(project.P("wwwroot/Styled.svelte"), text);

            var (exit, output) = project.Build();

            if (text.StartsWith("<p class=\"v", StringComparison.Ordinal))
            {
                Assert.True(exit == 0, output);
                good = Snapshot(project);
                continue;
            }
            Assert.NotEqual(0, exit);
            Assert.Contains("error HUSK100", output, StringComparison.Ordinal);
            Assert.True(good.SequenceEqual(Snapshot(project), new BytesComparer()), $"step {i}, {text.Trim()}: a failed compile changed the last good output\n{output}");
        }
    }

    private sealed class BytesComparer : IEqualityComparer<byte[]?>
    {
        public bool Equals(byte[]? x, byte[]? y) => x is null ? y is null : y is not null && x.AsSpan().SequenceEqual(y);
        public int GetHashCode(byte[]? obj) => obj?.Length ?? 0;
    }
}
