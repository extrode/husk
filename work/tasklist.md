# Tasklist — husk

Every task for the body of work currently underway. This file **stays put** while work moves
through it: the `status/` rotation is a three-deep sliding window over this list, not a copy of
it.

The unit is one body of work, not one task and not the whole project. "Migrate the storage layer"
is a tasklist. When that work is done the file is emptied and refilled for the next body of work,
and what shipped is summarised in `CHANGELOG.md`.

## How it feeds the rotation

```
tasklist.md        T1 [x]  T2 [x]  T3 [x]  T4 [ ]  T5 [ ]  T6 [ ]  T7 [ ]
                            |       |       |
                            v       v       v
status/          previous.md  current.md  next.md      <- one task each
```

On each task completion, in this order:
1. `current.md` -> `previous.md`
2. `next.md` -> `current.md`
3. the next unchecked task here -> `next.md`, and tick it off here when it lands

`/status roll` does exactly this. `tasklist.md` is not touched by the rotation except for the
tick.

## Relationship to the neighbours

- `todo.md` is the *unscheduled* backlog: things worth doing that nobody has committed to. A task
  arrives here from there, or from a spec, or from the user directly.
- For Durable-tier work this list is `docs/specs/<NNN>-*/<NNN>-tasks.md`. Point at it rather than
  duplicating it — two copies of a task list is one stale copy.
- `milestones.md` is where the project is going. A tasklist is how one milestone gets done.

## Current work: from spike to a first build

- [x] T1 Engine spike: Svelte compiler in ClearScript V8 and Jint — done when: results in `docs/architecture/001-compiler-engine.md` (2026-10-17)
- [x] T2 Cloudflare and Deno survey of saaaat.com and riwalqalb.com — done when: `docs/architecture/002-cloudflare-and-deno.md` (2026-10-17)
- [x] T3 Open decisions answered (browser floor 003, first app 004, type checking 005, musl 006) — done when: one decision record each in `docs/decisions/` (2026-10-17)
- [x] T4 Pare extension point for compile-to-JavaScript steps, in `../pare` — done when: released in a Pare version and documented there (2026-10-17: released in Pare 0.6.0, GitHub prerelease `v0.6.0`; README "Other compilers")
- [x] T5 `Extrode.Husk.Build`: compile `.svelte` on `dotnet build`, incremental, warm engine, CSS, dev mode — done when: a sample builds and runs in three browsers. Spec 001: tasks in `docs/specs/001-husk-build/001-tasks.md` (2026-10-17: merged to dev, unpublished)
- [ ] T6 Import map, content-hashed output, modulepreload — done when: the sample deploys to Cloudflare through the assets upload API
