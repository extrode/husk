# 005 Type checking: native TypeScript 7, no JavaScript runtime

Date: 2026-10-17. Status: accepted (the user).

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
  Tried with the user's approval; results below.

## svelte-check-native against svelte-check

saaaat.com `apps/web` at c5d31cb8, copied to `tmp/svelte-check-eval/`, `pnpm install
--frozen-lockfile --ignore-scripts`, `svelte-kit sync`. Official: `svelte-check` 4.4.8 with
TypeScript 5.9.3 (saaaat's lockfile), under Node 24.16. Native: the
`svelte-check-native-darwin-arm64` binary with `TSGO_BIN` pointing at the native `tsc` 7.0.2,
run with `env` holding only `PATH=/usr/bin:/bin`, so no Node.

| | Official | Native |
|---|---|---|
| Clean tree | 0 errors, 0 warnings | 0 errors, 0 warnings |
| Six planted problems | 5 errors, 2 warnings | the same 5 errors and 2 warnings: same files, positions and messages; order differs |
| Time | 5.6-6.4 s | 0.4 s |
| Files reported | 530 | 82 (counts differently; the diagnostics match) |

Planted: a `string` assigned to a `number` in a component's script, `.toFixed` on a `boolean` in
markup, a wrong prop type passed to `ToggleSwitch`, a type error in `src/lib/day.ts`, an import
of a missing export, and an `<img>` without `alt` (a11y warning). The native tool also raised the
Svelte compiler's `state_referenced_locally` warning that the official one raised.

## Decision

Type checks use the native TypeScript 7 compiler, with no Node or Deno: `tsc` for `.ts`,
`svelte-check-native` (on the same `tsc`) for `.svelte`.

## Consequences

- Checking needs dependencies' `.d.ts` files (`svelte`, `@capacitor/*`). They come from npm
  tarballs; fetching them needs no runtime, but something must resolve and unpack them (a
  package manager, or a small .NET step). To design.
- saaaat.com moves from TypeScript 5.6 to 7: new defaults and removed options may need changes.
- `svelte-check-native` is one maintainer's project and reimplements svelte2tsx in Rust. Pin it,
  and on every Svelte or tool bump re-run the planted comparison against official `svelte-check`
  (which still needs Node, so that check runs in a scratch copy, not in the product path).
- It needs `node_modules` for `.d.ts` files and the generated `.svelte-kit/tsconfig.json`; Husk
  replacing SvelteKit generates the equivalent itself.
