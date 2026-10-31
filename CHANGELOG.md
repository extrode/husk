# Changelog — husk

What shipped, newest first. One entry per release, not per commit: the git log already records
every commit, and a changelog that repeats it is read by nobody.

An entry is added when `dev` merges to `main` and gets its semver tag. Work that is merged into
`dev` but not yet released belongs in `status/previous.md`, not here.

Format is [Keep a Changelog](https://keepachangelog.com/), versions are semver. Use only the
sections that apply; drop the empty ones rather than leaving them blank.

## [Unreleased]

Merged into `dev`, not yet tagged.

### Changed

- `Extrode.Husk.Build` no longer packs the six V8 natives (85.2 MB to about 6 MB). It depends on `Microsoft.ClearScript.V8.Native.<rid>` at exactly 7.5.1.1; the build and the Development app find the native in the NuGet cache. New error HUSK003 when it is missing. Package source mappings must allow `Microsoft.ClearScript.*`; an app pinning another native version conflicts.
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

---

## [0.1.0-preview.2] — 2026-10-31

### Fixed
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
