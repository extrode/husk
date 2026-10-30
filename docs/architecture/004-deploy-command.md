# 004 Deploy command: Workers static assets without Node

Written 2026-10-30, without web access. Code: [src/Extrode.Husk.Deploy](../../src/Extrode.Husk.Deploy).

`Extrode.Husk.Deploy` uploads a static output folder to Cloudflare through the Workers static
assets direct-upload API, as outlined in [002](002-cloudflare-and-deno.md#cloudflare). HttpClient
and System.Text.Json only: no Node, no wrangler, no package.

```
CLOUDFLARE_API_TOKEN=... dotnet run --project src/Extrode.Husk.Deploy -- \
  --dir <folder> --script <name> --account <id> --compatibility-date <yyyy-mm-dd> [--dry-run]
```

- The token is read from `CLOUDFLARE_API_TOKEN` only. It is never an argument and never printed;
  neither are the upload and completion JWTs.
- `--dry-run` prints each file's size and path, then the file count and total bytes, and sends
  nothing. It needs no token.
- A non-2xx response stops the deploy with `<step> failed: HTTP <status>`, steps being
  `assets-upload-session`, `assets upload` and `script deploy`. The response body is not printed.
- The base URL must be https. Only the library (`Deployer`, `Program.Run`) takes the override
  that allows http, for the tests' loopback server; the CLI has no way to set it.
- Exit codes: 0 deployed or dry run, 1 a step failed, 2 bad arguments or no token.

## Sequence

1. `POST accounts/:account/workers/scripts/:script/assets-upload-session`, `Authorization: Bearer
   <API token>`, body `{"manifest": {"/path": {"hash": ..., "size": n}}}`. The response carries an
   upload JWT and the buckets of hashes still needed. Files whose hash is in no bucket are already
   on Cloudflare and are skipped. No buckets: the JWT is already the completion token.
2. `POST accounts/:account/workers/assets/upload?base64=true` once per bucket, `Authorization:
   Bearer <upload JWT>`, multipart, one part per hash with the file's base64 content. The last
   response that carries a JWT gives the completion token.
3. `PUT accounts/:account/workers/scripts/:script`, `Authorization: Bearer <API token>`, multipart
   with one `metadata` part holding the compatibility date and the assets JWT and config.

## Assumptions to verify against Cloudflare's docs

Every one is a constant or a single method in `CloudflareApi.cs`.

- ASSUMPTION: base URL `https://api.cloudflare.com/client/v4/`, paths under
  `accounts/:account_id/`.
- ASSUMPTION: the session and the script PUT authenticate with `Authorization: Bearer <API token>`;
  the bucket uploads with `Bearer <upload JWT>`.
- ASSUMPTION: hash is SHA-256 over the UTF-8 of `base64(content) + extension-without-dot`, lowercase
  hex, first 32 characters. Wrangler uses BLAKE3 with the same recipe; .NET has no BLAKE3 without a
  package. Assumed: Cloudflare only checks the 32-hex shape and uses the hash as an identity, so a
  different function is accepted but never matches hashes wrangler uploaded (first deploy after
  switching uploads everything).
- ASSUMPTION: manifest keys are paths rooted at `/` with forward slashes, relative to `--dir`;
  every file under the folder is included (no `.assetsignore`, no size limit check).
- ASSUMPTION: session request JSON is `{"manifest": {...}}` with `hash` and `size` (bytes) per
  entry.
- ASSUMPTION: responses wrap the payload in `result`: `result.jwt` (string) and `result.buckets`
  (array of arrays of hashes). Empty or missing `buckets` means nothing to upload and `result.jwt`
  is the completion token.
- ASSUMPTION: the completion token is the `result.jwt` of the last upload response that has one;
  intermediate responses may carry none. Any 2xx counts as success (201/202 are not told apart).
- ASSUMPTION: upload parts are `Content-Disposition: form-data; name="<hash>";
  filename="<hash>"`, body the base64 text, and `Content-Type` the file's MIME type by extension
  (a 15-entry table, else `application/octet-stream`). Unknown whether Cloudflare reads the part
  type or infers the type from the manifest path when serving.
- ASSUMPTION: the script PUT is `multipart/form-data` with a single part `name="metadata"`,
  `Content-Type: application/json`, body
  `{"compatibility_date": ..., "assets": {"jwt": ..., "config": {...}}}`, and no `main_module` and
  no script part: an assets-only Worker.
- ASSUMPTION: the minimal assets config is Cloudflare's defaults written out:
  `{"html_handling": "auto-trailing-slash", "not_found_handling": "none"}`. A single-page app
  would want `"not_found_handling": "single-page-application"`; there is no flag for it.

## Tests

`tests/Extrode.Husk.Tests/DeployTests.cs` runs the CLI against an `HttpListener` on loopback and
pins the three requests (methods, paths, auth headers, bodies), the skip of files the session does
not ask for, the no-bucket path, dry run sending nothing, the token and JWTs never printed, a failing
upload naming its step and status, and the https refusal.
