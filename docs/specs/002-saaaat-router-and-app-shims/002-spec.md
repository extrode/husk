# 002 — saaaat client router and `$app/*` shims
Status: draft
Created: 2026-10-31

## Problem
saaaat.com's web app (`apps/web`, the first app: decision 004, milestone 2) is a SvelteKit
single-page app, and Vite plus SvelteKit are what turn its `src/routes/` folder into pages, layouts,
parameters, redirects and navigation. Husk (spec 001) compiles the components but has no router,
so nothing tells a page which route it is, and the 28 imports of `$app/navigation`,
`$app/state` and `$app/environment` have nothing to import. Without both, the app cannot run on
Husk and still needs Deno. The need is narrow: saaaat uses a small part of SvelteKit's client half
(measured in `apps/web`, 2026-10-31), and server rendering, prerendering and server code are off
(`ssr = false`, empty server hooks).

## Goals
- saaaat's `apps/web` routes, layouts, parameters, redirects and in-app navigation work in
  Chromium, Firefox and WebKit from files Husk builds, with no Node or Deno.
- The app's source needs no edits for routing: the same `src/routes/` tree and the same
  `$app/*` imports it has today.
- Navigating inside the app never reloads the page and keeps the browser's back and forward
  buttons and address bar in step with what is on screen.
- A route's shape comes from the folder tree at build time, so an unknown address is detected
  without a server.

## Non-goals
- Server rendering, prerendering, server `load` functions, form actions, API endpoints, hooks,
  adapters. saaaat does not use them.
- Service worker, web manifest and Capacitor packaging (`@vite-pwa/sveltekit` replacement): a
  separate spec; this one leaves the app's own service worker registration working but does not
  build one.
- A Content-Security-Policy without hashes and the import map's CSP story beyond what spec 001
  already gives.
- Features saaaat does not use: route matchers, optional or rest parameters, `invalidate` by
  dependency, shallow routing (`pushState`), `navigating`, `$app/forms`, `$app/stores`,
  `$env/*`. Each fails the build with a message naming it, not silently.
- Running SvelteKit's own test suite, or being a general SvelteKit replacement for other apps.
- riwalqalb.com (milestone 3).

## User stories
- As the saaaat developer, I want to build `apps/web` with `dotnet build`, so that the Deno
  toolchain can be dropped.
- As a user of saaaat.com, I want links, the back button and a bookmarked or refreshed address to
  land on the same page as today.
- As the saaaat developer, I want a bad settings slug to redirect to the Appearance section as it
  does now, so that stale bookmarks keep working.
- As a Husk maintainer, I want a feature the router lacks to fail loudly at build time, so that a
  gap is found by the build and not by a user.

## Requirements
- R1 Husk derives the app's routes from `src/routes/`: a folder with a page is a route; a folder
  in brackets is a parameter; a folder in parentheses groups routes without adding to the address.
- R2 Layouts nest as SvelteKit's do: a route renders inside every layout above it, outermost first,
  and a group's layout applies to its routes only.
- R3 A page and its layouts receive the route parameters and the result of their `load`
  function, from `+layout.ts` and `+page.ts`, run in the browser before the page renders.
- R4 A `load` function can redirect (`redirect(status, location)`), and the browser lands on the
  target with the address bar showing it, replacing the redirecting entry in history.
- R5 An address that matches no route, and an error thrown by a `load` function, render the app's
  `+error.svelte` with the status and message, inside the layouts that can render.
- R6 `$app/navigation` provides `goto` (with `replaceState`, `keepFocus`, `noScroll`,
  `invalidateAll`), `beforeNavigate`, `afterNavigate` and `preloadCode`, behaving as SvelteKit's do for
  the ways `apps/web` calls them.
- R7 `beforeNavigate` callbacks can cancel a navigation or turn it into a full page load to the
  target, as the app's update handling needs.
- R8 `$app/state` provides `page` with `url`, `params`, `status` and `error`, reactive: a
  component reading `page.url.searchParams` updates when the address changes.
- R9 `$app/environment` provides `browser` (true in the browser) and `dev` (true in a Development
  build). (`setDev` is only in saaaat's test mock, so it is not needed.)
- R10 `<a href>` clicks to an address inside the app navigate in the app; clicks with a modifier
  key, `target`, `download`, `rel=external`, or to another origin are left to the browser.
- R11 The browser's back and forward buttons restore the previous page, scroll position and
  address; a refresh of any address loads the app and shows that address.
- R12 The app is served so that every address inside it returns the app's HTML (a single-page
  fallback) in Development and in the published output and on Cloudflare's static assets.
- R13 The `$lib` alias, `$types` imports and `import.meta.env` uses in `apps/web` resolve with no
  source edits (types are erased). The one `import.meta.env` value the app reads at run time,
  `PUBLIC_BACKEND_URL`, is set per build from the project, and an unset value is the empty string
  the app already handles.
- R14 Routes and their modules load on demand: a route's code is fetched when first needed
  (or on `preloadCode`), not with the first page.
- R15 A feature outside the supported set (see Non-goals), found in the routes or imports, fails
  the build with an error code and the file and line.
- R16 In Development, saving a route file or layout updates the route table and reloads the
  browser (spec 001's R11 behaviour extends to the router's inputs).

## Acceptance criteria
- Given `apps/web` as it is, when it is built with Husk alone, then every one of its pages (17 `+page.svelte` files on 2026-10-31)
  renders at its address in the three browsers with no console error.
- Given the app is open on `/month`, when the user follows an in-app link, then the document is
  not reloaded (a value set on `window` before the click is still there after).
- Given `/settings/nonsense`, then the browser ends on `/settings/appearance` and back goes to the
  entry before `/settings/nonsense`.
- Given `/no/such/page`, then `+error.svelte` shows status 404.
- Given a page reading `page.url.searchParams.get('year')`, when `goto('?year=1447', { replaceState: true })`
  runs, then the page shows the new value and history length is unchanged.
- Given a `beforeNavigate` handler that cancels, when a link is followed, then the address and page
  are unchanged.
- Given a route file using an unsupported feature, then the build fails with an error naming the
  feature, file and line.
- Given a Development app, when a route file is saved, then the browser reloads and shows it.

## Law candidates
- For every address, route resolution returns the same route and parameters on every run, and the
  address that a route is given back by navigation equals the address that was requested (after any
  redirect).
- After any sequence of in-app navigations, `page.url` equals the browser's address.
- For every route folder in `src/routes/`, exactly one address pattern results, and no two route
  folders produce the same pattern (a clash fails the build).
- For every unsupported feature in the list, the build fails and does not write a partial route
  table.

## Open questions
- Where the routes' `load` functions and the `$app/*` shims ship (inside the build package or a
  separate runtime package) is a `/plan` decision, not asked here.
- How a project sets `PUBLIC_BACKEND_URL` (MSBuild property, environment, file) is a `/plan`
  decision; the requirement is only that it can be set per build.
