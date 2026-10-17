using Extrode.Pare;

namespace Extrode.Husk.Compiler;

/// <summary>The pinned Svelte compiler in a pool of V8 engines. <c>.svelte</c> goes through
/// <c>compile</c>; <c>.svelte.js</c> and <c>.svelte.ts</c> through <c>compileModule</c>, a
/// <c>.svelte.ts</c> after Pare strips its types (which keeps every line and column, so positions
/// and the map hold) with the original TypeScript as the map's <c>sourcesContent</c>.</summary>
public sealed class SvelteCompiler : IDisposable
{
    internal EnginePool Pool { get; }

    public SvelteCompiler(string compilerPath)
        : this(compilerPath, TimeSpan.FromMinutes(10))
    {
    }

    internal SvelteCompiler(string compilerPath, TimeSpan idleTimeout, Func<long>? now = null) =>
        Pool = new EnginePool(compilerPath, idleTimeout, now);

    /// <summary>The <c>filename</c> Svelte is given: the path below the project, with <c>/</c>.</summary>
    public static string Filename(string projectDirectory, string path) =>
        Path.GetRelativePath(projectDirectory, path).Replace('\\', '/');

    public CompileResult Compile(CompileInput input, bool dev)
    {
        var engine = Pool.Rent();
        CompileResult result;
        try
        {
            result = Compile(engine, input, dev);
        }
        catch
        {
            Pool.Discard(engine);
            throw;
        }
        Pool.Return(engine);
        return result;
    }

    private static CompileResult Compile(Engine engine, CompileInput input, bool dev)
    {
        if (input.Filename.EndsWith(".svelte", StringComparison.Ordinal))
            return engine.Compile(module: false, input.Source, input.Filename, dev, null);
        if (input.Filename.EndsWith(".svelte.js", StringComparison.Ordinal))
            return engine.Compile(module: true, input.Source, input.Filename, dev, null);
        if (!input.Filename.EndsWith(".svelte.ts", StringComparison.Ordinal))
            return new CompileResult(null, null, null, [],
                new CompileDiagnostic("unsupported_source", "Husk compiles .svelte, .svelte.js and .svelte.ts files only", 1, 0));

        string? stripped = TypeStripper.Strip(input.Source, out var diagnostics);
        if (stripped is null)
        {
            if (diagnostics.Count == 0)
                return new CompileResult(null, null, null, [], new CompileDiagnostic("PARE001", "Pare could not strip the TypeScript", 1, 0));
            var first = diagnostics[0];
            var at = SourceLocation.FromOffset(input.Source, first.Start);
            var error = new CompileDiagnostic($"PARE{(int)first.Code:000}", DiagnosticMessages.Message(first.Code), at.Line, at.Column - 1);
            return new CompileResult(null, null, null, [], error);
        }
        return engine.Compile(module: true, stripped, input.Filename, dev, input.Source);
    }

    /// <summary>Compiles every input, in parallel when there are enough: one engine per worker, up to
    /// <c>min(cores, ceil(n / 8))</c>. Results are in input order and equal a sequential run's.</summary>
    public IReadOnlyList<CompileResult> CompileAll(IReadOnlyList<CompileInput> inputs, bool dev)
    {
        var results = new CompileResult[inputs.Count];
        int workers = Workers(inputs.Count, Environment.ProcessorCount);
        if (workers <= 1)
        {
            for (int i = 0; i < inputs.Count; i++)
                results[i] = Compile(inputs[i], dev);
            return results;
        }
        // Striped, so each worker's engine compiles a share of every size of input.
        var tasks = Enumerable.Range(0, workers).Select(w => Task.Run(() =>
        {
            var engine = Pool.Rent();
            try
            {
                for (int i = w; i < inputs.Count; i += workers)
                    results[i] = Compile(engine, inputs[i], dev);
            }
            catch
            {
                Pool.Discard(engine);
                throw;
            }
            Pool.Return(engine);
        })).ToArray();
        Task.WaitAll(tasks);
        return results;
    }

    internal static int Workers(int sources, int cores) => Math.Max(1, Math.Min(cores, (sources + 7) / 8));

    public void Dispose() => Pool.Dispose();
}
