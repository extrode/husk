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

## Dropped

Not doing, with the reason. Kept so the same idea does not get re-raised every quarter.

- (nothing yet)
- Husk build: run the `TaskHostFactory` `UsingTask` under Visual Studio's `MSBuild.exe` on Windows (spec 001 T13; not run, macOS only here).
- Husk: `SvelteCompiler.Shared` disposes the replaced compiler while another caller may still be compiling on it (needs a refcount or deferred dispose); dev saves are serialized by Pare, so only concurrent in-proc builds can hit it
- Husk: `CompilerBridge` compares only ClearScript.V8's version before sharing the app's ClearScript; ClearScript.Core and a compiler folder with no ClearScript.V8.dll are not checked
- Husk: in Development an unmarked page that is not UTF-8 (UTF-16, Latin-1) is re-encoded as UTF-8 by Pare's watcher, while the build copies its bytes; IPareCompiler gets text only
- PareKeepSources=true with Husk: a page or .svelte.js keeps its source's name, so wwwroot and obj/pare register one asset path twice (SDK error).
- Outside wwwroot / non-web projects, a component's next-to-source output (Child.svelte.js) overwrites a hand-written file of that name without warning.
- NativeProbe sets a DllImportResolver on a ClearScript the app shares; the app's own later SetDllImportResolver then throws.
- A marked page with invalid UTF-8 is decoded with replacement chars, changing bytes outside the marker (build and dev).
- SampleWatchTests (macOS): FIXED 2026-10-30 in the test. A write within about 45 s of a file being created also raises Created on macOS; dotnet watch (SDK 10.0.403) then logs "File added" and about half the runs exits with "Unexpected true - HotReloadMSBuildWorkspace.cs line 158". The test waits until 45 s after the copy on macOS (8 of 8 pass, 6 of 10 failed before). Real saves are of older files. Optional: report to dotnet/sdk (repro tmp/fsw/fsw.cs).
- CI (written, never run; review ccb60ff..4460a6a): macOS Safari theories need `safaridriver --enable` on the runner and no step runs it; Windows needs Git's `sh` on PATH and MSYS path conversion for scripts/pack-local.sh. Confirm on the first run.
- CI (2026-10-30, run 41040030030 on f495932): ubuntu and macOS pass; windows had 2 failures, fixed in 7ddb53d and confirmed on wp1 (Windows, ssh): Husk.Tests 174 of 174, Integration 54 passed and 3 skipped, and both Windows-only fixes fail when reverted. Open: SampleWatchTests fixed for macOS (see above); windows and ubuntu passed run 41052658431; the ubuntu L003 flake (HuskDevelopmentTests) did not reproduce in Docker, the baseline now waits for outputs to settle.
- Husk tests: CorpusTests riwalqalb and saaaat fail when the sibling checkout's worktree path (`ikhbat.org/riwalqalb.com/.worktrees/fix/whisper-window-skip`) is gone; make the corpus tests skip a missing root.
- Husk Build: up-to-date check compares mtimes, so an equal-mtime or coarse-resolution race can skip a rebuild.
- Husk Build: `$(_HuskCompilerOutput)**` packs stray files and the pdb is inconsistent.
- Husk Compiler: the V8 engine has no heap or time limit.
- Husk Compiler: ModuleGraph regex ignores comments and strings; PageWriter injects LF preloads into CRLF pages; `#` or `?` in `baseUrl` breaks Uri resolution; sourceMappingURL is not percent-encoded.
- Husk AspNetCore: HuskSession does not retry after a missing compiler file is restored.
- Husk Deploy: wrangler `_headers`/`_redirects` are refused, not implemented.
- Husk tests: EnginePool `Discard`, `Return` after `Dispose`, racing threads; `CompileAll` when one worker throws.
- Husk review 8ff3cbb..607bae0: Program ties zero retry delay to `allowInsecureBaseUrl`; PageWriter `AttributeEscape` leaves `'` and whitespace unescaped for single-quoted/unquoted `src`; HuskSession still loads the compiler bridge and compiles on save when the map says `HashNames: true`.
- Husk docs (audit 2026-10-31): error-codes.md lacks HUSK101 "listed twice" and the HUSK100 positionless/line-0 variants; first-app.md step 8 should give the real `obj/Debug/net10.0/pare/` and `…/husk/` paths and drop the redundant PrivateAssets step; `.svelte.ts` to `.svelte.js` import example; CONTRIBUTING.md test prerequisites (safaridriver, Git `sh` on Windows, corpus checkouts).
- Husk audit 2 (2026-10-31), compiler: PageWriter `Marker()` and `ModuleScript()` match inside HTML comments (a commented-out marker gets the import map, the real one stays empty); extension checks are case-sensitive where the glob is not (`Card.Svelte`).
- Husk audit 2, build: no guard on the SDK major (a .NET 9 SDK gets MSB4062, not a HUSK error).
- Husk audit 2, deploy: `AssetManifest` hashes and uploads in two reads (a build writing during deploy uploads new bytes under the old hash).
- Husk audit 2, CI: `scripts/pack-local.sh` versions sort below an earlier `preview.N` so the sample keeps a stale pack.
- Husk audit 2, tests: L002 seed (2002) never applies "add Image" or "edit Styled style" and checks no-change after the last step only; L003 build seed never uses `Broken[0]`/`[1]`; `ImportMapProbeTests` not in the `PackedFeed` collection (Safari allows one session); `SampleWatchTests` hard-codes 3 s and 250 ms limits; `HuskDevelopmentTests` css assert races Pare's js-then-css write order; `HuskPagesTests` `An_unmarked_page_is_written_newer...` cannot fail on the regression it names; no tests for `Program.Run` exit-2 paths, the `CompilerBridge` ClearScript version refusal, the "compiler moved" warning, Deploy timeout/IO exit 1; weak asserts in `A_2xx_answer_that_is_not_JSON...` and `An_existing_output_directory_is_refused`; console errors checked in Chrome only; `EnginePoolTests` leaves `bad-compiler.js` in the output folder.
- Husk review 85d4e35..0f32f3f: on Windows `File.Move(overwrite)` of `husk.css` may fail while a browser still reads it (unverified; needs FileShare.Delete on the static-file reader); the `"a\nb"` token test does not assert the token stays out of stderr; `HuskAssets.WriteHashed` checks a copy's length only, not its content.
- HUSK003 checks the SDK's RID (NETCoreSdkPortableRuntimeIdentifier) while the probe uses the process architecture; they differ under Rosetta or an x64 VS task host. Documented in Extrode.Husk.Build.targets, not changed.
- Husk pack (review 33d329d..ac22ec4): `_HuskPackageReadme` rewrites inline links and `src=` only; reference-style definitions (`[x]: docs/a.md`), links with a title and `href=` would ship relative, and the test catches only the title case.
- Husk Build (review 33d329d..ac22ec4): HUSK200-as-error skip relies on `IBuildEngine8.ShouldTreatWarningAsError`; unverified under the `TaskHostFactory` host (Visual Studio's MSBuild.exe).
- Husk Deploy: a folder link loop whose target spells an ancestor differently (`/private/var` for `/var`, other case) is not detected and fails with "Too many levels of symbolic links" (exit 1).
- Husk Build (review dd428b0..36abeb5): HUSK004's record (`NativeProbe.Resolve` SetData) and check (`HuskCompile`) are not atomic; two Husk task folders compiling at the same instant in one process (MSBuild `-mt`, unverified) could both load V8. A path spelled two ways for one file (symlinked NuGet folder) gives a false HUSK004.
- Husk tests (review dd428b0..36abeb5): `TempProject.Exec(limit, ...)` reads the pipes to the end after killing the tree, so a process that escaped the tree (a reused MSBuild node, a build server) holds it past the limit; callers must pass `-nodeReuse:false --disable-build-servers`.
- Husk Build (re-review db504df): `NativeProbe.Wanted()` re-reads the disk each run while the loaded native is fixed per load context, so a `runtimes/` folder appearing or vanishing beside a live node's task gives a false HUSK004; x86/arm32 hosts get no check (HUSK001 tests the SDK RID, not the node's); `Wanted()` and `CompilerBridge.MissingNative` duplicate the rid/cache path logic.
- Sample publish output ships each module under plain and hashed name plus .br/.gz copies (831 files for 277 uploadable): decide whether Husk.Deploy skips .br/.gz and plain duplicates (T8, docs/architecture/002).
- Husk router (spec 002): a side-effect CSS import from JS (`import '$lib/design/utilities.css'`) has no support; the corpus test stands in with `HuskImport` plus a module that adds a `<link>`. Needs a first-class item (copy the CSS, hash it, link it from the shell).
- Husk router (spec 002): JSON imports (`import x from './x.json' with { type: 'json' }`) in sources compiled with `PareServeAt` are not copied or served; the corpus test copies `umm-al-qura.json` and `sighting-record.json` into wwwroot by hand.
- Husk router (spec 002): `import.meta.env` in Pare-compiled `.ts` files is not rewritten in Development (Pare's dev compiler cannot be hooked); only Husk-compiled outputs are. Build and publish are fine.
- Husk router (spec 002, T13): adding or removing a route file in Development is not watched; `routes.js` is generated at build, so it needs a rebuild (`dotnet watch` restarts on item changes). Saving an existing route file reloads through Pare as before.
- Husk router (review of feat/002 vs dev): all router addresses, `UseHuskSpa` (`NoFallbackPrefixes`, `Page`) and `matchRoute` assume the app is served at the site root; `StaticWebAssetBasePath` other than empty puts modules and the shell under that path and `/app/` matches no route.
- Husk router (review): a `+layout.ts`/`+layout.js` without a `+layout.svelte` in the same folder, and a `+page.ts` without `+page.svelte`, are ignored with no diagnostic (RouteTable.cs ~78, ~116, ~123); SvelteKit runs the layout load under its default layout.
- Husk Build (review): `HuskAssets.AliasSources` extensionless and `.ts`-to-`.js` keys skip ImportMapWriter's "two sources share a bare key" rule: `a.ts` beside `a.svelte` makes `$lib/a` point at whichever output comes last.
- Husk Build (review): `HuskAssets.SourceUrls` keys paths with `StringComparer.Ordinal` while the rest compares per OS; `<HuskRoutes Include="src/Routes">` against `src/routes/**` globs gives a false HUSK304 on macOS/Windows.
- Husk Compiler (review): `KitFeatures` import regex `[^;'"]*?` crosses newlines, so semicolon-free `export let data\nimport { goto } from '$app/navigation'` can give a false HUSK302; imports in comments are also reported. Needs a tokenizer, not a regex.
- Husk Build (review): L002 (no change, no compile) is unproven for router apps; `RewriteEnv` rewrites Pare outputs in place after Pare wrote them. Add a router-app no-change build test before enacting a router law.
- Husk Build (review): the shell is a static web asset at `index.html`; a router app that also has `wwwroot/index.html` or a Husk page at `index.html` gets two assets on one path (unverified, likely a build error).
- Husk Deploy (review, unverified): CloudflareApi comment says a missing `/_husk/*.js` stays a 404 under `single-page-application`; Cloudflare documents the fallback for navigation requests only, check at the first real deploy.
- Husk router tests (review): no browser test yet for the `afterNavigate` replay fix, the error view URL, `goto` to another origin (now rejects) or a malformed hash.
- Husk router (review f8d0012..1cd3b5f): `index` does not move when a popstate arrives, only when its navigation commits or is superseded; Back, then a link click before the Back's loads finish, records a wrong `idx` and a later cancelled Back computes the wrong `history.go` delta (router.js `onPop`/`superseded`).
- Husk tests (review f8d0012..1cd3b5f): `A_cancelled_back_after_a_reload...` waits with `Thread.Sleep(1500)`; no tests yet for HUSK305 on an unreadable shell, `errorView` catch paths or `hashId`.
