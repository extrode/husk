using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Extrode.Husk.AspNetCore;

/// <summary>Which addresses the single-page fallback leaves alone.</summary>
public sealed class HuskSpaOptions
{
    /// <summary>Address prefixes that are 404 when no file matches, never the page: a cached page for a
    /// module URL once broke an app for a year. Defaults to the module directory and the data folder.</summary>
    public IList<string> NoFallbackPrefixes { get; } = ["/_husk/", "/data/"];

    /// <summary>The page every other unknown address gets.</summary>
    public string Page { get; set; } = "/index.html";
}

public static class HuskSpaExtensions
{
    /// <summary>
    /// For an app that routes in the browser: a GET or HEAD for an address nothing answers, that accepts
    /// HTML and is not under <see cref="HuskSpaOptions.NoFallbackPrefixes"/>, gets the app's page with
    /// status 200 and the address unchanged. Put it before the static files and endpoints it falls back
    /// from. Production hosting does the same (Cloudflare's single-page-application mode, <c>--spa</c>).
    /// </summary>
    public static IApplicationBuilder UseHuskSpa(this IApplicationBuilder app, Action<HuskSpaOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(app);
        var options = new HuskSpaOptions();
        configure?.Invoke(options);
        return app.Use(async (context, next) =>
        {
            await next();
            var request = context.Request;
            if (context.Response.StatusCode != StatusCodes.Status404NotFound || context.Response.HasStarted
                || !(HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method))
                || !AcceptsHtml(request)
                || options.NoFallbackPrefixes.Any(p => request.Path.StartsWithSegments(p.TrimEnd('/'), StringComparison.Ordinal)))
                return;
            string original = request.Path;
            request.Path = options.Page;
            context.Response.StatusCode = StatusCodes.Status200OK;
            try
            {
                await next();
            }
            finally
            {
                request.Path = original;
            }
        });
    }

    private static bool AcceptsHtml(HttpRequest request)
    {
        // A browser navigation sends text/html first; a script or image request does not.
        return request.Headers.Accept.Any(v => v is not null && v.Contains("text/html", StringComparison.OrdinalIgnoreCase));
    }
}
