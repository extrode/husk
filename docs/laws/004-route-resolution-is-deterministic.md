---
id: L004
title: Route resolution returns the same route and parameters on every run
status: enacted
enacted: 2026-11-01
proof: tests/Extrode.Husk.Tests/Laws/L004RouteResolutionIsDeterministicTests.cs
check: dotnet build tests/Extrode.Husk.Tests -v q -nologo && tests/Extrode.Husk.Tests/bin/Debug/net10.0/Extrode.Husk.Tests -trait "Law=L004"
kind: property
scope: src/Extrode.Husk.Build/runtime/router-core.js
---

## Statement
For every address and every route table, `matchRoute` returns the same route and the same parameters on every call, and an address with a trailing slash matches the route its slash-less form does.

## Counterexample looks like
The same `/settings/nonsense` matches `[section]` on one call and nothing on the next, or `/month/` reaches a different route from `/month`.

## Proof notes
Mutation: matchRoute stopped stripping a trailing slash; the test failed with a `slash` entry naming the route table and address. Repeat-call determinism has no mutation (the function is pure), it is asserted on every generated case.
