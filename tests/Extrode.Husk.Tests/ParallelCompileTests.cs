using System.Diagnostics;
using Extrode.Husk.Compiler;

namespace Extrode.Husk.Tests;

public sealed class ParallelCompileTests
{
    // 201, the size of the first app's component set (decision 004).
    private static readonly CompileInput[] Synthetic = Enumerable.Range(0, 201).Select(i => new CompileInput(
        $"wwwroot/c{i:000}.svelte",
        $$"""
        <script>
          let n = $state({{i}});
          let items = $derived(Array.from({ length: n % 7 }, (_, k) => k * {{i}}));
        </script>

        <button onclick={() => n++}>{n}</button>
        {#each items as item (item)}<span class="i{{i}}">{item}</span>{/each}

        <style>
          .i{{i}} { margin: {{i % 5}}px; }
        </style>
        """)).ToArray();

    [Theory]
    [InlineData(1, 1)]
    [InlineData(8, 1)]
    [InlineData(9, 2)]
    [InlineData(201, 10)]
    [InlineData(10_000, 10)]
    public void Workers_are_one_per_eight_sources_up_to_the_cores(int sources, int workers) =>
        Assert.Equal(workers, SvelteCompiler.Workers(sources, cores: 10));

    [Fact]
    public void A_parallel_run_equals_the_sequential_one_and_uses_every_engine()
    {
        using var sequential = new SvelteCompiler(ExpectedRow.CompilerPath);
        using var parallel = new SvelteCompiler(ExpectedRow.CompilerPath);
        var sw = Stopwatch.StartNew();
        var one = Synthetic.Select(s => sequential.Compile(s, dev: false)).ToList();
        var sequentialTime = sw.Elapsed;

        sw.Restart();
        var all = parallel.CompileAll(Synthetic, dev: false);
        var parallelTime = sw.Elapsed;

        Assert.Equal(Synthetic.Length, all.Count);
        for (int i = 0; i < Synthetic.Length; i++)
        {
            Assert.True(all[i].Succeeded, all[i].Error?.Message);
            Assert.Equal((one[i].Js, one[i].Css, one[i].Map), (all[i].Js, all[i].Css, all[i].Map));
        }
        int workers = SvelteCompiler.Workers(Synthetic.Length, Environment.ProcessorCount);
        Assert.InRange(parallel.Pool.Started, Math.Min(2, workers), workers);
        Assert.All(parallel.Pool.Engines, e => Assert.True(e.Compiles > 0));
        Assert.Equal(Synthetic.Length, parallel.Pool.Engines.Sum(e => e.Compiles));
        TestContext.Current.SendDiagnosticMessage(
            $"201 components: one engine {sequentialTime.TotalMilliseconds:F0} ms, {workers} engines {parallelTime.TotalMilliseconds:F0} ms (engine starts included)");
    }
}
