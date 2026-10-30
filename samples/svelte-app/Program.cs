using Extrode.Husk.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
// Development only: recompiles each saved component, rune module and page, so `dotnet watch`
// serves fresh output and refreshes the browser. Also re-strips saved .ts through Pare.
builder.AddHusk();
var app = builder.Build();

// MapStaticAssets serves no default document, so "/" is the page by name, before routing.
app.Use((context, next) =>
{
    if (context.Request.Path == "/")
        context.Request.Path = "/index.html";
    return next(context);
});
app.UseRouting();
app.MapStaticAssets();

app.Run();
