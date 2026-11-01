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

Order: Husk gaps first (they are what R10 forbids editing the app for), then the port project, then the
reference and parity test, then measurements. Husk work follows the repo's usual flow, on branches off
`dev` with tests; the saaaat branch is a separate repo and gets one commit per step.

Rejected: a long-lived fork copy of the app inside this repo (drifts, loses R1's "beside the Vite build");
running Vite inside the Husk build to produce the reference (breaks R2); real `@capacitor/*` packages via
npm (breaks R2; the stand-ins already work in the corpus test).

## Tech / dependencies
- No new packages. Husk packages come from nuget.org at 0.1.0-preview.7 (published 2026-11-01); Pare 0.7.2 is
  pinned by Husk. The port project references `Extrode.Husk.Build` and `Extrode.Husk.AspNetCore` only.
- The Vite reference needs Node and pnpm on the machine that records it, never in the port build.
- Test stack unchanged: xunit.v3, Selenium.WebDriver, the existing `StaticServer`, `Browsers`, `TempProject`.

## Changes by area
Husk repo (this one):
- `src/Extrode.Husk.Build/` side-effect stylesheet import (R11): a source that does `import 'x.css'` gets the
  file copied, hashed with the rest, and linked from the shell. Replaces the `HuskImport` + `<link>` crutch.
  Test: a fixture app, in three browsers (`RoutesBrowserTests` pattern).
- `src/Extrode.Husk.Build/` JSON module imports (R11): `import x from './x.json' with { type: 'json' }` in a
  `PareServeAt` source is copied and served beside it, and the import map covers it. Test: fixture build plus
  browser check; both sides of a missing file (HUSK error) tested.
- `src/Extrode.Husk.Build/` a way to serve a folder as-is (R8): spike S1 decides whether the SDK's own static
  asset items already cover `apps/web/static` from a project that sits beside it; if not, a `HuskStatic`
  item (folder, optional URL prefix) is added. Excludes nothing by default; the `_app/` and `_headers`
  exclusions stay in the port project's item list, not in Husk.
- `src/Extrode.Husk.Build/` + `tests/` router no-change build (R12): add the missing test from `work/todo.md`
  (second build of a router app compiles nothing). If `RewriteEnv` rewriting Pare outputs in place defeats
  the check, fix it by writing the rewritten copy to the Husk output instead of Pare's.
- Development-time `import.meta.env` in Pare-compiled `.ts` (law candidate 3): fix by rewriting at serve
  time in `Extrode.Husk.AspNetCore`, or narrow the law to "outside environment values" and record the gap.
  Decided in S2, below.
- `tests/Extrode.Husk.Integration/SaaaatCorpusTests.cs` loses the three crutches as each lands.
- `tests/Extrode.Husk.Integration/SaaaatParityTests.cs` (new, `Category=Corpus`, skipped without the
  checkout): builds the port project, serves it, freezes the clock, opens the 17 pages in English and one
  right-to-left language, and compares visible text with `tests/references/saaaat/` (R4, R5, R9).
- `tests/references/saaaat/<saaaat-sha>/` recorded page text from the Vite build of that saaaat commit;
  `scripts/node-reference/saaaat-pages.sh` regenerates it (R4). Needs Node; not run by CI.
- `docs/architecture/006-saaaat-port.md` the gap list (R10), the stand-ins (R13), measurements (R14).
- `work/milestones.md` row 2 to `shipped` when R1-R14 hold or are recorded as gaps.

saaaat repo, branch `feat/husk-port` off its `dev`:
- `apps/web-husk/Saaaat.Web.csproj` (+ `nuget.config`): the item list from `SaaaatCorpusTests.Build` pointed at
  `../web/src`, `../../packages/*/src`, the static folder; the build is `dotnet build apps/web-husk` (R1, R2, R6).
- `apps/web-husk/stubs/` the three Capacitor stand-ins, web behaviour only, each file listing what it omits (R13).
- No edit under `apps/web/src` or `packages/` (R10); anything that would need one is recorded instead.

## Data / interfaces
- Pinned conditions for R4: the clock is frozen by a script the test harness prepends to the shell
  (`StaticServer` option), so both builds see one time; location and saved settings via the same
  `localStorage` keys the corpus test already sets (`saaaat:v1:tos`, `locations`, `activeLocation`); window
  1280x900. The language key is read from the app in task S3.
- Page list for R3: the 17 corpus pages plus one address for each of `settings/[section]` (`appearance`) and
  any other `[param]` route found by listing `src/routes`; written into the parity test.
- Reference file: `<page-with-slashes-as-dashes>.<lang>.txt`, `document.body.innerText` after the page settles.

## Risks & mitigations
- Text equality is flaky if something animates or loads late (moon canvas, prayer countdowns). Mitigation:
  the frozen clock, wait for a per-page settled marker, and ignore elements the Vite and Husk builds both
  render non-deterministically only after the first run shows which; every ignore is recorded.
- Pare compiling the app's TypeScript (`satisfies`, ambient `namespace App`): already proven by the corpus
  build (T16, 17 pages render), so not a spike; re-checked by the first green parity run.
- The saaaat repo moves: the reference is keyed by saaaat commit; the parity test refuses a mismatch and
  says to re-record. The port branch is rebased, never merged into `main` (R1).
- Private checkout: CI cannot run the Corpus tests, so R3-R9 are verified locally and the measured result
  is written into `docs/architecture/006-saaaat-port.md`; this is a decision, recorded there.
- External input: the port only reads files; no network, auth or data exposure added. The dataset and
  backend URL behave as in the Vite build (`PUBLIC_BACKEND_URL` empty, as the corpus test does).
- R12 may be unachievable without a Husk change (see above); reported as unmet if so, per the spec.
- Firefox and WebKit console errors cannot be read (WebDriver): recorded, not closed (R9).

## Coverage check
| Req | Covered by |
|---|---|
| R1 | saaaat branch `feat/husk-port`, no edits outside `apps/web-husk/`; verified by listing the diff against `main` |
| R2 | port project references only Husk packages; nuget.org feed |
| R3 | page list in Data/interfaces; parity test opens each address and after reload |
| R4 | reference files + `SaaaatParityTests` with pinned conditions |
| R5 | existing spec 002 browser tests plus parity test navigation checks |
| R6 | aliases in the port project; corpus build already shows it works |
| R7 | parity test: English and one right-to-left language; other languages open-without-error |
| R8 | static-folder step (S1 / `HuskStatic`) |
| R9 | parity test, Chromium console; others recorded |
| R10 | gap list in `006-saaaat-port.md`; empty diff under `apps/web/src` and `packages/` |
| R11 | CSS import and JSON import changes |
| R12 | router no-change build test (+ fix or recorded) |
| R13 | `apps/web-husk/stubs/` and their omissions list |
| R14 | measurements section of `006-saaaat-port.md` |
Law candidates: parity (R4) becomes a law via `/laws` after the parity test is stable; R1's "never changes main"
is checked by the diff listing, not a test; the watch-equals-clean-build law waits on the env decision.
