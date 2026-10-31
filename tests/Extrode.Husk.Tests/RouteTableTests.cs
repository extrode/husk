using Extrode.Husk.Compiler;

namespace Extrode.Husk.Tests;

public sealed class RouteTableTests
{
    private static RouteTableResult Build(params string[] files) => RouteTable.Build(files);

    [Fact]
    public void Folders_with_pages_become_routes_and_the_root_is_a_slash()
    {
        var result = Build("+page.svelte", "month/+page.svelte", "settings/+page.svelte", "month/Helper.svelte", "month/page.test.ts");

        Assert.True(result.Ok);
        Assert.Equal(["/", "/month", "/settings"], result.Routes.Select(r => r.Pattern));
    }

    [Fact]
    public void A_parameter_folder_is_a_parameter_and_a_group_adds_nothing()
    {
        var result = Build("(view)/month/+page.svelte", "settings/[section]/+page.svelte");

        Assert.Equal(["/month", "/settings/[section]"], result.Routes.Select(r => r.Pattern));
        Assert.Equal([new RouteSegment("section", true)], result.Routes[1].Segments.Skip(1));
    }

    [Fact]
    public void Layouts_nest_outermost_first_and_a_groups_layout_applies_to_its_routes_only()
    {
        var result = Build("+layout.svelte", "+layout.ts", "(view)/+layout.svelte", "(view)/month/+page.svelte", "settings/+page.svelte");

        var month = result.Routes.Single(r => r.Pattern == "/month");
        Assert.Equal(["+layout.svelte", "(view)/+layout.svelte"], month.Layouts.Select(l => l.Component));
        Assert.Equal("+layout.ts", month.Layouts[0].Load);
        Assert.Equal(["+layout.svelte"], result.Routes.Single(r => r.Pattern == "/settings").Layouts.Select(l => l.Component));
    }

    [Fact]
    public void A_literal_segment_comes_before_a_parameter_at_the_same_place()
    {
        var result = Build("settings/[section]/+page.svelte", "settings/appearance/+page.svelte");

        Assert.Equal(["/settings/appearance", "/settings/[section]"], result.Routes.Select(r => r.Pattern));
    }

    [Fact]
    public void Two_folders_with_one_address_are_a_clash_even_with_different_parameter_names()
    {
        var result = Build("a/[x]/+page.svelte", "(g)/a/[y]/+page.svelte");

        var clash = Assert.Single(result.Diagnostics);
        Assert.Equal("HUSK300", clash.Code);
        Assert.Single(result.Routes);
    }

    [Theory]
    [InlineData("a/[...rest]/+page.svelte")]
    [InlineData("a/[[opt]]/+page.svelte")]
    [InlineData("a/[id=int]/+page.svelte")]
    [InlineData("a/+server.ts")]
    [InlineData("a/+page.server.ts")]
    [InlineData("a/+layout.server.ts")]
    [InlineData("a/+page@.svelte")]
    [InlineData("a/+error.svelte")]
    [InlineData("a/+weird.svelte")]
    public void An_unsupported_feature_is_reported_with_its_file(string file)
    {
        var result = Build(file, "+page.svelte");

        var d = Assert.Single(result.Diagnostics);
        Assert.Equal("HUSK301", d.Code);
        Assert.Equal(file, d.File);
        Assert.False(result.Ok);
    }

    [Fact]
    public void The_root_error_page_is_found_and_files_not_starting_with_plus_are_ignored()
    {
        var result = Build("+error.svelte", "x.server.ts", "lib/+page.svelte");

        Assert.Equal("+error.svelte", result.ErrorPage);
        Assert.True(result.Ok);
    }

    [Fact]
    public void Two_load_files_for_one_page_are_a_clash()
    {
        var d = Assert.Single(Build("+page.svelte", "+page.ts", "+page.js").Diagnostics);
        Assert.Equal("HUSK300", d.Code);
    }

    [Fact]
    public void The_same_files_in_any_order_give_the_same_table()
    {
        string[] files = ["a/+page.svelte", "+layout.svelte", "[id]/+page.svelte", "a/b/+page.svelte", "+page.ts", "+page.svelte"];

        var one = Build(files);
        var two = Build(files.Reverse().ToArray());

        Assert.Equal(one.Routes.Select(r => (r.Pattern, r.Page, r.PageLoad)), two.Routes.Select(r => (r.Pattern, r.Page, r.PageLoad)));
    }

    [Fact]
    public void Backslash_paths_are_read_as_folders()
    {
        Assert.Equal(["/a/b"], Build("a\\b\\+page.svelte").Routes.Select(r => r.Pattern));
    }
}
