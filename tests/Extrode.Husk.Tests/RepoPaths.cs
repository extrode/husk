namespace Extrode.Husk.Tests;

internal static class RepoPaths
{
    public static readonly string Root = FindRoot();
    public static readonly string Vendored = Path.Combine(Root, "third-party", "svelte-5.57.2");
    public static readonly string Fixtures = Path.Combine(Root, "tests", "fixtures");

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Husk.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Husk.slnx not found above the test output");
    }
}
