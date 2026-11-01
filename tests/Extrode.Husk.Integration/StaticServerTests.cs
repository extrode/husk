namespace Extrode.Husk.Integration;

public sealed class StaticServerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "husk-static-" + Guid.NewGuid().ToString("N"));

    public StaticServerTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, true);

    [Theory]
    [InlineData("a.svg", "image/svg+xml")]
    [InlineData("a.png", "image/png")]
    [InlineData("a.mp3", "audio/mpeg")]
    [InlineData("a.woff2", "font/woff2")]
    [InlineData("a.js", "text/javascript")]
    public async Task A_file_is_served_with_its_content_type(string name, string type)
    {
        File.WriteAllBytes(Path.Combine(_root, name), [1, 2, 3]);
        using var server = new StaticServer(_root);
        using var http = new HttpClient();
        var response = await http.GetAsync(new Uri(server.Address, name), TestContext.Current.CancellationToken);
        Assert.Equal(type, response.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task A_head_script_comes_before_every_other_script_in_the_page()
    {
        File.WriteAllText(Path.Combine(_root, "index.html"), "<!doctype html><html><head><meta charset=utf-8><script type=module src=/a.js></script></head><body></body></html>");
        using var server = new StaticServer(_root, headScript: "window.__frozen=1");
        using var http = new HttpClient();
        string html = await http.GetStringAsync(server.Address, TestContext.Current.CancellationToken);
        int injected = html.IndexOf("window.__frozen=1", StringComparison.Ordinal);
        Assert.True(injected > html.IndexOf("<head>", StringComparison.Ordinal));
        Assert.True(injected < html.IndexOf("src=/a.js", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Without_a_head_script_the_page_is_served_unchanged()
    {
        const string page = "<html><head></head></html>";
        File.WriteAllText(Path.Combine(_root, "index.html"), page);
        using var server = new StaticServer(_root);
        using var http = new HttpClient();
        Assert.Equal(page, await http.GetStringAsync(server.Address, TestContext.Current.CancellationToken));
    }
}
