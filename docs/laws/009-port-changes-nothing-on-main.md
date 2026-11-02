---
id: L009
title: The saaaat port branch changes nothing the Vite build uses
status: enacted
enacted: 2026-11-02
proof: tests/Extrode.Husk.Integration/Laws/L009PortChangesNothingTests.cs
check: dotnet build tests/Extrode.Husk.Integration -v q -nologo && tests/Extrode.Husk.Integration/bin/Debug/net10.0/Extrode.Husk.Integration -trait "Law=L009"
kind: guard
scope: saaaat repo, branch feat/husk-port
---

## Statement
For every file changed between the saaaat port branch and its branch point, the path is under `apps/web-husk/`. Nothing under `apps/web/src` or `packages/` differs. The proof skips when the saaaat checkout is absent.

## Counterexample looks like
A fix to `apps/web/src/lib/day.ts` is committed on `feat/husk-port` to make a page render under Husk.

## Proof notes
Mutation: the `StartsWith` test inverted; all 8 rows failed. The branch check lists `git diff --name-only` from the merge-base with saaaat `dev` and skips when the port worktree is absent (CI).
