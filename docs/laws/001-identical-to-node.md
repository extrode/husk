---
id: L001
title: Husk's compiler result is identical to Node's
status: enacted
enacted: 2026-10-17
kind: guard
scope: src/Extrode.Husk.Compiler/SvelteCompiler.cs
---

## Statement
For every committed fixture under `tests/fixtures/wwwroot` (`.svelte`, `.svelte.js`, `.svelte.ts`)
and for both `dev` values, `SvelteCompiler.Compile` gives the row the pinned Svelte compiler gave
under Node in `tests/fixtures/expected.tsv`: the same result (`ok` or `err`), the same SHA-256 of
the JavaScript, the CSS and the source map, and the same diagnostics as `code@line:column`.

## Counterexample looks like
`wwwroot/Injected.svelte` with `dev: true`: Node gives `ok` with a JavaScript hash, Husk gives
`err` because the engine had no `btoa` when Svelte loaded.
