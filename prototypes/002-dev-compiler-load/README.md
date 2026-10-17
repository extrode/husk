# 002 dev compiler load (2026-10-17)

Throwaway code that answered "can a running ASP.NET Core app load the ClearScript compiler from a
folder it does not reference, and compile on save". Results are in
[docs/architecture/003-dev-compiler-load.md](../../docs/architecture/003-dev-compiler-load.md).

| Path | What |
|---|---|
| `compiler/` | Stands in for `Extrode.Husk.Compiler`: one warm V8 engine with the Svelte compiler, `Compiler.Compile(compilerPath, source, filename)` |
| `app/` | A web app with no ClearScript reference that loads `compiler/` into its own `AssemblyLoadContext`, compiles `components/*.svelte` on start and on save. `-p:WithClearScript=true` adds a ClearScript of its own; `-p:Aot=true` sets `PublishAot` |

Run from this directory, with the Svelte compiler from `../001-engine-spike/js` (`npm --prefix
../001-engine-spike/js ci --ignore-scripts`):

```sh
dotnet build compiler/DevCompiler.csproj && dotnet build app/LoadApp.csproj
dotnet app/bin/Debug/net10.0/LoadApp.dll compiler/bin/Debug/net10.0 \
  ../001-engine-spike/js/node_modules/svelte/compiler/index.js app/components --urls http://127.0.0.1:5099
# then save app/components/Counter.svelte and watch stderr
```
