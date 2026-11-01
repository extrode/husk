using Extrode.Husk.Compiler;

namespace Extrode.Husk.Tests;

[Trait("Law", "L006")]
public sealed class L006OnePatternPerRouteFolderTests
{
    private static readonly string[] Names = ["a", "b", "(g)", "(h)", "[x]", "[y]"];

    private static string Pattern(string folder)
    {
        var parts = folder.Split('/', StringSplitOptions.RemoveEmptyEntries).Where(p => !(p.StartsWith('(') && p.EndsWith(')')));
        return "/" + string.Join('/', parts);
    }

    private static string Key(string folder) =>
        System.Text.RegularExpressions.Regex.Replace(Pattern(folder), @"\[[^\]]+\]", "[]");

    [Fact]
    public void Every_generated_route_tree_gives_each_page_folder_one_pattern_and_fails_on_two_folders_with_one()
    {
        var random = new Random(4006);
        int clashes = 0;
        for (int tree = 0; tree < 300; tree++)
        {
            var folders = new SortedSet<string>(StringComparer.Ordinal);
            for (int n = random.Next(1, 8); n > 0; n--)
                folders.Add(string.Join('/', Enumerable.Range(0, random.Next(0, 4)).Select(_ => Names[random.Next(Names.Length)])));
            var files = folders.Select(f => f.Length == 0 ? "+page.svelte" : f + "/+page.svelte").ToArray();

            var result = RouteTable.Build(files);

            string context = $"tree {tree}: {string.Join(", ", files)}";
            bool clash = folders.GroupBy(Key).Any(g => g.Count() > 1);
            if (clash)
            {
                clashes++;
                Assert.False(result.Ok, context);
                Assert.Contains(result.Diagnostics, d => d.Code == "HUSK300");
                Assert.All(result.Diagnostics, d => Assert.Equal("HUSK300", d.Code));
            }
            else
            {
                Assert.True(result.Ok, context);
                Assert.Equal(folders.Select(Pattern).Order(StringComparer.Ordinal), result.Routes.Select(r => r.Pattern).Order(StringComparer.Ordinal));
                Assert.Equal(folders.Count, result.Routes.Count);
            }
        }
        Assert.InRange(clashes, 20, 280);
    }
}
