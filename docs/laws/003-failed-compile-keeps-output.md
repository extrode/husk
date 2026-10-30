---
id: L003
title: A failed compile never replaces the last good output
status: enacted
enacted: 2026-10-17
proof: tests/Extrode.Husk.Integration/Laws/L003FailedCompileKeepsOutputTests.cs
check: dotnet build tests/Extrode.Husk.Tests -v q -nologo && tests/Extrode.Husk.Tests/bin/Debug/net10.0/Extrode.Husk.Tests -trait "Law=L003" && dotnet build tests/Extrode.Husk.Integration -v q -nologo && tests/Extrode.Husk.Integration/bin/Debug/net10.0/Extrode.Husk.Integration -trait "Law=L003"
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

## Proof notes
Build half here; Development half is `L003_a_rejected_save_...` in tests/Extrode.Husk.Tests/HuskDevelopmentTests.cs. Mutations: HuskCompile.cs emptying the outputs after HUSK100 failed the build half at "step 0, <style>.a { color: red; </style>"; HuskCompiler.cs returning empty texts after HUSK100 failed the dev half at "step 0, Other".
