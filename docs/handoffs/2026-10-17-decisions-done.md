# Handoff 2026-10-17: decisions done, T4 next

The second handoff of 2026-10-17. Work moves from sessions started in `../pare` to sessions started here.
Read [the first handoff](2026-10-17-start-here.md) first, then this one.

## Since the first handoff

- Name: kept as Husk (decision 002). The npm org `extrode` was claimed by the user; anything for
  npm is scoped `@extrode/`.
- Browser floor: import-map browsers first, older ones later as an opt-in (decision 003).
- First app: saaaat.com (decision 004).
- Type checking: native TypeScript 7 `tsc` for `.ts`, `svelte-check-native` for `.svelte`, no
  runtime; it matched official `svelte-check` on planted errors, 0.4 s against 5.6-6.4 s (decision 005).
- linux-musl: unsupported; no build of either app runs on Alpine (decision 006).
- T3 is done. `work/status/current.md` is T4.

## Where things are

| Repo | Branch | State |
|---|---|---|
| husk | `dev` | all work merged, no remote, nothing pushed |
| pare | `dev` | 4 commits ahead of `origin/dev` (merges b9af128 and 039f1d0: spike results, decision 004); the user pushes |

Scratch the user's cleanup scripts remove (dry run by default):

```sh
scripts/cleanup/type-check-eval.sh -e --delete-scratch          # husk: tmp/svelte-check-eval, 276 MB
../pare/scripts/cleanup/svelte-spike.sh -e --delete-scratch     # pare: tmp/svelte-spike, 148 MB, and wp1's copy
```

The native `tsc` 7.0.2 and the `svelte-check-native` binary used for decision 005 were in session
scratch and in `tmp/svelte-check-eval/native`; nothing of them is in the repository.

## Next: T4, Pare's extension point

The work is in `../pare`, not here: a public, documented MSBuild hook through which a
compile-to-JavaScript step (Husk first) puts its output where Pare's does (under `obj/`, static web
assets, embedded output, re-compile on save). Standard tier: plan first, approved by the user.
Pare's tracking: its `work/todo.md` item "Public MSBuild extension point", and its decision 004.
Pare's own rules apply there (its `AGENTS.md`: zero-package core, C# stays ASCII, tests first).

Then T5 here, `Extrode.Husk.Build`. Package approvals so far cover the prototypes only (ClearScript
7.5.1.1, Jint 4.17.0); ClearScript in the product package is a new ask.

## Working rules carried over

Session memory for this project holds the three rules from the Pare sessions: no `cd` prefix in
shell commands, no `rm` (write a cleanup script), and a real-browser check before calling web output
ready.
