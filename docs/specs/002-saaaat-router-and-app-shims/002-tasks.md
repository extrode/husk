# 002 — Tasks
Plan: ./002-plan.md

- [x] T1 Spike: Pare with sources outside `wwwroot` (`PareRelativePath`, `Link`) — files: `prototypes/002-outside-sources/`, `docs/architecture/005-router.md` — covers: R18 — done when: a fixture with sources in `../x/` builds and the finding is written up; if Pare cannot map them, stop with a Needs-decision (extension point in Pare's repo)
- [x] T2 `RouteTable` model: folder scan, groups, params, trailing slash, layout chains, clash error HUSK300 — files: `src/Extrode.Husk.Compiler/RouteTable.cs`, tests — covers: R1, R2 — done when: unit tests pass on a fixture tree, incl. clash and non-`+` files ignored
- [ ] T3 `RouteTable` emits `routes.js` (dynamic `import()` per route, hooks, error page); unsupported route features HUSK301, page options ignored; no partial output on failure — files: `RouteTable.cs`, `docs/error-codes.md` — covers: R3, R5, R15, R17 — done when: tests pin the emitted text and that a failure writes nothing
- [ ] T4 Router core in plain JS: `matchRoute`, `runLoads` (redirect/error results), pure and loadable in V8 — files: `src/Extrode.Husk.Build/runtime/router.js`, V8 tests — covers: R1, R3, R4, R5 — done when: V8 tests pass with no Node
- [ ] T5 Router browser half: history, link interception, popstate, fragment links, origin-safe redirects — files: `runtime/router.js`, `runtime/start.js` — covers: R4, R10, R11, R14, R17, R20 — done when: Selenium tests on a fixture app pass in Chrome
- [ ] T6 `$app/navigation` — files: `runtime/app/navigation.js` — covers: R6, R7, R14 — done when: tests cover query-only `goto`, `replaceState`, `invalidateAll`, cancel, `//evil` and `javascript:` targets
- [ ] T7 `$app/state` (`.svelte.js`, compiled by Husk) and `$app/environment` — files: `runtime/app/state.svelte.js`, `runtime/app/environment.js` — covers: R8, R9 — done when: a page reading `page.url.searchParams` updates on `goto`; `dev` true only in Development
- [ ] T8 Import-map entries: `$app/*`, `$lib` exact keys, `HuskPackage`, `HuskImport` — files: `ImportMapWriter.cs`, `Extrode.Husk.Build.targets` — covers: R13, R18 — done when: hashed and unhashed maps resolve `$lib/x` and a workspace package in tests
- [ ] T9 `HuskRoutes` task and targets, `HuskShell`/`HuskEnv` items, off unless a `HuskRoutes` item exists — files: `src/Extrode.Husk.Build/HuskRoutes.cs`, targets — covers: R1, R13, R19, R20 — done when: a spec 001 project builds unchanged; the fixture app builds with `routes.js`
- [ ] T10 `import.meta.env` substitution in `HuskAssets` and HUSK302/HUSK303 diagnostics — files: `HuskAssets.cs`, `docs/error-codes.md` — covers: R13, R15, R19 — done when: tests pass, L002 (no-change incremental) still holds
- [ ] T11 Shell from `app.html` (placeholders, import map, stylesheet, modulepreload, entry script) — files: `PageWriter.cs` — covers: R20 — done when: output text test passes and `#app-root` is the mount point
- [ ] T12 Dev single-page fallback (`UseHuskSpa`, `NoFallbackPrefixes`) — files: `src/Extrode.Husk.AspNetCore/` — covers: R12 — done when: extension-less address returns the shell, `/_husk/` and `/data/` misses return 404
- [ ] T13 Dev route watching and reload — files: `HuskDevelopmentMap`, `HuskSession` — covers: R16 — done when: saving, adding and removing a route file reloads the browser (Selenium)
- [ ] T14 Deploy `--spa` — files: `src/Extrode.Husk.Deploy/CloudflareApi.cs`, `Program.cs`, `DeployTests` — covers: R12 — done when: `--dry-run` shows `single-page-application`; default stays `none`
- [ ] T15 Fixture app mirroring saaaat's routes; Selenium acceptance in Chrome, Firefox, WebKit — files: `tests/Extrode.Husk.Integration/` — covers: R1-R11, R16, R17 — done when: every acceptance criterion in the spec except the saaaat corpus passes in three browsers
- [ ] T16 Optional saaaat `apps/web` corpus build (skips when the sibling checkout is missing); measure the redirect/back behaviour in the current app and set the spec criterion — files: `tests/Extrode.Husk.Integration/`, spec — covers: R18, acceptance 1 and 3 — done when: all 17 pages render, or the failures are in `work/todo.md`
- [ ] T17 User docs, `docs/architecture/005-router.md`, changelog line, CSP caveat — files: `docs/`, `CHANGELOG.md` — covers: R19, Non-goals — done when: every new item and code is documented
- [ ] T18 enact L004 resolution is deterministic — files: `docs/laws/004-*.md` — covers: law 1 — done when: user sets `status: enacted`
- [ ] T19 prove L004 via /prove — covers: law 1 — done when: `laws-check.js --run` holds (after T4)
- [ ] T20 enact L005 `page.url` equals the address — files: `docs/laws/005-*.md` — done when: user enacts
- [ ] T21 prove L005 via /prove — done when: `laws-check.js --run` holds (after T15)
- [ ] T22 enact L006 one pattern per route folder, no clashes — files: `docs/laws/006-*.md` — done when: user enacts
- [ ] T23 prove L006 via /prove — done when: `laws-check.js --run` holds (after T2)
- [ ] T24 enact L007 unsupported feature leaves no partial route table — files: `docs/laws/007-*.md` — done when: user enacts
- [ ] T25 prove L007 via /prove — done when: `laws-check.js --run` holds (after T3)

## Analyze
R1 T2,T4,T5,T9,T15 · R2 T2 · R3 T3,T4 · R4 T4,T5 · R5 T3,T4 · R6 T6 · R7 T6 · R8 T7 · R9 T7 · R10 T5 · R11 T5 · R12 T12,T14 · R13 T8,T9,T10 · R14 T5,T6 · R15 T3,T10 · R16 T13 · R17 T3,T5 · R18 T1,T8,T16 · R19 T9,T10,T17 · R20 T5,T9,T11. All 20 covered, no orphans. Every task traces to a "Changes by area" bullet.
Gate: T18/T20/T22/T24 need the user's enactment; `/implement` drafts the law files with `/laws new` and proceeds, and the prove tasks wait until enacted.
