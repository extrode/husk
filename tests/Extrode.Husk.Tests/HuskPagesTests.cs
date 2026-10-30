using System.Text;
using Extrode.Husk.Build;
using Microsoft.Build.Utilities;

namespace Extrode.Husk.Tests;

public sealed class HuskPagesTests : IDisposable
{
    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("husk-pages-");

    public void Dispose() => _dir.Delete(recursive: true);

    private string P(string relative) => Path.Combine(_dir.FullName, relative.Replace('/', Path.DirectorySeparatorChar));

    private bool Run(string name, byte[] page)
    {
        File.WriteAllBytes(P(name), page);
        File.WriteAllText(P("importmap.json"), "{}\n");
        return new HuskPages
        {
            BuildEngine = new FakeBuildEngine(),
            Pages = [new TaskItem(P(name), new Dictionary<string, string> { ["PareOutputPath"] = P("out/" + name) })],
            ImportMap = P("importmap.json"),
        }.Execute();
    }

    [Fact]
    public void A_marked_page_keeps_its_byte_order_mark()
    {
        byte[] page = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("<script type=importmap data-husk></script>\u00e9")];

        Assert.True(Run("a.html", page));

        Assert.Equal([0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("<script type=importmap data-husk>{}</script>\u00e9")], File.ReadAllBytes(P("out/a.html")));
    }

    [Fact]
    public void A_marked_page_without_a_mark_gets_none()
    {
        Assert.True(Run("a.html", Encoding.UTF8.GetBytes("<script type=importmap data-husk></script>")));

        Assert.Equal(Encoding.UTF8.GetBytes("<script type=importmap data-husk>{}</script>"), File.ReadAllBytes(P("out/a.html")));
    }

    [Fact]
    public void An_unmarked_page_is_written_newer_than_its_source()
    {
        Assert.True(Run("b.html", [0xFF, 0x00, 0x41]));
        File.SetLastWriteTimeUtc(P("b.html"), DateTime.UtcNow.AddDays(-1));
        Assert.True(Run("b.html", [0xFF, 0x00, 0x41]));

        Assert.Equal([0xFF, 0x00, 0x41], File.ReadAllBytes(P("out/b.html")));
        Assert.True(File.GetLastWriteTimeUtc(P("out/b.html")) > DateTime.UtcNow.AddHours(-1));
    }

    [Fact]
    public void A_page_that_cannot_be_read_is_named_and_the_others_are_written()
    {
        File.WriteAllText(P("importmap.json"), "{}\n");
        File.WriteAllText(P("b.html"), "<script type=importmap data-husk></script>");
        var engine = new FakeBuildEngine();

        bool ok = new HuskPages
        {
            BuildEngine = engine,
            Pages = [new TaskItem(P("missing.html"), new Dictionary<string, string> { ["PareOutputPath"] = P("out/missing.html") }),
                     new TaskItem(P("b.html"), new Dictionary<string, string> { ["PareOutputPath"] = P("out/b.html") })],
            ImportMap = P("importmap.json"),
        }.Execute();

        Assert.False(ok);
        var error = Assert.Single(engine.Errors);
        Assert.Equal(("HUSK102", P("missing.html")), (error.Code, error.File));
        Assert.Equal("<script type=importmap data-husk>{}</script>", File.ReadAllText(P("out/b.html")));
    }

    [Fact]
    public void With_a_graph_a_page_below_a_base_path_preloads_what_its_relative_entry_reaches()
    {
        Directory.CreateDirectory(P("docs"));
        File.WriteAllText(P("docs/a.html"), "<script type=importmap data-husk></script><script type=module src=\"../main.js\"></script>");
        File.WriteAllText(P("importmap.json"), "{\"imports\":{\"/app/main.js\":\"/app/main.h.js\"}}\n");
        File.WriteAllText(P("modulegraph.json"), "{\"/app/main.h.js\":[]}\n");

        Assert.True(new HuskPages
        {
            BuildEngine = new FakeBuildEngine(),
            Pages = [new TaskItem(P("docs/a.html"), new Dictionary<string, string> { ["PareOutputPath"] = P("out/docs/a.html"), ["PareRelativePath"] = "docs/a.html" })],
            ImportMap = P("importmap.json"),
            Graph = P("modulegraph.json"),
            BasePath = "app",
        }.Execute());

        Assert.Equal("<script type=importmap data-husk>{\"imports\":{\"/app/main.js\":\"/app/main.h.js\"}}</script>\n<link rel=\"modulepreload\" href=\"/app/main.h.js\">"
            + "<script type=module src=\"/app/main.h.js\"></script>", File.ReadAllText(P("out/docs/a.html")));
    }

    [Fact]
    public void A_missing_import_map_is_named_as_the_cause()
    {
        var engine = new FakeBuildEngine();

        Assert.False(new HuskPages { BuildEngine = engine, Pages = [], ImportMap = P("importmap.json") }.Execute());

        var error = Assert.Single(engine.Errors);
        Assert.Equal(P("importmap.json"), error.File);
        Assert.StartsWith("cannot read the import map", error.Message, StringComparison.Ordinal);
    }
}
