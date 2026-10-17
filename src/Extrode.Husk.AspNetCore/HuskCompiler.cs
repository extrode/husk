using System.Diagnostics;
using System.Text.Json;
using Extrode.Pare.AspNetCore;
using Microsoft.Extensions.Logging;

namespace Extrode.Husk.AspNetCore;

/// <summary>Compiles a saved <c>.svelte</c>, <c>.svelte.ts</c> or <c>.svelte.js</c> in the running
/// Development app, with the engines kept warm between saves. A failed compile is logged as HUSK100
/// and returns null, so Pare keeps the last good outputs; it never throws to Pare.</summary>
internal sealed class HuskCompiler(HuskSession session) : IPareCompiler
{

    public string Name => "husk";

    public Task<IReadOnlyDictionary<string, string>?> CompileAsync(string sourcePath, string source, IReadOnlyList<string> outputPaths,
        ILogger logger, CancellationToken cancellationToken) =>
        Task.FromResult(Compile(sourcePath, source, outputPaths, logger));

    private IReadOnlyDictionary<string, string>? Compile(string sourcePath, string source, IReadOnlyList<string> outputPaths, ILogger logger)
    {
        if (session.Load(logger) is not var (map, bridge))
            return null;
        string file = Path.GetRelativePath(map.ProjectDirectory, sourcePath);
        var sw = Stopwatch.StartNew();
        try
        {
            using var result = JsonDocument.Parse(bridge.Compile(map.Svelte, map.ProjectDirectory, sourcePath, source, map.Dev));
            if (Read(result.RootElement, file, outputPaths, logger, sw) is not { } texts)
                return null;
            WriteStylesheet(map, bridge, texts, logger);
            return texts;
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

    // Pare writes the returned outputs after this returns, so the saved component's CSS is taken
    // from them and every other component's from disk.
    private static void WriteStylesheet(HuskDevelopmentMap map, CompilerBridge bridge, Dictionary<string, string> texts, ILogger logger)
    {
        if (texts.Keys.FirstOrDefault(p => p.EndsWith(".svelte.css", StringComparison.Ordinal)) is not { } saved)
            return;
        try
        {
            var css = map.Styles.Select(s => Path.GetFullPath(s.Css) == Path.GetFullPath(saved) ? texts[saved] : File.Exists(s.Css) ? File.ReadAllText(s.Css) : "");
            string text = bridge.Stylesheet([.. map.Styles.Select(s => s.Name)], [.. css]);
            if (!File.Exists(map.Stylesheet) || File.ReadAllText(map.Stylesheet) != text)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(map.Stylesheet)!);
                File.WriteAllText(map.Stylesheet, text);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            logger.LogError("{Path}: error HUSK102: cannot write the app's stylesheet: {Message}", map.Stylesheet, e.Message);
        }
    }

    private static string OneLine(JsonElement diagnostic) => diagnostic.GetProperty("message").GetString()!.ReplaceLineEndings(" ");
}
