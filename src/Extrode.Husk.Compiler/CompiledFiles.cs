namespace Extrode.Husk.Compiler;

/// <summary>What a compiled source writes, by output extension, as the build names them
/// (Pare's rule, <c>%(Filename)</c> + extension: <c>Styled.svelte</c> to <c>Styled.svelte.js</c>,
/// <c>counter.svelte.ts</c> to <c>counter.svelte.js</c>). The build and a save in Development write
/// the same bytes.</summary>
public static class CompiledFiles
{
    private static readonly string[] Component = [".svelte.js", ".svelte.css", ".svelte.js.map"];
    private static readonly string[] Module = [".js", ".js.map"];

    public static bool IsComponent(string sourcePath) => sourcePath.EndsWith(".svelte", StringComparison.Ordinal);

    public static IReadOnlyList<string> Extensions(string sourcePath) => IsComponent(sourcePath) ? Component : Module;

    /// <summary>The output's file name for one of <see cref="Extensions"/>.</summary>
    public static string Name(string sourcePath, string extension) => Path.GetFileNameWithoutExtension(sourcePath) + extension;

    /// <summary>Each output's text by extension, for a result that succeeded.</summary>
    public static Dictionary<string, string> Texts(string sourcePath, CompileResult result)
    {
        bool component = IsComponent(sourcePath);
        string js = component ? ".svelte.js" : ".js";
        var texts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [js] = WithMapUrl(result.Js!, Name(sourcePath, js + ".map")),
            [js + ".map"] = result.Map!,
        };
        if (component)
            texts[".svelte.css"] = result.Css!;
        return texts;
    }

    /// <summary>The compiled JS with a <c>sourceMappingURL</c> comment naming the map beside it.</summary>
    public static string WithMapUrl(string js, string mapFileName) =>
        js + (js.EndsWith('\n') ? "" : "\n") + "//# sourceMappingURL=" + mapFileName + "\n";
}
