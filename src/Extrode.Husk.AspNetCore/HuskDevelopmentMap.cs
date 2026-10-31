using System.Text.Json;

namespace Extrode.Husk.AspNetCore;

/// <summary>Extrode.Husk.development.json, which the build writes next to the app (never to a
/// publish): where the compiler is and what the build compiled with.</summary>
internal sealed record HuskDevelopmentMap(string Compiler, string Svelte, string ProjectDirectory, bool Dev,
    string Stylesheet, string ImportMap, IReadOnlyList<(string Name, string Css)> Styles, bool HashNames = false, bool Routing = false)
{
    public static HuskDevelopmentMap? Read(string path)
    {
        if (!File.Exists(path))
            return null;
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        var root = json.RootElement;
        return new HuskDevelopmentMap(
            root.GetProperty("compiler").GetString()!,
            root.GetProperty("svelte").GetString()!,
            root.GetProperty("projectDirectory").GetString()!,
            root.GetProperty("dev").GetBoolean(),
            root.GetProperty("stylesheet").GetString()!,
            root.GetProperty("importMap").GetString()!,
            [.. root.GetProperty("styles").EnumerateArray().Select(s => (s.GetProperty("name").GetString()!, s.GetProperty("css").GetString()!))],
            root.TryGetProperty("hashNames", out var hashNames) && hashNames.GetBoolean(),
            root.TryGetProperty("routing", out var routing) && routing.GetBoolean());
    }
}
