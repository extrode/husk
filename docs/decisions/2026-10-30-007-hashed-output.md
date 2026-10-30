# 007 Hashed output: Husk writes hashed copies, registered as static web assets

Date: 2026-10-30. Status: accepted (T6, plan 002).

## Context

T6 needs content-hashed names for compiled modules and runtime files, an import map that points at
them, and `<link rel="modulepreload">` for a marked page's module graph. Pare places each output at
its source's path, so the hashed name cannot come from Pare's output path. Two options:

- (a) The SDK's fingerprinting. It already gives Husk's and Pare's assets an optional fingerprint
  (`App.svelte#[.{fingerprint}].js`): the sample's endpoints list `App.svelte.g8ih7n992i.js`, but
  the file on disk and in a publish stays `App.svelte.js`. The hashed name is a route that
  `MapStaticAssets` answers, nothing more.
- (b) Husk hashes each module's bytes, writes `name.<hash>.js` copies under
  `obj/<cfg>/<tfm>/husk/hashed/`, and registers them as static web assets.

## Decision

(b). Reasons:

- Cloudflare serves files, not ASP.NET Core endpoints, and T7 uploads the publish folder. Under (a)
  the hashed names exist only while an ASP.NET Core app serves them; the published files keep
  their plain names.
- The SDK computes its fingerprints when it defines the assets, after Husk wrote the import map
  and the pages. Under (a) Husk would have to read the SDK's endpoints back and write the map and
  pages later in the build, against the SDK's internal stage order.
- (b) uses only what Husk already does: hash bytes, write a file when it is missing, register a
  folder with `DefineStaticWebAssets`. The copies are the compiled bytes unchanged (R4, L001); only
  file names and map entries differ.

Each hash is the first 10 hex digits of the file's SHA-256. The map, not the module text, carries
it: a module's specifiers stay as the compiler wrote them, the map sends each source URL, plain
output URL and runtime specifier to the hashed URL, so editing one module renames that file alone.
A marked page's `<script type="module" src>` that the map names is pointed at the hashed URL too,
since browsers apply the map to imports, not to `src`.

## Development

Hashed names, the rewritten `src` and modulepreload apply when `HuskHashNames` is true: by default
in every configuration but Debug, so Release builds and publishes get them, and `dotnet watch`
(Debug, `AddHusk`) keeps plain names. A save in Development rewrites the plain output only, and a
map pointing at hashed copies would keep serving the old ones. With `HuskHashNames` set true in a
Development run, a save shows only after the next build.

## Consequences

- Plain outputs and runtime files stay served beside the hashed ones; a publish carries both.
- `husk.css` keeps its name: the page links it itself, and nothing rewrites that link.
- modulepreload lists come from a static scan of each module's `import`/`export ... from`
  specifiers. A missed import costs a preload, not correctness.
