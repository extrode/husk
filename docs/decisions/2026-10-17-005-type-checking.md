# 005 Type checking: native TypeScript 7, no JavaScript runtime

Date: 2026-10-17. Status: accepted (the user) for `.ts`; `.svelte` checking still to evaluate.

## Context

Husk and Pare remove types; neither checks them. Checking needs the whole program and every
dependency's type definitions: that is the TypeScript compiler. saaaat.com checks today with
`deno check`, `tsc` (TypeScript `^5.6.3`, JavaScript, needs a runtime) and `svelte-check`
`^4.1.0`. "Replace Deno completely" needs a checker that is not a JavaScript program.

## What exists (checked 2026-10-17)

- **TypeScript 7 is the native compiler**, written in Go: `typescript` 7.0.2 is npm's `latest`
  (7.0 released 2026-07-08). Its command is `tsc`. `tsgo` was the name during the preview
  (`@typescript/native-preview`, last published 2026-07-07); the two are now the same program.
- The npm package is a thin JavaScript launcher; the compiler itself is a native executable in a
  per-platform package (`@typescript/typescript-darwin-arm64` and 19 others: `lib/tsc`, 24 MB,
  plus the `lib*.d.ts` files). That executable runs with no Node:
  `env -i PATH=/usr/bin:/bin .../lib/tsc --version` prints `Version 7.0.2`.
- On copies of saaaat.com's `packages/` (core, i18n, geo-data: 66 files in `src/`), with each
  package's own `tsconfig.json`: **0 errors in `src/`**. The only errors are in `scripts/`, where
  `@types/node` was missing because the copy had no `node_modules`.
- TypeScript 7's programmatic API is not stable before 7.1, so tools built on the old API break.
- **Official `svelte-check`** 4.7.7 is JavaScript (peer `typescript` `^5 || ^6`); it can use
  TypeScript 7 for checking only behind `--tsgo`, and still needs a runtime.
- **`svelte-check-native`** 1.9.1 (2026-10-09): third-party, MIT, Rust, "powered by tsgo",
  native binaries for darwin, linux, win32 x64/arm64 (6 MB), about 8,300 downloads a week.
  Not tried; it is a new third-party tool, so it needs the user's approval.

## Decision

Type checks use the native TypeScript 7 compiler, with no Node or Deno. `.svelte` files: evaluate
`svelte-check-native` against `svelte-check` on saaaat.com's 78 components before relying on it.

## Consequences

- Checking needs dependencies' `.d.ts` files (`svelte`, `@capacitor/*`). They come from npm
  tarballs; fetching them needs no runtime, but something must resolve and unpack them (a
  package manager, or a small .NET step). To design.
- saaaat.com moves from TypeScript 5.6 to 7: new defaults and removed options may need changes.
- Without a native `.svelte` checker, `.svelte` files go unchecked or keep `svelte-check` under a
  runtime in CI.
