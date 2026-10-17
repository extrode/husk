using System.Security.Cryptography;
using System.Text;
using Extrode.Husk.Compiler;

namespace Extrode.Husk.Integration;

/// <summary>Husk against Node over the corpora present locally (R4), from the rows
/// scripts/node-reference/corpus.sh wrote to tmp/corpus/. Not gating: a corpus that is not there is
/// skipped with a message.</summary>
[Trait("Category", "Corpus")]
public sealed class CorpusTests
{
    public static TheoryData<string, string> Corpora => new()
    {
        { "svelte", "../pare/.corpus/realworld/svelte" },
        { "saaaat", "../../ikhbat.org/saaaat.com" },
        { "riwalqalb", "../../ikhbat.org/riwalqalb.com" },
    };

    private static string Sha(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private static string Format(IEnumerable<CompileDiagnostic> diagnostics) =>
        string.Join(",", diagnostics.Select(d => $"{d.Code}@{d.Line}:{d.Column}")) is { Length: > 0 } s ? s : "-";

    // The row Node wrote, from Husk's result: the same columns, so a difference is a string compare.
    internal static string Row(CompileResult result, bool strip)
    {
        if (strip)
            return result.Error?.Code.StartsWith("PARE", StringComparison.Ordinal) == true ? "err\t-\t-\t-\tstrip" : "err\t-\t-\t-\tnot a PARE error";
        return result.Error is { } error
            ? $"err\t-\t-\t-\t{Format([error])}"
            : $"ok\t{Sha(result.Js!)}\t{Sha(result.Css!)}\t{Sha(result.Map!)}\t{Format(result.Warnings)}";
    }

    [Theory]
    [MemberData(nameof(Corpora))]
    public void Every_source_compiles_as_it_does_under_Node(string name, string relativeRoot)
    {
        string root = Path.GetFullPath(Path.Combine(TempProject.RepoRoot, relativeRoot));
        string rows = Path.Combine(TempProject.RepoRoot, "tmp", "corpus", name + ".tsv");
        if (!Directory.Exists(root))
            Assert.Skip($"no {name} corpus at {root}");
        if (!File.Exists(rows))
            Assert.Skip($"no reference rows at {rows}: run scripts/node-reference/corpus.sh");

        var expected = File.ReadAllLines(rows).Where(l => l.Length > 0 && l[0] != '#').Select(l => l.Split('\t', 3)).ToList();
        Assert.NotEmpty(expected);
        using var compiler = new SvelteCompiler(Path.Combine(TempProject.VendorDir, "svelte", "compiler", "index.js"));
        var differences = new List<string>();
        foreach (var dev in new[] { false, true })
        {
            var group = expected.Where(r => r[1] == (dev ? "true" : "false")).ToList();
            var results = compiler.CompileAll([.. group.Select(r => new CompileInput(r[0], File.ReadAllText(Path.Combine(root, r[0]))))], dev);
            for (int i = 0; i < group.Count; i++)
            {
                string actual = Row(results[i], group[i][2].EndsWith("\tstrip", StringComparison.Ordinal));
                if (actual != group[i][2])
                    differences.Add($"{group[i][0]} dev={dev}: Node {group[i][2]}, Husk {actual}");
            }
        }
        TestContext.Current.SendDiagnosticMessage($"{name}: {expected.Count} rows, {differences.Count} differences");
        Assert.True(differences.Count == 0, string.Join("\n", differences.Take(20)));
    }
}
