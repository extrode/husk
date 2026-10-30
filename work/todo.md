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
- Husk: `SvelteCompiler.Shared` disposes the replaced compiler while another caller may still be compiling on it (needs a refcount or deferred dispose); dev saves are serialized by Pare, so only concurrent in-proc builds can hit it
- Husk: `CompilerBridge` compares only ClearScript.V8's version before sharing the app's ClearScript; ClearScript.Core and a compiler folder with no ClearScript.V8.dll are not checked
- Husk: in Development an unmarked page that is not UTF-8 (UTF-16, Latin-1) is re-encoded as UTF-8 by Pare's watcher, while the build copies its bytes; IPareCompiler gets text only
- Pare: ship `build/` as `buildTransitive/` too, so a project referencing only Extrode.Husk.Build gets Pare's targets and HUSK002 (direct Pare.Build reference) can go
- Husk: Extrode.Husk.Build nupkg is 82.6 MB (six V8 natives + ICU data); per-RID packages are the later option (plan, Risks)
- Integration tests pick a free port by bind-and-release; another process can take it before the app binds (RunningApp, SampleWatchTests).
- Each Sample()/Consumer() restores into its own globalPackagesFolder, so ClearScript natives download per copy; share one per PackedFeed.
- Two Extrode.Husk.Build versions in one reused MSBuild node load two ClearScript natives into one process (crash risk); consider TaskHostFactory or a version check.
- HuskPage globs wwwroot/**/*.html regardless of the user's Content Remove/Update metadata, so an excluded page is served and published again.
- PareKeepSources=true with Husk: a page or .svelte.js keeps its source's name, so wwwroot and obj/pare register one asset path twice (SDK error).
- Outside wwwroot / non-web projects, a component's next-to-source output (Child.svelte.js) overwrites a hand-written file of that name without warning.
- NativeProbe sets a DllImportResolver on a ClearScript the app shares; the app's own later SetDllImportResolver then throws.
- HuskAssets decides "served" with OrdinalIgnoreCase on every OS; Linux paths differing only in case are misclassified.
- A marked page with invalid UTF-8 is decoded with replacement chars, changing bytes outside the marker (build and dev).
- SampleWatchTests.Saves_reload_the_page_with_the_change_and_a_broken_save_keeps_it times out waiting for h1 "Husk edited" on base 0b24e5e and after T6; find out why (dotnet watch reload in Chrome) before relying on T21.
- CI (written, never run; review ccb60ff..4460a6a): macOS Safari theories need `safaridriver --enable` on the runner and no step runs it; Windows needs Git's `sh` on PATH and MSYS path conversion for scripts/pack-local.sh. Confirm on the first run.
- Release: add the required reviewer to the `packages` environment once extrode/husk is public (private plan rejects it): gh api -X PUT repos/extrode/husk/environments/packages -F 'reviewers[][type]=User' -F "reviewers[][id]=$(gh api user --jq .id)"
- Pages workflow: configure-pages, upload-pages-artifact and deploy-pages are pinned by movable tag (@v6/@v5/@v5), checkout by SHA; pin by digest (review 654d521..d32db26, same in jauntyq).
- CorpusTests (saaaat, riwalqalb) fail when the sibling checkouts lack the files the corpus lists, e.g. ../../ikhbat.org/riwalqalb.com/.worktrees/fix/whisper-window-skip/frontend/src/App.svelte is gone: make the corpus skip a missing root or read from the main checkout (seen 2026-10-30, not caused by the Pare 0.6.1 bump).
- Repo settings on public extrode/husk (2026-10-30): no `main` branch exists (default branch is `dev`; pages.yml and release docs assume `main`); Pages not enabled; private vulnerability reporting, secret scanning and push protection off; the `packages` environment has no reviewer.
- Repo settings, updated: `main` exists (pushed 2026-10-30); Pages is on at husk.extrode.com and deploys from `main` after `main` was added to the github-pages environment's branch policy; HTTPS enforcement turned on by the user.
- CI portability fixes (branch fix/ci-portability): Windows fixes cannot be run on macOS or Linux, so they are unverified until the next CI run on windows-latest (12 failures last time). Still open from the first run on macOS: ParallelCompileTests expects 5 engines on a runner with 4 cores; SampleWatchTests times out. The ubuntu L003 flake did not reproduce in Docker (arm64, 2 CPUs, 15 of 15 alone and 3 of 3 in the full project); the test now waits for the outputs to settle before its baseline.
