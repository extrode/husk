namespace Extrode.Husk.Compiler;

/// <summary>A source to compile. <see cref="Filename"/> is the path below the project with <c>/</c>
/// separators: the CSS scope hash is computed from it, so it must read the same on every OS.</summary>
public readonly record struct CompileInput(string Filename, string Source);

/// <summary>A compiler warning or error. <see cref="Line"/> is 1-based and <see cref="Column"/>
/// 0-based, as Svelte reports them; <see cref="Code"/> is Svelte's code, or <c>PARE&lt;n&gt;</c> when
/// Pare could not strip a <c>.svelte.ts</c>.</summary>
public sealed record CompileDiagnostic(string Code, string Message, int Line, int Column);

/// <summary>What one compile produced: the JS, the CSS (<c>""</c> when the component has none, and
/// for modules) and the JS source map, or an <see cref="Error"/> and no output.</summary>
public sealed record CompileResult(string? Js, string? Css, string? Map, IReadOnlyList<CompileDiagnostic> Warnings, CompileDiagnostic? Error)
{
    public bool Succeeded => Error is null;
}
