using System.Text;
using System.Text.Json;
using Extrode.Husk.Compiler;
using Microsoft.Build.Framework;

namespace Extrode.Husk.Build;

/// <summary>Writes the app-wide files from Pare's <c>@(PareOutput)</c> once the compile ran:
/// <c>husk.css</c>, every component's <c>.svelte.css</c> joined; <c>importmap.json</c>, the runtime
/// and every served <c>.js</c> output; and <c>Extrode.Husk.development.json</c>, what the
/// Development app needs to compile on save. Each is written only when its text changed, so a build
/// that changed nothing leaves them alone.</summary>
public sealed class HuskAssets : Microsoft.Build.Utilities.Task
{
    public const string Stylesheet = "husk.css";
    public const string ImportMap = "importmap.json";
    public const string DevelopmentMap = "Extrode.Husk.development.json";

    /// <summary><c>@(PareOutput)</c>: PareSource, PareRelativePath, PareCompiler.</summary>
    public ITaskItem[] Outputs { get; set; } = [];

    /// <summary>Where the app is served, <c>$(StaticWebAssetBasePath)</c>.</summary>
    public string BasePath { get; set; } = "/";

    [Required] public string OutputDirectory { get; set; } = "";

    [Required] public string RuntimeImports { get; set; } = "";

    [Required] public string CompilerAssembly { get; set; } = "";

    [Required] public string CompilerPath { get; set; } = "";

    public bool Dev { get; set; }

    public override bool Execute()
    {
        string root = Url(BasePath);
        var styles = new List<(string Name, string Path)>();
        var sources = new List<ImportMapSource>();
        foreach (var output in Outputs)
        {
            string relative = output.GetMetadata("PareRelativePath").Replace('\\', '/');
            if (output.GetMetadata("PareCompiler") == "husk" && relative.EndsWith(".svelte.css", StringComparison.Ordinal))
                styles.Add((relative[..^".css".Length], output.GetMetadata("FullPath")));
            else if (relative.EndsWith(".js", StringComparison.Ordinal) || relative.EndsWith(".mjs", StringComparison.Ordinal))
            {
                int slash = relative.LastIndexOf('/');
                string sourceName = Path.GetFileName(output.GetMetadata("PareSource"));
                sources.Add(new ImportMapSource(root + relative[..(slash + 1)] + sourceName, root + relative));
            }
        }
        styles.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));

        string stylesheet = Path.Combine(OutputDirectory, Stylesheet);
        string importMap = Path.Combine(OutputDirectory, ImportMap);
        try
        {
            Directory.CreateDirectory(OutputDirectory);
            WriteIfChanged(stylesheet, StylesheetWriter.Join(styles.Select(s => (s.Name, File.ReadAllText(s.Path)))));
            WriteIfChanged(importMap, ImportMapWriter.Write(Compiler.RuntimeImports.Load(RuntimeImports), root + "_husk/", Dev, sources));
            WriteIfChanged(Path.Combine(OutputDirectory, DevelopmentMap), Development(stylesheet, importMap, styles));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.LogError(null, "HUSK102", null, OutputDirectory, 0, 0, 0, 0, "cannot write the app's stylesheet or import map: " + e.Message);
        }
        return !Log.HasLoggedErrors;
    }

    private string Development(string stylesheet, string importMap, List<(string Name, string Path)> styles)
    {
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true, NewLine = "\n" }))
        {
            json.WriteStartObject();
            json.WriteString("compiler", CompilerAssembly);
            json.WriteString("svelte", CompilerPath);
            json.WriteBoolean("dev", Dev);
            json.WriteString("stylesheet", stylesheet);
            json.WriteString("importMap", importMap);
            json.WriteStartArray("styles");
            foreach (var (name, path) in styles)
            {
                json.WriteStartObject();
                json.WriteString("name", name);
                json.WriteString("css", path);
                json.WriteEndObject();
            }
            json.WriteEndArray();
            json.WriteEndObject();
        }
        return Encoding.UTF8.GetString(buffer.ToArray()) + "\n";
    }

    /// <summary>A base path as a URL folder: <c>/</c>, or <c>/app/</c> for <c>app</c>.</summary>
    internal static string Url(string basePath)
    {
        string trimmed = basePath.Replace('\\', '/').Trim('/');
        return trimmed.Length == 0 ? "/" : "/" + trimmed + "/";
    }

    internal static void WriteIfChanged(string path, string text)
    {
        if (File.Exists(path) && File.ReadAllText(path) == text)
            return;
        File.WriteAllText(path, text);
    }
}
