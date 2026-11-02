---
id: L010
title: A watch rebuild produces what a clean build produces
status: enacted
enacted: 2026-11-02
proof: tests/Extrode.Husk.Tests/HuskDevelopmentTests.cs
check: dotnet build tests/Extrode.Husk.Tests -v q -nologo && tests/Extrode.Husk.Tests/bin/Debug/net10.0/Extrode.Husk.Tests -trait "Law=L010"
kind: property
scope: src/Extrode.Husk.Build, src/Extrode.Husk.AspNetCore
---

## Statement
For every sequence of edits to Husk-compiled sources (`.svelte`, `.svelte.ts`, `.svelte.js`) and the shell, the Development outputs after the sequence equal the outputs of a clean build of the same files. Narrowed (docs/architecture/006-saaaat-port.md, P4): plain `.ts` using `import.meta.env`, and stylesheet and JSON edits, are known gaps and outside this law.

## Counterexample looks like
After saving a `.svelte` file that reads `import.meta.env.PUBLIC_X`, the served module still contains `import.meta.env`, while a clean build rewrites it.

## Proof notes
Ten random saves over three components, then the outputs are deleted and the app restarted; every output and husk.css must equal the watched ones. Mutation: `WriteStylesheet` read an empty string for the components not being saved; `obj/husk/husk.css differs between the watched and the clean output`. The clean side is Development's own start-up compile, not `dotnet build`; the build half is L001 and L002.
