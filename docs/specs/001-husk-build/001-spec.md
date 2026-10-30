# 001 — Compile Svelte on `dotnet build`
Status: done
Created: 2026-10-17

## Problem
A .NET developer with a Svelte front end today needs Node or Deno and Vite to turn `.svelte`
files into JavaScript, next to a .NET build that already handles their static files. saaaat.com
(the first app, decision 004) runs that second toolchain only for this. Husk's first build makes
`dotnet build` compile the components itself: one toolchain, no JavaScript runtime installed,
output Pare's plumbing already serves, publishes and cleans up (Pare 0.6.0's compile extension
point).

## Goals
- A web project that references Husk builds its `.svelte` components to ES modules with
  `dotnet build` alone: no Node, Deno or package manager on the machine.
- The output runs in Chromium, Firefox and WebKit with no build step outside `dotnet build`.
- An edit costs one compile, not a rebuild: a build after changing one component compiles that
  one; a build after changing nothing compiles none.
- A clean build of a large app uses the machine's cores.
- In Development, saving a component updates the running app and reloads the browser.

## Non-goals
- Hot module replacement that keeps component state across a save.
- Content-hashed file names, modulepreload, Cloudflare deploy (T6).
- SvelteKit: routing, `$app/*` shims, server rendering (milestone 2).
- Browsers without import maps (decision 003's later add-on).
- Type checking (decision 005: a separate native tool).
- Building on linux-musl (decision 006): it fails with a clear error instead.
- Choosing a Svelte version per project: Husk pins one.

## User stories
- As a .NET developer, I want to add one package and write `.svelte` files, so that I get a
  Svelte front end without installing Node.
- As a developer editing a component, I want a save to show in the browser in about a second, so
  that the loop feels like Vite's.
- As a developer on a large app, I want a clean build to take seconds, not tens of seconds.
- As a maintainer, I want Husk's output to match the official compiler's byte for byte, so that
  a bug report can be reproduced with plain Svelte.

## Requirements
- R1 Husk compiles every `.svelte` component in the project on build, to an ES module.
- R2 Husk compiles `.svelte.ts` and `.svelte.js` rune modules on build, to ES modules.
- R3 Husk carries its own pinned Svelte compiler and the `svelte` runtime; the build reads
  nothing from `node_modules` and runs no JavaScript runtime installed on the machine.
- R4 For every input, the compiler's JavaScript and CSS results are identical to what the official
  compiler of the pinned version produces under Node with the same options. (Husk appends one
  source map comment line to the JavaScript file; identity is on the compiler's result.)
- R5 The CSS of every component is joined into one stylesheet for the app, which a page links.
- R6 Husk generates an import map for the runtime and the compiled modules that a page includes,
  so the output runs in Chromium, Firefox and WebKit as served.
- R7 Each compiled module has a source map, so browser devtools show the original source.
- R8 A build compiles only the sources changed since the last build; a build with no changes
  compiles nothing.
- R9 A clean build compiles in parallel across cores.
- R10 In the running Development app, successive compiles reuse the loaded compiler, so a save
  does not pay the compiler's start-up again.
- R11 In Development under `dotnet watch`, saving a component or rune module recompiles it in the
  running app, the stylesheet is updated, and an open browser reloads.
- R12 A Debug build, and the Development app, compile with the compiler's development checks on;
  a Release build and publish compile without them.
- R17 Compiler warnings are reported with file, line and column, as build warnings and in the
  Development log.
- R13 A component with a compile error fails the build with an error naming the file, line and
  column and the compiler's message; on save in Development it is logged and the previous output
  stays.
- R14 Output is served, published, embedded and deleted when stale through Pare's rules, as
  Pare's TypeScript output is; `.svelte` sources are not published.
- R15 On a platform Husk does not support, the build fails with one error naming the platform and
  the supported ones.
- R16 A sample app shows R1-R13 and R17 working.

## Acceptance criteria
- R1, R6, R16: Given the sample, when built with `dotnet build` and run, then its pages work in
  Chromium, Firefox and WebKit with no console errors: runes, `$derived`, keyed `{#each}`, events,
  a child component, a rune module shared by two components.
- R3: Given a machine with no Node, Deno or `node_modules`, when the sample builds, then it
  succeeds.
- R4: Given a corpus of components (the spike's 201 app components and the Svelte repo's own),
  when each is compiled by Husk and by the official compiler under Node, then every JavaScript and
  CSS output matches byte for byte, and every error matches by code.
- R5: Given two components with styles, when built, then one stylesheet holds both components'
  scoped rules and the page linking it renders both styled.
- R7: Given a built component, when opened in browser devtools, then a breakpoint set in the
  `.svelte` source pauses at that line.
- R8: Given a built project, when nothing changes and it builds again, then no component is
  compiled; when one component changes, then exactly that one is compiled.
- R9: Given the spike's 201 components on a 10-core machine, when built clean, then compiling
  takes less time than one engine compiling them one after another.
- R10: Given the sample running under `dotnet watch` after one save, when a component is saved
  again, then compiling it takes no more than about 50 ms. (`dotnet build` starts the compiler
  each run, about 200 ms measured; R8 keeps that to one start per build.)
- R11: Given the sample running under `dotnet watch` with a browser open, when a component's markup or
  style is saved, then the browser shows the change within about a second, with no manual refresh.
- R12: Given the sample, when built Debug and run, then the compiler's development checks run in the
  browser; when published, then the compiled modules carry no development code and the runtime's
  development flag is off.
- R13: Given a component with a syntax error, when built, then the build fails with
  `File.svelte(line,col): error ...`; when saved in Development, then the error is logged and the
  page keeps the last good version.
- R14: Given a published sample, then compiled modules, the stylesheet and the runtime are
  published with compressed copies, and no `.svelte` file is.
- R17: Given a component with an `<img>` without `alt`, when built, then the build prints a
  warning naming its file, line and column.
- R15: Given a linux-musl host, when built, then the build fails with one error naming the cause.

## Law candidates
- L1 (R4): for every input, Husk's compiler result is identical to the official compiler's under
  Node.
- L2 (R8): after any sequence of builds, a build with no source changed compiles nothing.
- L3 (R13): a component that fails to compile never replaces its last good output.

## Open questions
- None. The three design questions raised here (R10's session, R11's reload channel, R4's
  Node reference) are answered in [001-plan.md](001-plan.md).
