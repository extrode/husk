# 001 engine spike (2026-10-17)

Throwaway code that answered "does the Svelte 5 compiler run in a .NET-hosted engine, and how
fast". Not product code: no tests, no reuse. Results are in
[docs/architecture/001-compiler-engine.md](../../docs/architecture/001-compiler-engine.md).

| Path | What |
|---|---|
| `js/` | `svelte` 5.57.2 pinned; `reference.mjs` runs the same compiler bundle under Node as the reference |
| `console/` | ClearScript V8 over a file list, same output format as the reference |
| `bench/` | Engine comparison: `v8`, `v8-noop` (interop only), `v8-cache` (V8 code cache), `v8-parallel` (one engine per core), `jint` |
| `msbuild-task/` | The compiler inside an MSBuild task, and `app/`, a project that runs it on `dotnet build` and under Visual Studio's `MSBuild.exe` |
| `browser-check.cjs` | Loads `msbuild-task/app/wwwroot/index.html` in headless Chromium, clicks, reports errors and module count |
| `reflect.cs` | Lists ClearScript.V8's `Compile` overloads and flags (`dotnet run --file reflect.cs -- <ClearScript.V8.dll>`) |

Run from this directory. Inputs are file lists, one absolute path per line; the spike used the
Svelte repository in Pare's benchmark corpus (`../pare`, `dotnet run scripts/get-bench-corpus.cs`)
and the components of saaaat.com and riwalqalb.com.

```sh
npm --prefix js ci --ignore-scripts
find ../../../pare/.corpus/realworld/svelte -name '*.svelte' | sort > files.txt
node js/reference.mjs files.txt node.tsv 5
dotnet run -c Release --project bench -- "$PWD/js/node_modules/svelte/compiler/index.js" files.txt node.tsv v8 5
dotnet build -c Release msbuild-task/SvelteTask.csproj && dotnet build msbuild-task/app/SvelteApp.csproj
python3 -m http.server 8765 --bind 127.0.0.1 &   # then:
node browser-check.cjs <playwright-core package dir> http://127.0.0.1:8765/msbuild-task/app/wwwroot/index.html
```

`bench` and `console` take `<compiler/index.js> <list> <node.tsv> <mode> [passes] [limit]`.
On Windows pass `-p:SpikeRid=win-x64` to the task build. A Playwright package directory ships
with any Microsoft.Playwright build, under `bin/<config>/<tfm>/.playwright/package`.
