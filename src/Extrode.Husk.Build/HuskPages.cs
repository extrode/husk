using System.Text;
using Extrode.Husk.Compiler;
using Microsoft.Build.Framework;

namespace Extrode.Husk.Build;

/// <summary>Writes each page (compiler <c>husk-page</c>) to its <c>PareOutputPath</c>: with the
/// import map inlined when it holds the marker, byte for byte as it is otherwise. With
/// <see cref="Graph"/> (hashed output), a marked page's module scripts also point at hashed URLs and
/// the map is followed by modulepreload links.</summary>
public sealed class HuskPages : Microsoft.Build.Utilities.Task
{
    [Required] public ITaskItem[] Pages { get; set; } = [];

    [Required] public string ImportMap { get; set; } = "";

    /// <summary><c>modulegraph.json</c>, when names are hashed.</summary>
    public string Graph { get; set; } = "";

    /// <summary>Where the app is served, <c>$(StaticWebAssetBasePath)</c>.</summary>
    public string BasePath { get; set; } = "/";

    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    public override bool Execute()
    {
        string map;
        string? graph = null;
        try
        {
            map = File.ReadAllText(ImportMap);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.LogError(null, "HUSK102", null, ImportMap, 0, 0, 0, 0, "cannot read the import map: " + e.Message);
            return false;
        }
        try
        {
            if (Graph.Length > 0)
                graph = File.ReadAllText(Graph);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.LogError(null, "HUSK102", null, Graph, 0, 0, 0, 0, "cannot read the module graph: " + e.Message);
            return false;
        }
        // Each page on its own: one that cannot be written does not stop the rest.
        foreach (var page in Pages)
        {
            string source = page.GetMetadata("FullPath");
            try
            {
                Write(source, page.GetMetadata("PareOutputPath"), map, graph, HuskAssets.Url(BasePath) + page.GetMetadata("PareRelativePath").Replace('\\', '/'));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Text.RegularExpressions.RegexMatchTimeoutException or System.Text.Json.JsonException)
            {
                Log.LogError(null, "HUSK102", null, source, 0, 0, 0, 0, "cannot write the page: " + e.Message);
            }
        }
        return !Log.HasLoggedErrors;
    }

    private static void Write(string source, string output, string map, string? graph, string url)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        // Written, not copied: a copy keeps the source's time, older than the map, and the
        // target would run every build.
        byte[] bytes = File.ReadAllBytes(source);
        bool bom = bytes.AsSpan().StartsWith(Utf8Bom);
        string html = new UTF8Encoding(false).GetString(bytes, bom ? 3 : 0, bytes.Length - (bom ? 3 : 0));
        if ((graph is null ? PageWriter.Inline(html, map) : PageWriter.Inline(html, map, url, graph)) is { } inlined)
            File.WriteAllText(output, inlined, new UTF8Encoding(bom));
        else
            File.WriteAllBytes(output, bytes);
    }
}
