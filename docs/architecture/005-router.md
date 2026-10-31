# 005 — Router and `$app/*` shims
Design notes for spec 002; plan: docs/specs/002-saaaat-router-and-app-shims/002-plan.md.

## Sources outside `wwwroot` (T1, 2026-10-31)
Pare places a compiled source by `PareRelativePath`, or by `PareServeAt` plus the source's
recursive directory. Husk copies item metadata onto its `PareCompileSource` items, so
`<HuskComponent Include="src/**/*.svelte" PareServeAt="app" />` serves `src/lib/A.svelte` at
`/app/lib/A.svelte.js` with an import-map entry `/app/lib/A.svelte`. Pinned by
`OutsideSourcesTests`. No Pare change needed. A `..\` include (workspace packages) uses the same
metadata; it is exercised in T8.
