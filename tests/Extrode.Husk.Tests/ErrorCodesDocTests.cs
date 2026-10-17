using System.Text.RegularExpressions;

namespace Extrode.Husk.Tests;

/// <summary>Keeps docs/error-codes.md and the README in step with the codes Husk emits.</summary>
public sealed partial class ErrorCodesDocTests
{
    private static readonly string Doc = File.ReadAllText(Path.Combine(RepoPaths.Root, "docs", "error-codes.md")).ReplaceLineEndings("\n");

    // Every code the sources emit, with its kind: a task's LogError/LogWarning, a log line's
    // "error HUSK..."/"warning HUSK...", a targets file's <Error Code=...>.
    internal static Dictionary<string, string> Emitted()
    {
        var codes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(Path.Combine(RepoPaths.Root, "src"), "*", SearchOption.AllDirectories)
                     .Where(f => f.EndsWith(".cs", StringComparison.Ordinal) || f.EndsWith(".targets", StringComparison.Ordinal))
                     .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                                 && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
        {
            foreach (Match m in EmitSite().Matches(File.ReadAllText(file)))
            {
                string kind = m.Groups["kind"].Value is "LogWarning" or "warning" ? "warning" : "error";
                string code = m.Groups["code"].Value;
                Assert.False(codes.TryGetValue(code, out var seen) && seen != kind, $"{code} is emitted as both {seen} and {kind}");
                codes[code] = kind;
            }
        }
        return codes;
    }

    [Fact]
    public void Documents_every_code_and_no_other()
    {
        var emitted = Emitted();

        Assert.Equal(emitted.Keys.Order(StringComparer.Ordinal), Heading().Matches(Doc).Select(m => m.Groups[1].Value).Order(StringComparer.Ordinal));
        foreach (var (code, kind) in emitted)
            Assert.Matches($@"\n\| \[{code}\]\(#{code.ToLowerInvariant()}\) \| {kind}( \(MSBuild\))? \| ", Doc);
    }

    [Fact]
    public void Every_code_the_readme_names_is_emitted()
    {
        string readme = File.ReadAllText(Path.Combine(RepoPaths.Root, "README.md"));
        var emitted = Emitted();

        var named = Code().Matches(readme).Select(m => m.Value).Distinct().ToList();
        Assert.NotEmpty(named);
        Assert.All(named, code => Assert.True(emitted.ContainsKey(code), $"README names {code}, which nothing emits"));
        Assert.Contains("docs/error-codes.md", readme, StringComparison.Ordinal);
    }

    [GeneratedRegex(@"(?<kind>LogError|LogWarning)\(null, ""(?<code>HUSK\d{3})""|\b(?<kind>error|warning) (?<code>HUSK\d{3}):|<(?<kind>Error) Code=""(?<code>HUSK\d{3})""")]
    private static partial Regex EmitSite();

    [GeneratedRegex(@"\n## (HUSK\d{3})\n")]
    private static partial Regex Heading();

    [GeneratedRegex(@"HUSK\d{3}")]
    private static partial Regex Code();
}
