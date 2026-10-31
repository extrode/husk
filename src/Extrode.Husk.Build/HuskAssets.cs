using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Extrode.Husk.Compiler;
using Microsoft.Build.Framework;

namespace Extrode.Husk.Build;

/// <summary>Writes the app-wide files from Pare's <c>@(PareOutput)</c> once the compile ran:
/// <c>husk.css</c>, every component's <c>.svelte.css</c> joined; <c>importmap.json</c>, the runtime
/// and every served <c>.js</c> output; and <c>Extrode.Husk.development.json</c>, what the
/// Development app needs to compile on save. With <see cref="HashNames"/>, also a
/// <c>name.&lt;hash&gt;.js</c> copy of every served module and runtime file under <c>hashed/</c>, a map
/// that points at them, and <c>modulegraph.json</c>, what each hashed module imports. Each is written
/// only when its text changed, and a copy only when missing, so a build that changed nothing leaves
/// them alone.</summary>
public sealed class HuskAssets : Microsoft.Build.Utilities.Task
{
    public const string Stylesheet = "husk.css";
    public const string ImportMap = "importmap.json";
    public const string DevelopmentMap = "Extrode.Husk.development.json";
    public const string Hashed = "hashed";
    public const string Graph = "modulegraph.json";

    /// <summary><c>@(PareOutput)</c>: PareSource, PareRelativePath, PareCompiler.</summary>
    public ITaskItem[] Outputs { get; set; } = [];

    /// <summary>Where the app is served, <c>$(StaticWebAssetBasePath)</c>.</summary>
    public string BasePath { get; set; } = "/";

    [Required] public string OutputDirectory { get; set; } = "";

    /// <summary>Pare's obj/ output folder. Only outputs under it are served; an unserved one
    /// (PARE202) gets a path below the project as PareRelativePath, which is no URL.</summary>
    [Required] public string ServedRoot { get; set; } = "";

    [Required] public string RuntimeImports { get; set; } = "";

    [Required] public string CompilerAssembly { get; set; } = "";

    /// <summary>The project folder: Svelte's <c>filename</c> is the path below it.</summary>
    [Required] public string ProjectDirectory { get; set; } = "";

    [Required] public string CompilerPath { get; set; } = "";

    public bool Dev { get; set; }

    public bool HashNames { get; set; }

    /// <summary>The served runtime files, each with its URL below the base path as RelativePath.</summary>
    public ITaskItem[] Runtime { get; set; } = [];

    public override bool Execute()
    {
        string root = Url(BasePath);
        var styles = new List<(string Name, string Path)>();
        var sources = new List<ImportMapSource>();
        var modules = new List<(string Relative, string Path)>();
        string served = Path.GetFullPath(ServedRoot);
        foreach (var output in Outputs)
        {
            if (!output.GetMetadata("FullPath").StartsWith(served, StringComparison.OrdinalIgnoreCase))
                continue;
            string relative = output.GetMetadata("PareRelativePath").Replace('\\', '/');
            if (output.GetMetadata("PareCompiler") == "husk" && relative.EndsWith(".svelte.css", StringComparison.Ordinal))
                styles.Add((relative[..^".css".Length], output.GetMetadata("FullPath")));
            else if (relative.EndsWith(".js", StringComparison.Ordinal) || relative.EndsWith(".mjs", StringComparison.Ordinal))
            {
                int slash = relative.LastIndexOf('/');
                string sourceName = Path.GetFileName(output.GetMetadata("PareSource"));
                sources.Add(new ImportMapSource(root + relative[..(slash + 1)] + sourceName, root + relative));
                modules.Add((relative, output.GetMetadata("FullPath")));
            }
        }
        styles.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));

        string stylesheet = Path.Combine(OutputDirectory, Stylesheet);
        string importMap = Path.Combine(OutputDirectory, ImportMap);
        try
        {
            Directory.CreateDirectory(OutputDirectory);
            WriteIfChanged(stylesheet, StylesheetWriter.Join(styles.Select(s => (s.Name, File.ReadAllText(s.Path)))));
            var runtime = Compiler.RuntimeImports.Load(RuntimeImports);
            if (HashNames)
            {
                modules.AddRange(Runtime.Select(r => (r.GetMetadata("RelativePath").Replace('\\', '/'), r.GetMetadata("FullPath"))));
                var hashed = WriteHashed(root, modules);
                string map = ImportMapWriter.Write(runtime, root + "_husk/", Dev, sources, hashed.ToDictionary(m => m.Url, m => m.HashedUrl, StringComparer.Ordinal));
                WriteIfChanged(importMap, map);
                WriteIfChanged(Path.Combine(OutputDirectory, Graph), ModuleGraphJson(map, hashed));
            }
            else
            {
                WriteHashed(root, []);
                WriteIfChanged(importMap, ImportMapWriter.Write(runtime, root + "_husk/", Dev, sources));
            }
            WriteIfChanged(Path.Combine(OutputDirectory, DevelopmentMap), Development(stylesheet, importMap, styles));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            Log.LogError(null, "HUSK102", null, OutputDirectory, 0, 0, 0, 0, "cannot write the app's stylesheet or import map: " + e.Message);
        }
        return !Log.HasLoggedErrors;
    }

    /// <summary>Copies each module to <c>hashed/</c> under its hashed name when missing, and deletes
    /// the copies no module has any longer.</summary>
    private List<(string Url, string HashedUrl, string Text)> WriteHashed(string root, List<(string Relative, string Path)> modules)
    {
        string folder = Path.Combine(OutputDirectory, Hashed);
        var result = new List<(string Url, string HashedUrl, string Text)>();
        var kept = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (relative, path) in modules)
        {
            byte[] bytes = File.ReadAllBytes(path);
            string name = ModuleGraph.Hashed(relative, ModuleGraph.Hash(bytes));
            string copy = Path.GetFullPath(Path.Combine(folder, name));
            if (kept.Add(copy) && !File.Exists(copy))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
                File.WriteAllBytes(copy, bytes);
            }
            result.Add((root + relative, root + name, Encoding.UTF8.GetString(bytes)));
        }
        if (Directory.Exists(folder))
            foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Where(f => !kept.Contains(Path.GetFullPath(f))).ToList())
                File.Delete(file);
        return result;
    }

    /// <summary>Each hashed module URL to the hashed modules it imports, as the browser resolves them.</summary>
    private static string ModuleGraphJson(string map, List<(string Url, string HashedUrl, string Text)> modules)
    {
        var imports = JsonNode.Parse(map)!["imports"]!.AsObject().ToDictionary(e => e.Key, e => (string)e.Value!, StringComparer.Ordinal);
        var known = modules.Select(m => m.HashedUrl).ToHashSet(StringComparer.Ordinal);
        var graph = new JsonObject();
        foreach (var module in modules.OrderBy(m => m.HashedUrl, StringComparer.Ordinal))
            graph[module.HashedUrl] = new JsonArray([.. ModuleGraph.Imports(module.Text)
                .Select(s => ModuleGraph.Resolve(s, module.HashedUrl, imports))
                .Where(u => u is not null && known.Contains(u)).Distinct(StringComparer.Ordinal)
                .Select(u => (JsonNode)JsonValue.Create(u)!)]);
        return graph.ToJsonString(new JsonSerializerOptions { WriteIndented = true, NewLine = "\n" }) + "\n";
    }

    private string Development(string stylesheet, string importMap, List<(string Name, string Path)> styles)
    {
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true, NewLine = "\n" }))
        {
            json.WriteStartObject();
            json.WriteString("compiler", CompilerAssembly);
            json.WriteString("svelte", CompilerPath);
            json.WriteString("projectDirectory", ProjectDirectory);
            json.WriteBoolean("dev", Dev);
            json.WriteBoolean("hashNames", HashNames);
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
