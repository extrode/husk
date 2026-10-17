using System.Diagnostics;
using System.Text.Json;
using Extrode.Pare.AspNetCore;
using Microsoft.Extensions.Logging;

namespace Extrode.Husk.AspNetCore;

/// <summary>Compiles a saved <c>.svelte</c>, <c>.svelte.ts</c> or <c>.svelte.js</c> in the running
/// Development app, with the engines kept warm between saves. A failed compile is logged as HUSK100
/// and returns null, so Pare keeps the last good outputs; it never throws to Pare.</summary>
internal sealed class HuskCompiler(string mapPath) : IPareCompiler
{
    private readonly Lock _gate = new();
    private DateTime _mapStamp;
    private HuskDevelopmentMap? _map;
    private CompilerBridge? _bridge;
    private string? _bridgePath;

    public string Name => "husk";

    public Task<IReadOnlyDictionary<string, string>?> CompileAsync(string sourcePath, string source, IReadOnlyList<string> outputPaths,
        ILogger logger, CancellationToken cancellationToken) =>
        Task.FromResult(Compile(sourcePath, source, outputPaths, logger));

    private IReadOnlyDictionary<string, string>? Compile(string sourcePath, string source, IReadOnlyList<string> outputPaths, ILogger logger)
    {
        if (Load(logger) is not var (map, bridge))
            return null;
        string file = Path.GetRelativePath(map.ProjectDirectory, sourcePath);
        var sw = Stopwatch.StartNew();
        try
        {
            using var result = JsonDocument.Parse(bridge.Compile(map.Svelte, map.ProjectDirectory, sourcePath, source, map.Dev));
            return Read(result.RootElement, file, outputPaths, logger, sw);
        }
        catch (Exception e)
        {
            logger.LogError(e, "{File}: error HUSK100: the compiler failed", file);
            return null;
        }
    }

    private static Dictionary<string, string>? Read(JsonElement root, string file, IReadOnlyList<string> outputPaths, ILogger logger, Stopwatch sw)
    {
        foreach (var warning in root.GetProperty("warnings").EnumerateArray())
            logger.LogWarning("{File}({Line},{Column}): warning HUSK200: {Code}: {Message}", file, warning.GetProperty("line").GetInt32(),
                warning.GetProperty("column").GetInt32() + 1, warning.GetProperty("code").GetString(), OneLine(warning));
        if (root.GetProperty("error") is { ValueKind: JsonValueKind.Object } error)
        {
            logger.LogError("{File}({Line},{Column}): error HUSK100: {Code}: {Message}", file, error.GetProperty("line").GetInt32(),
                error.GetProperty("column").GetInt32() + 1, error.GetProperty("code").GetString(), OneLine(error));
            return null;
        }
        var outputs = root.GetProperty("outputs");
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in outputPaths)
        {
            if (!outputs.TryGetProperty(Path.GetFileName(path), out var text))
            {
                logger.LogError("{File}: error HUSK101: the build gave an output {Output} the compiler does not write", file, path);
                return null;
            }
            texts[path] = text.GetString()!;
        }
        logger.LogInformation("Husk compiled {File} in {Milliseconds} ms", file, sw.ElapsedMilliseconds);
        return texts;
    }

    // The map is read again whenever a build rewrites it, so a build while the app runs is picked up.
    // The compiler stays loaded: a load context cannot drop V8, so a new compiler path needs a restart.
    private (HuskDevelopmentMap, CompilerBridge)? Load(ILogger logger)
    {
        lock (_gate)
        {
            var stamp = File.GetLastWriteTimeUtc(mapPath);
            if (stamp != _mapStamp)
            {
                _mapStamp = stamp;
                _map = null;
                try
                {
                    _map = HuskDevelopmentMap.Read(mapPath);
                    if (_map is null)
                        logger.LogError("{Path} is missing; build the project again to compile on save", mapPath);
                    else if (_bridge is null)
                        _bridge = CompilerBridge.Load(_map.Compiler, logger);
                    else if (_bridgePath != _map.Compiler)
                        logger.LogWarning("Husk's compiler moved to {Path}; restart the app to compile saves with it", _map.Compiler);
                    if (_bridge is not null && _map is not null)
                        _bridgePath ??= _map.Compiler;
                }
                catch (Exception e)
                {
                    _map = null;
                    logger.LogError(e, "Husk could not load its compiler from {Path}; build the project again to compile on save", mapPath);
                }
            }
            else if (_map is null || _bridge is null)
                logger.LogError("Husk is not compiling saves (see the error above); build the project again to compile on save");
            return _map is not null && _bridge is not null ? (_map, _bridge) : null;
        }
    }

    private static string OneLine(JsonElement diagnostic) => diagnostic.GetProperty("message").GetString()!.ReplaceLineEndings(" ");
}
