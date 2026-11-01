using System.Text.Json;
using Extrode.Husk.Compiler;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using Microsoft.ClearScript.V8;

namespace Extrode.Husk.Tests;

[Trait("Law", "L004")]
public sealed class L004RouteResolutionIsDeterministicTests : IDisposable
{
    private static readonly string[] Literals = ["a", "b", "month", "settings"];
    private static readonly string[] Params = ["id", "section"];
    private static readonly string[] Pieces = ["a", "b", "month", "settings", "x%20y", "%E4%B8%AD", "zzz", ""];

    private readonly V8ScriptEngine _engine;

    public L004RouteResolutionIsDeterministicTests()
    {
        NativeProbe.Register();
        _engine = new V8ScriptEngine(V8ScriptEngineFlags.EnableDynamicModuleImports) { DocumentSettings = { AccessFlags = DocumentAccessFlags.EnableFileLoading } };
    }

    public void Dispose() => _engine.Dispose();

    [Fact]
    public void Every_generated_table_and_address_resolves_the_same_every_time_and_with_or_without_a_trailing_slash()
    {
        var random = new Random(4004);
        var cases = new List<object>();
        for (int t = 0; t < 60; t++)
        {
            var routes = new List<object[]>();
            for (int r = random.Next(1, 7); r > 0; r--)
                routes.Add(Enumerable.Range(0, random.Next(0, 4)).Select(_ => random.Next(3) == 0
                    ? (object)new { param = Params[random.Next(Params.Length)] }
                    : Literals[random.Next(Literals.Length)]).ToArray());
            var addresses = new List<string>();
            for (int a = 0; a < 25; a++)
            {
                string path = "/" + string.Join('/', Enumerable.Range(0, random.Next(0, 4)).Select(_ => Pieces[random.Next(Pieces.Length)]));
                addresses.Add(random.Next(4) == 0 ? path + "?q=1#h" : path);
            }
            cases.Add(new { routes = routes.Select(p => new { pattern = p }), addresses });
        }

        string file = Path.Combine(RepoPaths.Root, "src", "Extrode.Husk.Build", "runtime", "router-core.js");
        _engine.Execute(new DocumentInfo(new Uri(file)) { Category = ModuleCategory.Standard }, $$"""
            import { matchRoute } from {{JsonSerializer.Serialize(new Uri(file).AbsoluteUri)}};
            const show = (m) => JSON.stringify(m && [m.route.pattern, m.params]);
            const bad = [];
            for (const c of {{JsonSerializer.Serialize(cases)}}) {
              for (const address of c.addresses) {
                const first = show(matchRoute(c.routes, address));
                const second = show(matchRoute(c.routes, address));
                if (first !== second) bad.push(['repeat', c.routes, address, first, second]);
                if (address !== '/' && !address.includes('?') && !address.includes('#') && !address.endsWith('/')) {
                  const slashed = show(matchRoute(c.routes, address + '/'));
                  if (first !== slashed) bad.push(['slash', c.routes, address, first, slashed]);
                }
              }
            }
            globalThis.out = JSON.stringify(bad);
            """);

        Assert.Equal("[]", (string)_engine.Evaluate("globalThis.out"));
    }
}
