using System.Text.RegularExpressions;

namespace Extrode.Husk.Tests;

/// <summary>The release path is decision 008: tag, reviewer-gated environment, OIDC. These pin the
/// parts a later edit could drop without anything else failing.</summary>
public sealed class WorkflowContractTests
{
    private static string Workflow(string name) => File.ReadAllText(Path.Combine(RepoPaths.Root, ".github", "workflows", name));

    [Fact]
    public void Release_publishes_by_tag_through_OIDC_in_the_packages_environment_with_no_stored_key()
    {
        string release = Workflow("release.yml");

        Assert.Contains("tags: ['v*']", release, StringComparison.Ordinal);
        Assert.Equal(2, Regex.Matches(release, @"environment: packages").Count);
        Assert.Matches(@"uses: NuGet/login@[0-9a-f]{40} # v1", release);
        Assert.Contains("user: ${{ vars.NUGET_USER }}", release, StringComparison.Ordinal);
        Assert.Contains("id-token: write", release, StringComparison.Ordinal);
        Assert.Contains("--skip-duplicate", release, StringComparison.Ordinal);
        Assert.DoesNotContain("secrets.", release.Replace("secrets.GITHUB_TOKEN", "", StringComparison.Ordinal), StringComparison.Ordinal);
    }

    [Fact]
    public void Release_refuses_a_tag_that_differs_from_the_version_in_Directory_Build_props()
    {
        string release = Workflow("release.yml");
        string props = File.ReadAllText(Path.Combine(RepoPaths.Root, "Directory.Build.props"));

        Assert.Contains("Directory.Build.props", release, StringComparison.Ordinal);
        Assert.Contains("!= <Version>", release, StringComparison.Ordinal);
        Assert.Matches(@"<Version>\d+\.\d+\.\d+[^<]*</Version>", props);
    }

    [Fact]
    public void Release_packs_exactly_the_two_packable_projects()
    {
        string release = Workflow("release.yml");

        Assert.Contains("for project in Extrode.Husk.Build Extrode.Husk.AspNetCore; do", release, StringComparison.Ordinal);
    }

    [Fact]
    public void CI_runs_the_solution_on_Linux_macOS_and_Windows()
    {
        string ci = Workflow("ci.yml");

        Assert.Contains("os: [ubuntu-latest, macos-latest, windows-latest]", ci, StringComparison.Ordinal);
        Assert.Contains("dotnet test --solution Husk.slnx", ci, StringComparison.Ordinal);
    }
}
