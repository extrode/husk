---
id: L007
title: An unsupported SvelteKit feature fails the build and writes no route table
status: enacted
enacted: 2026-11-01
proof: tests/Extrode.Husk.Integration/Laws/L007NoPartialRouteTableTests.cs
check: dotnet build tests/Extrode.Husk.Integration -v q -nologo && tests/Extrode.Husk.Integration/bin/Debug/net10.0/Extrode.Husk.Integration -trait "Law=L007"
kind: guard
scope: src/Extrode.Husk.Build/HuskRoutes.cs, src/Extrode.Husk.Compiler/RouteTable.cs
---

## Statement
For every unsupported feature Husk detects (HUSK301, HUSK302, HUSK303), the build fails and `routes.js` and the shell are not written or changed.

## Counterexample looks like
A route file imports `$app/forms`: the build reports HUSK302 but `routes.js` has been rewritten without that route.

## Proof notes
Mutations: writing routes.js before the diagnostics check, and writing app/shell/index.html (the shell template) before it; each failed the HUSK301/302/303 rows naming the changed file. app/index.html (the final shell) is written by a later target and no mutation of HuskRoutes reaches it.
