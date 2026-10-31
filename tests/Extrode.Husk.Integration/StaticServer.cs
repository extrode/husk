using System.Net;
using System.Net.Sockets;

namespace Extrode.Husk.Integration;

/// <summary>A static file server, as `python3 -m http.server` would be, for the life of a test.</summary>
public sealed class StaticServer : IDisposable
{
    private readonly HttpListener _listener;
    private readonly string _root;

    public Uri Address { get; }

    /// <summary>HttpListener cannot bind port 0, so a port is picked and bound; when something takes it
    /// in between, the next attempt picks again.</summary>
    public StaticServer(string root, Func<int>? pickPort = null)
    {
        _root = root;
        pickPort ??= FreePort;
        for (int attempt = 1; ; attempt++)
        {
            Address = new Uri($"http://127.0.0.1:{pickPort()}/");
            var listener = new HttpListener();
            listener.Prefixes.Add(Address.ToString());
            try
            {
                listener.Start();
                _listener = listener;
                break;
            }
            catch (HttpListenerException) when (attempt < 10)
            {
                listener.Close();
            }
        }
        _ = Answer();
    }

    private static int FreePort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        return ((IPEndPoint)probe.LocalEndpoint).Port;
    }

    private async Task Answer()
    {
        while (true)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception e) when (e is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                return;
            }
            using var response = context.Response;
            string path = Uri.UnescapeDataString(context.Request.Url!.AbsolutePath).TrimStart('/');
            string file = Path.Combine(_root, path.Length == 0 ? "index.html" : path);
            if (!File.Exists(file))
            {
                response.StatusCode = 404;
                continue;
            }
            // Browsers refuse a module script served without a JavaScript type.
            response.ContentType = Path.GetExtension(file) switch
            {
                ".js" or ".mjs" => "text/javascript",
                ".css" => "text/css",
                ".map" or ".json" => "application/json",
                _ => "text/html; charset=utf-8",
            };
            await response.OutputStream.WriteAsync(await File.ReadAllBytesAsync(file));
        }
    }

    public void Dispose() => _listener.Close();
}
