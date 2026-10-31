using System.Diagnostics;
using System.Text;

namespace Extrode.Husk.Integration;

/// <summary>A built app's dll, running on a loopback port the OS picks until disposed.</summary>
public sealed class RunningApp : IDisposable
{
    private readonly Process _process;
    private readonly StringBuilder _output = new();

    public Uri Address { get; private set; } = null!;

    public string Output
    {
        get { lock (_output) return _output.ToString(); }
    }

    public RunningApp(string contentRoot, string dll, string environment = "Development")
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = contentRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add(dll);
        start.ArgumentList.Add("--urls");
        start.ArgumentList.Add("http://127.0.0.1:0");
        start.Environment["ASPNETCORE_ENVIRONMENT"] = environment;
        foreach (var key in start.Environment.Keys.Where(k => k.StartsWith("MSBuild", StringComparison.OrdinalIgnoreCase)).ToList())
            start.Environment.Remove(key);
        _process = Process.Start(start)!;
        _process.OutputDataReceived += (_, e) => Append(e.Data);
        _process.ErrorDataReceived += (_, e) => Append(e.Data);
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        try
        {
            WaitUntilServing();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private void WaitUntilServing()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        var until = DateTime.UtcNow.AddSeconds(30);
        while (true)
        {
            if (_process.HasExited)
                throw new InvalidOperationException("the app exited: " + Output);
            if (Listening.Parse(Output) is not { } address)
            {
                if (DateTime.UtcNow >= until)
                    throw new TimeoutException("the app never said where it listens: " + Output);
                Thread.Sleep(100);
                continue;
            }
            Address = address;
            try
            {
                http.GetAsync(Address).GetAwaiter().GetResult();
                return;
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException && DateTime.UtcNow < until)
            {
                Thread.Sleep(100);
            }
        }
    }

    private void Append(string? line)
    {
        if (line is null)
            return;
        lock (_output)
            _output.Append(line).Append('\n');
    }

    public void Dispose()
    {
        if (!_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
            _process.WaitForExit();
        }
        _process.Dispose();
    }
}
