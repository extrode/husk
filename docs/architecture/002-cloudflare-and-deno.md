# 002 Cloudflare deployments, and replacing Deno in saaaat.com and riwalqalb.com

Surveyed 2026-10-17, read-only: `../../ikhbat.org/saaaat.com` (dev at c5d31cb8) and
`../../ikhbat.org/riwalqalb.com` (dev at 8dd069b0). Cloudflare facts from developers.cloudflare.com
the same day.

## Short answer

| | Husk can replace Deno? | What stands in the way |
|---|---|---|
| saaaat.com web | **Yes, with a client router.** | SvelteKit routing and `$app/*`; tests and type checks run under Deno |
| riwalqalb.com frontend | **Not while it supports Chrome 49 / Safari 11.** Yes if the floor moves to import-map browsers | `@vitejs/plugin-legacy`, eight Vite plugins of its own, 312 vitest tests |
| riwalqalb.com worker | **No JavaScript runtime can go: it runs on Cloudflare's workerd.** Deno is only its tooling | wrangler for deploy, vitest, `@aws-sdk/client-s3` needs bundling |
| Cloudflare hosting | **Fits.** Husk output is static files | Deploy without wrangler means the Workers assets upload API; cache busting is Husk's job |

"Replace Deno completely" also has a floor: type checking. `svelte-check` and `tsc` are JavaScript
programs, and Husk does not type-check. Either they keep a runtime (in CI only, or both), or type
checks run through a native TypeScript compiler, if one exists for the version in use (to check).

## saaaat.com (`apps/web`)

- SvelteKit 2 with `adapter-static`, `prerender = true`, `ssr = false`, `fallback: 'index.html'`:
  a single-page app, no server code (`hooks.server.ts` is an empty placeholder).
- 16 pages, 3 layouts, 2 `+layout.ts`/`+page.ts` load files, a `(view)` group, one `[section]`
  parameter. 78 components. `$app/navigation` (10 imports), `$app/environment` (8), `$app/state` (7).
- Runtime imports: `svelte`, `svelte/store`, `svelte/reactivity`, `@capacitor/core` and
  `/preferences`, and three workspace packages in TypeScript (`@saaaat/core`, `i18n`, `geo-data`;
  78 files) that Pare strips.
- CSP in hash mode, built by SvelteKit for its inline bootstrap script.
- PWA via `@vite-pwa/sveltekit`; Android and iOS via Capacitor.
- Deno runs: `vite dev`, the data copy script, a Svelte precompile step, 130 tests (`deno test`
  with jsdom and `@testing-library/dom`), and `deno check`. CI builds with pnpm and Node
  (`turbo run build`), the backend is already .NET.

What Husk needs for it: compile the 78 components (done in the spike, all compile); a small
client-side router that reads `src/routes/` at build time (pages, layouts, groups, params, load
functions, `+error.svelte`); `$app/navigation`, `$app/environment`, `$app/state` shims over that
router; an import map for the runtime and Capacitor; a service worker and manifest in place of
vite-pwa; no inline script, so the CSP needs no hashes. That router is SvelteKit's client half
rebuilt for one app's needs: tier 1 plus part of tier 2 in Pare's ecosystem doc.

Tests: jsdom is a Node library. The .NET-native route is real browsers through
Microsoft.Playwright, which saaaat already uses for e2e. Note that Playwright's .NET package ships
its own Node driver, so "no Deno" is achievable, "no Node binary anywhere" is not while Playwright
is in the loop.

## riwalqalb.com

### Frontend (`frontend/`)

- Plain Svelte 5 with Vite, no SvelteKit: the closest fit to Husk's tier 1. 123 components, all
  compile in the spike.
- Runtime imports: `svelte`, `svelte/store`, `@capacitor/core`. Nothing else from npm.
- **Browser floor: Chrome 49, Safari 11, Firefox 60** (`LEGACY_TARGETS`), via
  `@vitejs/plugin-legacy` (Babel to SystemJS) and lightningcss lowering for Chrome 49 CSS.
  Unbundled modules with an import map need Chrome 89, Safari 16.4, Firefox 108. Svelte 5's
  runtime is modern JavaScript too. Keeping that floor means downlevelling, which needs a
  JavaScript tool (Babel or similar, which could run in the same V8 at build time) and a module
  loader. **This is the blocker for riwalqalb**, and it is a product decision, not a Husk one.
- `vite.config.ts` carries eight plugins of its own: embed page, old-browser CSS appendix, local
  m4b range server and local Pages files (dev), sitemap, service worker bundle and precache list,
  inline-script externalising, API proxy. Each is a C# rewrite or an ASP.NET Core dev-server
  feature.
- 312 test files on vitest with jsdom and `@testing-library/svelte`; 44 dev scripts under Deno
  (smoke, align, visual, embed e2e, security headers, Android sync).
- Deploy: `deno run -A npm:wrangler@^4 pages deploy dist` in GitHub Actions.

### Worker (`worker/`)

- A Cloudflare Worker in TypeScript, 52 source files: R2, KV, D1, a Durable Object with SQLite,
  a cron trigger, a custom domain.
- It runs on workerd. .NET cannot replace it; JavaScript stays. Deno is only its tooling: wrangler
  (dev, deploy, tail), vitest (30 test files), `tsc --noEmit`.
- Pare can strip its TypeScript. One npm import, `@aws-sdk/client-s3`, would need bundling or
  replacing with signed `fetch` calls to R2's S3 API (or R2 bindings, if the use allows).
- Deploying without wrangler means the Workers script upload API with its bindings and Durable
  Object migrations in the metadata part: possible, and a reimplementation of `wrangler deploy`.

## Cloudflare

- Husk's output is static files: component modules, the vendored runtime, an import map, HTML.
  That fits Cloudflare Pages and Workers static assets as they are used today.
- Cache busting: the spike's modules have stable names. Husk has to write content-hashed file
  names and an import map that points at them, and `<link rel="modulepreload">` for the first
  page, because the unbundled runtime alone is 107 modules (Cloudflare serves HTTP/2 and HTTP/3
  and compresses, which keeps that cheap; the waterfall depth is what preload addresses).
- Deploy without Node: the Workers static assets direct upload API is documented
  (`POST .../workers/scripts/:scriptName/assets-upload-session` with a manifest of hashes, then
  `POST .../workers/assets/upload?base64=true` per bucket with the upload JWT, then
  `PUT .../workers/scripts/:scriptName` with the completion JWT). A small .NET deploy command can
  do that. Pages deploys use wrangler; Cloudflare's Pages build image v3 has Node, Bun, Go, Python
  and Ruby, not .NET, so builds run in GitHub Actions with `actions/setup-dotnet` (as saaaat's
  backend job already does) and upload the output.
- Limits (checked 2026-10-31 against [Cloudflare's Workers limits page](https://developers.cloudflare.com/workers/platform/limits)
  and the [September 2025 changelog](https://developers.cloudflare.com/changelog/post/2025-09-02-increased-static-asset-limits/)):
  static asset files per Worker version 20,000 on Free, 100,000 on Paid (the higher cap needs
  Wrangler 4.34.0+ and does not apply to older clients); 25 MiB per file on both plans.
  The numbers below are measured (sample) or counted from source (apps), so apps are estimates.

  | Site | Files to upload | Largest file | Free (20,000) | 25 MiB |
  |---|---|---|---|---|
  | `samples/svelte-app` (`dotnet publish`, measured) | 277 (831 with `.br`/`.gz`) | 38 KB | 1.4% | fits |
  | saaaat web (estimate) | about 800 | `intro.mp3`, 6.2 MB | about 4% | fits |
  | riwalqalb frontend (estimate) | about 2,400 | 386 KB (`Amiri-Regular.woff2`) | about 12% | fits |

  How the estimates were made: components and TypeScript modules counted in source (saaaat
  82 `.svelte` + 85 `.ts` in `apps/web/src` + 82 in `packages/`; riwalqalb 123 + 155), plus the
  sample's runtime (about 130 files), plus every file in `static/` or `public/` (saaaat 14,
  riwalqalb 1,793), with the module part doubled because the publish output carries each module
  under both its plain and its hashed name (sample: 134 hashed, 143 plain). Neither app can be
  built by Husk yet (router and `$app/*` shims are T12), so these are not measurements.
- Two things in the sample's publish output matter for a deploy: `.br` and `.gz` files sit beside
  every compressed-able file (554 of the 831), and Cloudflare compresses at the edge, so the
  deploy should upload a tree without them; and the plain-named duplicates of hashed files double
  the module count for no benefit on a static host. Neither blocks any limit.
