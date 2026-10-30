using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Extrode.Husk.Deploy;

/// <summary>A step answered with a status outside 2xx. The message names the step and the
/// status, never a token or a response body.</summary>
internal sealed class DeployException(string step, int status) : Exception($"{step} failed: HTTP {status}")
{
    public string Step { get; } = step;
    public int Status { get; } = status;
}

internal sealed record DeployResult(int Uploaded, int Skipped);

/// <summary>The three steps of the Workers static assets direct upload: open a session with the
/// manifest, upload the buckets of files the session still needs, then PUT the script with the
/// completion token.</summary>
internal sealed class Deployer
{
    public const string SessionStep = "assets-upload-session";
    public const string UploadStep = "assets upload";
    public const string ScriptStep = "script deploy";

    private readonly HttpClient _http;
    private readonly Uri _baseUrl;
    private readonly string _token;

    /// <param name="allowInsecureBaseUrl">For tests against a local http server only; the CLI
    /// never sets it.</param>
    public Deployer(HttpClient http, Uri baseUrl, string token, bool allowInsecureBaseUrl = false)
    {
        if (baseUrl.Scheme != Uri.UriSchemeHttps && !allowInsecureBaseUrl)
            throw new ArgumentException($"The API base URL must be https, not {baseUrl.Scheme}.", nameof(baseUrl));
        _http = http;
        _baseUrl = baseUrl;
        _token = token;
    }

    public async Task<DeployResult> DeployAsync(string account, string script, string compatibilityDate,
        IReadOnlyList<AssetFile> files, CancellationToken cancellationToken = default)
    {
        var manifest = new JsonObject();
        foreach (var file in files)
            manifest[file.Path] = new JsonObject { [CloudflareApi.HashField] = file.Hash, [CloudflareApi.SizeField] = file.Size };
        var session = await Send(SessionStep, HttpMethod.Post, CloudflareApi.SessionPath(account, script), _token,
            Json(new JsonObject { [CloudflareApi.ManifestField] = manifest }), cancellationToken);

        string? jwt = Jwt(session);
        List<List<string>> buckets = Buckets(session);
        string? completion = buckets.Count == 0 ? jwt : null;
        var byHash = new Dictionary<string, AssetFile>(StringComparer.Ordinal);
        foreach (var file in files)
            byHash.TryAdd(file.Hash, file);
        var needed = new HashSet<string>(StringComparer.Ordinal);

        foreach (var bucket in buckets)
        {
            if (jwt is null)
                throw new InvalidOperationException($"{SessionStep} returned buckets and no upload token.");
            using var form = new MultipartFormDataContent();
            foreach (string hash in bucket)
            {
                if (!byHash.TryGetValue(hash, out var file))
                    throw new InvalidOperationException($"{SessionStep} asked for {hash}, which is not in the manifest.");
                needed.Add(hash);
                var part = new ByteArrayContent(Encoding.ASCII.GetBytes(Convert.ToBase64String(await File.ReadAllBytesAsync(file.FullPath, cancellationToken))));
                part.Headers.ContentType = new MediaTypeHeaderValue(CloudflareApi.ContentType(file.Path));
                part.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = $"\"{hash}\"", FileName = $"\"{hash}\"" };
                form.Add(part);
            }
            completion = Jwt(await Send(UploadStep, HttpMethod.Post, CloudflareApi.UploadPath(account), jwt, form, cancellationToken)) ?? completion;
        }
        if (completion is null)
            throw new InvalidOperationException($"{UploadStep} returned no completion token.");

        var metadata = new JsonObject
        {
            [CloudflareApi.CompatibilityDateField] = compatibilityDate,
            [CloudflareApi.AssetsField] = new JsonObject
            {
                [CloudflareApi.JwtField] = completion,
                [CloudflareApi.ConfigField] = CloudflareApi.AssetsConfig(),
            },
        };
        using var scriptForm = new MultipartFormDataContent();
        var metadataPart = Json(metadata);
        metadataPart.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = $"\"{CloudflareApi.MetadataPart}\"" };
        scriptForm.Add(metadataPart);
        await Send(ScriptStep, HttpMethod.Put, CloudflareApi.ScriptPath(account, script), _token, scriptForm, cancellationToken);

        int skipped = files.Count(f => !needed.Contains(f.Hash));
        return new DeployResult(files.Count - skipped, skipped);
    }

    private async Task<JsonElement> Send(string step, HttpMethod method, string path, string bearer, HttpContent content,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, new Uri(_baseUrl, path)) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new DeployException(step, (int)response.StatusCode);
        string text = await response.Content.ReadAsStringAsync(cancellationToken);
        if (text.Length == 0)
            return default;
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }

    private static ByteArrayContent Json(JsonNode node)
    {
        var content = new ByteArrayContent(Encoding.UTF8.GetBytes(node.ToJsonString()));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return content;
    }

    private static bool Result(JsonElement root, out JsonElement result)
    {
        result = default;
        return root.ValueKind == JsonValueKind.Object && root.TryGetProperty(CloudflareApi.ResultField, out result)
            && result.ValueKind == JsonValueKind.Object;
    }

    private static string? Jwt(JsonElement root) =>
        Result(root, out var result) && result.TryGetProperty(CloudflareApi.JwtField, out var jwt) && jwt.ValueKind == JsonValueKind.String
            ? jwt.GetString()
            : null;

    private static List<List<string>> Buckets(JsonElement root)
    {
        var buckets = new List<List<string>>();
        if (!Result(root, out var result) || !result.TryGetProperty(CloudflareApi.BucketsField, out var array)
            || array.ValueKind != JsonValueKind.Array)
            return buckets;
        foreach (var bucket in array.EnumerateArray())
        {
            var hashes = bucket.EnumerateArray().Select(h => h.GetString()!).ToList();
            if (hashes.Count > 0)
                buckets.Add(hashes);
        }
        return buckets;
    }
}
