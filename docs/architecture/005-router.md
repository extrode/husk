# 005 — Router and `$app/*` shims
Design notes for spec 002; plan: docs/specs/002-saaaat-router-and-app-shims/002-plan.md.

## Sources outside `wwwroot` (T1, 2026-10-31)
Pare places a compiled source by `PareRelativePath`, or by `PareServeAt` plus the source's
recursive directory. Husk copies item metadata onto its `PareCompileSource` items, so
`<HuskComponent Include="src/**/*.svelte" PareServeAt="app" />` serves `src/lib/A.svelte` at
`/app/lib/A.svelte.js` with an import-map entry `/app/lib/A.svelte`. Pinned by
`OutsideSourcesTests`. No Pare change needed. A `..\` include (workspace packages) uses the same
metadata; it is exercised in T8.

## Shape (2026-10-31)
- `RouteTable` (Compiler) scans `src/routes` and writes `routes.js`: one dynamic `import()` per route
  and layout, resolved through the import map, so a route's code loads on first use.
- The browser half is plain JS in `src/Extrode.Husk.Build/runtime/` (`router-core.js` is pure and runs
  in V8 under test). `Root.svelte`, `ErrorFallback.svelte` and `app/state.svelte.js` are compiled by
  Husk from the package folder with `PareServeAt="_husk"`.
- The shell is `app.html` with the body placeholder replaced by `<div id="husk-body" style="display: contents">`
  inside the app's own root, so a boot-recovery script that looks for rendered children is satisfied.
- Redirects from `load` use `replaceState`, so back skips the redirecting entry; measured on the
  saaaat corpus in Chrome, Firefox and Safari.
- Corpus: `SaaaatCorpusTests` builds saaaat's `apps/web`, renders all 17 pages in three browsers
  (console errors checked in Chrome only: WebDriver exposes no console in the others).
- Known gaps: see work/todo.md (CSS side-effect imports, JSON modules, dev `import.meta.env` in
  Pare-compiled `.ts`, route add/remove in Development).
