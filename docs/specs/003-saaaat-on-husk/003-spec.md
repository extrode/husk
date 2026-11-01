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
- The web app of saaaat.com builds with `dotnet build`/`dotnet publish` and runs in Chromium,
  Firefox and WebKit, with no Node or Deno at build time.
- The first build covers the app's pages, router, in-app navigation, translations and the three
  workspace packages, and every page behaves as it does in the Vite build.
- The Vite build stays working and unchanged on saaaat's `main` for the whole port; the port is
  judged by comparison with it.
- Gaps Husk has are found by this port and recorded, not worked around inside saaaat's source.

## Non-goals
- Service worker, web manifest and offline behaviour (the `@vite-pwa/sveltekit` replacement):
  milestone 3 territory, a later spec.
- Capacitor/Android packaging (`cap:build`, `PUBLIC_TARGET=capacitor`).
- The app's Content-Security-Policy and its Playwright end-to-end suite passing against the Husk
  output; the Husk build need not reproduce the response headers in `static/_headers`.
- Replacing saaaat's unit tests or its Deno tasks, or moving the workspace packages out of the
  saaaat repo.
- Deploying the Husk build to production.
- riwalqalb.com.

## User stories
- As saaaat's maintainer, I want to run `dotnet build` in the app and get a site I can open, so that
  I no longer need Node, pnpm or Deno to build the web app.
- As saaaat's maintainer, I want the port on a branch beside the Vite build, so that I can compare
  the two and drop the port without losing anything.
- As Husk's maintainer, I want the port to expose what a real app needs, so that the gaps become
  Husk work items.

## Requirements
- R1 The port lives on a branch of the saaaat repo. Nothing on its `main` changes, and the Vite
  build is still runnable from `main`.
- R2 One command builds the app; it needs only the .NET SDK and the packages Husk already depends on.
- R3 Every page of the app (the 17 the corpus test renders, and every route in `src/routes/`) opens
  at its own address, after a reload, and with a trailing slash, as it does in the Vite build.
- R4 Each page shows the same visible text as the same page in the Vite build, for the same
  language and the same data, in all three browsers.
- R5 In-app navigation, Back and Forward, redirects, query parameters and the error page behave as
  in the Vite build (spec 002 behaviour, now against the real app).
- R6 The three workspace packages resolve by their package names from the app's source, as TypeScript
  sources, with no change to the imports in the app or the packages.
- R7 All shipped translations are available and switching language works; right-to-left languages
  lay out right to left.
- R8 The bundled city dataset reaches the browser from the built site and the pages that use it
  work, without a copy script run beforehand.
- R9 No page logs a console error on load in any of the three browsers.
- R10 The app's source needs no edit that exists only for Husk, beyond what a branch needs to point
  its build at Husk (project file, package references). A source edit that Husk makes unavoidable is
  recorded as a Husk gap, with the reason.
- R11 Building twice with no change compiles nothing the second time (law 002 holds on the real app).
- R12 The build output size and the time of a clean build are measured and written down next to the
  Vite build's.
- R13 Dependencies the app imports at runtime that Husk does not carry (the `@capacitor/*`
  packages are imported by the web code) are resolved in a way recorded in the plan; a page that
  does not use them does not fail because they are missing.

## Acceptance criteria
- Given a fresh clone of the port branch and the .NET SDK, when `dotnet build` runs in the app,
  then it exits 0 and `dotnet run`, or serving the publish output, opens the home page.
- Given the Vite build and the Husk build of the same commit, when each page is opened in each
  browser in each shipped language, then the visible text of the two builds is equal (R4).
- Given the port branch checked out, when `git diff main -- <app sources>` is read, then every
  difference is a build file or an entry in the recorded Husk-gap list (R10).
- Given a built app, when a second `dotnet build` runs with no change, then no component is
  compiled (R11).
- Given a recorded gap, when it is listed, then it names the saaaat file that hit it, what Husk did,
  and whether the fix belongs to Husk or to saaaat.

## Law candidates
- For every page of the app, the Husk build's visible text equals the Vite build's (R4).
- The port never changes anything reachable from saaaat's `main` (R1).
- After any sequence of source saves under `dotnet watch`, the served app equals a clean build of
  the same sources (spec 002 behaviour on the real app).

## Open questions
- None blocking. For `/plan`: how `@capacitor/*` imports are satisfied (R13); how the i18n
  translation files become modules; how the dataset is served (R8); where the saaaat checkout
  used by CI lives, since the port branch is in another repository.
