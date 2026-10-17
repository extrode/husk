---
id: L003
title: A failed compile never replaces the last good output
status: proposed
kind: invariant
scope: src/Extrode.Husk.Build/HuskCompile.cs, src/Extrode.Husk.AspNetCore/HuskCompiler.cs
---

## Statement
For every source that last compiled to outputs O, a later compile of a version Svelte rejects
(HUSK100), in a build or on a save in the Development app, leaves every file of O, and
`husk.css`, byte for byte as it was.

## Counterexample looks like
`App.svelte` compiled, then saved with `{#if}` appended: the build fails or the app logs HUSK100,
and `App.svelte.js` is now empty, or `husk.css` has lost App's rules.
