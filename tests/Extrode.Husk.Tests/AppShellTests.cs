using Extrode.Husk.Compiler;

namespace Extrode.Husk.Tests;

public sealed class AppShellTests
{
    private static readonly Dictionary<string, string> NoEnv = [];

    [Fact]
    public void The_head_and_body_placeholders_are_filled_and_the_rest_of_the_page_is_kept()
    {
        string html = AppShell.Build("<!doctype html>\n<html lang=\"ar\">\n<head><meta charset=\"utf-8\" />%sveltekit.head%</head>\n<body data-x>%sveltekit.body%</body></html>\n", "/", true, NoEnv);

        Assert.StartsWith("<!doctype html>\n<html lang=\"ar\">\n<head><meta charset=\"utf-8\" /><script>globalThis.__huskEnv = {", html);
        Assert.Contains("<script type=\"importmap\" data-husk></script>", html);
        Assert.Contains("<link rel=\"stylesheet\" href=\"/_husk/husk.css\">", html);
        Assert.Contains("<script type=\"module\" src=\"/_husk/start.js\"></script>", html);
        Assert.Contains("<body data-x><div id=\"husk-body\" style=\"display: contents\"></div></body>", html);
        Assert.DoesNotContain("%sveltekit", html);
    }

    [Fact]
    public void The_environment_has_the_vite_names_and_the_projects_values_sorted()
    {
        string html = AppShell.Build(AppShell.Default, "/app/", false, new Dictionary<string, string> { ["PUBLIC_BACKEND_URL"] = "https://api.example/</script>" });

        Assert.Contains("globalThis.__huskEnv = { \"BASE_URL\": \"/app/\", \"DEV\": false, \"MODE\": \"production\", \"PROD\": true, \"SSR\": false, \"PUBLIC_BACKEND_URL\": \"https://api.example/\\u003c/script>\", };", html);
        Assert.Contains("href=\"/app/_husk/husk.css\"", html);
    }

    [Fact]
    public void Other_placeholders_are_answered()
    {
        string html = AppShell.Build("%sveltekit.head%<img src=\"%sveltekit.assets%/a.png\" data-n=\"%sveltekit.nonce%\" data-u=\"%sveltekit.env.PUBLIC_X%\" data-m=\"%sveltekit.env.PUBLIC_MISSING%\">%sveltekit.body%", "/app/", false,
            new Dictionary<string, string> { ["PUBLIC_X"] = "x" });

        Assert.Contains("<img src=\"/app/a.png\" data-n=\"\" data-u=\"x\" data-m=\"\">", html);
    }

    [Theory]
    [InlineData("<html>%sveltekit.body%</html>")]
    [InlineData("<html>%sveltekit.head%</html>")]
    public void A_shell_missing_a_placeholder_is_refused(string template) =>
        Assert.Throws<FormatException>(() => AppShell.Build(template, "/", false, NoEnv));

    [Fact]
    public void An_env_placeholder_is_filled_raw_and_a_value_that_looks_like_a_placeholder_stays_text()
    {
        string html = AppShell.Build("%sveltekit.head%<script>const a = \"%sveltekit.env.PUBLIC_A%\"</script>%sveltekit.body%", "/", false,
            new Dictionary<string, string> { ["PUBLIC_A"] = "a?x=1&y=2", ["PUBLIC_B"] = "%sveltekit.env.PUBLIC_A%" });

        Assert.Contains("const a = \"a?x=1&y=2\"", html);
        Assert.Contains("\"PUBLIC_B\": \"%sveltekit.env.PUBLIC_A%\"", html);
    }
}
