using System.Text.RegularExpressions;

namespace Extrode.Husk.Tests;

/// <summary>The release path is decision 008: tag, reviewer-gated environment, OIDC. These pin the
/// parts a later edit could drop without anything else failing.</summary>
public sealed class WorkflowContractTests
{
    private static string Workflow(string name) => File.ReadAllText(Path.Combine(RepoPaths.Root, ".github", "workflows", name));

    private static string Job(string workflow, string name)
    {
        int start = workflow.IndexOf($"\n  {name}:\n", StringComparison.Ordinal);
        Assert.True(start >= 0, name);
        var next = Regex.Match(workflow[(start + 1)..], @"\n  [a-z-]+:\n");
        return next.Success ? workflow[start..(start + 1 + next.Index)] : workflow[start..];
    }

    private static string Step(string job, string name)
    {
        int start = job.IndexOf($"- name: {name}", StringComparison.Ordinal);
        Assert.True(start >= 0, name);
        int next = job.IndexOf("\n      - ", start + 1, StringComparison.Ordinal);
        return next < 0 ? job[start..] : job[start..next];
    }

    [Fact]
    public void Release_publishes_by_tag_through_OIDC_in_the_packages_environment_with_no_stored_key()
    {
        string release = Workflow("release.yml");

        Assert.Contains("tags: ['v*']", release, StringComparison.Ordinal);
        Assert.Equal(3, Regex.Matches(release, @"environment: packages").Count);
        Assert.Matches(@"uses: NuGet/login@[0-9a-f]{40} # v1", release);
        Assert.Contains("user: ${{ vars.NUGET_USER }}", release, StringComparison.Ordinal);
        Assert.Contains("id-token: write", Job(release, "release"), StringComparison.Ordinal);
        Assert.Contains("id-token: write", Job(release, "verify-trusted-publishing"), StringComparison.Ordinal);
        Assert.DoesNotContain("api-key \"${{", release, StringComparison.Ordinal);
        Assert.Contains("--skip-duplicate", Step(Job(release, "release"), "Push to NuGet.org"), StringComparison.Ordinal);
        Assert.DoesNotContain("secrets.", release.Replace("secrets.GITHUB_TOKEN", "", StringComparison.Ordinal), StringComparison.Ordinal);
    }

    [Fact]
    public void Release_also_pushes_to_GitHub_Packages_so_the_repository_Packages_tab_lists_them()
    {
        string release = Workflow("release.yml");
        string job = Job(release, "release");
        string backfill = Job(release, "backfill-github-packages");

        Assert.Contains("packages: write", job, StringComparison.Ordinal);
        Assert.Contains("https://nuget.pkg.github.com/${GITHUB_REPOSITORY_OWNER}/index.json", job, StringComparison.Ordinal);
        Assert.Contains("packages: write", backfill, StringComparison.Ordinal);
        Assert.Contains("https://nuget.pkg.github.com/${GITHUB_REPOSITORY_OWNER}/index.json", backfill, StringComparison.Ordinal);
        Assert.Contains("--skip-duplicate", Step(job, "Push to GitHub Packages"), StringComparison.Ordinal);
        Assert.Contains("--skip-duplicate", Step(backfill, "Push the release's packages to GitHub Packages"), StringComparison.Ordinal);
        Assert.Contains("github.event_name == 'workflow_dispatch' && startsWith(inputs.backfill_tag, 'v')", backfill, StringComparison.Ordinal);
        Assert.Contains("inputs.backfill_tag == ''", Job(release, "verify-trusted-publishing"), StringComparison.Ordinal);
        Assert.True(job.IndexOf("name: GitHub Release with the packages", StringComparison.Ordinal)
            < job.IndexOf("name: Push to GitHub Packages", StringComparison.Ordinal), "GitHub Packages push must follow the release step");
        Assert.DoesNotContain("api.nuget.org", backfill, StringComparison.Ordinal);
        Assert.DoesNotContain("NuGet/login", backfill, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Extrode.Husk.Build")]
    [InlineData("Extrode.Husk.AspNetCore")]
    public void Both_packages_carry_the_Husk_icon(string project)
    {
        string props = File.ReadAllText(Path.Combine(RepoPaths.Root, "Directory.Build.props"));
        string csproj = File.ReadAllText(Path.Combine(RepoPaths.Root, "src", project, $"{project}.csproj"));
        string icon = Path.Combine(RepoPaths.Root, "docs", "assets", "logo", "husk-icon-128.png");

        Assert.Contains("<PackageIcon>husk-icon-128.png</PackageIcon>", props, StringComparison.Ordinal);
        Assert.Contains(@"docs\assets\logo\husk-icon-128.png"" Pack=""true"" PackagePath=""""", csproj, StringComparison.Ordinal);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, File.ReadAllBytes(icon)[..4]);
    }

    [Fact]
    public void Release_refuses_a_tag_that_differs_from_the_version_in_Directory_Build_props()
    {
        string release = Workflow("release.yml");
        string props = File.ReadAllText(Path.Combine(RepoPaths.Root, "Directory.Build.props"));

        Assert.Contains("[ \"$PROP_VERSION\" = \"$TAG_VERSION\" ]", Job(release, "release"), StringComparison.Ordinal);
        Assert.Contains("sed -n 's/.*<Version>", Job(release, "release"), StringComparison.Ordinal);
        Assert.Matches(@"<Version>\d+\.\d+\.\d+[^<]*</Version>", props);
    }

    [Fact]
    public void A_0_x_version_carries_a_prerelease_suffix_so_nuget_org_and_GitHub_agree()
    {
        string props = File.ReadAllText(Path.Combine(RepoPaths.Root, "Directory.Build.props"));

        Assert.Matches(@"<Version>0\.\d+\.\d+-[0-9A-Za-z.]+</Version>", props);
    }

    [Fact]
    public void Release_publishes_only_for_a_tag_push_and_only_when_the_pinned_Pare_is_on_nuget_org()
    {
        string job = Job(Workflow("release.yml"), "release");

        Assert.Contains("if: github.event_name == 'push' && startsWith(github.ref, 'refs/tags/v')", job, StringComparison.Ordinal);
        Assert.Contains("<PareVersion>", job, StringComparison.Ordinal);
        int guard = job.IndexOf("is not on nuget.org", StringComparison.Ordinal);
        Assert.True(guard >= 0);
        Assert.True(guard < job.IndexOf("dotnet nuget push", StringComparison.Ordinal));
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
