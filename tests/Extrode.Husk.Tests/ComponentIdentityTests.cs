namespace Extrode.Husk.Tests;

/// <summary>Every <c>.svelte</c> fixture compiles to the bytes Node's run of the same bundle gave.</summary>
public sealed class ComponentIdentityTests(CompilerFixture fixture) : IClassFixture<CompilerFixture>
{
    public static TheoryData<string, bool> Components => ExpectedRow.Where(r => r.Filename.EndsWith(".svelte", StringComparison.Ordinal));

    [Theory]
    [MemberData(nameof(Components))]
    [Trait("Law", "L001")]
    public void Matches_node(string filename, bool dev)
    {
        var row = ExpectedRow.Find(filename, dev);

        row.AssertMatches(fixture.Compiler.Compile(row.Input, dev));
    }

    [Fact]
    public void Warnings_and_errors_carry_their_position()
    {
        var image = fixture.Compiler.Compile(ExpectedRow.Find("wwwroot/Image.svelte", false).Input, dev: false);
        var broken = fixture.Compiler.Compile(ExpectedRow.Find("wwwroot/Broken.svelte", false).Input, dev: false);

        var warning = Assert.Single(image.Warnings);
        Assert.Equal(("a11y_missing_attribute", 5, 0), (warning.Code, warning.Line, warning.Column));
        Assert.Contains("alt", warning.Message);
        Assert.Equal(("block_unclosed", 5, 0), (broken.Error!.Code, broken.Error.Line, broken.Error.Column));
        Assert.Null(broken.Js);
    }

    [Fact]
    public void A_component_without_style_has_empty_css()
    {
        var plain = fixture.Compiler.Compile(ExpectedRow.Find("wwwroot/Plain.svelte", false).Input, dev: false);

        Assert.Equal("", plain.Css);
    }
}
