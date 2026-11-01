---
id: L005
title: After any navigation, page.url equals the browser address
status: enacted
enacted: 2026-11-01
proof: tests/Extrode.Husk.Integration/Laws/L005PageUrlEqualsTheAddressTests.cs
check: dotnet build tests/Extrode.Husk.Integration -v q -nologo && tests/Extrode.Husk.Integration/bin/Debug/net10.0/Extrode.Husk.Integration -trait "Law=L005"
kind: invariant
scope: src/Extrode.Husk.Build/runtime/router.js, src/Extrode.Husk.Build/runtime/app/state.svelte.js
---

## Statement
After any sequence of in-app navigations (link, `goto`, `replaceState`, back, forward, redirect, cancelled navigation), `page.url.href` equals `location.href`.

## Counterexample looks like
After a cancelled `beforeNavigate`, `location.href` is `/month` and `page.url` still says `/settings`.

## Proof notes
Mutation: removed `ignorePop = true; history.go(-options.delta);` from the cancelled-navigation branch of router.js; the test failed naming seed 5005, the sequence and both urls. Chrome only; the sequences use `goto(..., { replaceState: true })` because the shim has no replaceState export.
