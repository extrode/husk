# 001 — Plan
Spec: ./001-spec.md

## Approach
Husk is two NuGet packages on Pare 0.6.0's compile extension point. `Extrode.Husk.Build` adds
`PareCompileSource` items for `.svelte`, `.svelte.ts` and `.svelte.js` files, and a `HuskCompile`
target in `PareCompileDependsOn`. That target runs a C# task holding a pool of ClearScript V8
engines with the pinned Svelte compiler loaded. Pare places, serves, compresses, publishes,
embeds and deletes the outputs exactly as it does its own `.ts` output, so Husk writes no
plumbing of its own for compiled files (R14). `Extrode.Husk.AspNetCore` registers an
`IPareCompiler` named `husk`, so Pare's watcher recompiles on save in the running app, and
`dotnet watch`'s browser refresh reloads the page (R11). Pare already relies on that refresh for
`.ts`, and it is answer 2 below. The engine, compiler and option handling live in one assembly,
`Extrode.Husk.Compiler`, shared by the task and the dev compiler, so a build and a save produce
the same bytes.

Compiled code is not rewritten. A component's `import Child from './Child.svelte'` stays as the
compiler wrote it, and the generated import map maps each source URL to its output URL
(`/Components/Child.svelte` -> `/Components/Child.svelte.js`). It also maps `svelte/*`, `esm-env`
and `clsx` to the runtime files Husk ships. That keeps every compiled byte identical to Node's
(R4), at the cost of one import map entry per source. T6 replaces those entries with
content-hashed ones. The rejected alternative was rewriting `.svelte` specifiers to `.svelte.js`
in the output: no map entries, but the output no longer matches the reference, and every rewrite
is a parser Husk owns.

## Tech / dependencies
| Package | Version | Where | Status |
|---|---|---|---|
| `Microsoft.ClearScript.V8` + `.Native.{osx-arm64,osx-x64,linux-x64,linux-arm64,win-x64,win-arm64}` | 7.5.1.1 | `Extrode.Husk.Compiler`, packed into Husk.Build's task folder | approved for the spike only; **needs approval for the product** |
| `Extrode.Pare.Build`, `Extrode.Pare.AspNetCore` | 0.6.0 | package dependencies | own packages; vendored under `packages/` from the GitHub release (not on nuget.org) |
| `Extrode.Pare` (library) | 0.6.0 | `Extrode.Husk.Compiler`: strips `.svelte.ts` before `compileModule` | own package |
| `Microsoft.Build.Framework`, `Microsoft.Build.Utilities.Core` | 18.9.6 | task, `PrivateAssets` | Pare's versions; **needs approval** |
| `xunit.v3` 4.0.1, `Microsoft.Playwright` 1.63.0 | | tests only | Pare's versions; **needs approval** |
| Svelte compiler and runtime | 5.57.2 | vendored files, not a package | pinned by AGENTS.md |

- .NET 10 SDK, `global.json` as in Pare (`10.0.100`, `latestFeature`, Microsoft.Testing.Platform).
- Svelte's client runtime needs only `esm-env` and `clsx` (the spike's 13-entry import map). Their
  versions come from `svelte` 5.57.2's `package.json` when they are vendored. Not guessed here.
- `compileModule` parses without TypeScript (`parse(source, comments, false, false)` in
  `phases/2-analyze`), so `.svelte.ts` is stripped by Pare first. Pare keeps every line and
  column, so the source map still points at the `.ts` source.

## Changes by area
- `third-party/svelte-5.57.2/`: the compiler bundle (`compiler/index.js`), the runtime `src/` files
  the import map reaches, `esm-env` and `clsx`, and their licenses. `scripts/vendor-svelte.cs`
  fetches the npm tarballs over HTTPS, checks each `integrity` sha512 against a committed lock,
  and unpacks them. No Node involved. (R3)
- `src/Extrode.Husk.Compiler/`: `EnginePool` holds V8 engines with the compiler loaded and
  reuses them while the process lives (a static, so a reused MSBuild node or the app process
  keeps them warm). Parallel compiling uses one engine per worker, up to
  `min(cores, ceil(sources / 8))` (R9, R10). `SvelteCompiler.Compile(path, text, options)` returns
  JS, CSS, a JS map and diagnostics. `compile` serves `.svelte`; `compileModule` serves
  `.svelte.js` and Pare-stripped `.svelte.ts` (R1, R2). Options are `rootDir` = the project
  directory (the CSS hash depends on it, per architecture 001), `filename` relative to it,
  `css: "external"`, and `dev` from the configuration (R12). The JS map gets `sourcesContent`,
  so devtools show the source while no `.svelte` is published, and the JS gets a
  `//# sourceMappingURL` line (R7). `StylesheetWriter` joins the per-component CSS in ordinal
  path order into one file (R5). `ImportMapWriter` writes the import map (R6).
- `src/Extrode.Husk.Build/`: `HuskCompile` task (C#), and `build/Extrode.Husk.Build.{props,targets}`:
  - props, which load after Pare's since Husk depends on it:
    - default items `wwwroot/**/*.svelte` -> `.svelte.js`, `.svelte.css` and `.svelte.js.map`:
      three `PareCompileSource` items per component, one output each, as Pare's README asks;
    - `wwwroot/**/*.svelte.{ts,js}` -> `.js` and `.js.map`;
    - `TypeScriptSource Remove="**/*.svelte.ts"`, so Pare does not also strip them (PARE201
      otherwise).
  - targets:
    - `HuskCompile`, in `PareCompileDependsOn`, with `Inputs`/`Outputs` over the sources, so
      MSBuild hands the task only changed ones (R8).
    - The joined `husk.css` and `importmap.json` under `obj/<cfg>/<tfm>/husk/`, outside Pare's
      output folder so Pare's stale deletion leaves them alone. They are registered as static web
      assets with `DefineStaticWebAssets`, as Pare does (`_PareDefineStaticWebAssets`), at
      `/_husk/husk.css`. The runtime files go the same way, at `/_husk/svelte/...` (R14).
    - `UsingTask` in-process under `dotnet build`, `TaskHostFactory Runtime="NET"` under
      `MSBuild.exe`, as Pare does.
    - A platform gate: `error HUSK001` on any RID outside the six (R15).
    - `Extrode.Husk.development.json` next to the app, never published: the compiler assembly's
      path, `rootDir`, `dev`, and where `husk.css` and the import map live (for R11).
- Pages: an HTML page under `wwwroot` that holds `<script type="importmap" data-husk></script>`
  is a `PareCompileSource` with compiler `husk-page` (default glob `wwwroot/**/*.html`, filtered
  by the task to pages with the marker). Its output, at the same URL, has the map inlined, since
  browsers do not load import maps by `src`. The page links `/_husk/husk.css` itself (R5, R6).
  Pages without the marker are copied unchanged.
- `src/Extrode.Husk.AspNetCore/`: `builder.AddHusk()` registers `IPareCompiler` `husk` and
  `husk-page`, then calls `AddPareDevelopment()`. In Development it loads
  `Extrode.Husk.Compiler` from the path in `Extrode.Husk.development.json` into its own
  `AssemblyLoadContext`. The package itself references no ClearScript, so V8 never ships in the
  app (architecture 001: build time only). On save it returns the three outputs, rewrites
  `husk.css`, and leaves a component that fails to compile as it was (R11, R13). Outside
  Development, `AddHusk` does nothing.
- Errors: `HuskCompile` logs `File.svelte(line,col): error HUSK100: <svelte code>: <message>` and
  keeps the old outputs (R13). Codes are listed in `docs/error-codes.md`.
- `samples/svelte-app/`: an ASP.NET Core app with:
  - `index.html` holding the marker;
  - `App.svelte` with runes, `$derived`, keyed `{#each}`, events and a child;
  - `Child.svelte` with styles;
  - `counter.svelte.ts`, a rune module shared by two components;
  - a `.ts` helper stripped by Pare.

  It references Husk through a local feed, as Pare's `samples/WebApp` does (R16).
- `tests/Extrode.Husk.Tests/` (unit) and `tests/Extrode.Husk.Integration/` (build, publish, `dotnet
  watch` and Playwright in Chromium, Firefox and WebKit), each criterion break-checked.
- `scripts/node-reference/`: `reference.mjs` from the spike, extended to CSS, maps and modules.
  It writes `expected.tsv` (path, SHA-256 of `js.code`, of `css.code`, or the error code). This is
  answer 3: Node runs here by hand when the pin moves, never in a build or a test.

## Data / interfaces
- `PareCompileSource` metadata set by Husk: `PareCompiler` `husk` | `husk-page`,
  `PareOutputExtension` `.svelte.js` | `.svelte.css` | `.svelte.js.map` | `.js` | `.js.map` | `.html`.
- `importmap.json`: `{ "imports": { "svelte": "/_husk/svelte/src/index-client.js", ...,
  "/<source url>": "/<output url>" } }`. The runtime entries are the spike's 13. The source entries
  cover every compiled source and every Pare `.ts` output, because `<script lang="ts">` imports
  keep `.ts`.
- `Extrode.Husk.development.json`: `{ "compiler": "<path>", "rootDir": "...", "dev": true,
  "stylesheet": "<path>", "importMap": "<path>", "sources": ["..."] }`.
- MSBuild properties: `HuskDev` (default: `true` for Debug, otherwise `false`) and
  `EnableHuskDefaultItems`.

## Answers to the spec's design questions
1. **R10's session:** the 50 ms target binds to a reused MSBuild node under `dotnet build`, and
   to the app process under `dotnet watch`. It does not bind to `MSBuild.exe` (Visual Studio's
   task host starts a new process each build) or to `-nodeReuse:false`.
2. **R11's reload channel:** `dotnet watch`'s browser refresh. It reloads when a `.js` static
   asset changes and applies `.css` changes in place. Husk adds no channel.
3. **R4's reference:** `scripts/node-reference/`, run by hand, with its hashes committed.
   Corpus: the sample and Husk's own fixtures (committed), the Svelte repo's components (fetched
   with Pare's `get-bench-corpus.cs`), and saaaat.com and riwalqalb.com when present locally.
   Tests over a missing corpus are skipped with a message, not passed.

## Risks & mitigations
- **ClearScript is not yet approved for the product.** Nothing compiles without it. Ask first
  (Needs decision).
- **Loading V8 from the build package into the app in Development** is untested: native library
  lookup, a second ClearScript in the process, `PublishAot` apps. The first task is a spike of
  exactly this, with a fallback: the dev compiler runs the task assembly in a child process the
  app starts.
- **Import map entries for source URLs** depend on browsers matching URL-like keys after
  resolution. The spec says they do; checked in all three browsers in the first sample test
  before anything builds on it.
- **Stylesheet order:** ordinal path order is not Vite's order (import order). Rules with equal
  specificity in two components could cascade differently. Documented; T6 can order by graph.
- **An empty `.svelte.css` for a component without styles** is still written, so `Outputs` stays
  satisfied. Noise, but correct incrementality matters more.
- **The task holds V8 engines in a static:** memory in long-lived MSBuild nodes. Engines are
  capped at the pool size and freed after 10 idle minutes.
- **Input handling:** sources are the developer's own files, and nothing here takes network or
  user input at run time. The vendor script downloads over HTTPS and checks every integrity hash
  before unpacking.
- **Licenses:** Svelte, `esm-env` and `clsx` are MIT; their license texts ship in the package and
  are listed in a NOTICE.

## Coverage check
| R | Covered by |
|---|---|
| R1 | Compiler `compile`, default items, `HuskCompile` |
| R2 | `compileModule`, Pare strip of `.svelte.ts`, `TypeScriptSource Remove` |
| R3 | `third-party/svelte-5.57.2`, `vendor-svelte.cs`, packed into Husk.Build |
| R4 | One compiler assembly for build and dev, unmodified output, `scripts/node-reference` + tests |
| R5 | `.svelte.css` outputs, `StylesheetWriter`, `/_husk/husk.css` |
| R6 | `ImportMapWriter`, `husk-page` inlining, runtime static assets |
| R7 | `.svelte.js.map` output, `sourcesContent`, `sourceMappingURL` |
| R8 | `Inputs`/`Outputs` on `HuskCompile` |
| R9 | `EnginePool` parallel compile |
| R10 | Static `EnginePool`, answer 1 |
| R11 | `Extrode.Husk.AspNetCore`, `IPareCompiler`, `dotnet watch` refresh, answer 2 |
| R12 | `HuskDev`, `dev` option, `esm-env` mapping per configuration |
| R13 | `HUSK100` with file/line/col, old outputs kept (task and dev) |
| R14 | Pare 0.6.0 for compiled outputs; `DefineStaticWebAssets` for `husk.css`, map, runtime |
| R15 | `HUSK001` platform gate |
| R16 | `samples/svelte-app` |

No requirement is uncovered.
