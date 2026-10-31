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
        var files = new List<AssetFile>();
        foreach (string full in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            string name = Path.GetFileName(full);
            if (name == ".DS_Store")
                continue;
            string relative = Path.GetRelativePath(root, full);
            if (relative is "_headers" or "_redirects")
                throw new InvalidOperationException($"{relative} is not supported: Cloudflare reads it as configuration, which this deploy does not send yet. Remove it from {directory}.");
            byte[] content = File.ReadAllBytes(full);
            string path = "/" + (Path.DirectorySeparatorChar == '\\' ? relative.Replace('\\', '/') : relative);
            files.Add(new AssetFile(path, full, CloudflareApi.Hash(content, full), content.LongLength));
        }
        files.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));
        return files;
    }
}
