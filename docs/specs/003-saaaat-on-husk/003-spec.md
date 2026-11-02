# 003 — saaaat.com web app on Husk
Status: draft
Created: 2026-11-01

## Problem
Milestone 2 (decision 004) is saaaat.com's web app running on Husk. Spec 002 gave Husk the router and
the `$app/*` shims, and the saaaat corpus test renders the app's 17 pages in three browsers, but the
real app is still built by Vite, SvelteKit and pnpm, and its tasks run on Deno. Until the app builds
with `dotnet build`, Husk has not been shown to carry a real product, and saaaat's maintainer still
needs Node and Deno to ship a change. The app is 82 components and 86 TypeScript modules (tests
excluded) that import three workspace packages (`@saaaat/core`, `@saaaat/i18n`, `@saaaat/geo-data`)
by package name, plus a 6 MB city dataset copied into the served folder by a script.

## Goals
- The web app of saaaat.com builds with the .NET SDK alone and runs in Chromium,
  Firefox and WebKit, with no Node or Deno at build time.
- The first build covers the app's pages, router, in-app navigation, translations and the three
  workspace packages, and every page behaves as it does in the Vite build.
- The Vite build stays working and unchanged on saaaat's `main` for the whole port; the port is
  judged by comparison with it.
- Gaps Husk has are found by this port and recorded, not worked around inside saaaat's source.

## Non-goals
- Service worker, web manifest and offline behaviour: milestone 3 territory, a later spec.
- Native-app packaging.
- The app's Content-Security-Policy, its response headers, and its end-to-end suite passing against the
  Husk output.
- Reproducing the version stamp the Vite build writes into each HTML file.
- Replacing saaaat's unit tests or its Deno tasks, or moving the workspace packages out of the
  saaaat repo.
- Deploying the Husk build to production.
- Running the Vite build as part of the Husk build: the comparison with it (R4) is a separate step on
  a machine that has Node, and its results are recorded files, not a build input.
- riwalqalb.com.

## User stories
- As saaaat's maintainer, I want to build the web app with the .NET SDK and get a site I can open, so
  that I no longer need Node, pnpm or Deno to build it.
- As saaaat's maintainer, I want the port on a branch beside the Vite build, so that I can compare
  the two and drop the port without losing anything.
- As Husk's maintainer, I want the port to expose what a real app needs, so that the gaps become
  Husk work items.

## Requirements
- R1 The port lives on a branch of the saaaat repo. Nothing on its `main` changes, and the Vite
  build is still runnable from `main`.
- R2 One command builds the app; it needs only the .NET SDK and the packages Husk already depends on.
  No npm package is fetched.
- R3 Every static route of the app, and one address for each parameterised route, opens at its own
  address and after a reload, as it does in the Vite build. The address list is written down in the
  plan. (Trailing-slash handling follows spec 002, not the Vite host.)
- R4 For a pinned clock, location and saved settings, each of the 17 pages shows the same visible
  text as the same page in the Vite build, in Chromium, Firefox and WebKit, in English and in one
  right-to-left language. The Vite side is a recorded reference taken on a machine with Node; the
  other shipped languages are checked for opening without error only.
- R5 In-app navigation, Back and Forward, redirects, query parameters and the error page behave as
  they do under spec 002, now against the real app.
- R6 The three workspace packages resolve by their package names from the app's source, as TypeScript
  sources, with no change to the imports (spec 002 R18 already requires this; this spec shows it holds
  on the real app).
- R7 All shipped translations are available and switching language works; right-to-left languages
  lay out right to left.
- R8 Everything the app serves as-is (the 6 MB city dataset, images, audio, scripts, icons) reaches
  the browser from the built site, without a copy step run beforehand.
- R9 No page logs a console error on load in Chromium. Firefox and WebKit are checked the way spec 002
  checks them (WebDriver exposes no console there); the gap is recorded, not closed here.
- R10 The app's source needs no edit that exists only for Husk, beyond the files a branch needs to
  point its build at Husk. A source edit Husk makes unavoidable is recorded as a Husk gap, with the
  reason.
- R11 The two source forms the unedited app uses and Husk does not yet handle build: a stylesheet
  imported for its side effect (the root layout), and JSON modules imported with an import attribute
  (the Hijri table and the sighting record). They are fixed in Husk, not edited out of the app.
- R12 Building twice with no change compiles nothing the second time. This is not yet shown for router
  apps (`work/todo.md`: the environment rewrite changes outputs in place); if it cannot hold, that is
  recorded as a Husk gap and R12 is reported as unmet, not weakened.
- R13 The app imports a native-bridge package at runtime, statically from a module every page loads, and
  dynamically from the notification code. Those imports are satisfied without npm (R2) by stand-ins
  that behave as the web app needs on a browser; the stand-ins and what they omit are recorded.
- R14 Build size and the time of a clean build are measured and written down next to the Vite
  build's.

## Acceptance criteria
- Given a fresh clone of the port branch and the .NET SDK, when the app is built, then the build
  exits 0 and the home page opens when the output is served.
- Given the recorded Vite reference and the Husk build, when each of the 17 pages is opened with the
  pinned clock, location and settings, then the visible text is equal in all three browsers (R4).
- Given the port branch, when its source differences from `main` are listed, then each is a build
  file or an entry in the recorded Husk-gap list (R10).
- Given a built app, when the build runs again with no change, then no component is compiled (R12),
  or the gap is recorded.
- Given a recorded gap, when it is read, then it names the saaaat file that hit it, what Husk did,
  and whether the fix belongs to Husk or to saaaat.

## Law candidates
- For every page of the app, the Husk build's visible text equals the Vite reference under the pinned
  conditions (R4).
- The port never changes anything reachable from saaaat's `main` (R1).
- After any sequence of source saves under a watching run, the served app equals a clean build of the
  same sources, for the source forms the app uses. (False today for environment values in compiled
  TypeScript: `work/todo.md`; the plan must fix that or narrow the law.)

## Open questions
- None blocking. For `/plan`: the address list for R3; the pinned conditions for R4; the shape of the
  stand-ins in R13; where the saaaat checkout used by CI lives, since the port branch is in another
  repository; whether the reference snapshots live in the saaaat repo or in this one.
