---
id: L006
title: Each route folder gives exactly one address pattern, and no two share one
status: enacted
enacted: 2026-11-01
proof: tests/Extrode.Husk.Tests/Laws/L006OnePatternPerRouteFolderTests.cs
check: dotnet build tests/Extrode.Husk.Tests -v q -nologo && tests/Extrode.Husk.Tests/bin/Debug/net10.0/Extrode.Husk.Tests -trait "Law=L006"
kind: property
scope: src/Extrode.Husk.Compiler/RouteTable.cs
---

## Statement
For every `src/routes/` tree, each folder with a page yields exactly one address pattern, and two folders yielding the same pattern fail the build with HUSK300.

## Counterexample looks like
`(a)/x/+page.svelte` and `(b)/x/+page.svelte` both give `/x` and the build succeeds with one silently shadowing the other.

## Proof notes
Mutation: the HUSK300 same-address diagnostic in RouteTable.cs was never added; the test failed on tree 0 naming its files.
