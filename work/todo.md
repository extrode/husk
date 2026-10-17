# Todo — husk

The unscheduled backlog: everything known to be worth doing that nobody has committed to a date.
Items enter here from anywhere (a review comment, an out-of-scope note, a bug found in passing)
and leave by being promoted into `status/next.md` or dropped with a reason.

Keep it honest rather than tidy. A backlog that only contains things someone intends to do is a
wish list; the value is in recording the ones nobody has got to yet.

## Now-ish

Small, clear, and could be picked up without a design decision.

- (nothing yet)

## Later

Real, but needs a decision, a dependency, or more information first. Say which.

- Fetch dependencies' `.d.ts` for type checking without a package manager runtime (decision 005).
- Why ClearScript's V8 is 1.4-1.6x slower than Node on the same bundle: V8 version, build flags.
  Needs nothing; a profiling session.
- Not yet checked: a Linux and a win-arm64 run, memory per engine, `compileModule` for
  `.svelte.ts`, source maps. Before T5 ships.
- Cloudflare account limits (files per deployment, file size) against the two apps' output.
  Before T6.

## Dropped

Not doing, with the reason. Kept so the same idea does not get re-raised every quarter.

- (nothing yet)
- Husk build: run the `TaskHostFactory` `UsingTask` under Visual Studio's `MSBuild.exe` on Windows (spec 001 T13; not run, macOS only here).
- Pare 0.6.0 warns PARE200 ("Delete it") on every same-name compiled output (`clock.svelte.js`, each `wwwroot/*.html` page). Fixed on pare `dev` (fix/pare200-same-name-output); Husk moves to the release that carries it.
- Husk: `SvelteCompiler.Shared` disposes the replaced compiler while another caller may still be compiling on it (needs a refcount or deferred dispose); dev saves are serialized by Pare, so only concurrent in-proc builds can hit it
- Husk: `CompilerBridge` compares only ClearScript.V8's version before sharing the app's ClearScript; ClearScript.Core and a compiler folder with no ClearScript.V8.dll are not checked
- Husk: in Development an unmarked page that is not UTF-8 (UTF-16, Latin-1) is re-encoded as UTF-8 by Pare's watcher, while the build copies its bytes; IPareCompiler gets text only
- Pare: ship `build/` as `buildTransitive/` too, so a project referencing only Extrode.Husk.Build gets Pare's targets and HUSK002 (direct Pare.Build reference) can go
- Husk: Extrode.Husk.Build nupkg is 82.6 MB (six V8 natives + ICU data); per-RID packages are the later option (plan, Risks)
- Integration tests pick a free port by bind-and-release; another process can take it before the app binds (RunningApp, SampleWatchTests).
- Each Sample()/Consumer() restores into its own globalPackagesFolder, so ClearScript natives download per copy; share one per PackedFeed.
