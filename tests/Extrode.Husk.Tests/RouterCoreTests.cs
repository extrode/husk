using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using Microsoft.ClearScript.V8;
using Extrode.Husk.Compiler;

namespace Extrode.Husk.Tests;

/// <summary>Runs the router core in V8, with no Node. Promises settle when the script ends.</summary>
public sealed class RouterCoreTests : IDisposable
{
    private readonly V8ScriptEngine _engine;

    public RouterCoreTests()
    {
        NativeProbe.Register();
        _engine = new V8ScriptEngine(V8ScriptEngineFlags.EnableDynamicModuleImports) { DocumentSettings = { AccessFlags = DocumentAccessFlags.EnableFileLoading } };
    }

    public void Dispose() => _engine.Dispose();

    private static string Runtime(string name) => Path.Combine(RepoPaths.Root, "src", "Extrode.Husk.Build", "runtime", name);

    // The module's body is a function of the router core, returning JSON-able data.
    private string Run(string body, string module = "router-core.js")
    {
        string file = Runtime(module);
        _engine.Execute(new DocumentInfo(new Uri(file)) { Category = ModuleCategory.Standard }, $$"""
            import * as core from {{System.Text.Json.JsonSerializer.Serialize(new Uri(file).AbsoluteUri)}};
            globalThis.out = undefined;
            Promise.resolve((async () => { {{body}} })()).then(v => { globalThis.out = JSON.stringify(v); }, e => { globalThis.out = 'THROWN ' + e; });
            """);
        _engine.Execute("0");
        return (string)_engine.Evaluate("globalThis.out");
    }

    private const string Routes = """
        const route = (pattern, extra = {}) => ({ pattern, layouts: [], load: null, ...extra });
        const routes = [route([]), route(['settings', 'appearance']), route(['settings', { param: 'section' }]), route(['a', 'b'])];
        """;

    [Theory]
    [InlineData("/", "[]", "{}")]
    [InlineData("/settings/appearance", "[\"settings\",\"appearance\"]", "{}")]
    [InlineData("/settings/appearance/", "[\"settings\",\"appearance\"]", "{}")]
    [InlineData("/settings/nonsense", "[\"settings\",{\"param\":\"section\"}]", "{\"section\":\"nonsense\"}")]
    [InlineData("/settings/a%20b", "[\"settings\",{\"param\":\"section\"}]", "{\"section\":\"a b\"}")]
    [InlineData("/a/b?x=1#y", "[\"a\",\"b\"]", "{}")]
    public void An_address_matches_the_first_route_and_gives_its_parameters(string path, string pattern, string parameters)
    {
        string result = Run($"{Routes}\nconst m = core.matchRoute(routes, {System.Text.Json.JsonSerializer.Serialize(path)}); return [m.route.pattern, m.params];");

        Assert.Equal($"[{pattern},{parameters}]", result);
    }

    [Theory]
    [InlineData("/nothing")]
    [InlineData("/settings")]
    [InlineData("/a//b")]
    [InlineData("settings")]
    [InlineData("/settings/%E0%A4%A")]
    public void An_address_with_no_route_matches_nothing(string path) =>
        Assert.Equal("null", Run($"{Routes}\nreturn core.matchRoute(routes, {System.Text.Json.JsonSerializer.Serialize(path)});"));

    [Fact]
    public void Loads_run_together_and_parent_gives_the_loads_above()
    {
        string result = Run("""
            const log = [];
            const route = {
              pattern: [], 
              layouts: [{ load: async () => ({ load: async () => { log.push('layout'); return { a: 1 }; } }) }],
              load: async () => ({ load: async ({ parent, params }) => ({ b: (await parent()).a + 1, params }) }),
            };
            const r = await core.runLoads(route, { id: '7' }, { href: 'x' });
            return [r, core.cumulative(r.data), log];
            """);

        Assert.Equal("""[{"kind":"data","data":[{"a":1},{"b":2,"params":{"id":"7"}}]},[{"a":1},{"a":1,"b":2,"params":{"id":"7"}}],["layout"]]""", result);
    }

    [Fact]
    public void A_module_without_a_load_gives_undefined_data()
    {
        Assert.Equal("""[{"kind":"data","data":[null]}]""".Replace("[null]", "[null]"), Run("""
            const r = await core.runLoads({ pattern: [], layouts: [], load: async () => ({ default: 1 }) }, {}, {});
            return [{ kind: r.kind, data: r.data.map(d => d === undefined ? null : d) }];
            """));
    }

    [Fact]
    public void A_redirect_an_http_error_and_a_plain_error_are_results_not_throws()
    {
        string result = Run("""
            const of = (fn) => core.runLoads({ pattern: [], layouts: [], load: async () => ({ load: fn }) }, {}, {});
            return [
              await of(() => core.redirect(307, '/settings/appearance')),
              await of(() => core.error(404, 'Not here')),
              await of(() => { throw new Error('boom'); }),
              await core.runLoads({ pattern: [], layouts: [], load: async () => { throw new Error('import failed'); } }, {}, {}),
            ];
            """);

        Assert.Equal("""[{"kind":"redirect","status":307,"location":"/settings/appearance"},{"kind":"error","status":404,"message":"Not here"},{"kind":"error","status":500,"message":"boom"},{"kind":"error","status":500,"message":"import failed"}]""", result);
    }

    [Fact]
    public void The_kit_shim_exports_the_same_redirect_the_core_throws()
    {
        string result = Run("""
            const kit = await import(%KIT%);
            try { kit.redirect(302, '/x'); } catch (e) { return [core.isRedirect(e), kit.isRedirect(e), e.status, e.location]; }
            """.Replace("%KIT%", System.Text.Json.JsonSerializer.Serialize(new Uri(Runtime("kit.js")).AbsoluteUri)));

        Assert.Equal("""[true,true,302,"/x"]""", result);
    }

    [Fact]
    public void The_route_id_names_the_pattern()
    {
        Assert.Equal("\"/settings/[section]\"", Run("return core.routeId({ pattern: ['settings', { param: 'section' }] });"));
    }
}
