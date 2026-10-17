# 001 Husk is a separate product, not part of Pare

Date: 2026-10-17. Status: accepted (the user).

## Context

Pare (`../pare`) is a TypeScript type stripper: zero-package core, NativeAOT CLI, an MSBuild
package, `libpare`. Its ecosystem doc ("Svelte and SvelteKit the way Deno runs them", tier 1)
proposed compiling Svelte components at build time with the Svelte compiler in an embedded engine.
The spike ([architecture 001](../architecture/001-compiler-engine.md)) showed it works with
ClearScript V8, at 44 MB of native V8 per platform and no linux-musl support.

The Svelte compiler strips the TypeScript in `<script lang="ts">` itself, so Pare's stripper never
sees a `.svelte` file. What the two share is build plumbing: output under `obj/`, static web
assets, embedded output, re-compiling on save.

## Options

| | Cost | Forecloses |
|---|---|---|
| Inside Pare.Build | V8 and Newtonsoft.Json for every Pare user; Pare.Build stops working on Alpine; Svelte bumps force Pare releases | Pare's zero-dependency, runs-anywhere promise |
| Extra package in the Pare repo | Pare's tests, pack and README grow by half; shared version line | Little; a later split is a package move |
| Fork of Pare, renamed | Every plumbing fix ported by hand | Nothing short term; the most expensive long term |
| **Separate product using Pare's packages** | Its own repo, CI, pack; Pare must expose a public "compile X to `.js`" MSBuild hook (est. 1-2 days) | Nothing |

## Decision

Husk is a separate product with its own repository (`../husk`), name and version line. It depends
on Pare's packages and does not fork them. Pare stays a type stripper and gains one public,
documented extension point that any compile-to-JavaScript step can use; Husk is its first user.

## Consequences

- Pare's work: the extension point, tracked in Pare's `work/todo.md`, designed before Husk's build
  integration starts.
- Husk owns the engine dependency (ClearScript) and its platform limits.
- Husk's versions can follow the pinned Svelte compiler.
- Package IDs: `Extrode.Husk.*` (nuget.org has no `husk` package; npm's `husk` is an unrelated
  stream library and `husky` is the git-hooks tool, so the bare name is not used on npm).
