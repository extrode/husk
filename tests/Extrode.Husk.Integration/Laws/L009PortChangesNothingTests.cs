using System.Diagnostics;

namespace Extrode.Husk.Integration.Laws;

/// <summary>L009: the saaaat port branch changes only files under apps/web-husk/.</summary>
[Trait("Law", "L009")]
public sealed class L009PortChangesNothingTests
{
    private static readonly string Saaaat = Path.GetFullPath(Path.Combine(TempProject.RepoRoot, "..", "..", "ikhbat.org", "saaaat.com"));

    internal static IEnumerable<string> Offenders(IEnumerable<string> changed) =>
        changed.Where(p => !p.StartsWith("apps/web-husk/", StringComparison.Ordinal));

    [Theory]
    [InlineData("apps/web-husk/Saaaat.Web.csproj", false)]
    [InlineData("apps/web-husk/stubs/capacitor-core.js", false)]
    [InlineData("apps/web/src/lib/day.ts", true)]
    [InlineData("apps/web/svelte.config.js", true)]
    [InlineData("packages/core/src/index.ts", true)]
    [InlineData("apps/web-husk-extra/a.ts", true)]
    [InlineData("README.md", true)]
    public void A_path_outside_the_port_folder_is_an_offender(string path, bool offends) =>
        Assert.Equal(offends, Offenders([path]).Any());

    [Fact]
    public void Every_file_the_port_branch_changed_is_under_the_port_folder()
    {
        string worktree = Path.Combine(Saaaat, ".worktrees", "feat-husk-port");
        if (!Directory.Exists(worktree))
            Assert.Skip($"no port worktree at {worktree}");
        string basePoint = Git(worktree, "merge-base", "HEAD", "dev").Trim();
        var changed = Git(worktree, "diff", "--name-only", basePoint, "HEAD").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.NotEmpty(changed);
        Assert.Empty(Offenders(changed));
    }

    private static string Git(string directory, params string[] args)
    {
        var start = new ProcessStartInfo("git") { RedirectStandardOutput = true, WorkingDirectory = directory };
        foreach (var a in args) start.ArgumentList.Add(a);
        using var git = Process.Start(start)!;
        string output = git.StandardOutput.ReadToEnd();
        git.WaitForExit();
        return git.ExitCode == 0 ? output : throw new InvalidOperationException($"git {string.Join(' ', args)} failed in {directory}");
    }
}
