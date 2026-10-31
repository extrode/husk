---
id: L007
title: An unsupported SvelteKit feature fails the build and writes no route table
status: proposed
kind: guard
scope: src/Extrode.Husk.Build/HuskRoutes.cs, src/Extrode.Husk.Compiler/RouteTable.cs
---

## Statement
For every unsupported feature Husk detects (HUSK301, HUSK302, HUSK303), the build fails and `routes.js` and the shell are not written or changed.

## Counterexample looks like
A route file imports `$app/forms`: the build reports HUSK302 but `routes.js` has been rewritten without that route.
