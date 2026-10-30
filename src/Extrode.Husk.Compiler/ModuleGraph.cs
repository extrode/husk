using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Extrode.Husk.Compiler;

/// <summary>What hashed output and modulepreload need from a module: its hashed name, the static
/// specifiers it imports, and where the import map sends each. A read-only scan: nothing is
/// rewritten, and a specifier it misses costs a preload only.</summary>
public static partial class ModuleGraph
{
    /// <summary>The first 10 hex digits of the bytes' SHA-256.</summary>
    public static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes))[..10];

    /// <summary>The hash before the last extension: <c>x/Child.svelte.js</c> becomes
    /// <c>x/Child.svelte.&lt;hash&gt;.js</c>.</summary>
    public static string Hashed(string path, string hash)
    {
        int dot = path.LastIndexOf('.');
        return dot > path.LastIndexOf('/') ? path[..dot] + "." + hash + path[dot..] : path + "." + hash;
    }

    /// <summary>The specifiers of <c>import ... from</c>, <c>import "..."</c> and
    /// <c>export ... from</c>, in order. Not <c>import()</c>, and not JSDoc's <c>@import</c>.</summary>
    public static IEnumerable<string> Imports(string code) =>
        Static().Matches(code).Select(m => m.Groups["specifier"].Value);

    /// <summary>The URL the browser loads for a specifier in a module or page at
    /// <paramref name="baseUrl"/>: a URL-like one resolved against it, then through the map's
    /// <paramref name="imports"/>. Null for a bare specifier the map does not name, or a full URL.</summary>
    public static string? Resolve(string specifier, string baseUrl, IReadOnlyDictionary<string, string> imports)
    {
        if (specifier.StartsWith('/') || specifier.StartsWith("./", StringComparison.Ordinal) || specifier.StartsWith("../", StringComparison.Ordinal))
        {
            if (specifier.StartsWith("//", StringComparison.Ordinal))
                return null;
            string url = Uri.UnescapeDataString(new Uri(new Uri("http://h" + baseUrl), specifier).AbsolutePath);
            return imports.GetValueOrDefault(url, url);
        }
        return imports.GetValueOrDefault(specifier);
    }

    [GeneratedRegex("""(?<![\w$.@])(?:import|export)\s*(?:[^;"'`()]*?[\s}*]from\s*)?(?<q>["'])(?<specifier>[^"'\r\n]+)\k<q>""",
        RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex Static();
}
