using System.Text.RegularExpressions;

namespace Extrode.Husk.Tests;

/// <summary>dist/docs-site is committed and published as it is, so it must be rebuilt when the markdown changes.</summary>
public sealed class DocsSiteTests
{
    [Fact]
    public void The_built_site_installs_no_Pare_reference_and_names_the_Pare_version_the_build_pins()
    {
        string packages = File.ReadAllText(Path.Combine(RepoPaths.Root, "Directory.Packages.props"));
        string pare = Regex.Match(packages, @"<PareVersion>([^<]+)</PareVersion>").Groups[1].Value;
        Assert.NotEmpty(pare);

        string site = Path.Combine(RepoPaths.Root, "dist", "docs-site");
        string firstApp = File.ReadAllText(Path.Combine(site, "01-getting-started-first-app.html"));
        Assert.Contains("Extrode.Husk.Build", firstApp, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"Extrode\.Pare\.Build[^V<]*?Version", firstApp);
        Assert.DoesNotContain("add package Extrode.Pare.Build", firstApp, StringComparison.Ordinal);

        string start = File.ReadAllText(Path.Combine(site, "01-getting-started-readme.html"));
        var shown = Regex.Matches(start, @"(\d+\.\d+\.\d+) or later").Select(m => m.Groups[1].Value).ToList();
        Assert.NotEmpty(shown);
        Assert.All(shown, v => Assert.Equal(pare, v));
    }
}
