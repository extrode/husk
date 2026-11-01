using System.Text.RegularExpressions;

namespace Extrode.Husk.Integration;

/// <summary>Reads the address a Kestrel app bound to from its "Now listening on:" log line, so a test
/// asks for port 0 and never picks a port that something else can take first.</summary>
public static partial class Listening
{
    public static Uri? Parse(string output)
    {
        var match = Line().Match(output);
        return match.Success ? new Uri(match.Groups[1].Value.TrimEnd('/') + "/") : null;
    }

    [GeneratedRegex(@"Now listening on:\s*(http://\S+)")]
    private static partial Regex Line();
}
