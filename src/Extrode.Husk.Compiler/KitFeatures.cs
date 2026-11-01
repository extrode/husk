using System.Text.RegularExpressions;

namespace Extrode.Husk.Compiler;

/// <summary>What of SvelteKit's client half the router provides, and a scan that names the rest. A feature
/// outside this set fails the build (HUSK302, HUSK303) instead of failing in a browser.</summary>
public static partial class KitFeatures
{
    public sealed record Finding(int Line, string Message);

    private static readonly Dictionary<string, HashSet<string>> Modules = new(StringComparer.Ordinal)
    {
        ["$app/navigation"] = ["goto", "beforeNavigate", "afterNavigate", "preloadCode"],
        ["$app/state"] = ["page"],
        ["$app/environment"] = ["browser", "dev", "building", "version"],
        ["@sveltejs/kit"] = ["redirect", "error", "isRedirect", "isHttpError"],
    };

    private static readonly HashSet<string> KitOptionsAccepted = new(StringComparer.Ordinal) { "adapter", "alias", "paths", "csp" };

    [GeneratedRegex("""\b(?:import|export)\s+(?<type>type\s+)?(?<clause>[^;'"]*?)\s*\bfrom\s*['"](?<spec>[^'"\n]+)['"]""", RegexOptions.Singleline)]
    private static partial Regex StaticImport();

    [GeneratedRegex("""\bimport\s*\(\s*['"](?<spec>[^'"\n]+)['"]\s*\)""")]
    private static partial Regex DynamicImport();

    [GeneratedRegex(@"\bimport\.meta\.env\b")]
    private static partial Regex MetaEnv();

    /// <summary>Imports in <paramref name="source"/> of a <c>$app/*</c>, <c>$env/*</c> or <c>@sveltejs/kit</c> name the
    /// router lacks. Type-only imports are erased and are not findings.</summary>
    public static IReadOnlyList<Finding> Scan(string source)
    {
        var found = new List<Finding>();
        foreach (Match m in StaticImport().Matches(source))
        {
            if (m.Groups["type"].Success)
                continue;
            string spec = m.Groups["spec"].Value;
            if (!Concerns(spec))
                continue;
            int line = LineOf(source, m.Index);
            if (!Modules.TryGetValue(spec, out var allowed))
            {
                found.Add(new Finding(line, $"{spec} is not provided"));
                continue;
            }
            string clause = m.Groups["clause"].Value.Trim();
            if (clause.StartsWith('*'))
                continue;
            int open = clause.IndexOf('{', StringComparison.Ordinal);
            if (open != 0 && clause.Length > 0)
                found.Add(new Finding(line, $"{spec} has no default export"));
            if (open < 0)
                continue;
            int close = clause.IndexOf('}', open + 1);
            foreach (string part in clause[(open + 1)..(close < 0 ? clause.Length : close)].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                if (part.StartsWith("type ", StringComparison.Ordinal))
                    continue;
                string name = part.Split(" as ", 2, StringSplitOptions.TrimEntries)[0];
                if (!allowed.Contains(name))
                    found.Add(new Finding(line, $"{name} from {spec} is not provided"));
            }
        }
        foreach (Match m in DynamicImport().Matches(source))
            if (Concerns(m.Groups["spec"].Value) && !Modules.ContainsKey(m.Groups["spec"].Value))
                found.Add(new Finding(LineOf(source, m.Index), $"{m.Groups["spec"].Value} is not provided"));
        return found;
    }

    private static bool Concerns(string spec) =>
        spec.StartsWith("$app/", StringComparison.Ordinal) || spec.StartsWith("$env/", StringComparison.Ordinal)
        || spec.StartsWith("$service-worker", StringComparison.Ordinal) || spec == "@sveltejs/kit" || spec.StartsWith("@sveltejs/kit/", StringComparison.Ordinal);

    private static int LineOf(string text, int index)
    {
        int line = 1;
        for (int i = 0; i < index; i++)
            if (text[i] == '\n')
                line++;
        return line;
    }

    /// <summary>The names of <c>kit</c> options in a <c>svelte.config.js</c> that Husk does not honour and whose
    /// absence would change the app: everything but <c>adapter</c>, <c>alias</c>, <c>paths</c> and <c>csp</c>.</summary>
    public static IReadOnlyList<Finding> ScanConfig(string config)
    {
        var found = new List<Finding>();
        var kit = Regex.Match(config, @"\bkit\s*:\s*\{");
        if (!kit.Success)
            return found;
        int depth = 1, i = kit.Index + kit.Length;
        bool expectKey = true;
        while (i < config.Length && depth > 0)
        {
            char c = config[i];
            if (c is '\'' or '"' or '`')
            {
                char quote = c;
                int start = ++i;
                while (i < config.Length && config[i] != quote)
                    i += config[i] == '\\' ? 2 : 1;
                if (depth == 1 && expectKey && Peek(config, i + 1) == ':')
                    Check(config[start..Math.Min(i, config.Length)], LineOf(config, start));
                expectKey = false;
                i++;
                continue;
            }
            if (c == '/' && i + 1 < config.Length && config[i + 1] == '/')
            {
                while (i < config.Length && config[i] != '\n')
                    i++;
                continue;
            }
            if (c == '/' && i + 1 < config.Length && config[i + 1] == '*')
            {
                int end = config.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = end < 0 ? config.Length : end + 2;
                continue;
            }
            if (c is '{' or '[' or '(')
                depth++;
            else if (c is '}' or ']' or ')')
                depth--;
            else if (depth == 1 && c == ',')
                expectKey = true;
            else if (depth == 1 && expectKey && (char.IsLetter(c) || c is '_' or '$'))
            {
                int start = i;
                while (i < config.Length && (char.IsLetterOrDigit(config[i]) || config[i] is '_' or '$'))
                    i++;
                if (Peek(config, i) is ':' or ',' or '}')
                    Check(config[start..i], LineOf(config, start));
                expectKey = false;
                continue;
            }
            else if (depth == 1 && !char.IsWhiteSpace(c) && c != ':')
                expectKey = false;
            i++;
        }
        return found;

        void Check(string name, int line)
        {
            if (!KitOptionsAccepted.Contains(name))
                found.Add(new Finding(line, $"the kit option {name} is not supported"));
        }
    }

    private static char Peek(string text, int index)
    {
        while (index < text.Length && char.IsWhiteSpace(text[index]))
            index++;
        return index < text.Length ? text[index] : '\0';
    }

    /// <summary><c>import.meta.env</c> becomes <c>globalThis.__huskEnv</c>, which the app's page defines. Idempotent.</summary>
    public static string RewriteEnv(string js) =>
        js.Contains("import.meta.env", StringComparison.Ordinal) ? MetaEnv().Replace(js, "globalThis.__huskEnv") : js;
}
