using System.Diagnostics;

namespace Extrode.Husk.Integration;

/// <summary>scripts/vendor-svelte.cs, run as the README says: from the repo root, with no Node.</summary>
public sealed class VendorTests : IDisposable
{
    private static readonly string Committed = Path.Combine(TempProject.RepoRoot, "third-party", "svelte-5.57.2");
    private static readonly string Cache = Path.Combine(TempProject.RepoRoot, "tmp", "vendor-cache");
    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("husk-vendor-");

    public void Dispose() => _dir.Delete(recursive: true);

    private static (int Exit, string Output) Vendor(params string[] args)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = TempProject.RepoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var arg in (string[])["run", "scripts/vendor-svelte.cs", "--", .. args])
            start.ArgumentList.Add(arg);
        foreach (var key in start.Environment.Keys.Where(k => k.StartsWith("MSBuild", StringComparison.OrdinalIgnoreCase)).ToList())
            start.Environment.Remove(key);
        using var process = Process.Start(start)!;
        var stderr = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        string stdout = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, stdout + stderr.Result);
    }

    private static SortedDictionary<string, byte[]> Tree(string root) =>
        new(Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .ToDictionary(f => Path.GetRelativePath(root, f).Replace('\\', '/'), File.ReadAllBytes), StringComparer.Ordinal);

    [Fact]
    public void A_rerun_reproduces_the_committed_tree_byte_for_byte()
    {
        string fresh = Path.Combine(_dir.FullName, "out");

        var (exit, output) = Vendor("--out", fresh, "--cache", Cache);

        Assert.True(exit == 0, output);
        var expected = Tree(Committed);
        var actual = Tree(fresh);
        Assert.Equal(expected.Keys, actual.Keys);
        foreach (var (path, bytes) in expected)
            Assert.True(bytes.AsSpan().SequenceEqual(actual[path]), path);
    }

    [Fact]
    public void A_tampered_tarball_is_refused_and_nothing_is_unpacked()
    {
        Assert.Equal(0, Vendor("--out", Path.Combine(_dir.FullName, "warm"), "--cache", Cache).Exit);
        string cache = Path.Combine(_dir.FullName, "cache");
        Directory.CreateDirectory(cache);
        foreach (var tarball in Directory.GetFiles(Cache, "*.tgz"))
            File.Copy(tarball, Path.Combine(cache, Path.GetFileName(tarball)));
        string svelte = Path.Combine(cache, "svelte-5.57.2.tgz");
        var bytes = File.ReadAllBytes(svelte);
        bytes[bytes.Length / 2] ^= 0xFF;
        File.WriteAllBytes(svelte, bytes);
        string fresh = Path.Combine(_dir.FullName, "out");

        var (exit, output) = Vendor("--out", fresh, "--cache", cache);

        Assert.Equal(1, exit);
        Assert.Contains("svelte-5.57.2.tgz: integrity", output);
        Assert.Contains("Refusing to unpack", output);
        Assert.False(Directory.Exists(fresh));
    }

    [Fact]
    public void An_existing_output_directory_is_refused()
    {
        var (exit, output) = Vendor("--out", _dir.FullName, "--cache", Cache);

        Assert.Equal(1, exit);
        Assert.Contains("exists", output);
    }
}
