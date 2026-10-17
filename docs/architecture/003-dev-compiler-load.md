# 003 Dev compiler load: in-process, through its own load context

Measured 2026-10-17 on an Apple M1 Max, .NET SDK 10.0.401, ClearScript 7.5.1.1, Svelte 5.57.2.
Code: [prototypes/002-dev-compiler-load](../../prototypes/002-dev-compiler-load).

**Verdict: go, in-process.** The app loads the compiler assembly from the package folder into an
`AssemblyLoadContext` of its own, with an `AssemblyDependencyResolver` over the folder's
`deps.json`. No child process.

## What was run

A web app that references no ClearScript loads `DevCompiler.dll` (ClearScript V8, its
`osx-arm64` native, copied to the build output with `CopyLocalLockFileAssemblies` and
`EnableDynamicLoading`) from a folder, calls `Compiler.Compile` through a delegate made by
reflection, and compiles a component on start and on each save.

| Case | Load | First compile (engine start) | Each save after |
|---|---:|---:|---:|
| Plain app, cold disk | 9 ms | 1,133 ms (engine 1,105 ms) | 1.2-2.2 ms |
| Plain app, warm disk | 9 ms | 128 ms (engine 101 ms) | 2.7 ms |
| App with its own ClearScript 7.5.1.1, shared | 4 ms | 81 ms (engine 62 ms) | - |
| `PublishAot` app, run with `dotnet` | 9 ms | 128 ms | 2.7 ms |

- The native library resolves from the folder's `runtimes/<rid>/native/` through
  `LoadUnmanagedDll` and the resolver. Nothing goes on the app's probing path.
- The default context never sees ClearScript: an endpoint listing the default context's
  assemblies finds none.
- A compile error comes back as text (`element_unclosed ...`) in 1.2 ms. The engine survives it.

## Two ClearScript copies abort the process

An app with its own ClearScript, loading a second copy into the compiler's context, dies on the
first compile: `Fatal error ... Check failed: new_capacity <= old_capacity`. Each copy loads its
own V8 native library, and two V8s in one process do not coexist.

Fix, measured: when the app already has `ClearScript.V8` (loaded, or `ClearScript.V8.dll` in the
app's base directory), the compiler's context returns `null` from `Load` for `ClearScript.*`, so
the app's copy is shared. That ran: 62 ms engine start, one compile.

Not run: an app with a different ClearScript version. Sharing it would run Husk's compiler on an
untested ClearScript. The dev compiler logs a warning and turns itself off when the versions
differ; `dotnet build` still compiles.

## PublishAot

A `PublishAot` app runs under the JIT in Development (`dotnet run`, `dotnet watch`), so the load
works there. `LoadFromAssemblyPath` carries `RequiresUnreferencedCode`. With
`UnconditionalSuppressMessage` on the loader, `dotnet publish -p:PublishAot=true` builds with no
warning and the binary is 13 MB. The loader runs only in Development, so a published app never
reaches it.

## What this sets for the product

- `Extrode.Husk.AspNetCore` references no ClearScript, and loads `Extrode.Husk.Compiler` from the
  path in `Extrode.Husk.development.json`.
- The package's task folder must carry `Extrode.Husk.Compiler.deps.json` with the six natives as
  `runtimeTargets`, so the resolver finds the host's.
- The first compile in a session pays the engine start, 60 ms to 1.1 s depending on the disk
  cache. A save after it is 1-3 ms (R10, R11). T21's 50 ms bound has room.
