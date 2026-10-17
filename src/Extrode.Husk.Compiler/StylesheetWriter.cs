using System.Text;

namespace Extrode.Husk.Compiler;

/// <summary>Joins every component's CSS into the one stylesheet an app links, in ordinal order of
/// the components' paths, so a build and a save write the same bytes.</summary>
public static class StylesheetWriter
{
    public static string Join(IEnumerable<(string Path, string Css)> components)
    {
        var text = new StringBuilder();
        foreach (var (path, css) in components.OrderBy(c => c.Path, StringComparer.Ordinal))
        {
            if (css.Length == 0)
                continue;
            text.Append("/* ").Append(path.Replace("*/", "*\\/", StringComparison.Ordinal)).Append(" */\n").Append(css);
            if (!css.EndsWith('\n'))
                text.Append('\n');
        }
        return text.ToString();
    }
}
