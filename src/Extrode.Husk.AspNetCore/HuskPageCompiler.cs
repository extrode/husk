using System.Text.RegularExpressions;
using Extrode.Pare.AspNetCore;
using Microsoft.Extensions.Logging;

namespace Extrode.Husk.AspNetCore;

/// <summary>Writes a saved page as the build does: with the current import map inlined when it holds
/// the marker, unchanged otherwise, and with its UTF-8 BOM if it had one.</summary>
internal sealed class HuskPageCompiler(HuskSession session) : IPareCompiler
{
    public string Name => "husk-page";

    public Task<IReadOnlyDictionary<string, string>?> CompileAsync(string sourcePath, string source, IReadOnlyList<string> outputPaths,
        ILogger logger, CancellationToken cancellationToken) =>
        Task.FromResult(Compile(sourcePath, source, outputPaths, logger));

    private IReadOnlyDictionary<string, string>? Compile(string sourcePath, string source, IReadOnlyList<string> outputPaths, ILogger logger)
    {
        if (session.Load(logger) is not var (map, bridge))
            return null;
        try
        {
            string page = bridge.Page(source, File.ReadAllText(map.ImportMap)) ?? source;
            if (StartsWithBom(sourcePath) && !page.StartsWith('\uFEFF'))
                page = "\uFEFF" + page;
            return outputPaths.ToDictionary(p => p, _ => page);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or RegexMatchTimeoutException)
        {
            logger.LogError("{File}: error HUSK102: cannot write the page: {Message}", Path.GetRelativePath(map.ProjectDirectory, sourcePath), e.Message);
            return null;
        }
    }

    private static bool StartsWithBom(string path)
    {
        Span<byte> head = stackalloc byte[3];
        using var file = File.OpenRead(path);
        return file.ReadAtLeast(head, 3, throwOnEndOfStream: false) == 3 && head is [0xEF, 0xBB, 0xBF];
    }
}
