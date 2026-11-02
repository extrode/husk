---
id: L010
title: A watch rebuild produces what a clean build produces
status: enacted
enacted: 2026-11-02
kind: property
scope: src/Extrode.Husk.Build, src/Extrode.Husk.AspNetCore
---

## Statement
For every sequence of edits to Husk-compiled sources (`.svelte`, `.svelte.ts`, `.svelte.js`) and the shell, the Development outputs after the sequence equal the outputs of a clean build of the same files. Narrowed (docs/architecture/006-saaaat-port.md, P4): plain `.ts` using `import.meta.env`, and stylesheet and JSON edits, are known gaps and outside this law.

## Counterexample looks like
After saving a `.svelte` file that reads `import.meta.env.PUBLIC_X`, the served module still contains `import.meta.env`, while a clean build rewrites it.
