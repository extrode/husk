using System.Text.RegularExpressions;

namespace Extrode.Husk.Tests;

/// <summary>The public site is built from README.md and the numbered sections of docs/. A link that
/// points at a moved or renamed page is a dead link on husk.extrode.com.</summary>
public sealed class DocsLinksTests
{
    public static TheoryData<string> Pages()
    {
        var data = new TheoryData<string> { "README.md" };
        foreach (string file in Directory.EnumerateFiles(Path.Combine(RepoPaths.Root, "docs"), "*.md", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(RepoPaths.Root, file).Replace('\\', '/');
            if (Regex.IsMatch(relative, @"^docs/0\d-")) data.Add(relative);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(Pages))]
    public void Every_relative_link_and_image_points_at_a_file_that_exists(string page)
    {
        string path = Path.Combine(RepoPaths.Root, page);
        string dir = Path.GetDirectoryName(path)!;
        var missing = new List<string>();
        foreach (Match m in Regex.Matches(File.ReadAllText(path), @"\]\(([^)\s]+)\)"))
        {
            string target = m.Groups[1].Value.Split('#')[0];
            if (target.Length == 0 || target.Contains("://", StringComparison.Ordinal)) continue;
            if (!File.Exists(Path.GetFullPath(Path.Combine(dir, target)))) missing.Add(target);
        }
        Assert.Empty(missing);
    }
}
