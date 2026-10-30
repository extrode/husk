namespace Extrode.Husk.Deploy;

/// <summary>One file of the output folder. <see cref="Path"/> is the manifest key: rooted at
/// <c>/</c>, forward slashes.</summary>
internal sealed record AssetFile(string Path, string FullPath, string Hash, long Size);

internal static class AssetManifest
{
    /// <summary>Every file under <paramref name="directory"/>, ordered by path.</summary>
    public static IReadOnlyList<AssetFile> Read(string directory)
    {
        string root = Path.GetFullPath(directory);
        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(full =>
            {
                byte[] content = File.ReadAllBytes(full);
                string path = "/" + Path.GetRelativePath(root, full).Replace('\\', '/');
                return new AssetFile(path, full, CloudflareApi.Hash(content, full), content.LongLength);
            })
            .OrderBy(f => f.Path, StringComparer.Ordinal)
            .ToList();
    }
}
