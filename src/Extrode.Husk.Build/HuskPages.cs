using System.Text;
using Extrode.Husk.Compiler;
using Microsoft.Build.Framework;

namespace Extrode.Husk.Build;

/// <summary>Writes each page (compiler <c>husk-page</c>) to its <c>PareOutputPath</c>: with the
/// import map inlined when it holds the marker, byte for byte as it is otherwise.</summary>
public sealed class HuskPages : Microsoft.Build.Utilities.Task
{
    [Required] public ITaskItem[] Pages { get; set; } = [];

    [Required] public string ImportMap { get; set; } = "";

    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    public override bool Execute()
    {
        try
        {
            string map = File.ReadAllText(ImportMap);
            foreach (var page in Pages)
            {
                string source = page.GetMetadata("FullPath");
                string output = page.GetMetadata("PareOutputPath");
                Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                // Written, not copied: a copy keeps the source's time, older than the map, and the
                // target would run every build.
                byte[] bytes = File.ReadAllBytes(source);
                bool bom = bytes.AsSpan().StartsWith(Utf8Bom);
                if (PageWriter.Inline(new UTF8Encoding(false).GetString(bytes, bom ? 3 : 0, bytes.Length - (bom ? 3 : 0)), map) is { } inlined)
                    File.WriteAllText(output, inlined, new UTF8Encoding(bom));
                else
                    File.WriteAllBytes(output, bytes);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Text.RegularExpressions.RegexMatchTimeoutException)
        {
            Log.LogError(null, "HUSK102", null, ImportMap, 0, 0, 0, 0, "cannot write a page: " + e.Message);
        }
        return !Log.HasLoggedErrors;
    }
}
