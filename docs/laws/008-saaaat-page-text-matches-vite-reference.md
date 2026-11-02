---
id: L008
title: The saaaat port shows the text the Vite build shows
status: enacted
enacted: 2026-11-02
proof: tests/Extrode.Husk.Integration/SaaaatPortTests.cs
check: dotnet build tests/Extrode.Husk.Integration -v q -nologo && tests/Extrode.Husk.Integration/bin/Debug/net10.0/Extrode.Husk.Integration -trait "Law=L008"
kind: invariant
scope: tests/Extrode.Husk.Integration/SaaaatPortTests.cs, tests/references/saaaat/
---

## Statement
For every one of the 17 saaaat pages, in English and in Arabic, in every browser the tests run, the visible text of the Husk port, once settled under the pinned clock and stored state, equals the text recorded from the Vite build of the same saaaat commit. A saaaat commit with no recorded reference is refused, not skipped.

## Counterexample looks like
A change to the router makes `/moon/details` show no heading in Firefox; the port's text for it differs from `moon-details.en.firefox.txt`.

## Proof notes
Mutation: one character appended to a reference file (`moon.ar.firefox.txt`) and one leading space in `qibla.en.chrome.txt`; each failed the test naming the file. Removing the reference folder fails with 'no reference for saaaat commit'. Skips without the saaaat checkout or the port project (CI).
