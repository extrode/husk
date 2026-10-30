namespace Extrode.Husk.Deploy;

/// <summary>Deploys a static output folder as a Workers static assets script. The API token comes
/// from <see cref="TokenVariable"/> only, and is never printed.</summary>
internal static class Program
{
    public const string TokenVariable = "CLOUDFLARE_API_TOKEN";

    private const string Usage =
        "usage: Extrode.Husk.Deploy --dir <folder> --script <name> --account <id> --compatibility-date <yyyy-mm-dd> [--dry-run]\n" +
        "  The API token is read from " + TokenVariable + ".";

    private static readonly string[] Options = ["--dir", "--script", "--account", "--compatibility-date"];

    private static async Task<int> Main(string[] args)
    {
        using var http = new HttpClient();
        return await Run(args, Environment.GetEnvironmentVariable(TokenVariable), Console.Out, Console.Error, http, CloudflareApi.BaseUrl);
    }

    /// <summary>The CLI. <paramref name="allowInsecureBaseUrl"/> is for tests only; <see cref="Main"/>
    /// never sets it.</summary>
    internal static async Task<int> Run(string[] args, string? token, TextWriter stdout, TextWriter stderr,
        HttpClient http, Uri baseUrl, bool allowInsecureBaseUrl = false)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        bool dryRun = false;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--dry-run")
                dryRun = true;
            else if (Options.Contains(args[i]) && i + 1 < args.Length)
                values[args[i]] = args[++i];
            else
            {
                // Only an option name is echoed: a stray value could be a pasted token.
                stderr.WriteLine(args[i].StartsWith("--", StringComparison.Ordinal)
                    ? $"Unknown or incomplete option {args[i]}."
                    : $"Unexpected argument at position {i + 1}.");
                stderr.WriteLine(Usage);
                return 2;
            }
        }
        var missing = Options.Where(o => !values.ContainsKey(o)).ToList();
        if (missing.Count > 0)
        {
            stderr.WriteLine($"Missing {string.Join(", ", missing)}.");
            stderr.WriteLine(Usage);
            return 2;
        }
        string dir = values["--dir"], script = values["--script"];
        if (!Directory.Exists(dir))
        {
            stderr.WriteLine($"No folder at {dir}.");
            return 2;
        }

        var files = AssetManifest.Read(dir);
        if (dryRun)
        {
            foreach (var file in files)
                stdout.WriteLine($"{file.Size,12}  {file.Path}");
            stdout.WriteLine($"{files.Count} files, {files.Sum(f => f.Size)} bytes. Dry run: nothing sent.");
            return 0;
        }
        if (string.IsNullOrEmpty(token))
        {
            stderr.WriteLine($"{TokenVariable} is not set.");
            return 2;
        }

        try
        {
            var deployer = new Deployer(http, baseUrl, token, allowInsecureBaseUrl);
            var result = await deployer.DeployAsync(values["--account"], script, values["--compatibility-date"], files);
            stdout.WriteLine($"Deployed {script}: {files.Count} files, {result.Uploaded} uploaded, {result.Skipped} unchanged.");
            return 0;
        }
        catch (Exception e) when (e is DeployException or HttpRequestException or InvalidOperationException)
        {
            stderr.WriteLine(e.Message);
            return 1;
        }
    }
}
