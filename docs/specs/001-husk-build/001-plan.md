# 001 — Plan
Spec: ./001-spec.md
Reviewed: 2026-10-17 by a Fable design review (13 findings, all folded in below).

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
(R4), at the cost of a few import map entries per source. T6 replaces those entries with
content-hashed ones.

Two alternatives were rejected:
- **Rewriting `.svelte` specifiers to `.svelte.js` in the output:** it needs no map entries, but
  the output no longer matches the reference, and every rewrite is a parser Husk owns.
- **Serving `App.svelte.js` at `/App.svelte`:** it removes the component entries, but not the
  `.ts` ones the compiler leaves, and T6 needs the map anyway.

## Tech / dependencies
| Package | Version | Where | Status |
|---|---|---|---|
| `Microsoft.ClearScript.V8` + `.Native.{osx-arm64,osx-x64,linux-x64,linux-arm64,win-x64,win-arm64}` | 7.5.1.1 | `Extrode.Husk.Compiler`, packed into Husk.Build's task folder | approved 2026-10-17 |
| `Extrode.Pare.Build`, `Extrode.Pare.AspNetCore`, `Extrode.Pare` (library) | 0.6.0 | package dependencies; the library strips `.svelte.ts` before `compileModule` | approved 2026-10-17; vendored under `packages/` from the GitHub release (not on nuget.org) |
| `Microsoft.Build.Framework`, `Microsoft.Build.Utilities.Core` | 18.9.6 | task, `PrivateAssets` | approved 2026-10-17 |
| `xunit.v3` | 4.0.2 | tests only | approved 2026-10-17 |
| Browser driver with no Node: `Selenium.WebDriver` | 4.51.0 | tests only: Chrome, Firefox, Safari (WebKit, macOS) | approved 2026-10-17. Playwright is out: its .NET package bundles and runs Node |
| Svelte compiler and runtime | 5.57.2 | vendored files, not a package | pinned by AGENTS.md |

- .NET 10 SDK, `global.json` as in Pare (`10.0.100`, `latestFeature`, Microsoft.Testing.Platform).
- The runtime's dependencies (`esm-env`, `clsx`) are vendored at the versions `svelte` 5.57.2's
  `package.json` names; they are read from it, not guessed.
- `compileModule` parses without TypeScript (`2-analyze/index.js:263`,
  `parse(source, comments, false, false)`), so `.svelte.ts` is stripped by Pare first. Pare keeps
  every line and column, so map positions hold. The map's `sourcesContent` is replaced with the
  original `.ts`, so devtools show the TypeScript rather than the stripped text.
- Safari's driver needs a one-time `sudo safaridriver --enable` on the test machine. WebKit is
  covered on macOS only; Chrome and Firefox everywhere.

## Changes by area
- `third-party/svelte-5.57.2/`: the compiler bundle (`compiler/index.js`), the runtime `src/` files,
  `esm-env` and `clsx`, and their licenses. `scripts/vendor-svelte.cs` (R3):
  - fetches the npm tarballs over HTTPS and checks each `integrity` sha512 against a committed
    lock before unpacking, with no Node involved;
  - writes `runtime-imports.json`, the runtime half of the import map, from `svelte`'s
    `package.json` `exports` (browser and default conditions) and `imports` (`#client/constants`).
    That covers every runtime subpath, `transition`, `animate`, `motion`, `easing`, `legacy`,
    `attachments` and `action` included, not only the spike's 13 (R1, R6).
- `src/Extrode.Husk.Compiler/`:
  - `EnginePool` holds V8 engines with the compiler loaded and reuses them while the process
    lives, so the Development app's engines stay warm (R10). A `dotnet build` starts them once
    per build, about 200 ms measured (architecture 001); R8 keeps the rest to changed files. It
    uses one engine per worker, up to `min(cores, ceil(sources / 8))` (R9), and frees engines
    after 10 idle minutes.
  - `SvelteCompiler.Compile(path, text, options)` returns JS, CSS, the JS map, warnings and
    errors. `compile` serves `.svelte`; `compileModule` serves `.svelte.js` and Pare-stripped
    `.svelte.ts` (R1, R2).
  - Options: `filename` is the project-relative path with `/` separators, and `rootDir` is not
    passed. The CSS scope hash is `hash(filename)` after Svelte strips `rootDir`, which Svelte
    normalizes and `filename` it does not (`validate-options.js:77`, `state.js:165-173`), so
    this is the only form that hashes the same on Windows as in the macOS reference (R4). Also
    `css: "external"`, and `dev` from `HuskDev` (R12).
  - Identity (R4) is defined on `js.code`, `css.code` and the map. Husk then appends
    `//# sourceMappingURL=<name>.js.map` to the JS file (R7); the compiler already writes
    `sourcesContent` (`3-transform/index.js:39`).
  - `StylesheetWriter` joins every component's CSS in ordinal path order into one file (R5).
    `ImportMapWriter` writes the import map (R6).
- `src/Extrode.Husk.Build/`: the `HuskCompile` task (C#), and
  `build/Extrode.Husk.Build.{props,targets}`.
  - props: default items, under `EnableHuskDefaultItems`.
    - `wwwroot/**/*.svelte` -> `.svelte.js`, `.svelte.css`, `.svelte.js.map`: three
      `PareCompileSource` items per component, one output each, as Pare's README asks.
    - `wwwroot/**/*.svelte.{ts,js}` -> `.js` and `.js.map`.
  - targets:
    - `TypeScriptSource Remove="**/*.svelte.ts"`. It sits in `.targets`, not `.props`, because
      every package's props import before any package's targets, so the Remove sees Pare's
      Include whatever order NuGet imports the two packages' props. Without it, Pare would also
      strip them, and PARE201 would fire (R2).
    - `HuskCompile`, in `PareCompileDependsOn`. Its `Inputs` are the sources, plus the task
      assembly, the vendored `compiler/index.js` and the `.targets` file, as Pare lists its own.
      A Svelte bump or Husk update then rebuilds everything (R8, R4).
    - Every compiled source also gets a `.svelte.css` output, empty when the component has no
      styles, so `Outputs` stays satisfied.
    - The joined `husk.css` and `importmap.json` live under `obj/<cfg>/<tfm>/husk/`, outside
      Pare's output folder, so Pare's stale deletion (`_PareDeleteStaleOutputs`) leaves them
      alone. They are registered as static web assets with `DefineStaticWebAssets`, as Pare does
      (`_PareDefineStaticWebAssets`), at `/_husk/husk.css`. The runtime files go the same way, at
      `/_husk/svelte/...` (R14). Both files are rebuilt when any source is.
    - `UsingTask` runs in-process under `dotnet build` and with `TaskHostFactory Runtime="NET"`
      under `MSBuild.exe`, as Pare does.
    - A platform gate on `$(NETCoreSdkRuntimeIdentifier)`, the build machine and not the publish
      target: `error HUSK001` outside the six RIDs. Publishing `-r linux-musl-x64` from macOS
      still works (R15, decision 006).
    - `Extrode.Husk.development.json` next to the app, never published, for R11. It records:
      - the compiler assembly's path;
      - `dev`;
      - where `husk.css` and the import map live;
      - the source list.
- Pages: an HTML page under `wwwroot` holding `<script type="importmap" data-husk></script>` is a
  `PareCompileSource` with compiler `husk-page`. Its output, at the same URL, has the map inlined,
  since browsers do not load import maps by `src`. The page links `/_husk/husk.css` itself (R5,
  R6).
  - Default glob: `wwwroot/**/*.html`, filtered by the task to pages with the marker. Pages
    without it are copied unchanged.
  - The page target's `Inputs` include every `PareCompileSource` and the runtime list, so adding a
    component rewrites the page's map.
- `src/Extrode.Husk.AspNetCore/`: `builder.AddHusk()` registers `IPareCompiler` `husk` and
  `husk-page`, then calls `AddPareDevelopment()`.
  - In Development it loads `Extrode.Husk.Compiler` from the path in
    `Extrode.Husk.development.json` into its own `AssemblyLoadContext`. The package references no
    ClearScript, so V8 never ships in the app (architecture 001: build time only).
  - On save it returns text for all three output paths, `""` for the CSS of a component without
    styles. Pare errors when any path is missing (`TypeScriptWatcher.cs:280-286`).
  - It rewrites `husk.css` from the CSS text it just returned plus the other components'
    `.svelte.css` on disk, because Pare writes the returned outputs only after the call returns.
  - A component that fails to compile is left as it was (R11, R13). Warnings are logged (R17).
  - Outside Development, `AddHusk` does nothing.
- Diagnostics, listed in `docs/error-codes.md`:
  - `File.svelte(line,col): error HUSK100: <svelte code>: <message>`, with the old outputs kept
    (R13);
  - `warning HUSK200: <svelte code>: <message>` for every compiler warning, at its position (R17).
- `samples/svelte-app/`: an ASP.NET Core app (R16) with:
  - `index.html` holding the marker, and `/` routed to it explicitly, since `MapStaticAssets`
    serves no default document;
  - `App.svelte` with runes, `$derived`, keyed `{#each}`, events, a child and a `fade` transition;
  - `Child.svelte` with styles;
  - `counter.svelte.ts`, a rune module imported as `./counter.svelte` by two components;
  - a `.ts` helper stripped by Pare.

  It references Husk through a local feed, as Pare's `samples/WebApp` does.
- `tests/Extrode.Husk.Tests/` (unit) and `tests/Extrode.Husk.Integration/` (build, publish,
  `dotnet watch`, and the browsers through WebDriver: Chrome, Firefox, Safari). Each criterion is
  break-checked.
- `scripts/node-reference/`: `reference.mjs` from the spike, extended to CSS, maps, modules and the
  same `filename` rule. It writes `expected.tsv` (path, SHA-256 of `js.code`, of `css.code`, of the
  map, or the error code). This is answer 3: Node runs here by hand when the pin moves, never in a
  build or a test.

## Data / interfaces
- `PareCompileSource` metadata set by Husk: `PareCompiler` `husk` | `husk-page`,
  `PareOutputExtension` `.svelte.js` | `.svelte.css` | `.svelte.js.map` | `.js` | `.js.map` | `.html`.
- `importmap.json`, `{ "imports": { ... } }`, with three groups of entries:
  - **Runtime:** the entries from `runtime-imports.json`, with `esm-env/development` mapped to its
    `false` file when `HuskDev` is false (R12).
  - **Each compiled source:** keys for its URL with its extension (`/x/Child.svelte`), without
    it (`/x/Child`), and for a `.svelte.ts` the `.svelte` form (`/x/counter.svelte`). Each maps to
    the output URL.
  - **Each Pare `.ts` output:** the same with and without the extension, because
    `<script lang="ts">` imports keep `.ts` or omit it. Pare already rewrites a `.ts` file's own
    imports (`StripTypeScript.cs:56`), so those need nothing.
- `Extrode.Husk.development.json`: `{ "compiler": "<path>", "dev": true, "stylesheet": "<path>",
  "importMap": "<path>", "sources": ["..."] }`.
- MSBuild properties: `HuskDev` (default `true` for Debug, otherwise `false`) and
  `EnableHuskDefaultItems`.

## Answers to the spec's design questions
1. **R10's session:** the running Development app under `dotnet watch`. The engines in a
   `dotnet build` process die with it: a single-project build runs in the CLI's own process. So
   the build pays one engine start, about 200 ms, and R8 keeps the rest to changed files.
2. **R11's reload channel:** `dotnet watch`'s browser refresh. It reloads when a `.js` static
   asset changes and applies `.css` changes in place. Husk adds no channel.
3. **R4's reference:** `scripts/node-reference/`, run by hand, with its hashes committed. The
   corpus:
   - the sample and Husk's own fixtures, committed;
   - the Svelte repo's components, fetched with Pare's `get-bench-corpus.cs`;
   - saaaat.com and riwalqalb.com, when present locally.

   Tests over a missing corpus are skipped with a message, not passed.

## Risks & mitigations
- **Loading V8 from the build package into the app in Development** is untested: native library
  lookup, a second ClearScript in the process, `PublishAot` apps. The first task is a spike of
  exactly this. Its fallback: the dev compiler runs the compiler assembly in a child process the
  app starts.
- **Import map entries for source URLs** depend on browsers matching URL-like keys after
  resolution, extensionless keys included. Checked in all three browsers in the first sample test,
  before anything builds on it.
- **Package size:** `Extrode.Husk.Build` carries six V8 natives (11-16 MB each as nupkgs), ICU
  (15 MB), Newtonsoft.Json (11 MB) and ClearScript (11 MB): about 100 MB. NuGet cannot pick the
  host RID's native at restore, so every restore pays it once per machine (architecture 001).
  Stated in the README; per-RID packages are a later option.
- **Runtime dev modules:** the unbundled runtime's `src/internal/client/dev/*` files are served in
  every configuration. Only `esm-env/development` gates them. They are inert in Release, and the
  R12 criterion is worded to match.
- **Stylesheet order:** ordinal path order is not Vite's import order. Rules with equal
  specificity in two components could cascade differently. Documented; T6 can order by import
  graph.
- **Input handling:** sources are the developer's own files, and nothing here takes network or
  user input at run time. The vendor script downloads over HTTPS and checks every integrity hash
  before unpacking.
- **Licenses:** Svelte, `esm-env` and `clsx` are MIT; their license texts ship in the package and
  are listed in a NOTICE.

## Coverage check
| R | Covered by |
|---|---|
| R1 | Compiler `compile`, default items, `HuskCompile`, runtime map from `exports` |
| R2 | `compileModule`, Pare strip of `.svelte.ts`, `TypeScriptSource Remove` in targets |
| R3 | `third-party/svelte-5.57.2`, `vendor-svelte.cs`, packed into Husk.Build |
| R4 | One compiler assembly; `filename` rule; identity on the compiler result; compiler in `Inputs`; `scripts/node-reference` + tests |
| R5 | `.svelte.css` outputs, `StylesheetWriter`, `/_husk/husk.css` |
| R6 | `ImportMapWriter` (runtime, extension and extensionless keys), `husk-page` inlining with all sources in `Inputs` |
| R7 | `.svelte.js.map` output, `sourceMappingURL`, original `.ts` as `sourcesContent` |
| R8 | `Inputs`/`Outputs` on `HuskCompile` |
| R9 | `EnginePool` parallel compile |
| R10 | `EnginePool` in the Development app, answer 1 |
| R11 | `Extrode.Husk.AspNetCore`, `IPareCompiler`, `dotnet watch` refresh, answer 2 |
| R12 | `HuskDev`, `dev` option, `esm-env/development` mapping |
| R13 | `HUSK100` with file/line/col, old outputs kept (task and dev) |
| R14 | Pare 0.6.0 for compiled outputs; `DefineStaticWebAssets` for `husk.css`, map, runtime |
| R15 | `HUSK001` gate on `NETCoreSdkRuntimeIdentifier` |
| R16 | `samples/svelte-app` |
| R17 | `HUSK200` warnings in build and Development log |

No requirement is uncovered.
