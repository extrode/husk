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
    [InlineData("<scriptx type=\"importmap\" data-husk></scriptx>")]
    [InlineData("<script-a type=\"importmap\" data-husk></script>")]
    public void A_page_without_the_marker_is_left_alone(string html) =>
        Assert.Null(PageWriter.Inline(html, Map));

    [Fact]
    public void Only_the_first_marker_is_filled() =>
        Assert.Equal("<script type=importmap data-husk>{}</script><script type=importmap data-husk></script>",
            PageWriter.Inline("<script type=importmap data-husk></script><script type=importmap data-husk></script>", "{}\n"));

    private const string HashedMap = """
        {
          "imports": {
            "/main.js": "/main.m.js",
            "/App.svelte": "/App.svelte.a.js",
            "svelte": "/_husk/index.s.js"
          }
        }

        """;

    private const string Graph = """
        {
          "/main.m.js": ["/_husk/index.s.js", "/App.svelte.a.js"],
          "/App.svelte.a.js": ["/_husk/index.s.js"],
          "/_husk/index.s.js": [],
          "/unreached.u.js": []
        }
        """;

    [Fact]
    public void A_quote_or_ampersand_in_the_hashed_urls_is_escaped_in_both_attributes()
    {
        const string Map = "{\"imports\": {\"/main.js\": \"/a\\\"b&c.js\"}}";
        const string Graph = "{\"/a\\\"b&c.js\": [\"/d&e.js\"], \"/d&e.js\": []}";

        string page = PageWriter.Inline("<script type=\"importmap\" data-husk></script><script type=\"module\" src=\"main.js\"></script>", Map, "/", Graph)!;

        Assert.Contains("src=\"/a&quot;b&amp;c.js\"", page);
        Assert.Contains("<link rel=\"modulepreload\" href=\"/d&amp;e.js\">", page);
        Assert.DoesNotContain("src=\"/a\"b", page);
    }

    [Fact]
    public void With_a_graph_the_entry_points_at_its_hashed_url_and_what_it_reaches_is_preloaded()
    {
        const string Page = "<head>\n  <script type=\"importmap\" data-husk></script>\n  <script type=\"module\" src=\"main.js\"></script>\n</head>";

        string page = PageWriter.Inline(Page, HashedMap, "/", Graph)!;

        Assert.Equal($"""
            <head>
              <script type="importmap" data-husk>{HashedMap.TrimEnd('\n')}</script>
              <link rel="modulepreload" href="/main.m.js">
              <link rel="modulepreload" href="/_husk/index.s.js">
              <link rel="modulepreload" href="/App.svelte.a.js">
              <script type="module" src="/main.m.js"></script>
            </head>
            """, page);
    }

    [Fact]
    public void With_a_graph_an_inline_module_script_s_imports_are_preloaded_and_its_text_kept()
    {
        const string Page = "<script type=importmap data-husk></script><script type=module>import App from '/App.svelte';</script><script src=\"/main.js\"></script>";

        Assert.Equal(
            $"<script type=importmap data-husk>{HashedMap.TrimEnd('\n')}</script>\n<link rel=\"modulepreload\" href=\"/App.svelte.a.js\">\n"
            + "<link rel=\"modulepreload\" href=\"/_husk/index.s.js\"><script type=module>import App from '/App.svelte';</script><script src=\"/main.js\"></script>",
            PageWriter.Inline(Page, HashedMap, "/", Graph));
    }

    [Fact]
    public void With_a_graph_a_page_without_the_marker_is_left_alone() =>
        Assert.Null(PageWriter.Inline("<script type=module src=/main.js></script>", HashedMap, "/", Graph));
}
