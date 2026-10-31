using System.Net;
using Extrode.Husk.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Extrode.Husk.Tests;

public sealed class HuskSpaTests : IAsyncLifetime
{
    private WebApplication? _app;
    private HttpClient? _http;

    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseSetting("urls", "http://127.0.0.1:0");
        _app = builder.Build();
        _app.UseHuskSpa();
        _app.Use(async (context, next) =>
        {
            if (context.Request.Path == "/index.html")
                await context.Response.WriteAsync("shell");
            else if (context.Request.Path == "/month")
                await context.Response.WriteAsync("real");
            else
                await next();
        });
        await _app.StartAsync();
        _http = new HttpClient { BaseAddress = new Uri(_app.Urls.First()) };
    }

    public async ValueTask DisposeAsync()
    {
        _http?.Dispose();
        if (_app is not null)
            await _app.DisposeAsync();
    }

    private async Task<(HttpStatusCode Status, string Body)> Get(string path, string? accept = "text/html,application/xhtml+xml", HttpMethod? method = null)
    {
        using var request = new HttpRequestMessage(method ?? HttpMethod.Get, path);
        if (accept is not null)
            request.Headers.TryAddWithoutValidation("Accept", accept);
        using var response = await _http!.SendAsync(request, TestContext.Current.CancellationToken);
        return (response.StatusCode, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("/settings/appearance")]
    [InlineData("/no/such/page")]
    [InlineData("/_husk")]
    public async Task An_unknown_navigation_gets_the_page_unless_it_is_under_a_module_prefix(string path)
    {
        var (status, body) = await Get(path);

        if (path == "/_husk")
            Assert.Equal((HttpStatusCode.NotFound, ""), (status, body));
        else
            Assert.Equal((HttpStatusCode.OK, "shell"), (status, body));
    }

    [Theory]
    [InlineData("/_husk/router.js")]
    [InlineData("/data/hijri.json")]
    public async Task An_address_under_the_module_directory_or_data_is_a_404_even_for_a_navigation(string path) =>
        Assert.Equal(HttpStatusCode.NotFound, (await Get(path)).Status);

    [Fact]
    public async Task A_request_that_does_not_accept_html_stays_a_404() =>
        Assert.Equal(HttpStatusCode.NotFound, (await Get("/missing", accept: "*/*")).Status);

    [Fact]
    public async Task An_address_that_has_an_answer_is_left_alone() =>
        Assert.Equal((HttpStatusCode.OK, "real"), await Get("/month"));

    [Fact]
    public async Task A_post_is_not_answered_with_the_page()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/anything");
        request.Headers.TryAddWithoutValidation("Accept", "text/html");
        using var response = await _http!.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

public sealed class HuskSpaWithEndpointsTests
{
    [Fact]
    public async Task The_fallback_reaches_a_page_served_by_an_endpoint()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseSetting("urls", "http://127.0.0.1:0");
        await using var app = builder.Build();
        app.UseHuskSpa();
        app.UseRouting();
        app.MapGet("/index.html", () => "shell");
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var http = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };
        using var request = new HttpRequestMessage(HttpMethod.Get, "/settings/appearance");
        request.Headers.TryAddWithoutValidation("Accept", "text/html");

        using var response = await http.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal((HttpStatusCode.OK, "shell"), (response.StatusCode, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)));
    }
}
