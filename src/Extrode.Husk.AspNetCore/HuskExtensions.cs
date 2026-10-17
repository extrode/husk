using Extrode.Pare.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Extrode.Husk.AspNetCore;

public static class HuskExtensions
{
    /// <summary>
    /// In Development, compiles each <c>.svelte</c>, <c>.svelte.ts</c> and <c>.svelte.js</c> again as it
    /// is saved, through <see cref="PareDevelopmentExtensions.AddPareDevelopment"/>, which this calls;
    /// rewrites <c>husk.css</c> with the saved component's styles, and writes a saved page with the
    /// import map inlined.
    /// The compiler is loaded from the build package, so it never ships in the app. Does nothing in any
    /// other environment.
    /// </summary>
    public static WebApplicationBuilder AddHusk(this WebApplicationBuilder builder) =>
        AddHusk(builder, Path.Combine(AppContext.BaseDirectory, "Extrode.Husk.development.json"));

    internal static WebApplicationBuilder AddHusk(WebApplicationBuilder builder, string mapPath)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (!builder.Environment.IsDevelopment())
            return builder;
        if (!builder.Services.Any(s => s.ServiceType == typeof(IPareCompiler) && s.ImplementationInstance is HuskCompiler))
        {
            var session = new HuskSession(mapPath);
            builder.Services.AddSingleton<IPareCompiler>(new HuskCompiler(session));
            builder.Services.AddSingleton<IPareCompiler>(new HuskPageCompiler(session));
        }
        return builder.AddPareDevelopment();
    }
}
