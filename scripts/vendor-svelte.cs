// Vendors the pinned Svelte compiler and runtime into third-party/svelte-<version>/, with no Node.
// Each npm tarball is fetched over HTTPS (or read from the cache) and its sha512 checked against
// scripts/vendor-svelte.lock.json before anything is unpacked. Writes runtime-imports.json, the
// runtime half of the import map, from the packages' own `exports` and `imports`.
//
// Usage: dotnet run scripts/vendor-svelte.cs -- [--out <dir>] [--cache <dir>]
// Run from the repo root. --out must not exist yet: a re-run goes to a fresh directory, so nothing
// stale survives and nothing is deleted. --cache defaults to tmp/vendor-cache.
using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

var lockPath = Path.Combine("scripts", "vendor-svelte.lock.json");
var pins = JsonNode.Parse(File.ReadAllText(lockPath))!.AsObject();
string svelteVersion = (string)pins["svelte"]!["version"]!;
string outDir = Option("--out") ?? Path.Combine("third-party", $"svelte-{svelteVersion}");
string cacheDir = Option("--cache") ?? Path.Combine("tmp", "vendor-cache");

if (Directory.Exists(outDir) || File.Exists(outDir))
    return Fail($"{outDir} exists. Vendor into a fresh directory with --out, or move it aside first.");
Directory.CreateDirectory(cacheDir);

// Package -> which of its files ship. Paths are below the tarball's package/ folder.
var keep = new Dictionary<string, Func<string, bool>>
{
    ["svelte"] = p => p is "package.json" or "LICENSE.md" or "compiler/index.js" or "compiler/package.json"
        || (p.StartsWith("src/", StringComparison.Ordinal) && p.EndsWith(".js", StringComparison.Ordinal) && !p.StartsWith("src/compiler/", StringComparison.Ordinal)),
    ["esm-env"] = p => p is "package.json" or "LICENSE" || (!p.Contains('/') && p.EndsWith(".js", StringComparison.Ordinal)),
    ["clsx"] = p => p is "package.json" or "license" or "dist/clsx.mjs",
};

using var http = new HttpClient();
var files = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
foreach (var (name, pin) in pins)
{
    string version = (string)pin!["version"]!;
    string integrity = (string)pin["integrity"]!;
    string url = (string)pin["resolved"]!;
    if (!url.StartsWith("https://", StringComparison.Ordinal))
        return Fail($"{name}: {url} is not HTTPS.");
    if (!keep.TryGetValue(name, out var wanted))
        return Fail($"{name}: the lock names a package this script does not know which files to keep from.");

    string cached = Path.Combine(cacheDir, $"{name}-{version}.tgz");
    byte[] tarball = File.Exists(cached) ? File.ReadAllBytes(cached) : await http.GetByteArrayAsync(url);
    string actual = "sha512-" + Convert.ToBase64String(SHA512.HashData(tarball));
    if (actual != integrity)
        return Fail($"{name}-{version}.tgz: integrity {actual} does not match the lock's {integrity}. Refusing to unpack.");
    if (!File.Exists(cached))
        File.WriteAllBytes(cached, tarball);

    using var reader = new TarReader(new GZipStream(new MemoryStream(tarball), CompressionMode.Decompress));
    while (reader.GetNextEntry() is { } entry)
    {
        if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile))
            continue;
        string path = entry.Name.Replace('\\', '/');
        if (!path.StartsWith("package/", StringComparison.Ordinal) || path.Contains("/../", StringComparison.Ordinal))
            return Fail($"{name}: unexpected tarball entry {entry.Name}.");
        path = path["package/".Length..];
        if (!wanted(path))
            continue;
        using var data = new MemoryStream();
        entry.DataStream?.CopyTo(data);
        files[$"{name}/{path}"] = data.ToArray();
    }
}

// The versions the runtime asks for must be the ones pinned (a caret range: same major, not lower).
var svelteManifest = JsonNode.Parse(files["svelte/package.json"])!;
foreach (var dep in new[] { "esm-env", "clsx" })
{
    string range = (string)svelteManifest["dependencies"]![dep]!;
    string pinned = (string)pins[dep]!["version"]!;
    if (!Satisfies(pinned, range))
        return Fail($"svelte {svelteVersion} asks for {dep} {range}; the lock pins {pinned}.");
}

files["runtime-imports.json"] = Encoding.UTF8.GetBytes(RuntimeImports(files) + "\n");

foreach (var (relative, bytes) in files)
{
    string path = Path.Combine(outDir, relative);
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllBytes(path, bytes);
}
Console.WriteLine($"vendored {files.Count} files into {outDir}");
return 0;

// Runtime specifier -> file below the vendored root. A subpath gets an entry when the condition a
// browser resolves (browser, then import, then default) names a module that was vendored. Server-only
// entry points are left out, and `production` lists the entries that differ with HuskDev false.
static string RuntimeImports(SortedDictionary<string, byte[]> files)
{
    string[] dev = ["browser", "import", "development", "default"];
    string[] production = ["browser", "import", "production", "default"];
    string[] serverOnly = ["svelte/server", "svelte/internal/server"];
    var imports = new SortedDictionary<string, string>(StringComparer.Ordinal);
    var overrides = new SortedDictionary<string, string>(StringComparer.Ordinal);
    foreach (var name in new[] { "svelte", "esm-env", "clsx" })
    {
        var manifest = JsonNode.Parse(files[$"{name}/package.json"])!;
        foreach (var (subpath, target) in manifest["exports"]!.AsObject())
            Add(name + subpath[1..], target, name);
        if (manifest["imports"] is JsonObject privateImports)
            foreach (var (specifier, target) in privateImports)
                Add(specifier, target, name);
    }
    var json = new JsonObject
    {
        ["imports"] = Object(imports),
        ["production"] = Object(overrides),
    };
    return json.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

    void Add(string specifier, JsonNode? target, string package)
    {
        if (serverOnly.Contains(specifier))
            return;
        string? file = Resolve(target, dev);
        if (file is null || !(file.EndsWith(".js", StringComparison.Ordinal) || file.EndsWith(".mjs", StringComparison.Ordinal))
            || !files.ContainsKey($"{package}/{file[2..]}"))
            return;
        imports[specifier] = $"{package}/{file[2..]}";
        string? prod = Resolve(target, production);
        if (prod is not null && prod != file)
            overrides[specifier] = $"{package}/{prod[2..]}";
    }
}

static string? Resolve(JsonNode? target, string[] conditions)
{
    if (target is JsonValue value)
        return value.GetValue<string>();
    if (target is not JsonObject map)
        return null;
    foreach (var (condition, next) in map)
        if (conditions.Contains(condition))
            return Resolve(next, conditions);
    return null;
}

static JsonObject Object(SortedDictionary<string, string> entries)
{
    var json = new JsonObject();
    foreach (var (key, value) in entries)
        json[key] = value;
    return json;
}

static bool Satisfies(string version, string range)
{
    if (!range.StartsWith('^'))
        return version == range;
    var want = range[1..].Split('.').Select(int.Parse).ToArray();
    var have = version.Split('.').Select(int.Parse).ToArray();
    return have[0] == want[0] && (have[1], have[2]).CompareTo((want[1], want[2])) >= 0;
}

static int Fail(string message)
{
    Console.Error.WriteLine("vendor-svelte: " + message);
    return 1;
}

string? Option(string name)
{
    int i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}
