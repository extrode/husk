# Changelog — husk

What shipped, newest first. One entry per release, not per commit: the git log already records
every commit, and a changelog that repeats it is read by nobody.

An entry is added when `dev` merges to `main` and gets its semver tag. Work that is merged into
`dev` but not yet released belongs in `status/previous.md`, not here.

Format is [Keep a Changelog](https://keepachangelog.com/), versions are semver. Use only the
sections that apply; drop the empty ones rather than leaving them blank.

## [Unreleased]

Merged into `dev`, not yet tagged.

### Added
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
- Error codes HUSK001, HUSK002, HUSK100 to HUSK102, HUSK200 (`docs/error-codes.md`), a sample
  app (`samples/svelte-app`), tested in Chrome, Firefox and Safari.
- Laws L001 (identical to Node), L002 (no change, no compile), L003 (a failed compile keeps the
  last good output), each proven.

---

## [0.1.0] — <date>

### Added
- (nothing yet)
