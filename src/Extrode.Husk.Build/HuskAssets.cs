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

    /// <summary><c>@(HuskAlias)</c>: Include is a name (<c>$lib</c>, <c>@saaaat/core</c>), Path the source
    /// folder it stands for, Entry the file a bare import of the name means.</summary>
    public ITaskItem[] Aliases { get; set; } = [];

    /// <summary><c>@(HuskImport)</c>: Include is a specifier, Url the file it means.</summary>
    public ITaskItem[] Imports { get; set; } = [];

    /// <summary>The app has a router: <c>$app/*</c> and <c>@sveltejs/kit</c> resolve to its shims.</summary>
    public bool Routing { get; set; }

    /// <summary>The route folder, <c>@(HuskRoutes)</c>, and what to build again when a route file is
    /// added or removed in Development: the project, its configuration and target framework.</summary>
    public string RoutesRoot { get; set; } = "";

    public string Project { get; set; } = "";

    public string Configuration { get; set; } = "";

    public string TargetFramework { get; set; } = "";

    private static readonly StringComparison PathComparison =
        OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

    /// <summary>True when <paramref name="path"/> is inside the <paramref name="servedRoot"/> folder.
    /// The folder boundary counts: <c>pare-old/x.js</c> is not inside <c>pare</c>.</summary>
    internal static bool IsServed(string path, string servedRoot, StringComparison comparison)
    {
        string folder = servedRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return path.StartsWith(folder, comparison);
    }

    /// <summary><c>@(HuskStylesheet)</c>: a stylesheet a module imports for its side effect, as
    /// <c>import '$lib/app.css'</c>. Each becomes a module that adds the CSS in a style element ahead of
    /// <c>husk.css</c>, so an imported stylesheet comes before component styles, as Vite orders them.</summary>
    public ITaskItem[] Stylesheets { get; set; } = [];

    public const string CssFolder = "css";

    /// <summary><c>@(HuskJson)</c>: a JSON file a module imports with an import attribute
    /// (<c>import x from './t.json' with { type: 'json' }</c>). Root is the folder the URL is counted from and
    /// ServeAt the URL folder below the base path (the item's own, as <c>PareServeAt</c> is for a source).
    /// The file is served there, and under a hashed name with <c>HashNames</c>.</summary>
    public ITaskItem[] JsonFiles { get; set; } = [];

    public const string JsonFolder = "json";

    private List<(string Relative, string Path)> WriteJson()
    {
        string folder = Path.Combine(OutputDirectory, JsonFolder);
        var result = new List<(string, string)>();
        var kept = new HashSet<string>(OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
        var owners = new Dictionary<string, string>(OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
        foreach (var item in JsonFiles)
        {
            string source = Path.GetFullPath(item.ItemSpec, ProjectDirectory);
            if (!File.Exists(source))
            {
                Log.LogError(null, "HUSK104", null, source, 0, 0, 0, 0, "a HuskJson file does not exist");
                continue;
            }
            if (!source.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                Log.LogError(null, "HUSK104", null, source, 0, 0, 0, 0, "a HuskJson file must have the extension .json");
                continue;
            }
            if (IsServed(source, Path.Combine(ProjectDirectory, "wwwroot"), PathComparison))
            {
                Log.LogError(null, "HUSK104", null, source, 0, 0, 0, 0, "a HuskJson file under wwwroot is already served by the SDK; drop the HuskJson item");
                continue;
            }
            string rootFolder = Path.GetFullPath(item.GetMetadata("Root"), ProjectDirectory);
            if (!IsServed(source, rootFolder, PathComparison))
            {
                Log.LogError(null, "HUSK104", null, source, 0, 0, 0, 0, $"a HuskJson file is outside its Root '{rootFolder}'");
                continue;
            }
            string relative = (item.GetMetadata("ServeAt").Trim('/') + "/" + Path.GetRelativePath(rootFolder, source).Replace('\\', '/')).TrimStart('/');
            string target = Path.GetFullPath(Path.Combine(folder, relative));
            if (!IsServed(target, folder, PathComparison))
            {
                Log.LogError(null, "HUSK104", null, source, 0, 0, 0, 0, $"a HuskJson ServeAt '{item.GetMetadata("ServeAt")}' leaves the served folder");
                continue;
            }
            if (owners.TryGetValue(target, out string? owner))
            {
                if (!string.Equals(owner, source, PathComparison))
                    Log.LogError(null, "HUSK104", null, source, 0, 0, 0, 0, $"a HuskJson file is served at the URL of '{owner}' too");
                continue;
            }
            owners[target] = source;
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            byte[] bytes = File.ReadAllBytes(source);
            if (!File.Exists(target) || !File.ReadAllBytes(target).AsSpan().SequenceEqual(bytes))
                File.WriteAllBytes(target, bytes);
            kept.Add(target);
            result.Add((relative, target));
        }
        if (Directory.Exists(folder))
            foreach (string stale in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Where(f => !kept.Contains(Path.GetFullPath(f))).ToList())
                File.Delete(stale);
        return result;
    }

    private const string CssModule = """
        const style = document.createElement('style');
        style.setAttribute('data-husk-css', {0});
        style.textContent = {1};
        document.head.insertBefore(style, document.head.querySelector('link[href*="husk.css"]'));
        """;

    /// <summary>Writes one module per stylesheet below <c>css/</c>, removes the stale ones, and returns each
    /// with its URL below the root, its import specifiers and its text.</summary>
    private List<(string Relative, string Path, string[] Specifiers)> WriteStylesheets()
    {
        string folder = Path.Combine(OutputDirectory, CssFolder);
        var result = new List<(string, string, string[])>();
        var kept = new HashSet<string>(OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
        foreach (var item in Stylesheets)
        {
            string source = Path.GetFullPath(item.ItemSpec, ProjectDirectory);
            if (!File.Exists(source))
            {
                Log.LogError(null, "HUSK103", null, source, 0, 0, 0, 0, "a HuskStylesheet does not exist");
                continue;
            }
            string identity = Path.GetRelativePath(ProjectDirectory, source).Replace('\\', '/');
            if (!OperatingSystem.IsLinux())
                identity = identity.ToLowerInvariant();
            string name = Path.GetFileName(source) + "." + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..8].ToLowerInvariant() + ".js";
            string text = string.Format(CssModule, JsonSerializer.Serialize(Path.GetFileName(source)), JsonSerializer.Serialize(File.ReadAllText(source))) + "\n";
            string target = Path.Combine(folder, name);
            if (!kept.Add(target))
                continue;
            Directory.CreateDirectory(folder);
            WriteIfChanged(target, text);
            var specifiers = AliasSources(source, "").Select(a => a.SourceUrl).Where(u => u.EndsWith(".css", StringComparison.Ordinal)).ToArray();
            if (specifiers.Length == 0)
            {
                Log.LogError(null, "HUSK105", null, source, 0, 0, 0, 0, "a HuskStylesheet is under no HuskAlias folder, so no import can name it");
                continue;
            }
            result.Add(("_husk/" + CssFolder + "/" + name, target, specifiers));
        }
        if (Directory.Exists(folder))
            foreach (string stale in Directory.EnumerateFiles(folder).Where(f => !kept.Contains(f)).ToList())
                File.Delete(stale);
        return result;
    }

    private IEnumerable<ImportMapSource> AliasSources(string source, string outputUrl)
    {
        string full = Path.GetFullPath(source);
        foreach (var alias in Aliases)
        {
            string folder = Path.GetFullPath(Path.Combine(ProjectDirectory, alias.GetMetadata("Path")));
            if (!IsServed(full, folder, PathComparison))
                continue;
            string relative = Path.GetRelativePath(folder, full).Replace('\\', '/');
            yield return new ImportMapSource(alias.ItemSpec + "/" + relative, outputUrl);
            // The import without its last extension, as for the project's own sources.
            if (Path.GetExtension(relative) is { Length: > 0 } extension)
            {
                yield return new ImportMapSource(alias.ItemSpec + "/" + relative[..^extension.Length], outputUrl);
                // TypeScript's own convention: a .ts file is imported as .js.
                if (extension == ".ts")
                    yield return new ImportMapSource(alias.ItemSpec + "/" + relative[..^extension.Length] + ".js", outputUrl);
            }
            if (alias.GetMetadata("Entry") is { Length: > 0 } entry && string.Equals(relative, entry.Replace('\\', '/'), StringComparison.Ordinal))
                yield return new ImportMapSource(alias.ItemSpec, outputUrl);
        }
    }

    private Dictionary<string, string> FixedImports(string root)
    {
        var fixedImports = new Dictionary<string, string>(StringComparer.Ordinal);
        if (Routing)
        {
            fixedImports["$app/navigation"] = root + "_husk/app/navigation.js";
            fixedImports["$app/state"] = root + "_husk/app/state.svelte.js";
            fixedImports["$app/environment"] = root + "_husk/app/environment.js";
            fixedImports["@sveltejs/kit"] = root + "_husk/kit.js";
        }
        foreach (var import in Imports)
            fixedImports[import.ItemSpec] = import.GetMetadata("Url");
        return fixedImports;
    }

    /// <summary>Each served script output's source file (full path) to the URL the source is imported
    /// by: <c>/x/Child.svelte</c> for <c>x/Child.svelte.js</c>, the key the import map holds.</summary>
    internal static Dictionary<string, string> SourceUrls(IEnumerable<ITaskItem> outputs, string servedRoot, string root)
    {
        var urls = new Dictionary<string, string>(StringComparer.Ordinal);
        string served = Path.GetFullPath(servedRoot);
        foreach (var output in outputs)
        {
            string relative = output.GetMetadata("PareRelativePath").Replace('\\', '/');
            if (!IsServed(output.GetMetadata("FullPath"), served, PathComparison) || !(relative.EndsWith(".js", StringComparison.Ordinal) || relative.EndsWith(".mjs", StringComparison.Ordinal)))
                continue;
            urls[Path.GetFullPath(output.GetMetadata("PareSource"))] = root + relative[..(relative.LastIndexOf('/') + 1)] + Path.GetFileName(output.GetMetadata("PareSource"));
        }
        return urls;
    }

    public override bool Execute()
    {
        string root = Url(BasePath);
        var styles = new List<(string Name, string Path)>();
        var sources = new List<ImportMapSource>();
        var modules = new List<(string Relative, string Path)>();
        string served = Path.GetFullPath(ServedRoot);
        foreach (var output in Outputs)
        {
            if (!IsServed(output.GetMetadata("FullPath"), served, PathComparison))
                continue;
            string relative = output.GetMetadata("PareRelativePath").Replace('\\', '/');
            if (output.GetMetadata("PareCompiler") == "husk" && relative.EndsWith(".svelte.css", StringComparison.Ordinal))
                styles.Add((relative[..^".css".Length], output.GetMetadata("FullPath")));
            else if (relative.EndsWith(".js", StringComparison.Ordinal) || relative.EndsWith(".mjs", StringComparison.Ordinal))
            {
                int slash = relative.LastIndexOf('/');
                string sourceName = Path.GetFileName(output.GetMetadata("PareSource"));
                sources.Add(new ImportMapSource(root + relative[..(slash + 1)] + sourceName, root + relative));
                sources.AddRange(AliasSources(output.GetMetadata("PareSource"), root + relative));
                modules.Add((relative, output.GetMetadata("FullPath")));
            }
        }
        styles.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        try
        {
            foreach (var (relative, path, specifiers) in WriteStylesheets())
            {
                foreach (string specifier in specifiers)
                    sources.Add(new ImportMapSource(specifier, root + relative));
                modules.Add((relative, path));
            }
            modules.AddRange(WriteJson());
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.LogError(null, "HUSK102", null, OutputDirectory, 0, 0, 0, 0, "cannot write the stylesheet or JSON modules: " + e.Message);
        }
        if (Log.HasLoggedErrors)
            return false;

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
                string map = ImportMapWriter.Write(runtime, root + "_husk/", Dev, sources, hashed.ToDictionary(m => m.Url, m => m.HashedUrl, StringComparer.Ordinal), FixedImports(root));
                WriteIfChanged(importMap, map);
                WriteIfChanged(Path.Combine(OutputDirectory, Graph), ModuleGraphJson(map, hashed));
            }
            else
            {
                WriteHashed(root, []);
                WriteIfChanged(importMap, ImportMapWriter.Write(runtime, root + "_husk/", Dev, sources, null, FixedImports(root)));
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
            // A copy of the wrong length is a build cut short before the move below existed; the name is a hash, so any other copy is right.
            if (kept.Add(copy) && (!File.Exists(copy) || new FileInfo(copy).Length != bytes.Length))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
                // Beside the copy, then moved over it: a build cut short must not leave half a file under a hash that says it is whole.
                string temp = copy + ".husk-tmp";
                File.WriteAllBytes(temp, bytes);
                File.Move(temp, copy, overwrite: true);
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
        // A JSON file is imported, not preloaded as a module (a browser refuses it without its import attribute).
        var known = modules.Where(m => !m.Url.EndsWith(".json", StringComparison.Ordinal)).Select(m => m.HashedUrl).ToHashSet(StringComparer.Ordinal);
        var graph = new JsonObject();
        foreach (var module in modules.Where(m => !m.Url.EndsWith(".json", StringComparison.Ordinal)).OrderBy(m => m.HashedUrl, StringComparer.Ordinal))
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
            json.WriteBoolean("routing", Routing);
            json.WriteString("routesRoot", RoutesRoot);
            json.WriteString("project", Project);
            json.WriteString("configuration", Configuration);
            json.WriteString("targetFramework", TargetFramework);
            json.WriteString("servedRoot", Path.GetFullPath(ServedRoot));
            json.WriteString("basePath", Url(BasePath));
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
