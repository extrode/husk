using System.Text.RegularExpressions;

namespace Extrode.Husk.Compiler;

/// <summary>Inlines the import map into a page that asks for it with
/// <c>&lt;script type="importmap" data-husk&gt;&lt;/script&gt;</c>: browsers do not load an import map
/// by <c>src</c>. The map's <c>&lt;</c> is already escaped, so it cannot close the tag.</summary>
public static partial class PageWriter
{
    /// <summary>The page with the map between the marker's tags, or null when it has no marker.</summary>
    public static string? Inline(string html, string importMap)
    {
        var marker = Marker().Match(html);
        if (!marker.Success)
            return null;
        return html[..marker.Index] + marker.Groups["open"].Value + importMap.TrimEnd('\n') + "</script>"
            + html[(marker.Index + marker.Length)..];
    }

    // An empty importmap script with a data-husk attribute, attributes in any order and quoting.
    [GeneratedRegex("""(?<open><script(?=[^>]*\stype\s*=\s*["']?importmap["']?[\s>])(?=[^>]*\sdata-husk[\s=>])[^>]*>)\s*</script\s*>""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Marker();
}
