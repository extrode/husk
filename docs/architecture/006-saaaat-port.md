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
