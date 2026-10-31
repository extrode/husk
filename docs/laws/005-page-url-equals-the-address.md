---
id: L005
title: After any navigation, page.url equals the browser address
status: proposed
kind: invariant
scope: src/Extrode.Husk.Build/runtime/router.js, src/Extrode.Husk.Build/runtime/app/state.svelte.js
---

## Statement
After any sequence of in-app navigations (link, `goto`, `replaceState`, back, forward, redirect, cancelled navigation), `page.url.href` equals `location.href`.

## Counterexample looks like
After a cancelled `beforeNavigate`, `location.href` is `/month` and `page.url` still says `/settings`.
