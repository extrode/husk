using Extrode.Husk.Compiler;

namespace Extrode.Husk.Tests;

public sealed class EnginePoolTests
{
    private static readonly CompileInput Plain = ExpectedRow.Find("wwwroot/Plain.svelte", false).Input;

    [Fact]
    public void A_second_compile_reuses_the_warm_engine()
    {
        using var compiler = new SvelteCompiler(ExpectedRow.CompilerPath);

        compiler.Compile(Plain, dev: false);
        compiler.Compile(Plain, dev: true);

        Assert.Equal(1, compiler.Pool.Started);
        Assert.Equal(2, compiler.Pool.Engines.Single().Compiles);
    }

    [Fact]
    public void An_engine_idle_for_the_timeout_is_released_and_the_next_compile_starts_one()
    {
        long now = 0;
        using var compiler = new SvelteCompiler(ExpectedRow.CompilerPath, TimeSpan.FromMinutes(10), () => now);
        compiler.Compile(Plain, dev: false);

        now += (long)TimeSpan.FromMinutes(9).TotalMilliseconds;
        Assert.Equal(0, compiler.Pool.ReleaseIdle());
        Assert.Equal(1, compiler.Pool.Idle);

        now += (long)TimeSpan.FromMinutes(1).TotalMilliseconds;
        Assert.Equal(1, compiler.Pool.ReleaseIdle());
        Assert.Equal(0, compiler.Pool.Idle);
        Assert.Empty(compiler.Pool.Engines);

        Assert.True(compiler.Compile(Plain, dev: false).Succeeded);
        Assert.Equal(2, compiler.Pool.Started);
    }

    [Fact]
    public void An_engine_survives_a_failed_compile()
    {
        using var compiler = new SvelteCompiler(ExpectedRow.CompilerPath);

        Assert.False(compiler.Compile(ExpectedRow.Find("wwwroot/Broken.svelte", false).Input, dev: false).Succeeded);
        Assert.True(compiler.Compile(Plain, dev: false).Succeeded);
        Assert.Equal(1, compiler.Pool.Started);
    }

    [Fact]
    public void An_engine_that_fails_to_start_is_not_counted_or_kept()
    {
        string bad = Path.Combine(AppContext.BaseDirectory, "bad-compiler.js");
        File.WriteAllText(bad, "this is not javascript (");
        using var compiler = new SvelteCompiler(bad);

        Assert.ThrowsAny<Exception>(() => compiler.Compile(Plain, dev: false));

        Assert.Equal(0, compiler.Pool.Started);
        Assert.Empty(compiler.Pool.Engines);
    }

    [Fact]
    public void A_non_positive_idle_timeout_is_refused() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new SvelteCompiler(ExpectedRow.CompilerPath, TimeSpan.Zero));

    [Fact]
    public void A_disposed_pool_refuses_to_rent()
    {
        var compiler = new SvelteCompiler(ExpectedRow.CompilerPath);
        compiler.Dispose();

        Assert.Throws<ObjectDisposedException>(() => compiler.Compile(Plain, dev: false));
    }
}
