using Extrode.Husk.Compiler;

namespace Extrode.Husk.Tests;

public sealed class PageWriterTests
{
    private const string Map = "{\n  \"imports\": {}\n}\n";

    [Theory]
    [InlineData("<script type=\"importmap\" data-husk></script>", "<script type=\"importmap\" data-husk>")]
    [InlineData("<script data-husk type='importmap'>\n  </script >", "<script data-husk type='importmap'>")]
    [InlineData("<SCRIPT type=importmap data-husk=\"\"></SCRIPT>", "<SCRIPT type=importmap data-husk=\"\">")]
    public void The_marker_gets_the_map_between_its_tags(string marker, string open) =>
        Assert.Equal($"<head>{open}{{\n  \"imports\": {{}}\n}}</script>\n</head>", PageWriter.Inline($"<head>{marker}\n</head>", Map));

    [Theory]
    [InlineData("<script type=\"importmap\"></script>")]
    [InlineData("<script type=\"module\" data-husk></script>")]
    [InlineData("<script type=\"importmap\" data-husky></script>")]
    [InlineData("<script type=\"importmap\" data-husk>{}</script>")]
    [InlineData("<p>no scripts</p>")]
    public void A_page_without_the_marker_is_left_alone(string html) =>
        Assert.Null(PageWriter.Inline(html, Map));

    [Fact]
    public void Only_the_first_marker_is_filled() =>
        Assert.Equal("<script type=importmap data-husk>{}</script><script type=importmap data-husk></script>",
            PageWriter.Inline("<script type=importmap data-husk></script><script type=importmap data-husk></script>", "{}\n"));
}
