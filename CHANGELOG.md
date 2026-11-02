# Changelog — husk

What shipped, newest first. One entry per release, not per commit: the git log already records
every commit, and a changelog that repeats it is read by nobody.

An entry is added when `dev` merges to `main` and gets its semver tag. Work that is merged into
`dev` but not yet released belongs in `status/previous.md`, not here.

Format is [Keep a Changelog](https://keepachangelog.com/), versions are semver. Use only the
sections that apply; drop the empty ones rather than leaving them blank.

## [Unreleased]

### Added

- saaaat.com web builds and runs on Husk: a port project on the saaaat branch `feat/husk-port` (`apps/web-husk`, no edit to the app or packages). `SaaaatPortTests` and the parity test against a Vite reference recorded per browser (`tests/references/saaaat/`) pass in Chromium, Firefox and Safari; notes in `docs/architecture/006-saaaat-port.md`.

### Known gaps

- The router does not announce navigation to screen readers (SvelteKit's route announcer).

## [0.1.0-preview.8] — 2026-11-01

### Added

- `HuskStylesheet`: a stylesheet imported for its side effect (`import '$lib/app.css'`) becomes a module that adds the CSS ahead of `husk.css`. A missing file is HUSK103.
- `HuskJson`: a JSON file imported with an import attribute (`with { type: 'json' }`) is served, hashed with `HuskHashNames` and never preloaded. A missing file, or one outside its `Root`, is HUSK104.
- A recipe for serving a folder outside the project with the SDK's own `Content` item (`docs/03-reference/msbuild.md`).

### Known gaps

- Development: an edit to a `HuskStylesheet` or `HuskJson` file shows after the next build, not on save; a saved plain `.ts` that uses `import.meta.env` is served unrewritten until the next build.

## [0.1.0-preview.7] — 2026-11-01

### Added

- A SvelteKit-style client router (spec 002): `HuskRoutes`, `HuskShell`, `HuskAlias`, `HuskImport`, `HuskEnv` items and the `HuskHooks` property build `src/routes/` into a single-page app with `$app/navigation`, `$app/state`, `$app/environment` and `@sveltejs/kit` shims. Unsupported SvelteKit features fail with HUSK300 to HUSK306. `UseHuskSpa()` serves the single-page fallback in Development and `husk-deploy --spa` on Cloudflare. See `docs/02-guides/router.md`. Not covered: Content-Security-Policy, service worker and manifest. In Development, adding or removing a route file builds the project again and reloads the browser under `dotnet watch`.

### Fixed

- Router: Back and Forward keep the history position in step with the page on screen, including after a reload and when `beforeNavigate` cancels the move; a link followed during a Back continues from where the Back reached.
- Router: the error page shows the URL that failed and still renders when the root layout or the error page cannot load; an unreadable `app.html` is HUSK305; env values in `app.html` placeholders are no longer HTML-encoded.
- Development: the route watcher survives a file it cannot read, builds for a change that lands as a build finishes, and the browser refreshes once more after a route rebuild.
- A page removed from `@(Content)` is no longer built or published.

---

## [0.1.0-preview.6] — 2026-10-31

### Changed

- The install is one build reference: `Extrode.Husk.Build` depends on `Extrode.Pare.Build` 0.7.2 (was 0.7.0), whose `buildTransitive/` targets reach the app, so the app no longer references Pare itself. A direct `Extrode.Pare.Build` reference keeps working at 0.7.2 or later; an older one is NU1605. HUSK002 now means a direct `Extrode.Pare.Build` reference excludes its build assets.

---

## [0.1.0-preview.5] — 2026-10-31

### Fixed

- A reused build process (`dotnet build` keeps them for a few minutes, across projects) that had loaded V8 for one Husk could load a second V8 library for another (a different ClearScript version, or a Husk outside the NuGet cache); V8 then spins and dies. Husk now stops with HUSK004, which says to run `dotnet build-server shutdown`. Husk versions on the same ClearScript share one library and build as before. A Husk older than this one does not record what it loaded, so the check starts with this version.

---

## [0.1.0-preview.4] — 2026-10-31

`v0.1.0-preview.3` was tagged but never published: its release build failed on two Linux/Windows-only test bugs, and release tags cannot be moved.

### Changed

- Pare 0.7.0 (was 0.6.1); the install snippets and HUSK002's message name it.
- Release workflow: tests and packing run in a job with a read-only token; the publishing job takes the packed artifact and waits for the `packages` environment's reviewer. Every workflow job has a timeout.

### Fixed

- The package README on nuget.org: its links and images were relative to the repo and pointed nowhere. Packing rewrites them to absolute URLs at the release tag.
- HUSK003 checks the NuGet folder that holds Extrode.Husk.Build, the folder the compiler loads V8 from, instead of the global packages folder; with a fallback package folder the two could disagree.
- A HUSK200 the build treats as an error (`TreatWarningsAsErrors`, `WarningsAsErrors`) left the source's outputs written, so the next build found them up to date and passed. They are now left as they were, as with HUSK100.
- Deploy: an upload whose connection drops is sent again, like a 429 or 5xx (one that times out is not); a 429 waits the `Retry-After` it names, up to a minute; a directory link back up the tree is skipped instead of failing with "Too many levels of symbolic links", and other linked directories are still uploaded.

---

## [0.1.0-preview.2] — 2026-10-31

### Changed

- `Extrode.Husk.Build` no longer packs the six V8 natives (85.2 MB to about 6 MB). It depends on `Microsoft.ClearScript.V8.Native.<rid>` at exactly 7.5.1.1; the build and the Development app find the native in the NuGet cache. New error HUSK003 when it is missing. Package source mappings must allow `Microsoft.ClearScript.*`; an app pinning another native version conflicts, and lock-file consumers (`--locked-mode`) must regenerate `packages.lock.json` for the six new dependencies.
- Svelte pinned at 5.57.3 (was 5.57.2). In the test fixtures component output is unchanged; rune-module (`.svelte.js`, `.svelte.ts`) output changes with the compiler.

### Fixed

- A page URL containing `&` or `"` is escaped in the rewritten `src` and the modulepreload link.
- Compiler outputs are written to a temp file and moved over the old one, and the temp file is removed when the write fails.
- The build stamp covers the compiler's path, so a package upgrade rebuilds every source though no file is newer.
- Deploy retries uploads on 429 and 5xx, refuses `_headers` and `_redirects`, skips `.DS_Store`, and reports malformed API answers as errors instead of crashing.
- A dev map from a hashed build logs a warning that saves have no visible effect.
- A compiler file that briefly goes missing no longer leaves a disposed compiler cached for the life of the process.
- Hashed module copies are written atomically, and a truncated copy is written again, so an interrupted build no longer publishes an empty fingerprinted file.
- Deploy trims the Cloudflare token and reports one with a newline inside as exit 1, not a crash; the Development stylesheet is written atomically.
- Both packages carry the Husk icon. `0.1.0-preview.1` was built from a tag that predates it, so nuget.org shows the default icon for that version.
- Both packages are also published to GitHub Packages (`release.yml`), so the repository's Packages tab lists them.

---

## [0.1.0-preview.1] — 2026-10-30

### Added
- License: ISL-P 1.2 with the ISL-OE 1.2 output exception, as Pare. `LICENSE.md` and `NOTICE.md` at the root;
  both packages carry them and declare the license file.
- `Extrode.Husk.Build`: `.svelte`, `.svelte.ts` and `.svelte.js` under `wwwroot` compile on
  `dotnet build` with the vendored Svelte 5.57.2 in embedded V8, no Node or Deno. Output is
  byte-identical to Node running the same compiler: the fixtures, the Svelte repo's 4720 sources
  and two apps' 430, 0 differences.
- Incremental per source, parallel across engines, one `husk.css` for every component's CSS, the
  `svelte` runtime served unbundled under `/_husk/` behind an import map inlined into pages at
  `<script type="importmap" data-husk></script>`, source maps back to the `.svelte`.
- Publish ships compiled modules, `husk.css` and the runtime with compressed copies, production
  builds (`esm-env` development off), and no sources or dev files.
- `Extrode.Husk.AspNetCore`: `builder.AddHusk()` recompiles a saved component in the running
  Development app with warm engines (6 ms measured after the first save); `dotnet watch` reloads.
- Error codes HUSK001, HUSK002, HUSK100 to HUSK102, HUSK200 (`docs/03-reference/error-codes.md`), a sample
  app (`samples/svelte-app`), tested in Chrome, Firefox and Safari.
- Laws L001 (identical to Node), L002 (no change, no compile), L003 (a failed compile keeps the
  last good output), each proven.
