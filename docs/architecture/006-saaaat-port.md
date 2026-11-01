# 006 — The saaaat port

Notes for spec 003 (`docs/specs/003-saaaat-on-husk/`). Sections are added as the tasks land.

## Spikes

### P1 — reference determinism (T3, 2026-11-01)

Measured with the Vite build of saaaat commit `c553e0f4` (macOS, Node 24.16.0), served by `StaticServer`
with the pinned conditions of the plan, two recordings per browser, 17 pages, English and Arabic.

- **Two recordings agree in Chromium, Firefox and Safari** with no ignore rules: nothing needs to be masked.
- **Pinning that sufficed:** the clock frozen at 2026-11-01T12:00:00Z by a script at the start of `<head>`
  (`SaaaatParityTests.Pinned`), the four stored keys seeded only when absent, a 1280x900 window.
- **Wait heuristic:** the page text is read every 200 ms and counts as settled after five equal non-empty reads
  (one second), at most 15 s.
- **Time zone:** not pinned and not needed. The pages use the stored location's zone (`Asia/Riyadh`), not the
  browser's. Revisit if a recording ever differs across machines.
- **Browsers differ from each other** (the text is not byte-equal across Chromium and Firefox), so references are
  recorded per browser, as the plan has them.
- The Vite build prints a version stamp (`<commit>-<date>`) into the build; it is not in the page text.
- Build served as: prerendered `<page>.html` files, with `index.html` as the single-page fallback.

### P2 — a side-effect stylesheet import (T4, T7)

`import '$lib/design/utilities.css'` (`+layout.svelte:8`) has no JavaScript to run, so the browser needs a module
for the specifier. Chosen: a **generated module per stylesheet** (`HuskStylesheet` item), listed in the build file,
resolved through the import map like any alias (`$lib/app.css`). The module adds the CSS text in a `<style>` placed
**before the `husk.css` link**.

- **Cascade order:** imported stylesheets, then `husk.css` (component styles). A component's own style wins a tie
  over an imported one. The Vite build also puts a layout's imported CSS ahead of the component styles.
- **Why inline text, not a `<link>`:** the module changes when the CSS does, so hashed names and the browser cache
  follow without a second asset kind. Cost: `url(...)` and `@import` inside the CSS are not rewritten, and the
  stylesheet arrives after the first module runs, not in the HTML head (a flash is possible on a slow load).
- Checked in Chromium, Firefox and Safari by `StylesheetImportTests` (imported style applies, a component style wins).

### P3 — serving `apps/web/static` as it is (T5, T9)

**The SDK's own items cover it; no `HuskStatic` item.** A `Content` item with a `Link` below `wwwroot/` serves a
folder outside the project and publishes it, with exclusions in `Exclude`:

```xml
<Content Include="$(SaaaatRoot)/apps/web/static/**" Exclude="$(SaaaatRoot)/apps/web/static/_app/**;$(SaaaatRoot)/apps/web/static/_headers"
         Link="wwwroot/%(RecursiveDir)%(Filename)%(Extension)" CopyToPublishDirectory="PreserveNewest" />
```

`StaticFolderTests` publishes such a folder and checks that a nested file is in `out/wwwroot` and the excluded
`_app/` and `_headers` are not. Content types are the host's, not Husk's.

### P4 — `import.meta.env` in Pare-compiled `.ts` under Development (T6, T10)

Read from the code (not run). A build rewrites `import.meta.env` to `globalThis.__huskEnv` in every served `.js`
output, Pare's own `.ts` output included (`HuskRoutes.RewriteEnv`). In Development, Husk's `IPareCompiler`
(`HuskCompiler.RewriteEnv`) rewrites only the sources Husk compiles (`.svelte`, `.svelte.ts`, `.svelte.js`). A
saved plain `.ts` goes through Pare's own strip step, and `IPareCompiler` is not a hook on that output.

- **Consequence:** after a `.ts` file containing `import.meta.env` is saved under `dotnet watch`, it is served
  unrewritten until the next build, so `import.meta.env.X` throws in the browser. saaaat has one such file
  (`apps/web/src/lib/backend-env.ts`).
- **Decision (T10):** serve-time rewriting does not extend to Pare's `.ts` without a Pare change. Law candidate 3
  (watch equals clean build) is **narrowed to Husk-compiled outputs and the shell**; the gap is listed here and in
  `work/todo.md` as a Pare extension point (a post-process hook on its output). No Husk code changes for it.
