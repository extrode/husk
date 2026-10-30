using System.Security.Cryptography;
using System.Text;
using Extrode.Husk.Compiler;

namespace Extrode.Husk.Tests;

/// <summary>A row of tests/fixtures/expected.tsv, the Node reference (scripts/node-reference).</summary>
public sealed record ExpectedRow(string Filename, bool Dev, string Result, string Js, string Css, string Map, string Diagnostics)
{
    public static readonly string CompilerPath = Path.Combine(RepoPaths.Vendored, "svelte", "compiler", "index.js");

    public static IReadOnlyList<ExpectedRow> All { get; } = File.ReadAllLines(Path.Combine(RepoPaths.Fixtures, "expected.tsv"))
        .Where(l => l.Length > 0 && l[0] != '#')
        .Select(l => l.Split('\t'))
        .Select(f => new ExpectedRow(f[0], bool.Parse(f[1]), f[2], f[3], f[4], f[5], f[6]))
        .ToList();

    public static TheoryData<string, bool> Where(Func<ExpectedRow, bool> predicate)
    {
        var data = new TheoryData<string, bool>();
        foreach (var row in All.Where(predicate))
            data.Add(row.Filename, row.Dev);
        return data;
    }

    public static ExpectedRow Find(string filename, bool dev) => All.Single(r => r.Filename == filename && r.Dev == dev);

    public CompileInput Input => new(Filename, File.ReadAllText(Path.Combine(RepoPaths.Fixtures, Filename)));

    public static string Sha(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    public static string Format(IEnumerable<CompileDiagnostic> diagnostics) =>
        string.Join(",", diagnostics.Select(d => $"{d.Code}@{d.Line}:{d.Column}")) is { Length: > 0 } s ? s : "-";

    /// <summary>Asserts the result is the one Node gave, field by field.</summary>
    public void AssertMatches(CompileResult result)
    {
        if (Result == "err")
        {
            Assert.False(result.Succeeded);
            Assert.Equal(Diagnostics, Format([result.Error!]));
            return;
        }
        Assert.True(result.Succeeded, result.Error?.Message);
        Assert.Equal(Js, Sha(result.Js!));
        Assert.Equal(Css, Sha(result.Css!));
        Assert.Equal(Map, Sha(result.Map!));
        Assert.Equal(Diagnostics, Format(result.Warnings));
    }
}

/// <summary>One compiler for a test class: engine start is the slow part.</summary>
public sealed class CompilerFixture : IDisposable
{
    public SvelteCompiler Compiler { get; } = new(ExpectedRow.CompilerPath);

    public void Dispose() => Compiler.Dispose();
}
