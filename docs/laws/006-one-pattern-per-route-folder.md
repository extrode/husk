---
id: L006
title: Each route folder gives exactly one address pattern, and no two share one
status: proposed
kind: property
scope: src/Extrode.Husk.Compiler/RouteTable.cs
---

## Statement
For every `src/routes/` tree, each folder with a page yields exactly one address pattern, and two folders yielding the same pattern fail the build with HUSK300.

## Counterexample looks like
`(a)/x/+page.svelte` and `(b)/x/+page.svelte` both give `/x` and the build succeeds with one silently shadowing the other.
