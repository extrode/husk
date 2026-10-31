using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Extrode.Husk.Compiler;

/// <summary>Inlines the import map into a page that asks for it with
/// <c>&lt;script type="importmap" data-husk&gt;&lt;/script&gt;</c>: browsers do not load an import map
/// by <c>src</c>. The map's <c>&lt;</c> is already escaped, so it cannot close the tag.</summary>
public static partial class PageWriter
{
    /// <summary>The page with the map between the marker's tags, or null when it has no marker.</summary>
    public static string? Inline(string html, string importMap) => Inline(html, importMap, "");

    /// <summary>As <see cref="Inline(string, string)"/>, for hashed output: each module script's
    /// <c>src</c> goes through the map, since browsers apply it to imports only, and the map is
    /// followed by a <c>modulepreload</c> link for every module the page's module scripts reach in
    /// <paramref name="graph"/>, JSON of each hashed URL to the hashed URLs it imports.</summary>
    public static string? Inline(string html, string importMap, string pageUrl, string graph)
    {
        if (!Marker().IsMatch(html))
            return null;
        var imports = JsonNode.Parse(importMap)!["imports"]!.AsObject().ToDictionary(e => e.Key, e => (string)e.Value!, StringComparer.Ordinal);
        var edges = JsonNode.Parse(graph)!.AsObject().ToDictionary(e => e.Key, e => e.Value!.AsArray().Select(d => (string)d!).ToList(), StringComparer.Ordinal);
        var entries = new List<string>();
        html = ModuleScript().Replace(html, script =>
        {
            foreach (var imported in ModuleGraph.Imports(script.Groups["body"].Value))
                if (ModuleGraph.Resolve(imported, pageUrl, imports) is { } resolved)
                    entries.Add(resolved);
            var src = Src().Match(script.Groups["open"].Value);
            if (!src.Success)
                return script.Value;
            var value = src.Groups["src"];
            // A src is a URL, never a bare specifier: main.js is ./main.js.
            string specifier = value.Value.StartsWith('/') || value.Value.Contains(':') ? value.Value : "./" + value.Value;
            if (ModuleGraph.Resolve(specifier, pageUrl, NoImports) is not { } url)
                return script.Value;
            if (!imports.TryGetValue(url, out var target))
            {
                entries.Add(url);
                return script.Value;
            }
            entries.Add(target);
            return script.Value[..value.Index] + AttributeEscape(target) + script.Value[(value.Index + value.Length)..];
        });

        var preload = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
            Visit(entry);
        var marker = Marker().Match(html);
        int line = marker.Index == 0 ? 0 : html.LastIndexOf('\n', marker.Index - 1) + 1;
        string indent = html[line..marker.Index].Trim().Length == 0 ? html[line..marker.Index] : "";
        return Inline(html, importMap, string.Concat(preload.Select(url =>
            "\n" + indent + "<link rel=\"modulepreload\" href=\"" + AttributeEscape(url) + "\">")));

        static string AttributeEscape(string url) =>
            url.Replace("&", "&amp;", StringComparison.Ordinal).Replace("\"", "&quot;", StringComparison.Ordinal);

        void Visit(string url)
        {
            if (!edges.TryGetValue(url, out var imported) || !seen.Add(url))
                return;
            preload.Add(url);
            foreach (var next in imported)
                Visit(next);
        }
    }

    private static readonly Dictionary<string, string> NoImports = [];

    private static string? Inline(string html, string importMap, string after)
    {
        var marker = Marker().Match(html);
        if (!marker.Success)
            return null;
        return html[..marker.Index] + marker.Groups["open"].Value + importMap.TrimEnd('\n') + "</script>" + after
            + html[(marker.Index + marker.Length)..];
    }

    // An empty importmap script with a data-husk attribute, attributes in any order and quoting.
    [GeneratedRegex("""(?<open><script(?![\w-])(?=[^>]*\stype\s*=\s*["']?importmap["']?[\s>])(?=[^>]*\sdata-husk[\s=>])[^>]*>)\s*</script\s*>""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex Marker();

    // A module script, its open tag and its inline text.
    [GeneratedRegex("""(?<open><script(?![\w-])(?=[^>]*\stype\s*=\s*["']?module["']?[\s>])[^>]*>)(?<body>.*?)</script\s*>""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline, matchTimeoutMilliseconds: 2000)]
    private static partial Regex ModuleScript();

    [GeneratedRegex("""\ssrc\s*=\s*(?:"(?<src>[^"]*)"|'(?<src>[^']*)'|(?<src>[^\s"'>]+))""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex Src();
}
