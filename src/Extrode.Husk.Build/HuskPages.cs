using Extrode.Husk.Compiler;
using Microsoft.Build.Framework;

namespace Extrode.Husk.Build;

/// <summary>Writes each page (compiler <c>husk-page</c>) to its <c>PareOutputPath</c>: with the
/// import map inlined when it holds the marker, byte for byte as it is otherwise.</summary>
public sealed class HuskPages : Microsoft.Build.Utilities.Task
{
    [Required] public ITaskItem[] Pages { get; set; } = [];

    [Required] public string ImportMap { get; set; } = "";

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
                if (PageWriter.Inline(File.ReadAllText(source), map) is { } inlined)
                    File.WriteAllText(output, inlined);
                else
                    File.Copy(source, output, overwrite: true);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.LogError(null, "HUSK102", null, ImportMap, 0, 0, 0, 0, "cannot write a page: " + e.Message);
        }
        return !Log.HasLoggedErrors;
    }
}
