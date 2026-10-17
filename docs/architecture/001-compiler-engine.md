# 001 Compiler engine: ClearScript V8, not Jint

Measured 2026-10-17 on an Apple M1 Max (10 cores), .NET 10.0.12, ClearScript 7.5.1.1, Jint
4.17.0, Node 24.16.0, Svelte 5.57.2. Load average 2-9 from other work during the runs, so the
ratios hold better than the absolute numbers. Code: [prototypes/001-engine-spike](../../prototypes/001-engine-spike).

## The compiler runs unchanged

`svelte/compiler/index.js` is one 874 KB UMD bundle that sets `globalThis.svelte`. It runs in V8
and in Jint as is. One host duty: pass `rootDir`. The compiler defaults it to `process.cwd()`,
which neither engine has, and the CSS scope hash is computed from the path relative to it, so
without `rootDir` every scoped class name differs from Node's.

Correctness, with output compared as SHA-256 of `js.code`, or the error code:

| Input | Files | Size | Compile | Errors | Differ from Node |
|---|---:|---:|---:|---:|---:|
| Svelte repo's own `.svelte` files (Pare's bench corpus) | 4,587 | 1.1 MB | 3,926 | 661 | V8 0, Jint 0 of the 300 run |
| saaaat.com + riwalqalb.com components | 201 | 1.6 MB | 201 | 0 | V8 0, Jint 0 of the 20 run |

The 661 errors are the Svelte repo's error tests and 328 files that need `experimental.async`. The
two apps' components need no preprocessor: `vitePreprocess` in saaaat's config does nothing the
compiler does not.

## Speed

Times are milliseconds for one pass over the list.

| 201 app components (1.6 MB) | Engine start | Compiler load | First pass | Warm best |
|---|---:|---:|---:|---:|
| Node 24.16 | - | 28 | 1,238 | 921 |
| ClearScript V8 | 44 (874 the first time after boot) | 60 | 1,474 | 1,283 |
| ClearScript V8, code cache | 1 | 51 | 1,582 | 1,458 |
| ClearScript V8, 10 engines in parallel (start and load included) | | | 1,022 | 899 |
| Jint (20 of the 201) | 11 | 383 | 5,177 | 3,106 |

| 4,587 corpus files (1.1 MB) | First pass | Warm best |
|---|---:|---:|
| Node 24.16 | 1,637 | 1,085 |
| ClearScript V8 | 1,846 | 1,710 |
| ClearScript V8, 10 engines | 827 | 793 |
| Jint (first 300) vs V8 (first 300) | 3,564 vs 201 | 1,678 vs 132 |

What that says:

- **Jint is 13-25x slower than V8** (13x on the small corpus files, about 25x on real
  components, 155 ms a component warm). A full build of the two apps would take 30-50 s against
  1.5 s. Jint is only a fallback for linux-musl, where ClearScript has no native package.
- **V8 under ClearScript is 1.4-1.6x slower than Node.** Interop is not it: a no-op call
  with the same arguments costs 1 ms for 201 calls. Not investigated: V8 build flags and version
  differences between ClearScript's V8 and Node's.
- **Code cache is not worth it**: 10 ms off the compiler load, and slower passes after.
- **Parallel helps full rebuilds 1.4-2.2x on 10 cores.** Each engine pays its own load and warm-up,
  so the gain grows with the file count.
- **The real lever is not compiling.** A component takes about 6 ms warm. An edit touches one
  file: with MSBuild `Inputs`/`Outputs` (only changed components), and the engine kept alive
  between builds (a static in the task, which a reused MSBuild node keeps) or in the dev watcher's
  process, a save costs one compile, about 10 ms, plus 100-170 ms for an engine that is not warm yet.

## Inside MSBuild

A task holding a `V8ScriptEngine` loads with no `AssemblyLoadContext` or native-library
workarounds: ClearScript finds `runtimes/<rid>/native/` next to the task assembly.

| Build | Engine + compiler | With 2 components |
|---|---:|---:|
| macOS, `dotnet build`, first build | 661 | 687 |
| macOS, `dotnet build`, reused node | 174 | 208 |
| Windows x64, `dotnet build` | 135 | 170 |
| Windows x64, Visual Studio 18 `MSBuild.exe`, .NET task host (`TaskFactory="TaskHostFactory" Runtime="NET"`, as Pare.Build registers its task) | 297 | 356 |

## In the browser

The output runs in headless Chromium with the `svelte` package's `src/` served as is behind an
import map of 13 entries: `svelte`, `svelte/internal/client`, `svelte/internal/disclose-version`,
`svelte/internal/flags/legacy`, `svelte/events`, `svelte/reactivity`, `svelte/store`,
`#client/constants` (a package-internal `imports` entry), `esm-env` and its `browser`,
`development` and `node` conditions resolved by hand, and `clsx`. Runes, `$derived`, keyed
`{#each}` and events work, with no console errors. Unbundled, the runtime is 107 module files,
505 KB uncompressed.

## Cost and platforms

| Package | Size |
|---|---|
| `Microsoft.ClearScript.V8` 7.5.1.1 (managed) | 5.3 MB |
| `Microsoft.ClearScript.Core` (dependency) | 6.1 MB |
| `Microsoft.ClearScript.V8.ICUData` (dependency) | 15 MB |
| `Newtonsoft.Json` (dependency) | 11 MB |
| `Microsoft.ClearScript.V8.Native.<rid>` | osx-arm64 library 34 MB; nupkg download 11-16 MB |
| `Jint` 4.17.0 | depends only on `Acornima` |

Native packages exist for osx-arm64, osx-x64, linux-x64, linux-arm64, win-x64 and win-arm64.
**None for linux-musl-x64 or linux-musl-arm64** (404 on nuget.org): no Husk builds on Alpine with
V8. The engine runs at build time only, so none of this ships in the application.

## Not checked

A Linux or win-arm64 run; CSS output (`css: "external"`, the default, puts it in `result.css`,
which the spike dropped); development mode (`dev: true`, `esm-env/development`); source maps;
`.svelte.ts` / `.svelte.js` rune modules (`compileModule`); memory use per engine.
