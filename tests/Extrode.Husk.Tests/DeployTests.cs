using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Extrode.Husk.Deploy;

namespace Extrode.Husk.Tests;

public sealed class DeployTests : IDisposable
{
    private const string Token = "test-api-token-5f1c";
    private const string UploadJwt = "upload-jwt-9a2e";
    private const string CompletionJwt = "completion-jwt-77b0";

    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("husk-deploy-");
    private readonly FakeCloudflare _server = new();
    private readonly HttpClient _http = new();
    private readonly StringWriter _out = new();
    private readonly StringWriter _err = new();

    public DeployTests()
    {
        Write("index.html", "<!doctype html><p>hi</p>");
        Write("app/main.js", "export {};\n");
        Write("app/site.css", "p{}");
    }

    public void Dispose()
    {
        _http.Dispose();
        _server.Dispose();
        _dir.Delete(recursive: true);
    }

    private void Write(string relative, string text)
    {
        string path = Path.Combine(_dir.FullName, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private string Hash(string relative) => CloudflareApi.Hash(File.ReadAllBytes(Path.Combine(_dir.FullName, relative)), relative);

    private Task<int> Run(params string[] extra) => Program.Run(
        ["--dir", _dir.FullName, "--script", "site", "--account", "acc1", "--compatibility-date", "2026-10-30", .. extra],
        Token, _out, _err, _http, _server.BaseUrl, allowInsecureBaseUrl: true);

    private static string SessionBody(string jwt, params string[][] buckets) =>
        JsonSerializer.Serialize(new { success = true, result = new { jwt, buckets } });

    private static string JwtBody(string? jwt) => JsonSerializer.Serialize(new { success = true, result = new { jwt } });

    [Fact]
    public async Task The_three_requests_carry_the_documented_paths_methods_auth_and_bodies()
    {
        string html = Hash("index.html"), js = Hash("app/main.js"), css = Hash("app/site.css");
        _server.Respond(SessionBody(UploadJwt, [html, js], [css]), JwtBody(null), JwtBody(CompletionJwt), JwtBody(null));

        Assert.Equal(0, await Run());

        var r = _server.Requests;
        Assert.Equal(4, r.Count);

        Assert.Equal("POST", r[0].Method);
        Assert.Equal("/client/v4/accounts/acc1/workers/scripts/site/assets-upload-session", r[0].Path);
        Assert.Equal("Bearer " + Token, r[0].Authorization);
        using (var session = JsonDocument.Parse(r[0].Body))
        {
            var manifest = session.RootElement.GetProperty("manifest");
            Assert.Equal(["/app/main.js", "/app/site.css", "/index.html"], manifest.EnumerateObject().Select(p => p.Name));
            Assert.Equal(html, manifest.GetProperty("/index.html").GetProperty("hash").GetString());
            Assert.Equal(24, manifest.GetProperty("/index.html").GetProperty("size").GetInt64());
            Assert.Matches("^[0-9a-f]{32}$", html);
        }

        foreach (var upload in r.Skip(1).Take(2))
        {
            Assert.Equal("POST", upload.Method);
            Assert.Equal("/client/v4/accounts/acc1/workers/assets/upload?base64=true", upload.Path);
            Assert.Equal("Bearer " + UploadJwt, upload.Authorization);
        }
        var first = Parts(r[1]);
        Assert.Equal([html, js], first.Keys);
        Assert.Equal(Convert.ToBase64String("<!doctype html><p>hi</p>"u8.ToArray()), first[html].Body);
        Assert.Equal("text/html", first[html].ContentType);
        Assert.Equal(Convert.ToBase64String("export {};\n"u8.ToArray()), first[js].Body);
        Assert.Equal([css], Parts(r[2]).Keys);

        Assert.Equal("PUT", r[3].Method);
        Assert.Equal("/client/v4/accounts/acc1/workers/scripts/site", r[3].Path);
        Assert.Equal("Bearer " + Token, r[3].Authorization);
        var metadata = Parts(r[3])["metadata"];
        Assert.Equal("application/json", metadata.ContentType);
        using (var json = JsonDocument.Parse(metadata.Body))
        {
            Assert.Equal("2026-10-30", json.RootElement.GetProperty("compatibility_date").GetString());
            Assert.Equal(CompletionJwt, json.RootElement.GetProperty("assets").GetProperty("jwt").GetString());
            Assert.Equal(JsonValueKind.Object, json.RootElement.GetProperty("assets").GetProperty("config").ValueKind);
        }
        Assert.Contains("3 uploaded, 0 unchanged", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Files_the_session_already_has_are_not_uploaded()
    {
        string css = Hash("app/site.css");
        _server.Respond(SessionBody(UploadJwt, [css]), JwtBody(CompletionJwt), JwtBody(null));

        Assert.Equal(0, await Run());

        var r = _server.Requests;
        Assert.Equal(["POST", "POST", "PUT"], r.Select(x => x.Method));
        Assert.Equal([css], Parts(r[1]).Keys);
        Assert.Contains("1 uploaded, 2 unchanged", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Nothing_to_upload_goes_straight_to_the_script_with_the_session_token()
    {
        _server.Respond(SessionBody(CompletionJwt), JwtBody(null));

        Assert.Equal(0, await Run());

        var r = _server.Requests;
        Assert.Equal(["POST", "PUT"], r.Select(x => x.Method));
        using var json = JsonDocument.Parse(Parts(r[1])["metadata"].Body);
        Assert.Equal(CompletionJwt, json.RootElement.GetProperty("assets").GetProperty("jwt").GetString());
        Assert.Contains("0 uploaded, 3 unchanged", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Dry_run_prints_the_manifest_and_sends_nothing()
    {
        Assert.Equal(0, await Run("--dry-run"));

        Assert.Empty(_server.Requests);
        string output = _out.ToString();
        Assert.Contains("/index.html", output, StringComparison.Ordinal);
        Assert.Contains("3 files, 38 bytes", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_failing_step_is_named_with_its_status_and_the_token_is_not_printed()
    {
        string failure = "{\"errors\":[\"" + Token + "\"]}";
        _server.Respond((200, SessionBody(UploadJwt, [Hash("index.html")])), (500, failure), (500, failure), (500, failure));

        Assert.Equal(1, await Run());

        Assert.Equal(4, _server.Requests.Count);
        Assert.Contains("assets upload failed: HTTP 500", _err.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(Token, _out.ToString() + _err.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_upload_answered_503_or_429_is_sent_again_and_the_deploy_goes_on()
    {
        _server.Respond((200, SessionBody(UploadJwt, [Hash("index.html")])), (503, "{}"), (429, "{}"), (200, JwtBody(CompletionJwt)), (200, JwtBody(null)));

        Assert.Equal(0, await Run());

        Assert.Equal(["POST", "POST", "POST", "POST", "PUT"], _server.Requests.Select(r => r.Method));
        Assert.Equal(Parts(_server.Requests[1]).Values, Parts(_server.Requests[3]).Values);
    }

    [Fact]
    public async Task A_client_error_on_upload_is_not_sent_again()
    {
        _server.Respond((200, SessionBody(UploadJwt, [Hash("index.html")])), (403, "{}"));

        Assert.Equal(1, await Run());

        Assert.Equal(2, _server.Requests.Count);
    }

    [Fact]
    public async Task A_2xx_answer_that_is_not_JSON_exits_1_with_no_stack_trace()
    {
        _server.Respond((200, "<html>proxy</html>"));

        Assert.Equal(1, await Run());

        Assert.DoesNotContain("   at ", _err.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_bucket_entry_that_is_not_a_string_exits_1()
    {
        _server.Respond((200, "{\"result\":{\"jwt\":\"j\",\"buckets\":[[null]]}}"));

        Assert.Equal(1, await Run());

        Assert.Contains("not a string", _err.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("_headers")]
    [InlineData("_redirects")]
    public async Task A_headers_or_redirects_file_is_refused_not_uploaded_as_an_asset(string name)
    {
        Write(name, "/*\n  X-Frame-Options: DENY\n");

        Assert.Equal(1, await Run());

        Assert.Empty(_server.Requests);
        Assert.Contains(name + " is not supported", _err.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_DS_Store_file_is_not_deployed()
    {
        Write(".DS_Store", "x");

        Assert.Equal(0, await Run("--dry-run"));

        Assert.DoesNotContain(".DS_Store", _out.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("a.wasm", "application/wasm")]
    [InlineData("a.woff", "font/woff")]
    [InlineData("a.gif", "image/gif")]
    [InlineData("a.unknown", "application/octet-stream")]
    public void Content_types_cover_wasm_fonts_and_images(string path, string expected) =>
        Assert.Equal(expected, CloudflareApi.ContentType(path));

    [Fact]
    public async Task The_token_never_appears_in_the_output()
    {
        _server.Respond(SessionBody(UploadJwt, [Hash("index.html")]), JwtBody(CompletionJwt), JwtBody(null));
        Assert.Equal(0, await Run());
        Assert.Equal(0, await Run("--dry-run"));

        string all = _out.ToString() + _err.ToString();
        Assert.DoesNotContain(Token, all, StringComparison.Ordinal);
        Assert.DoesNotContain(UploadJwt, all, StringComparison.Ordinal);
        Assert.DoesNotContain(CompletionJwt, all, StringComparison.Ordinal);
    }

    [Fact]
    public void A_base_url_that_is_not_https_is_refused_without_the_override()
    {
        Assert.Throws<ArgumentException>(() => new Deployer(_http, _server.BaseUrl, Token));
        _ = new Deployer(_http, new Uri("https://api.example/"), Token);
    }

    private sealed record Part(string ContentType, string Body);

    /// <summary>The multipart body's parts by name, in order.</summary>
    private static Dictionary<string, Part> Parts(FakeCloudflare.Request request)
    {
        string boundary = MediaTypeHeaderValue.Parse(request.ContentType).Parameters
            .Single(p => p.Name == "boundary").Value!.Trim('"');
        var parts = new Dictionary<string, Part>(StringComparer.Ordinal);
        foreach (string raw in request.Body.Split("--" + boundary).Skip(1))
        {
            if (raw.StartsWith("--", StringComparison.Ordinal))
                break;
            int split = raw.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            string[] headers = raw[..split].Trim().Split("\r\n");
            string body = raw[(split + 4)..^2];
            var disposition = ContentDispositionHeaderValue.Parse(headers.Single(h => h.StartsWith("Content-Disposition:", StringComparison.OrdinalIgnoreCase))["Content-Disposition:".Length..].Trim());
            string type = headers.Single(h => h.StartsWith("Content-Type:", StringComparison.OrdinalIgnoreCase))["Content-Type:".Length..].Trim();
            parts.Add(disposition.Name!.Trim('"'), new Part(type, body));
        }
        return parts;
    }
}

/// <summary>An in-process HTTP server on loopback that records each request and answers with the
/// queued responses in order.</summary>
internal sealed class FakeCloudflare : IDisposable
{
    internal sealed record Request(string Method, string Path, string? Authorization, string ContentType, string Body);

    private readonly HttpListener _listener;
    private readonly Queue<(int Status, string Body)> _responses = new();
    private readonly List<Request> _requests = [];
    private readonly Task _loop;

    public Uri BaseUrl { get; }

    public FakeCloudflare()
    {
        for (int attempt = 0; ; attempt++)
        {
            int port = FreePort();
            var listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            try
            {
                listener.Start();
                _listener = listener;
                BaseUrl = new Uri($"http://127.0.0.1:{port}/client/v4/");
                break;
            }
            catch (HttpListenerException) when (attempt < 5)
            {
                listener.Close();
            }
        }
        _loop = Task.Run(Serve);
    }

    public IReadOnlyList<Request> Requests
    {
        get { lock (_requests) return [.. _requests]; }
    }

    public void Respond(params string[] bodies)
    {
        foreach (string body in bodies)
            Respond((200, body));
    }

    public void Respond(params (int Status, string Body)[] responses)
    {
        lock (_responses)
            foreach (var response in responses)
                _responses.Enqueue(response);
    }

    private async Task Serve()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception e) when (e is HttpListenerException or ObjectDisposedException)
            {
                return;
            }
            using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
            {
                var request = new Request(context.Request.HttpMethod, context.Request.RawUrl!,
                    context.Request.Headers["Authorization"], context.Request.ContentType ?? "", await reader.ReadToEndAsync());
                lock (_requests)
                    _requests.Add(request);
            }
            (int status, string body) = (404, "{}");
            lock (_responses)
                if (_responses.Count > 0)
                    (status, body) = _responses.Dequeue();
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/json";
            byte[] bytes = Encoding.UTF8.GetBytes(body);
            await context.Response.OutputStream.WriteAsync(bytes);
            context.Response.Close();
        }
    }

    private static int FreePort()
    {
        using var socket = new TcpListener(IPAddress.Loopback, 0);
        socket.Start();
        return ((IPEndPoint)socket.LocalEndpoint).Port;
    }

    public void Dispose()
    {
        _listener.Stop();
        _listener.Close();
        _loop.Wait(TimeSpan.FromSeconds(5));
    }
}
