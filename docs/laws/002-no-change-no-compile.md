---
id: L002
title: A build with no source changed compiles nothing
status: proposed
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
