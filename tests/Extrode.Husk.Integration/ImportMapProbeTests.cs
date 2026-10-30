namespace Extrode.Husk.Integration;

/// <summary>The import map's key forms, before anything builds on them: a key with the source's
/// extension, one without, and the `.svelte` form of a `.svelte.ts` module. Each target is served only
/// at its output URL, so a key that does not match is a 404.</summary>
public sealed class ImportMapProbeTests : IDisposable
{
    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("husk-probe-");

    public void Dispose() => _dir.Delete(recursive: true);

    private void Put(string relative, string text)
    {
        string path = Path.Combine(_dir.FullName, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    [Theory]
    [MemberData(nameof(Browsers.All), MemberType = typeof(Browsers))]
    public void Every_key_form_resolves(string browser)
    {
        Put("index.html", """
            <!doctype html>
            <script type="importmap">
            { "imports": {
                "/m/Child.svelte": "/m/Child.svelte.js",
                "/m/Child": "/m/Child.svelte.js",
                "/m/helper.ts": "/m/helper.js",
                "/m/helper": "/m/helper.js",
                "/m/counter.svelte.ts": "/m/counter.svelte.js",
                "/m/counter.svelte": "/m/counter.svelte.js"
            } }
            </script>
            <script type="module" src="/m/main.js"></script>
            """);
        Put("m/main.js", """
            import a from './Child.svelte';
            import b from './Child';
            import { h as c } from './helper.ts';
            import { h as d } from './helper';
            import { n as e } from './counter.svelte.ts';
            import { n as f } from './counter.svelte';
            window.probe = [a, b, c, d, e, f].join(',');
            """);
        Put("m/Child.svelte.js", "export default 'child';\n");
        Put("m/helper.js", "export const h = 'helper';\n");
        Put("m/counter.svelte.js", "export const n = 'counter';\n");
        using var server = new StaticServer(_dir.FullName);
        using var driver = Browsers.Start(browser);

        driver.Navigate().GoToUrl(server.Address);

        Assert.Equal("child,child,helper,helper,counter,counter", Browsers.Until(driver, "return window.probe ?? null;"));
    }
}
