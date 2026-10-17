using System.Text;
using Extrode.Husk.Build;
using Microsoft.Build.Utilities;

namespace Extrode.Husk.Tests;

public sealed class HuskPagesTests : IDisposable
{
    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("husk-pages-");

    public void Dispose() => _dir.Delete(recursive: true);

    private string P(string relative) => Path.Combine(_dir.FullName, relative);

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
}
