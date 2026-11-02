# 003 — Plan
Spec: ./003-spec.md

## Approach
Husk already builds the real app: `tests/Extrode.Husk.Integration/SaaaatCorpusTests.cs` compiles all of
`apps/web` and the three packages from the sibling checkout and renders 17 pages in three browsers (spec 002
T16). It does so with three hand-made crutches: stand-in modules for `@capacitor/*`, a module that adds a
`<link>` for the side-effect CSS import, and the static folder and two JSON files copied by the test. This
spec turns the crutches into either Husk features (CSS import, JSON import, static folder, R11 and R8) or
recorded stand-ins that live on the port branch (Capacitor, R13), then moves the project from a test
fixture to a real project on a `feat/husk-port` branch of the saaaat repo (R1, R2), and adds a parity test
against a recorded Vite reference (R4).

Order: four spikes (P1-P4, below) answer the questions that could change the design; then the Husk gaps (they
are what R10 forbids editing the app for); then a Husk preview release, since the port project builds from
nuget.org; then the port project, the parity test, and measurements. Husk work follows the repo's usual flow, on branches off
`dev` with tests; the saaaat branch is a separate repo and gets one commit per step.

Rejected: a long-lived fork copy of the app inside this repo (drifts, loses R1's "beside the Vite build");
running Vite inside the Husk build to produce the reference (breaks R2); real `@capacitor/*` packages via
npm (breaks R2; the stand-ins already work in the corpus test).

## Tech / dependencies
- No new packages. Husk packages come from nuget.org: 0.1.0-preview.7 (published 2026-11-01) has none of the
  gap fixes below, so a new preview is released before the port project can build on a clean machine (R2);
  until then the port project uses a local feed. Pare 0.7.2 is pinned by Husk. The port project references
  `Extrode.Husk.Build` and `Extrode.Husk.AspNetCore` only.
- The Vite reference needs Node, pnpm and saaaat's `pnpm install` on the machine that records it, never in the
  port build. Page text is recorded by the C# harness (Selenium) serving the Vite output, so no second
  browser driver is added.
- Test stack unchanged: xunit.v3, Selenium.WebDriver, the existing `StaticServer`, `Browsers`, `TempProject`.

## Spikes (each ends with a note in `docs/architecture/006-saaaat-port.md`)
- P1 Reference determinism, first because it decides whether R4 is achievable: build saaaat's Vite output, serve it
  with the harness under a frozen clock and pinned settings, record every page twice in each of the three
  browsers, diff. Output: the elements that must be ignored, the wait heuristic, and whether the browser time
  zone (read in `apps/web/src/lib/day.ts`) needs pinning.
- P2 How the side-effect stylesheet import resolves. Leaning: Husk generates a small module per imported
  stylesheet (adds a `<link>` to the hashed copy) and the import map maps the bare specifier to it, so the
  compiled output is not rewritten (spec 002's plan rewrites outputs only for `import.meta.env`). Cascade order
  is then link-append order, written down.
- P3 Serving a folder as-is: `DefineStaticWebAssets` with a `ContentRoot` outside the project already exists
  for the vendor folder (`Extrode.Husk.Build.targets` ~272); the spike checks it covers `apps/web/static` and
  decides the `HuskStatic` item shape.
- P4 Development `import.meta.env` for Pare-compiled `.ts`: serve-time rewriting exists for Husk-compiled `.js`
  (`Extrode.Husk.AspNetCore/HuskCompiler.cs`); the gap is Pare's `.ts` only. Decide: extend that, or narrow
  law candidate 3.

## Changes by area
Husk repo (this one), each on its own branch off `dev` with tests:
- `src/Extrode.Husk.Build/` stylesheet import (R11), per P2. Test: fixture app in three browsers.
- `src/Extrode.Husk.Build/` JSON module imports (R11): JSON files are not Pare outputs, so a new item source
  feeds `HuskAssets`; the import map covers the file. Hash caveat: a JSON edit does not change the importing
  module's hashed name, so the JSON is served under a hashed name too, or decision 007's cache rule is extended;
  decided in the task. Also check the corpus test's filter on "unsupported MIME type" console errors
  (`SaaaatCorpusTests.cs`), which may be hiding a current failure. Tests: build plus browser; a missing file
  fails with a HUSK code.
- `src/Extrode.Husk.Build/` `HuskStatic` item (R8), per P3. Exclusions (`_app/`, `_headers`) are in the port
  project's list, not in Husk.
- `tests/Extrode.Husk.Integration/StaticServer.cs`: correct content types for images, audio and fonts (it
  serves them as `text/html` today, so R8 would prove nothing), and a new `HeadScript` option that prepends a
  script at the start of `<head>` of the served shell (before the app's own boot script) for the frozen clock.
- `tests/Extrode.Husk.Integration/Laws/L002NoChangeNoCompileTests.cs`: extend with a router-app case (R12). If
  `RewriteEnv` (`HuskRoutes.cs`, rewrites Pare's outputs in place) defeats it, the fix is its own task, and
  R12 is reported as unmet until then.
- Dev `import.meta.env` for Pare `.ts`, per P4.
- `tests/Extrode.Husk.Integration/SaaaatCorpusTests.cs` drops the stylesheet, JSON and static-copy crutches as
  each lands; it keeps the Capacitor stand-ins, because it builds `apps/web` directly.
- Release: bump to the next preview, changelog, tag (the flow just used for preview.7).
- `tests/Extrode.Husk.Integration/SaaaatParityTests.cs` (new, `Category=Corpus`, skipped without the checkout):
  builds the port project, serves it with the frozen clock, and (a) compares visible text of the 17 pages in
  English and Arabic, per browser, with `tests/references/saaaat/` (R4, new directory); (b) walks navigation:
  a link click, Back and Forward, the `/settings/nonsense` redirect, a query address, an unknown address (R5);
  (c) opens every shipped locale on the home page without error and switches language once in-app (R7);
  (d) reads the console in Chromium (R9). Recording mode: an environment variable names a Vite build
  folder to record instead of compare.
- `scripts/node-reference/saaaat-vite-build.sh` (new): in a saaaat checkout, `pnpm install`, Vite build, prints
  build size and wall time (the Vite side of R14) and the folder to record from. Needs Node; not run by CI.
- `tests/references/saaaat/<saaaat-sha>/` (new): `<page>.<locale>.<browser>.txt`, `document.body.innerText` after
  the page settles. Per browser because ICU and `innerText` differ between Blink, Gecko and WebKit.
- `docs/architecture/006-saaaat-port.md`: spike notes, the gap list (R10), the stand-ins (R13), measurements
  (R14), and the decision that Corpus tests are not gating.
- `work/milestones.md`: row 2 to `shipped` when R1-R14 hold or are recorded as gaps; row 1 is stale (`planned`
  though spec 001 shipped) and is corrected in the same edit.

saaaat repo, branch `feat/husk-port` off its `dev`; R1 and R10 are checked against the branch point, not
against `main`, so unmerged `dev` work is not counted as port changes:
- `apps/web-husk/Saaaat.Web.csproj` (+ `nuget.config`): the item list from `SaaaatCorpusTests.Build` pointed at
  `../web/src`, `../../packages/*/src` and the static folder; `dotnet build apps/web-husk` builds it (R1, R2, R6).
- `apps/web-husk/stubs/`: the three Capacitor stand-ins, web behaviour only, each listing what it omits (R13).
- No edit under `apps/web/src` or `packages/` (R10); anything that would need one is recorded instead.

## Data / interfaces
- Pinned conditions for R4, set before the app loads: the clock by `HeadScript`; saved state through
  `localStorage` (`saaaat:v1:tos`, `locations`, `activeLocation`, and `saaaat:v1:settings` carrying `locale`,
  numerals and theme, because unset values come from `navigator.language` and the OS colour scheme); window
  1280x900. The same script and keys are used when recording the Vite build.
- Page list for R3 (complete): `/`, `/today`, `/calendar`, `/month`, `/moon`, `/moon/details`, `/qibla`,
  `/about/estimated-times`, `/about/scholarly-differences`, `/about/umm-al-qura`, `/admin`, `/guide/terminology`,
  `/guide/transliteration`, `/history`, `/location`, `/settings/appearance`, `/settings`. The only parameterised
  route is `settings/[section]`, covered by `/settings/appearance` and the redirect from `/settings/nonsense`.
- Right-to-left locales are `ar`, `ur`, `fa`, `ps` (`packages/i18n/src/types.ts`); R4 uses `ar`.
- Settling: the app has no ready marker and R10 forbids adding one, so the wait is a Husk-side heuristic from P1
  (DOM unchanged for a fixed interval).

## Risks & mitigations
- Text equality is flaky if something animates or loads late (moon canvas, countdowns). P1 measures this first;
  every ignored element is recorded.
- Pare compiling the app's TypeScript (`satisfies`, ambient `namespace App`): shown by the corpus build (T16),
  re-checked by the first green parity run.
- Clock freezing: the app reads `Date` after load, not at module scope (grep found none), so a script at the
  start of `<head>` is enough; `Intl` and time zone are not frozen by it, hence P1.
- The saaaat repo moves: references are keyed by saaaat commit; the parity test refuses a mismatch and says to
  re-record.
- Private checkout: CI cannot run Corpus tests, so R3-R9 are verified locally and the result is written into
  `006-saaaat-port.md`.
- Release ordering: the port project cannot build on a clean machine until the new preview is on nuget.org.
- External input: the port only reads files; no network, auth or data exposure added. `PUBLIC_BACKEND_URL` stays
  empty, as in the corpus test.
- Firefox and WebKit console errors cannot be read (WebDriver): recorded, not closed (R9).

## Coverage check
| Req | Covered by |
|---|---|
| R1 | branch `feat/husk-port`; edits only under `apps/web-husk/`; checked by listing the diff against the branch point |
| R2 | port project references only Husk packages; new preview release; local feed until then |
| R3 | page list in Data/interfaces; the parity test opens each address and after a reload |
| R4 | P1, reference files per browser, `SaaaatParityTests` (a), pinned conditions |
| R5 | `SaaaatParityTests` (b) on the real app |
| R6 | aliases in the port project; shown by the corpus build |
| R7 | `SaaaatParityTests` (a) English and Arabic text, (c) every locale opens and one switch |
| R8 | P3, `HuskStatic`, `StaticServer` content types |
| R9 | `SaaaatParityTests` (d), Chromium; others recorded |
| R10 | gap list in `006-saaaat-port.md`; empty diff under `apps/web/src` and `packages/` against the branch point |
| R11 | P2 stylesheet import; JSON import task |
| R12 | L002 router-app case, fix or recorded as unmet |
| R13 | `apps/web-husk/stubs/` and their omissions list |
| R14 | `saaaat-vite-build.sh` (Vite side) and the port build timing; table in `006-saaaat-port.md` |
Law candidates: parity (R4) becomes a law via `/laws` once the parity test is stable; R1 is checked by the diff
listing, not a test; the watch-equals-clean-build law waits on P4.
