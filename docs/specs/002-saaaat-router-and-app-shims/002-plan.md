# 002 — Plan
Spec: ./002-spec.md

## Approach
The router is browser JavaScript that Husk ships, plus build-time code that turns the folder tree
into data for it. Three parts:

1. **Build time (C#).** A new `RouteTable` in `Extrode.Husk.Compiler` (shared by the build task and
   the Development app, as the compiler is) reads the route root (default `src/routes`) and writes
   `routes.js`: an ES module exporting the route list, each route with its address pattern, its
   layout chain, its `+page`/`+layout` modules and its `load` modules as dynamic `import()` of the
   compiled files' source URLs (the import map resolves them, so hashing from T6 applies unchanged).
   It enforces R1, R2, R15 and the clash law. A new MSBuild task `HuskRoutes` and a target run it
   after `HuskCompile`.
2. **Browser runtime (plain JS, no build step, shipped in the package).** `router.js` (matching,
   `load`, redirect, history, link interception, `goto`, navigation hooks), `app/navigation.js`,
   `app/state.svelte.js`, `app/environment.js`, and `start.js` (runs the client `init` hook, loads the
   root layout, mounts). The one shim that needs runes, `$app/state`'s `page`, is a `.svelte.js`
   module compiled by Husk's own compiler as part of the app build, like any rune module, so it is
   byte-identical to the official compiler's output (R4). Everything is served under `/_husk/`
   with the vendored Svelte runtime and hashed the same way.
3. **Serving.** `index.html` is generated from the app's own `app.html` (R20). A single-page
   fallback is added in the Development app and, for Cloudflare, `Extrode.Husk.Deploy --spa`
   sets the Workers assets `not_found_handling` to `single-page-application`.

Compiled code is still not rewritten (spec 001's rule). The import map carries everything that
bridges SvelteKit's names to files: `$app/*` to the shims, `$lib/<file>` to each output file, the
workspace packages and Capacitor to their files. Alternatives rejected:
- **Reuse SvelteKit's client (`@sveltejs/kit`) in the browser:** it needs its generated manifest,
  Vite's virtual modules and `$app/*` internals from a bundle; that is Node/Vite by another name,
  and its size would dwarf saaaat's small use.
- **Rewrite `$app/*`/`$lib` specifiers in compiled output:** needs a parser Husk would own and breaks
  byte-identity (R4). The import map does the same job for free.
- **Pre-render a route manifest as JSON read by a generic router:** the dynamic `import()` list
  is the manifest; a JSON file would need a second loader.

`import.meta.env` is the one exception to "no rewriting": the browser has no such object, and the
specifier cannot be remapped, so the build replaces the exact token `import.meta.env` in app output
files that contain it, in the asset stage (`HuskAssets`), after Pare/Husk have written them. The
compiler's result stays identical; this is a documented post-step (R13).

## Tech / dependencies
No new packages. ClearScript's V8 (already approved) runs the router's matching and load logic in
tests without Node: the router is written so `matchRoute` and `resolveLoad` are pure functions,
loadable in the engine. Browser behavior is tested with Selenium 4.52.0 (approved) in Chrome,
Firefox and Safari. Verified facts: Cloudflare Workers assets `not_found_handling:
"single-page-application"` serves `index.html` only for navigation requests
(`Sec-Fetch-Mode: navigate`), so a missing module request is a 404 (R12); the Deploy command
already writes `not_found_handling` as `"none"` (`CloudflareApi.cs:42-43`). Source:
developers.cloudflare.com/workers/static-assets/routing/single-page-application.

## Changes by area
- `src/Extrode.Husk.Compiler/RouteTable.cs` (new): folder listing in, route model and `routes.js`
  text out; pure, tested directly. R1, R2, R3 (module wiring), R5 (error page), R15 (unsupported
  route files), R17 (hooks module), law 3.
- `src/Extrode.Husk.Compiler/ImportMapWriter.cs`: alias entries (`$lib/...`), package entries,
  `$app/*` shim entries; exact keys per file so T6's hashing still applies. R13, R18.
- `src/Extrode.Husk.Build/HuskRoutes.cs` (new task) and `build/Extrode.Husk.Build.targets`: items
  `HuskRoutes` (route root, default `src/routes`), `HuskAlias` (`$lib` -> `src/lib`), `HuskPackage`
  (name, source dir, entry), `HuskImport` (name, project-supplied file, for Capacitor), `HuskShell`
  (`app.html`), `HuskEnv` (name, value; `PUBLIC_BACKEND_URL`). Default off unless a `HuskRoutes`
  item exists, so spec 001 projects are unchanged. R1, R13, R18, R20.
- `src/Extrode.Husk.Build/HuskAssets.cs`: `import.meta.env` substitution; unsupported
  `$app/*`/`$env/*`/`@sveltejs/kit` named imports found in compiled output fail with a HUSK3xx
  code, file and line (R15).
- `src/Extrode.Husk.Build/runtime/` (new, packed beside the vendored runtime): `router.js`,
  `start.js`, `app/navigation.js`, `app/state.svelte.js`, `app/environment.js`. R4 to R11, R14, R17.
- `src/Extrode.Husk.AspNetCore/` : `HuskDevelopmentMap` and `HuskSession` watch the route root and
  regenerate `routes.js` and reload on add, remove or rename of a route file (R16); a
  `UseHuskSpa` fallback that serves the shell for extension-less addresses and returns 404 for
  anything under `NoFallbackPrefixes` (default `/_husk/`, `/data/`) (R12).
- `src/Extrode.Husk.Deploy/`: `--spa` option sets `not_found_handling`; tests extend `DeployTests`. R12.
- `src/Extrode.Husk.Compiler/PageWriter.cs`: the shell gets the import map, stylesheet, modulepreload
  of the entry modules and `<script type="module" src="/_husk/start.js">`; `%sveltekit.head%` and
  `%sveltekit.body%` are accepted as placeholders (R20), `data-sveltekit-*` ignored.
- `docs/`: user docs for the new items, error codes, `docs/architecture/005-router.md` for the
  runtime's design; `work/todo.md` gets the `src/routes/*.test.ts` exclusion note if not solved.

## Data / interfaces
- `routes.js`: `export const routes = [{ id, pattern: ["month"] | [{param:"section"}], layouts:
  [() => import("/routes/+layout.svelte"), ...], page: () => import(...), loads: [{ level, module:
  () => import(".../+page.ts") }], error: () => import(...) }]; export const hooks = () =>
  import("/hooks.client.ts") | null; export const errorPage = ...`
- Router (browser): `start({ routes, hooks, base })`. Pure helpers exported for tests:
  `matchRoute(routes, pathname) -> {route, params} | null` (trailing slash removed, R1) and
  `runLoads(route, params, url) -> {data} | {redirect:{status,location}} | {error:{status,message}}`.
- `$app/*` surfaces exactly: `goto(url, {replaceState, keepFocus, noScroll, invalidateAll})`,
  `beforeNavigate(cb)`, `afterNavigate(cb)`, `preloadCode(...paths)`; `page` with `url`, `params`,
  `status`, `error`; `browser`, `dev`. Any other named export is a build error (R15).
- New diagnostics: HUSK300 route clash, HUSK301 unsupported route feature (matcher, rest/optional
  parameter, `+server`, `+page.server`), HUSK302 unsupported `$app/*`/`$env/*`/kit import,
  HUSK303 unsupported `kit` option in a project that sets one. Added to `docs/error-codes.md`
  (its test pins every code).
- MSBuild items as in "Changes", properties `HuskRoutesRoot`, `HuskSpaFallback`.

## Risks & mitigations
- **Sources outside `wwwroot`.** saaaat keeps `src/` and `../../packages/` outside any `wwwroot`;
  spec 001 only places outputs for sources below `wwwroot`, and `HuskCompile.cs:153` already
  errors when an output lands on its source. What Pare does with `Link`/relative paths for such
  items is not documented here. Mitigation: task 1 is a spike on a fixture with sources in `../x/`;
  if Pare cannot map them, the fix is a Pare extension point (in Pare's repo, per AGENTS.md), and
  plan 002 stops at that task with a Needs-decision.
- **`import.meta.env` rewrite (R13)** touches Pare's `.ts` output (`backend-env.ts`) after Pare
  writes it. Mitigation: do it in `HuskAssets` on the obj/ outputs with write-if-changed, test with
  the incremental no-change law (L002) still holding.
- **Browser-only behavior** (history, redirect hop, link interception) cannot be unit-tested in V8.
  Mitigation: Selenium tests in three browsers against a fixture app that mirrors saaaat's routes;
  the redirect/back criterion is measured in the real app first (spec open question).
- **Router scope creep.** Mitigation: R15 makes every unsupported feature an error with a code, so
  a gap is found by the build.
- **CSP** (spec Non-goals): Husk reads no `svelte.config.js`, so the app ships without its policy
  until the service-worker/headers follow-up. Flagged in the user docs and the changelog; needs the
  user to accept before saaaat.com goes live on Husk (Needs-decision at release, not at build).
- **Input handling / data exposure:** route addresses and `redirect` locations are untrusted input.
  `goto`/redirect targets resolve against `location.origin` and a different origin is a full page
  load, never a cross-origin `pushState` (which throws). Tests cover `//evil`, `javascript:`.
- **Hashed import map size** grows with `$lib` exact keys (saaaat: 119 `$lib` imports); measured at
  the end against the Cloudflare limits (T8 numbers).

## Coverage check
| R | Covered by |
|---|---|
| R1 | RouteTable (folders, groups, params, `+` files only, trailing slash in `matchRoute`) |
| R2 | RouteTable layout chains; router renders outermost first |
| R3 | `routes.js` loads; `runLoads`; page options accepted and ignored in RouteTable |
| R4 | `runLoads` redirect result; `router.js` replaceState |
| R5 | `routes.js` `errorPage`; `runLoads` error result |
| R6 | `app/navigation.js` |
| R7 | `beforeNavigate` payload in `app/navigation.js` |
| R8 | `app/state.svelte.js` (compiled by Husk) |
| R9 | `app/environment.js` |
| R10 | `router.js` link interception |
| R11 | `router.js` popstate, history |
| R12 | `UseHuskSpa` prefixes, Deploy `--spa` |
| R13 | ImportMapWriter alias entries, `import.meta.env` step, type erasure by Pare |
| R14 | dynamic `import()` in `routes.js`, `preloadCode` |
| R15 | RouteTable (HUSK300, 301), `HuskAssets` (HUSK302), HUSK303 |
| R16 | AspNetCore session watches the route root |
| R17 | `start.js` awaits `hooks.init()` before the root layout import; RouteTable `hooks` |
| R18 | `HuskPackage`/`HuskImport` items and ImportMapWriter entries |
| R19 | targets read no `svelte.config.js`; HUSK303 |
| R20 | PageWriter shell and placeholders; `start.js` mounts into the app root |
All 20 covered. The spec's four law candidates map to: law 1 and 2 -> `matchRoute` and browser
sequence tests; law 3 -> RouteTable clash test; law 4 -> build test that no `routes.js` exists
after an R15 failure.
