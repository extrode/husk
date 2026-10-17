using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Extrode.Husk.Integration;

/// <summary>A built app's dll, running on a free loopback port until disposed.</summary>
public sealed class RunningApp : IDisposable
{
    private readonly Process _process;
    private readonly StringBuilder _output = new();

    public Uri Address { get; }

    public string Output
    {
        get { lock (_output) return _output.ToString(); }
    }

    public RunningApp(string contentRoot, string dll, string environment = "Development")
    {
        using (var probe = new TcpListener(IPAddress.Loopback, 0))
        {
            probe.Start();
            Address = new Uri($"http://127.0.0.1:{((IPEndPoint)probe.LocalEndpoint).Port}/");
        }
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = contentRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add(dll);
        start.ArgumentList.Add("--urls");
        start.ArgumentList.Add(Address.ToString());
        start.Environment["ASPNETCORE_ENVIRONMENT"] = environment;
        foreach (var key in start.Environment.Keys.Where(k => k.StartsWith("MSBuild", StringComparison.OrdinalIgnoreCase)).ToList())
            start.Environment.Remove(key);
        _process = Process.Start(start)!;
        _process.OutputDataReceived += (_, e) => Append(e.Data);
        _process.ErrorDataReceived += (_, e) => Append(e.Data);
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        using var http = new HttpClient();
        var until = DateTime.UtcNow.AddSeconds(30);
        while (true)
        {
            if (_process.HasExited)
                throw new InvalidOperationException("the app exited: " + Output);
            try
            {
                http.GetAsync(Address).GetAwaiter().GetResult();
                return;
            }
            catch (HttpRequestException) when (DateTime.UtcNow < until)
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
