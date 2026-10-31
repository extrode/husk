using System.Text.Json;
using System.Text.RegularExpressions;

namespace Extrode.Husk.Compiler;

/// <summary>The app's HTML shell from its <c>app.html</c>: <c>%sveltekit.head%</c> becomes the
/// environment script, the import-map marker (<see cref="PageWriter"/> fills it), the stylesheet and the
/// entry script; <c>%sveltekit.body%</c> becomes the element the router mounts in. Other placeholders
/// are answered as SvelteKit answers them for a client-only app.</summary>
public static partial class AppShell
{
    public const string Default = "<!doctype html>\n<html>\n<head>\n  %sveltekit.head%\n</head>\n<body>\n  %sveltekit.body%\n</body>\n</html>\n";

    /// <summary>The element id the router mounts in; <c>routes.js</c> names it as a selector.</summary>
    public const string MountId = "husk-body";

    public static string Build(string template, string root, bool dev, IReadOnlyDictionary<string, string> env)
    {
        if (!template.Contains("%sveltekit.head%", StringComparison.Ordinal) || !template.Contains("%sveltekit.body%", StringComparison.Ordinal))
            throw new FormatException("the shell needs both %sveltekit.head% and %sveltekit.body%");

        var values = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["BASE_URL"] = root,
            ["DEV"] = dev ? "true" : "false",
            ["MODE"] = dev ? "development" : "production",
            ["PROD"] = dev ? "false" : "true",
            ["SSR"] = "false",
        };
        var script = new System.Text.StringBuilder("globalThis.__huskEnv = {");
        foreach (var (name, value) in values)
            script.Append(' ').Append(Quote(name)).Append(": ").Append(name is "DEV" or "PROD" or "SSR" ? value : Quote(value)).Append(',');
        foreach (var (name, value) in env.OrderBy(e => e.Key, StringComparer.Ordinal))
            script.Append(' ').Append(Quote(name)).Append(": ").Append(Quote(value)).Append(',');
        script.Append(" };");

        string head = $"<script>{script}</script>\n  <script type=\"importmap\" data-husk></script>\n  <link rel=\"stylesheet\" href=\"{root}_husk/husk.css\">\n  <script type=\"module\" src=\"{root}_husk/start.js\"></script>";
        string body = $"<div id=\"{MountId}\" style=\"display: contents\"></div>";

        // Placeholders in the template are filled before the generated head and body go in, so a build value
        // that looks like a placeholder is left as text.
        string html = template.Replace("%sveltekit.assets%", root.TrimEnd('/'), StringComparison.Ordinal).Replace("%sveltekit.nonce%", "", StringComparison.Ordinal);
        html = EnvPlaceholder().Replace(html, m => env.TryGetValue(m.Groups[1].Value, out var v) ? System.Net.WebUtility.HtmlEncode(v) : "");
        return html.Replace("%sveltekit.head%", head, StringComparison.Ordinal).Replace("%sveltekit.body%", body, StringComparison.Ordinal);
    }

    // The script sits in HTML: '<' stays escaped so a value cannot close the tag.
    private static string Quote(string value) =>
        JsonSerializer.Serialize(value, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping })
            .Replace("<", "\\u003c", StringComparison.Ordinal);

    [GeneratedRegex(@"%sveltekit\.env\.([A-Za-z0-9_]+)%")]
    private static partial Regex EnvPlaceholder();
}
