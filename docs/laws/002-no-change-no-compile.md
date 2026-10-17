---
id: L002
title: A build with no source changed compiles nothing
status: enacted
enacted: 2026-10-17
proof: tests/Extrode.Husk.Integration/Laws/L002NoChangeNoCompileTests.cs
check: dotnet build tests/Extrode.Husk.Integration -v q -nologo && tests/Extrode.Husk.Integration/bin/Debug/net10.0/Extrode.Husk.Integration -trait "Law=L002"
kind: invariant
scope: src/Extrode.Husk.Build/build/Extrode.Husk.Build.targets
---

## Statement
After any sequence of builds of a project, a further build in which no Husk source and no Husk
input (the task assembly, the compiler, the targets file, `HuskDev`) changed runs the Svelte
compiler on zero sources and leaves every output, `husk.css` and `importmap.json` with the bytes
and last-write time they had.

## Counterexample looks like
`dotnet build`, then `dotnet build` again with nothing touched, and `App.svelte.js` gets a new
last-write time, or the build log shows the HuskCompile task compiling one source.

## Proof notes
Mutation: `UpToDate` in HuskCompile.cs returning false; L002 failed "after first build: a build with nothing changed compiled 4". Seeded sequence (2002) of touch, edit, add, remove and style edits.
