namespace Extrode.Husk.Compiler;

/// <summary>Warm engines, reused while the process lives. An engine idle for longer than the timeout
/// is disposed; the next caller starts a fresh one.</summary>
internal sealed class EnginePool : IDisposable
{
    private readonly string _compilerSource;
    private readonly string _glueSource;
    private readonly TimeSpan _idleTimeout;
    private readonly Func<long> _now;
    private readonly Stack<(Engine Engine, long ReturnedAt)> _idle = new();
    private readonly List<Engine> _all = [];
    private readonly Timer? _timer;
    private bool _disposed;

    public EnginePool(string compilerPath, TimeSpan idleTimeout, Func<long>? now = null)
    {
        _compilerSource = File.ReadAllText(compilerPath);
        using var glue = typeof(EnginePool).Assembly.GetManifestResourceStream("husk-compile.js")!;
        _glueSource = new StreamReader(glue).ReadToEnd();
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(idleTimeout, TimeSpan.Zero);
        _idleTimeout = idleTimeout;
        _now = now ?? (() => Environment.TickCount64);
        if (now is null)
        {
            var period = TimeSpan.FromTicks(Math.Min(idleTimeout.Ticks, TimeSpan.FromMinutes(1).Ticks));
            _timer = new Timer(_ => ReleaseIdle(), null, period, period);
        }
    }

    /// <summary>Engines started over the pool's life.</summary>
    public int Started { get; private set; }

    public int Idle
    {
        get
        {
            lock (_idle)
                return _idle.Count;
        }
    }

    /// <summary>Every engine the pool has started and not released.</summary>
    public IReadOnlyList<Engine> Engines
    {
        get
        {
            lock (_idle)
                return [.. _all];
        }
    }

    public Engine Rent()
    {
        lock (_idle)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_idle.TryPop(out var idle))
                return idle.Engine;
        }
        var engine = new Engine(_compilerSource, _glueSource);
        lock (_idle)
        {
            if (!_disposed)
            {
                Started++;
                _all.Add(engine);
                return engine;
            }
        }
        engine.Dispose();
        throw new ObjectDisposedException(nameof(EnginePool));
    }

    /// <summary>Disposes an engine that threw outside a compile error: it is not trusted again.</summary>
    public void Discard(Engine engine)
    {
        lock (_idle)
            _all.Remove(engine);
        engine.Dispose();
    }

    public void Return(Engine engine)
    {
        lock (_idle)
        {
            if (_disposed)
            {
                _all.Remove(engine);
                engine.Dispose();
                return;
            }
            _idle.Push((engine, _now()));
        }
    }

    /// <summary>Disposes the engines idle for the timeout or longer; returns how many.</summary>
    public int ReleaseIdle()
    {
        List<Engine> released = [];
        lock (_idle)
        {
            long now = _now();
            var kept = _idle.Where(e => now - e.ReturnedAt < (long)_idleTimeout.TotalMilliseconds).Reverse().ToList();
            released.AddRange(_idle.Where(e => now - e.ReturnedAt >= (long)_idleTimeout.TotalMilliseconds).Select(e => e.Engine));
            _idle.Clear();
            foreach (var e in kept)
                _idle.Push(e);
            foreach (var e in released)
                _all.Remove(e);
        }
        foreach (var engine in released)
            engine.Dispose();
        return released.Count;
    }

    public void Dispose()
    {
        _timer?.Dispose();
        List<Engine> idle;
        lock (_idle)
        {
            _disposed = true;
            idle = [.. _idle.Select(e => e.Engine)];
            _idle.Clear();
            _all.Clear();
        }
        foreach (var engine in idle)
            engine.Dispose();
    }
}
