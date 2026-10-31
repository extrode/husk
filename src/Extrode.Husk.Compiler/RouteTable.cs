namespace Extrode.Husk.Compiler;

/// <summary>One part of an address: <c>settings</c>, or the parameter <c>[section]</c>.</summary>
public readonly record struct RouteSegment(string Text, bool IsParam);

/// <summary>A layout in a route's chain and its <c>+layout.ts</c>/<c>+layout.js</c>, if any. Paths are
/// below the routes root, with <c>/</c>.</summary>
public sealed record RouteLayout(string Component, string? Load);

public sealed record Route(IReadOnlyList<RouteSegment> Segments, string Page, string? PageLoad, IReadOnlyList<RouteLayout> Layouts)
{
    /// <summary>The address pattern, <c>/settings/[section]</c>; the root is <c>/</c>.</summary>
    public string Pattern => Segments.Count == 0 ? "/" : "/" + string.Join('/', Segments.Select(s => s.IsParam ? "[" + s.Text + "]" : s.Text));
}

/// <summary>A problem in the routes folder. <paramref name="Code"/> is HUSK300 (two routes for one address)
/// or HUSK301 (a SvelteKit feature Husk does not provide); <paramref name="File"/> is below the routes root.</summary>
public sealed record RouteDiagnostic(string Code, string File, string Message);

public sealed record RouteTableResult(IReadOnlyList<Route> Routes, string? ErrorPage, IReadOnlyList<RouteDiagnostic> Diagnostics, RouteLayout? RootLayout = null)
{
    public bool Ok => Diagnostics.Count == 0;
}

/// <summary>Turns the list of files below the routes root into routes, the way SvelteKit's folder
/// rules do for the part saaaat uses: a folder with <c>+page.svelte</c> is a route, <c>[name]</c> a
/// parameter, <c>(name)</c> a group that adds nothing to the address, layouts nest outermost first.
/// Only files starting with <c>+</c> are route files. Pure: the same file list gives the same table,
/// in an order that puts a literal segment before a parameter at the same place.</summary>
public static class RouteTable
{
    private static readonly string[] Scripts = [".ts", ".js"];

    public static RouteTableResult Build(IEnumerable<string> files)
    {
        var diagnostics = new List<RouteDiagnostic>();
        var folders = new Dictionary<string, Folder>(StringComparer.Ordinal);
        string? errorPage = null;

        foreach (string file in files.Select(f => f.Replace('\\', '/')).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            int slash = file.LastIndexOf('/');
            string dir = slash < 0 ? "" : file[..slash];
            string name = file[(slash + 1)..];
            if (!name.StartsWith('+'))
                continue;

            if (Unsupported(name) is { } why)
            {
                diagnostics.Add(new("HUSK301", file, why));
                continue;
            }

            if (name == "+error.svelte")
            {
                if (dir.Length == 0)
                    errorPage = file;
                else
                    diagnostics.Add(new("HUSK301", file, "an error page below the routes root (only the root +error.svelte is supported)"));
                continue;
            }

            var folder = folders.TryGetValue(dir, out var f) ? f : folders[dir] = new Folder();
            switch (name)
            {
                case "+page.svelte": folder.Page = file; break;
                case "+layout.svelte": folder.Layout = file; break;
                case "+page.ts" or "+page.js": Load(diagnostics, ref folder.PageLoad, file); break;
                case "+layout.ts" or "+layout.js": Load(diagnostics, ref folder.LayoutLoad, file); break;
                default: diagnostics.Add(new("HUSK301", file, "the route file " + name)); break;
            }
        }

        var routes = new List<Route>();
        var seen = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (dir, folder) in folders.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            if (folder.Page is null)
                continue;

            var segments = new List<RouteSegment>();
            foreach (string part in dir.Length == 0 ? [] : dir.Split('/'))
            {
                if (part.StartsWith('(') && part.EndsWith(')'))
                    continue;
                if (part.StartsWith('['))
                {
                    if (!Parameter(part, out string? parameter, out string? problem))
                    {
                        diagnostics.Add(new("HUSK301", folder.Page, problem!));
                        segments = null;
                        break;
                    }
                    segments.Add(new(parameter!, true));
                }
                else
                    segments.Add(new(part, false));
            }
            if (segments is null)
                continue;

            string key = "/" + string.Join('/', segments.Select(s => s.IsParam ? "[]" : s.Text));
            if (seen.TryGetValue(key, out string? first))
            {
                diagnostics.Add(new("HUSK300", folder.Page, $"the same address as {first}: {key.Replace("[]", "[param]")}"));
                continue;
            }
            seen[key] = folder.Page;

            var layouts = new List<RouteLayout>();
            string at = "";
            foreach (string part in new[] { "" }.Concat(dir.Length == 0 ? [] : dir.Split('/')))
            {
                if (part.Length > 0)
                    at = at.Length == 0 ? part : at + "/" + part;
                if (folders.TryGetValue(at, out var above) && above.Layout is not null)
                    layouts.Add(new(above.Layout, above.LayoutLoad));
            }
            routes.Add(new(segments, folder.Page, folder.PageLoad, layouts));
        }

        routes.Sort(Specificity);
        var root = folders.TryGetValue("", out var top) && top.Layout is not null ? new RouteLayout(top.Layout, top.LayoutLoad) : null;
        return new(routes, errorPage, diagnostics, root);
    }

    private static void Load(List<RouteDiagnostic> diagnostics, ref string? slot, string file)
    {
        if (slot is null)
            slot = file;
        else
            diagnostics.Add(new("HUSK300", file, $"a second load file beside {slot}"));
    }

    private static string? Unsupported(string name)
    {
        if (name.StartsWith("+server", StringComparison.Ordinal))
            return "an API endpoint (" + name + ")";
        if (name.Contains(".server.", StringComparison.Ordinal))
            return "a server load or action (" + name + ")";
        if (name.Contains('@'))
            return "a layout reset (" + name + ")";
        string stem = name.Contains('.') ? name[..name.IndexOf('.')] : name;
        return stem is "+page" or "+layout" or "+error" ? null : "the route file " + name;
    }

    private static bool Parameter(string part, out string? name, out string? problem)
    {
        name = null;
        problem = null;
        if (!part.EndsWith(']'))
            problem = "a malformed parameter folder " + part;
        else if (part.StartsWith("[[", StringComparison.Ordinal))
            problem = "an optional parameter " + part;
        else if (part.StartsWith("[...", StringComparison.Ordinal))
            problem = "a rest parameter " + part;
        else if (part.Contains('='))
            problem = "a parameter matcher " + part;
        else if (part.Length == 2)
            problem = "an unnamed parameter folder " + part;
        else
            name = part[1..^1];
        return problem is null;
    }

    private static int Specificity(Route a, Route b)
    {
        for (int i = 0; i < Math.Min(a.Segments.Count, b.Segments.Count); i++)
        {
            if (a.Segments[i].IsParam != b.Segments[i].IsParam)
                return a.Segments[i].IsParam ? 1 : -1;
            int c = string.CompareOrdinal(a.Segments[i].Text, b.Segments[i].Text);
            if (c != 0)
                return c;
        }
        return a.Segments.Count.CompareTo(b.Segments.Count);
    }

    private sealed class Folder
    {
        public string? Page, Layout, PageLoad, LayoutLoad;
    }
}

/// <summary>Writes <c>routes.js</c>, the module the browser router imports. Every module is a dynamic
/// <c>import()</c> of its source URL (<paramref name="urlOf"/> of the file below the routes root),
/// which the import map resolves, so a route's code loads on first use. It refuses a table with
/// diagnostics: a failed build writes no route table.</summary>
public static class RoutesModule
{
    public static string Write(RouteTableResult table, Func<string, string> urlOf, string? hooksUrl, bool dev = false, string target = "body")
    {
        if (!table.Ok)
            throw new InvalidOperationException("the route table has " + table.Diagnostics.Count + " problem(s)");

        string Import(string? file) => file is null ? "null" : "() => import(" + Quote(urlOf(file)) + ")";

        var text = new System.Text.StringBuilder("// Generated by Husk from the routes folder. Do not edit.\n");
        text.Append("export const hooks = ").Append(hooksUrl is null ? "null" : "() => import(" + Quote(hooksUrl) + ")").Append(";\n");
        text.Append("export const errorPage = ").Append(Import(table.ErrorPage)).Append(";\n");
        text.Append("export const rootLayout = ")
            .Append(table.RootLayout is null ? "null" : "{ component: " + Import(table.RootLayout.Component) + ", load: " + Import(table.RootLayout.Load) + " }").Append(";\n");
        text.Append("export const dev = ").Append(dev ? "true" : "false").Append(";\n");
        text.Append("export const target = ").Append(Quote(target)).Append(";\n");
        text.Append("export const routes = [\n");
        foreach (var route in table.Routes)
        {
            text.Append("  {\n    pattern: [")
                .Append(string.Join(", ", route.Segments.Select(s => s.IsParam ? "{ param: " + Quote(s.Text) + " }" : Quote(s.Text))))
                .Append("],\n    layouts: [")
                .Append(string.Join(", ", route.Layouts.Select(l => "{ component: " + Import(l.Component) + ", load: " + Import(l.Load) + " }")))
                .Append("],\n    page: ").Append(Import(route.Page))
                .Append(",\n    load: ").Append(Import(route.PageLoad))
                .Append(",\n  },\n");
        }
        return text.Append("];\n").ToString();
    }

    private static readonly System.Text.Json.JsonSerializerOptions Plain = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    // A .js file served as a file, never inlined in HTML, so '+', '<' and '&' need no escape.
    private static string Quote(string value) => System.Text.Json.JsonSerializer.Serialize(value, Plain);
}
