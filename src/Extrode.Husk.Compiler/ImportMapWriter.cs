using System.Text.Json;
using System.Text.Json.Nodes;

namespace Extrode.Husk.Compiler;

/// <summary>A compiled source or a Pare <c>.ts</c> output, by URL: <c>/x/Child.svelte</c> served as
/// <c>/x/Child.svelte.js</c>.</summary>
public readonly record struct ImportMapSource(string SourceUrl, string OutputUrl);

/// <summary>The runtime half of the import map, vendored as runtime-imports.json: specifier to file
/// below the vendored root, and the entries that differ when dev is off.</summary>
public sealed class RuntimeImports(IReadOnlyDictionary<string, string> imports, IReadOnlyDictionary<string, string> production)
{
    public IReadOnlyDictionary<string, string> Imports { get; } = imports;

    public IReadOnlyDictionary<string, string> Production { get; } = production;

    public static RuntimeImports Load(string path)
    {
        var json = JsonNode.Parse(File.ReadAllText(path))!;
        return new RuntimeImports(Read(json["imports"]), Read(json["production"]));

        static Dictionary<string, string> Read(JsonNode? node) =>
            node?.AsObject().ToDictionary(e => e.Key, e => (string)e.Value!, StringComparer.Ordinal) ?? [];
    }
}

/// <summary>Writes the import map. Compiled code keeps the specifiers the compiler wrote
/// (<c>./Child.svelte</c>, <c>svelte/internal/client</c>), so the map resolves them:
/// each runtime specifier to its file under the runtime base URL, and each source URL, with its
/// extension and without its last one, to its output URL. Keys are sorted ordinally. With
/// <paramref name="hashed"/> (plain URL to hashed URL), every value it names becomes the hashed URL,
/// and each plain URL is a key too, for the relative imports between modules. <paramref name="fixedImports"/>
/// (specifier to URL) are added as given, before that step.</summary>
public static class ImportMapWriter
{
    public static string Write(RuntimeImports runtime, string runtimeBase, bool dev, IEnumerable<ImportMapSource> sources,
        IReadOnlyDictionary<string, string>? hashed = null, IReadOnlyDictionary<string, string>? fixedImports = null)
    {
        var imports = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (specifier, file) in runtime.Imports)
            imports[specifier] = runtimeBase + (dev ? file : runtime.Production.GetValueOrDefault(specifier, file));

        var list = sources.ToList();
        foreach (var source in list)
            if (source.SourceUrl != source.OutputUrl)
                imports[source.SourceUrl] = source.OutputUrl;
        // A key without the extension that two sources would share (a.svelte and a.ts) is left out:
        // neither import is the obvious one.
        var bare = list.Select(s => (Key: WithoutExtension(s.SourceUrl), s.OutputUrl))
            .Where(s => s.Key is not null && s.Key != s.OutputUrl && !imports.ContainsKey(s.Key))
            .GroupBy(s => s.Key!, StringComparer.Ordinal);
        foreach (var group in bare)
            if (group.Select(s => s.OutputUrl).Distinct(StringComparer.Ordinal).Count() == 1)
                imports[group.Key] = group.First().OutputUrl;
        // Specifiers that name a file directly ($app/navigation, a vendored package): the file's URL,
        // hashed like any other value below.
        foreach (var (specifier, url) in fixedImports ?? new Dictionary<string, string>())
            imports[specifier] = url;
        if (hashed is not null)
        {
            foreach (var (key, value) in imports.ToList())
                imports[key] = hashed.GetValueOrDefault(value, value);
            foreach (var (plain, url) in hashed)
                imports.TryAdd(plain, url);
        }

        var map = new JsonObject();
        foreach (var (key, value) in imports)
            map[key] = value;
        // `<` only appears inside strings: escaped, the map can be inlined in a <script> safely.
        return new JsonObject { ["imports"] = map }.ToJsonString(Indented).Replace("<", "\\u003c", StringComparison.Ordinal) + "\n";
    }

    // "\n" on every OS: a build on Windows writes the same bytes as one on macOS.
    private static readonly JsonSerializerOptions Indented = new()
    {
        WriteIndented = true,
        NewLine = "\n",
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static string? WithoutExtension(string url)
    {
        int slash = url.LastIndexOf('/');
        int dot = url.LastIndexOf('.');
        return dot > slash + 1 ? url[..dot] : null;
    }
}
