namespace Extrode.Husk.Tests.Laws;

/// <summary>L001: for every committed fixture and both dev values, Husk's compile gives Node's row
/// in tests/fixtures/expected.tsv.</summary>
[Trait("Law", "L001")]
public sealed class L001IdenticalToNodeTests(CompilerFixture fixture) : IClassFixture<CompilerFixture>
{
    public static TheoryData<string, bool> Rows => ExpectedRow.Where(_ => true);

    [Fact]
    public void Every_fixture_has_a_row_for_both_dev_values()
    {
        var sources = Directory.EnumerateFiles(Path.Combine(RepoPaths.Fixtures, "wwwroot"), "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(RepoPaths.Fixtures, f).Replace('\\', '/'))
            .Where(f => f.EndsWith(".svelte", StringComparison.Ordinal) || f.EndsWith(".svelte.js", StringComparison.Ordinal) || f.EndsWith(".svelte.ts", StringComparison.Ordinal))
            .SelectMany(f => new[] { (f, false), (f, true) })
            .Order();

        Assert.Equal(sources, ExpectedRow.All.Select(r => (r.Filename, r.Dev)).Order());
    }

    [Theory]
    [MemberData(nameof(Rows))]
    public void Husk_gives_node_s_row(string filename, bool dev)
    {
        var row = ExpectedRow.Find(filename, dev);

        row.AssertMatches(fixture.Compiler.Compile(row.Input, dev));
    }
}
