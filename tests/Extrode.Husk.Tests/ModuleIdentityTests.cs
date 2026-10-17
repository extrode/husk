using System.Text.Json.Nodes;
using Extrode.Husk.Compiler;

namespace Extrode.Husk.Tests;

/// <summary><c>.svelte.js</c> and <c>.svelte.ts</c> fixtures through compileModule, against Node.</summary>
public sealed class ModuleIdentityTests(CompilerFixture fixture) : IClassFixture<CompilerFixture>
{
    public static TheoryData<string, bool> Modules => ExpectedRow.Where(r => !r.Filename.EndsWith(".svelte", StringComparison.Ordinal));

    [Theory]
    [MemberData(nameof(Modules))]
    [Trait("Law", "L001")]
    public void Matches_node(string filename, bool dev)
    {
        var row = ExpectedRow.Find(filename, dev);

        row.AssertMatches(fixture.Compiler.Compile(row.Input, dev));
    }

    [Fact]
    public void A_svelte_ts_map_shows_the_typescript()
    {
        var input = ExpectedRow.Find("wwwroot/counter.svelte.ts", false).Input;

        var result = fixture.Compiler.Compile(input, dev: false);

        var map = JsonNode.Parse(result.Map!)!;
        Assert.Equal(input.Source, (string?)map["sourcesContent"]![0]);
        Assert.DoesNotContain(": number", result.Js);
    }

    [Fact]
    public void A_svelte_ts_that_pare_rejects_fails_at_the_typescript_position()
    {
        var input = new CompileInput("wwwroot/bad.svelte.ts", "let a = 1;\nenum E { A }\n");

        var result = fixture.Compiler.Compile(input, dev: false);

        Assert.Equal(("PARE100", 2, 0), (result.Error!.Code, result.Error.Line, result.Error.Column));
        Assert.Null(result.Js);
    }
}
