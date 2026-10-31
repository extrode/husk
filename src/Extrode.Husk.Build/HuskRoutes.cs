using System.Text.Json;
using Extrode.Husk.Compiler;
using Microsoft.Build.Framework;

namespace Extrode.Husk.Build;

/// <summary>Reads the routes folder and writes what the browser router needs into
/// <see cref="OutputDirectory"/>/app/: <c>routes.js</c>, the router's own plain JavaScript (copied from the
/// package), and the shell page's template (<c>shell/index.html</c>, from the project's <c>app.html</c>).
/// Runs once Pare's outputs are known, since a route file's import is its served URL. A problem in the
/// routes (a clash, a feature Husk lacks) fails the build and writes nothing.</summary>
public sealed class HuskRoutes : Microsoft.Build.Utilities.Task
{
    public const string AppFolder = "app";
    public const string ShellTemplateName = "shell/index.html";

    /// <summary>The routes folder, <c>src/routes</c>.</summary>
    [Required] public string RoutesRoot { get; set; } = "";

    /// <summary><c>@(PareOutput)</c>: PareSource, PareRelativePath.</summary>
    public ITaskItem[] Outputs { get; set; } = [];

    public string BasePath { get; set; } = "/";

    [Required] public string ServedRoot { get; set; } = "";

    /// <summary>The package's <c>runtime/</c> folder.</summary>
    [Required] public string RuntimeDirectory { get; set; } = "";

    [Required] public string OutputDirectory { get; set; } = "";

    /// <summary>The app's HTML shell; empty for a plain one.</summary>
    public string AppHtml { get; set; } = "";

    /// <summary>The client hooks file; empty or missing for none.</summary>
    public string Hooks { get; set; } = "";

    public bool Dev { get; set; }

    /// <summary>The project's <c>svelte.config.js</c>, scanned for <c>kit</c> options Husk lacks; empty or missing for none.</summary>
    public string SvelteConfig { get; set; } = "";

    /// <summary><c>@(HuskEnv)</c>: Include is the name, Value its value.</summary>
    public ITaskItem[] Env { get; set; } = [];

    /// <summary>The files to serve below <c>_husk/</c> (RelativePath) from <see cref="OutputDirectory"/>/app/.</summary>
    [Output] public ITaskItem[] AppFiles { get; set; } = [];

    [Output] public string ShellTemplate { get; set; } = "";

    public override bool Execute()
    {
        if (!Directory.Exists(RoutesRoot))
        {
            Log.LogError(null, "HUSK306", null, RoutesRoot, 0, 0, 0, 0, "the routes folder does not exist");
            return false;
        }
        string root = HuskAssets.Url(BasePath);
        var urls = HuskAssets.SourceUrls(Outputs, ServedRoot, root);

        var files = Directory.EnumerateFiles(RoutesRoot, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(RoutesRoot, f).Replace('\\', '/')).ToList();
        var table = RouteTable.Build(files);
        foreach (var d in table.Diagnostics)
        {
            string file = Path.Combine(RoutesRoot, d.File);
            if (d.Code == "HUSK300")
                Log.LogError(null, "HUSK300", null, file, 1, 1, 0, 0, d.Message);
            else
                Log.LogError(null, "HUSK301", null, file, 1, 1, 0, 0, d.Message);
        }
        if (!table.Ok)
            return false;
        if (!CheckFeatures())
            return false;

        bool missing = false;
        string UrlOf(string file)
        {
            string full = Path.GetFullPath(Path.Combine(RoutesRoot, file));
            if (urls.TryGetValue(full, out var url))
                return url;
            missing = true;
            Log.LogError(null, "HUSK304", null, full, 1, 1, 0, 0, "a route file the build does not compile or serve: add it to HuskComponent, HuskModule or TypeScriptSource items");
            return "";
        }

        string? hooksUrl = null;
        if (Hooks.Length > 0 && File.Exists(Hooks))
        {
            if (urls.TryGetValue(Path.GetFullPath(Hooks), out var found))
                hooksUrl = found;
            else
            {
                missing = true;
                Log.LogError(null, "HUSK304", null, Hooks, 1, 1, 0, 0, "the client hooks file is not compiled or served: add it to TypeScriptSource or HuskModule items");
            }
        }

        string routesJs = RoutesModule.Write(table, UrlOf, hooksUrl, Dev, "#" + AppShell.MountId);
        if (missing)
            return false;

        string template;
        try
        {
            template = AppHtml.Length > 0 ? File.ReadAllText(AppHtml) : AppShell.Default;
        }
        catch (IOException e)
        {
            Log.LogError(null, "HUSK305", null, AppHtml, 0, 0, 0, 0, "cannot read the app shell: " + e.Message);
            return false;
        }
        var env = Env.ToDictionary(e => e.ItemSpec, e => e.GetMetadata("Value"), StringComparer.Ordinal);
        string shell;
        try
        {
            shell = AppShell.Build(template, root, Dev, env);
        }
        catch (FormatException e)
        {
            Log.LogError(null, "HUSK305", null, AppHtml.Length > 0 ? AppHtml : "app.html", 1, 1, 0, 0, e.Message);
            return false;
        }

        try
        {
            RewriteEnv();
            string app = Path.Combine(OutputDirectory, AppFolder);
            var written = new List<ITaskItem>();
            Directory.CreateDirectory(app);
            Place(written, app, "routes.js", routesJs);
            foreach (string file in Directory.EnumerateFiles(RuntimeDirectory, "*.js", SearchOption.AllDirectories)
                         .Where(f => !f.EndsWith(".svelte.js", StringComparison.Ordinal)).Order(StringComparer.Ordinal))
                Place(written, app, Path.GetRelativePath(RuntimeDirectory, file).Replace('\\', '/'), File.ReadAllText(file));
            string shellPath = Path.Combine(OutputDirectory, AppFolder, ShellTemplateName);
            Directory.CreateDirectory(Path.GetDirectoryName(shellPath)!);
            HuskAssets.WriteIfChanged(shellPath, shell);
            AppFiles = [.. written];
            ShellTemplate = shellPath;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.LogError(null, "HUSK102", null, OutputDirectory, 0, 0, 0, 0, "cannot write the router files: " + e.Message);
        }
        return !Log.HasLoggedErrors;
    }

    // HUSK302 and HUSK303: the whole app is read before anything is written, so a failure leaves no partial table.
    private bool CheckFeatures()
    {
        string runtime = Path.GetFullPath(RuntimeDirectory);
        foreach (string source in Outputs.Select(o => o.GetMetadata("PareSource")).Where(f => f.Length > 0).Select(Path.GetFullPath)
                     .Where(f => f.EndsWith(".svelte", StringComparison.Ordinal) || f.EndsWith(".ts", StringComparison.Ordinal) || f.EndsWith(".js", StringComparison.Ordinal))
                     .Where(f => !f.StartsWith(runtime, StringComparison.Ordinal)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            if (!File.Exists(source))
                continue;
            foreach (var finding in KitFeatures.Scan(File.ReadAllText(source)))
                Log.LogError(null, "HUSK302", null, source, finding.Line, 1, 0, 0, finding.Message + ": Husk's router covers goto, beforeNavigate, afterNavigate, preloadCode, page, browser, dev, building, version, redirect, error, isRedirect and isHttpError");
        }
        if (SvelteConfig.Length > 0 && File.Exists(SvelteConfig))
            foreach (var finding in KitFeatures.ScanConfig(File.ReadAllText(SvelteConfig)))
                Log.LogError(null, "HUSK303", null, SvelteConfig, finding.Line, 1, 0, 0, finding.Message);
        return !Log.HasLoggedErrors;
    }

    // import.meta.env has no browser meaning; the page defines globalThis.__huskEnv in its place. In place, and
    // only when the text changes, so an unchanged build touches nothing.
    private void RewriteEnv()
    {
        string runtime = Path.GetFullPath(RuntimeDirectory);
        foreach (var output in Outputs.Where(o => o.ItemSpec.EndsWith(".js", StringComparison.Ordinal) && !o.GetMetadata("PareSource").StartsWith(runtime, StringComparison.Ordinal)))
        {
            string path = output.GetMetadata("FullPath");
            if (File.Exists(path))
                HuskAssets.WriteIfChanged(path, KitFeatures.RewriteEnv(File.ReadAllText(path)));
        }
    }

    private static void Place(List<ITaskItem> written, string app, string relative, string text)
    {
        string path = Path.Combine(app, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        HuskAssets.WriteIfChanged(path, text);
        written.Add(new Microsoft.Build.Utilities.TaskItem(path, new Dictionary<string, string> { ["RelativePath"] = "_husk/" + relative }));
    }
}
