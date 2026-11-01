using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Extrode.Husk.AspNetCore;

/// <summary>
/// In Development, a router app's route folder: a route file added, removed or renamed builds the
/// project again, which rewrites routes.js and the page, so <c>dotnet watch</c> reloads the browser.
/// <c>dotnet watch</c> itself never sees a file added after it starts, and Pare compiles on save only
/// the sources it was started with. A file the build writes after the app started has no static
/// asset endpoint, so a 404 for one is answered from Pare's output folder.
/// </summary>
internal sealed class HuskRouteWatcher(string mapPath, ILogger logger,
    Func<HuskDevelopmentMap, CancellationToken, Task<(bool Ok, string Output)>> build) : IHostedService, IStartupFilter, IDisposable
{
    internal static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(300);

    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private readonly Lock _gate = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly HashSet<string> _known = new(StringComparer.FromComparison(PathComparison));
    private HuskDevelopmentMap? _map;
    private FileSystemWatcher? _watcher;
    private bool _pending;
    private bool _active;
    private Task _running = Task.CompletedTask;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _map = HuskDevelopmentMap.Read(mapPath);
        }
        catch (Exception e) when (e is IOException or System.Text.Json.JsonException or KeyNotFoundException or InvalidOperationException)
        {
            logger.LogWarning("Husk could not read {Path}, so added route files need a rebuild: {Message}", mapPath, e.Message);
        }
        if (_map is not { Routing: true } || _map.RoutesRoot.Length == 0 || _map.Project.Length == 0 || !Directory.Exists(_map.RoutesRoot))
            return Task.CompletedTask;
        // The files Pare compiles on save: a save of one of them is Pare's, anything else is a build.
        foreach (string file in Directory.EnumerateFiles(_map.RoutesRoot, "*", SearchOption.AllDirectories))
            _known.Add(file);
        _watcher = new FileSystemWatcher(_map.RoutesRoot) { IncludeSubdirectories = true };
        _watcher.Created += (_, e) => Changed(e.FullPath, removed: false);
        _watcher.Changed += (_, e) => Changed(e.FullPath, removed: false);
        _watcher.Deleted += (_, e) => Changed(e.FullPath, removed: true);
        _watcher.Renamed += (_, e) =>
        {
            Changed(e.OldFullPath, removed: true);
            Changed(e.FullPath, removed: false);
        };
        _watcher.EnableRaisingEvents = true;
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_watcher is not null)
            _watcher.EnableRaisingEvents = false;
        await _stop.CancelAsync();
        try
        {
            await _running.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _stop.Dispose();
    }

    private void Changed(string path, bool removed)
    {
        string name = Path.GetFileName(path);
        // Editors' swap and backup files.
        if (name.StartsWith('.') || name.EndsWith('~'))
            return;
        lock (_gate)
        {
            if (removed)
            {
                // A removed folder takes its files with it; one put back is not Pare's any more.
                string folder = path + Path.DirectorySeparatorChar;
                _known.RemoveWhere(f => string.Equals(f, path, PathComparison) || f.StartsWith(folder, PathComparison));
            }
            else if (_known.Contains(path) || Directory.Exists(path) && !IsNewFolder(path))
                return;
            _pending = true;
            if (!_active)
            {
                _active = true;
                _running = Task.Run(Build);
            }
        }
    }

    // A folder only raises Changed for what happens inside it; Created is a new folder.
    private bool IsNewFolder(string path) =>
        !Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).All(_known.Contains);

    private async Task Build()
    {
        var map = _map!;
        while (true)
        {
            try
            {
                await Task.Delay(Settle, _stop.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            lock (_gate)
            {
                if (!_pending)
                {
                    _active = false;
                    return;
                }
                _pending = false;
            }
            var clock = Stopwatch.StartNew();
            try
            {
                var (ok, output) = await build(map, _stop.Token);
                if (ok)
                {
                    logger.LogInformation("Husk built the routes again in {Elapsed} ms", clock.ElapsedMilliseconds);
                    await Task.Delay(Settle, _stop.Token);
                    Touch(map);
                }
                else
                    logger.LogError("Husk could not build the routes again:\n{Output}", output);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
            {
                logger.LogError("Husk could not build the routes again: {Message}", e.Message);
            }
        }
    }

    // dotnet watch refreshes the browser at the first file a build writes, which can be routes.js before
    // the import map and the shell are: touching them once the build is over gives it a last refresh.
    internal static void Touch(HuskDevelopmentMap map)
    {
        if (map.ImportMap.Length == 0 || Path.GetDirectoryName(map.ImportMap) is not { } folder)
            return;
        foreach (string file in new[] { map.ImportMap, Path.Combine(folder, "app", "index.html") })
            if (File.Exists(file))
                File.SetLastWriteTimeUtc(file, DateTime.UtcNow);
    }

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        var types = new FileExtensionContentTypeProvider();
        app.Use(async (context, nextMiddleware) =>
        {
            await nextMiddleware();
            var map = _map;
            var request = context.Request;
            if (map is null || map.ServedRoot.Length == 0 || context.Response.StatusCode != StatusCodes.Status404NotFound
                || context.Response.HasStarted || !(HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method))
                || !request.Path.StartsWithSegments(map.BasePath.TrimEnd('/'), StringComparison.Ordinal, out var rest)
                || !Directory.Exists(map.ServedRoot))
                return;
            using var files = new PhysicalFileProvider(map.ServedRoot);
            var file = files.GetFileInfo(rest.Value ?? "");
            if (!file.Exists || file.IsDirectory)
                return;
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = types.TryGetContentType(file.Name, out string? type) ? type : "application/octet-stream";
            context.Response.ContentLength = file.Length;
            context.Response.Headers.CacheControl = "no-cache";
            if (HttpMethods.IsGet(request.Method))
                await context.Response.SendFileAsync(file, context.RequestAborted);
        });
        next(app);
    };

    /// <summary>The project built as it was built for the app, in a child process with none of the
    /// running app's MSBuild or <c>dotnet watch</c> settings.</summary>
    internal static async Task<(bool Ok, string Output)> Run(HuskDevelopmentMap map, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } host ? host : "dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = map.ProjectDirectory,
        };
        string[] args = ["build", map.Project, "--no-restore", "-nologo", "-v:q", "-clp:NoSummary"];
        foreach (string arg in args)
            start.ArgumentList.Add(arg);
        if (map.Configuration.Length > 0)
        {
            start.ArgumentList.Add("-c");
            start.ArgumentList.Add(map.Configuration);
        }
        if (map.TargetFramework.Length > 0)
        {
            start.ArgumentList.Add("-f");
            start.ArgumentList.Add(map.TargetFramework);
        }
        foreach (string key in start.Environment.Keys.ToList())
            if (key.StartsWith("MSBuild", StringComparison.OrdinalIgnoreCase) || key.StartsWith("DOTNET_WATCH", StringComparison.OrdinalIgnoreCase)
                || key is "DOTNET_STARTUP_HOOKS" or "DOTNET_MODIFIABLE_ASSEMBLIES")
                start.Environment.Remove(key);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }
        return (process.ExitCode == 0, (await output + await error).Trim());
    }
}
