using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace Extrode.Husk.Deploy;

/// <summary>Everything the deploy assumes about Cloudflare's API, in one place. Each member is an
/// ASSUMPTION listed in docs/architecture/004, to check against Cloudflare's docs.</summary>
internal static class CloudflareApi
{
    public static readonly Uri BaseUrl = new("https://api.cloudflare.com/client/v4/");

    public static string SessionPath(string account, string script) =>
        $"accounts/{Uri.EscapeDataString(account)}/workers/scripts/{Uri.EscapeDataString(script)}/assets-upload-session";

    public static string UploadPath(string account) =>
        $"accounts/{Uri.EscapeDataString(account)}/workers/assets/upload?base64=true";

    public static string ScriptPath(string account, string script) =>
        $"accounts/{Uri.EscapeDataString(account)}/workers/scripts/{Uri.EscapeDataString(script)}";

    // Session request: {"manifest": {"/path": {"hash": ..., "size": n}}}.
    public const string ManifestField = "manifest";
    public const string HashField = "hash";
    public const string SizeField = "size";

    // Responses: {"result": {"jwt": ..., "buckets": [[hash, ...], ...]}}.
    public const string ResultField = "result";
    public const string JwtField = "jwt";
    public const string BucketsField = "buckets";

    // Script PUT: a multipart part "metadata" holding
    // {"compatibility_date": ..., "assets": {"jwt": ..., "config": {...}}}.
    public const string MetadataPart = "metadata";
    public const string CompatibilityDateField = "compatibility_date";
    public const string AssetsField = "assets";
    public const string ConfigField = "config";

    /// <summary>Cloudflare's defaults, written out so the deploy does not depend on them.</summary>
    public static JsonObject AssetsConfig() => new()
    {
        ["html_handling"] = "auto-trailing-slash",
        ["not_found_handling"] = "none",
    };

    public const int HashHexLength = 32;

    /// <summary>Wrangler's recipe with SHA-256 in place of BLAKE3: the hash of the base64 content
    /// followed by the extension without its dot, as lowercase hex, cut to
    /// <see cref="HashHexLength"/>.</summary>
    public static string Hash(byte[] content, string fileName)
    {
        string extension = Path.GetExtension(fileName).TrimStart('.');
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(Convert.ToBase64String(content) + extension));
        return Convert.ToHexStringLower(digest)[..HashHexLength];
    }

    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".html"] = "text/html",
        [".js"] = "text/javascript",
        [".mjs"] = "text/javascript",
        [".css"] = "text/css",
        [".json"] = "application/json",
        [".map"] = "application/json",
        [".webmanifest"] = "application/manifest+json",
        [".svg"] = "image/svg+xml",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".webp"] = "image/webp",
        [".ico"] = "image/x-icon",
        [".woff2"] = "font/woff2",
        [".txt"] = "text/plain",
    };

    /// <summary>The content type of an upload part, by extension.</summary>
    public static string ContentType(string path) =>
        ContentTypes.GetValueOrDefault(Path.GetExtension(path), "application/octet-stream");
}
