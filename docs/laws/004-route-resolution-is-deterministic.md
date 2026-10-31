---
id: L004
title: Route resolution returns the same route and parameters on every run
status: proposed
kind: property
scope: src/Extrode.Husk.Build/runtime/router-core.js
---

## Statement
For every address and every route table, `matchRoute` returns the same route and the same parameters on every call, and an address with a trailing slash matches the route its slash-less form does.

## Counterexample looks like
The same `/settings/nonsense` matches `[section]` on one call and nothing on the next, or `/month/` reaches a different route from `/month`.
