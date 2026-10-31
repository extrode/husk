namespace Extrode.Husk.Deploy;

/// <summary>One file of the output folder. <see cref="Path"/> is the manifest key: rooted at
/// <c>/</c>, forward slashes.</summary>
internal sealed record AssetFile(string Path, string FullPath, string Hash, long Size);

internal static class AssetManifest
{
    /// <summary>Every file under <paramref name="directory"/>, ordered by path. A linked directory
    /// is not entered, so a link back up the tree cannot loop; a linked file is read.</summary>
    public static IReadOnlyList<AssetFile> Read(string directory)
    {
        string root = Path.GetFullPath(directory);
        var files = new List<AssetFile>();
        foreach (string full in Files(root))
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

    private static IEnumerable<string> Files(string root)
    {
        var pending = new Stack<string>([root]);
        while (pending.Count > 0)
        {
            string directory = pending.Pop();
            foreach (string file in Directory.EnumerateFiles(directory))
                yield return file;
            foreach (string child in Directory.EnumerateDirectories(directory))
                if (new DirectoryInfo(child).LinkTarget is null)
                    pending.Push(child);
        }
    }
}
