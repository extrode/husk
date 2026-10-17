# Handoff 2026-10-17: Husk starts here

Written at the end of the session that ran the engine spike in Pare and created this repository.
Read this, then [the decision](../decisions/2026-10-17-001-separate-product.md), then the two
architecture notes.

## What Husk is

Svelte components for .NET projects with no Node or Deno at build time: the Svelte compiler runs
in an embedded V8 (ClearScript) inside MSBuild, components compile to ES modules, and the
`svelte` runtime is served unbundled behind an import map. It builds on Pare (`../pare`, a
TypeScript type stripper with an MSBuild package) and does not fork it.

## State

- Repository: `main` (empty root commit) and `dev`; nothing pushed, no remote. Layout from
  `~/.claude/scripts/install-layout.js`.
- No product code yet. `prototypes/001-engine-spike/` is the spike, runnable as its README says.
- Package approvals (the user, 2026-10-17): ClearScript for the spike; Jint for the comparison.
  Husk's own code adds no package without asking.

## Findings, in one place

- The Svelte 5.57.2 compiler runs unchanged in ClearScript V8 and Jint. Pass `rootDir` or CSS
  scope hashes differ from Node's.
- Output is byte-identical to Node on 4,587 + 201 files.
- V8 is 1.4-1.6x slower than Node, Jint 13-25x slower than V8. Code cache: no gain. Parallel
  engines: 1.4-2.2x on full rebuilds. Incremental compiles and a warm engine are what make edits
  fast (about 6 ms a component warm).
- Works inside `dotnet build` and Visual Studio's `MSBuild.exe` (.NET task host), on macOS and
  Windows x64.
- Output runs in Chromium; the unbundled runtime is 107 modules, 505 KB uncompressed.
- Costs: 44 MB per platform at build time, `Newtonsoft.Json` comes along, no linux-musl.
- Cloudflare: static output fits; deploy without wrangler via the Workers assets upload API;
  builds in GitHub Actions (Pages' build image has no .NET).
- saaaat.com: replaceable, needs a client router and `$app/*` shims. riwalqalb.com frontend:
  blocked by its Chrome 49 / Safari 11 floor. riwalqalb.com worker: stays JavaScript on workerd.

## Open decisions (the user's)

1. Browser floor for Husk output: import-map browsers only (Chrome 89, Safari 16.4, Firefox 108),
   or a downlevel path too. Decides whether riwalqalb's frontend can move.
2. First target app: saaaat.com (needs the router) or riwalqalb.com (needs the floor decision).
3. Type checking without Deno: keep `svelte-check`/`tsc` under a runtime in CI, or find a native
   checker.
4. linux-musl: unsupported, or a Jint fallback at 13-25x the build time.
5. Pare's extension point: its shape is designed in Pare, before Husk's build integration.

## Next steps

See `work/tasklist.md`. In order: Pare's extension point (in `../pare`), the Husk MSBuild package
(compile, incremental, warm engine, CSS, dev mode), import map and hashed output, then the router
if saaaat.com is first.
