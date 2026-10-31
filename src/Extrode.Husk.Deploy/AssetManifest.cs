namespace Extrode.Husk.Deploy;

/// <summary>One file of the output folder. <see cref="Path"/> is the manifest key: rooted at
/// <c>/</c>, forward slashes.</summary>
internal sealed record AssetFile(string Path, string FullPath, string Hash, long Size);

internal static class AssetManifest
{
    /// <summary>Every file under <paramref name="directory"/>, ordered by path. Linked files and
    /// folders are followed, except a folder link back up the tree, which would loop.</summary>
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

    // Each pending folder carries the folders above it, as reached and as their links resolve; a
    // link that resolves to one of them would loop and is not entered.
    private static IEnumerable<string> Files(string root)
    {
        var pending = new Stack<(string Directory, HashSet<string> Above)>([(root, new HashSet<string>(StringComparer.Ordinal) { root })]);
        while (pending.Count > 0)
        {
            var (directory, above) = pending.Pop();
            foreach (string file in Directory.EnumerateFiles(directory))
                yield return file;
            foreach (string child in Directory.EnumerateDirectories(directory))
            {
                string? target = Directory.ResolveLinkTarget(child, returnFinalTarget: true)?.FullName;
                if (target is not null && above.Contains(Path.TrimEndingDirectorySeparator(target)))
                    continue;
                var next = new HashSet<string>(above, StringComparer.Ordinal) { child };
                if (target is not null)
                    next.Add(Path.TrimEndingDirectorySeparator(target));
                pending.Push((child, next));
            }
        }
    }
}
