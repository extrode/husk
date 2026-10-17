# 003 Browser floor: import-map browsers first, older browsers as a later add-on

Date: 2026-10-17. Status: accepted (the user).

## Context

Husk serves compiled components and the unbundled `svelte` runtime as ES modules behind an
import map ([architecture 001](../architecture/001-compiler-engine.md)). Import maps need
Chrome 89, Safari 16.4, Firefox 108. riwalqalb.com's frontend supports Chrome 49, Safari 11,
Firefox 60 through `@vitejs/plugin-legacy` ([architecture 002](../architecture/002-cloudflare-and-deno.md)).

## Options

| | Cost | Forecloses |
|---|---|---|
| **Import-map browsers first, older ones later as an opt-in** | Nothing now; the add-on is its own piece of work | Nothing |
| Older browsers from the start | A downlevel step (Babel or similar in the same V8) and a module loader before the first build; slower builds | Delays M1 and M2 |
| Import-map browsers only, for good | Nothing | riwalqalb.com's frontend while it keeps its floor |

## Decision

Husk's default output targets import-map browsers. Support for older browsers comes later as an
opt-in addition for projects that need it, not as part of the default build.

## Consequences

- M1 and M2 do not wait on downlevelling.
- The opt-in is a later task; riwalqalb.com's frontend (M3) waits on it or on dropping its floor.
- Default output must not assume the add-on exists: no design choice that only works with a loader.
