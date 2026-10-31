# husk — agent instructions

This is the instruction file. `CLAUDE.md` beside it is a one-line `@AGENTS.md` shim, because
Claude Code injects `CLAUDE.md` into context at load and does **not** inject `AGENTS.md`
(verified empirically on 2.1.220, both directions). Other agent tools read `AGENTS.md` directly.
Edit this file; never edit the shim.

## Where things live

**Do not search the repo to find these. This table is the map — read it and go straight there.**

| Path | Holds | Lifetime |
|---|---|---|
| `README.md` | What this project is, how to run it, how to build it | Repo |
| `AGENTS.md` | This file. Rules an agent must follow here | Repo |
| `CHANGELOG.md` | What shipped, written at release, not per commit | Repo |
| `work/tasklist.md` | **Every task for the body of work underway.** Start here | Body of work |
| `work/status/current.md` | The one task being worked on right now | One task |
| `work/status/next.md` | The one task to start next, pulled from the tasklist | One task |
| `work/status/previous.md` | The task that just finished | One task |
| `work/todo.md` | Unscheduled backlog. Out-of-scope findings land here | Repo |
| `work/milestones.md` | Where the project is going. Coarse, rarely edited | Repo |
| `work/archive/` | Completed tasklists, `YYYY-MM-DD-<slug>.md`, one per shipped body of work | Repo |
| `docs/specs/<NNN>-<slug>/` | `<NNN>-{spec,plan,tasks}.md` for durable work | Per spec |
| `docs/plans/<date>-<NNN>-<slug>.md` | Approved plans with no spec | Per plan |
| `docs/architecture/<NNN>-<slug>.md` | Design and architecture notes | Repo |
| `docs/decisions/<date>-<NNN>-<slug>.md` | Decision records, superseded not edited | Repo |
| `docs/laws/<NNN>-<slug>.md` | Rules that must never break, each proven by a tagged test. Human owns the claim, `/prove` owns the proof | Repo |
| `docs/constitution.md` | Binding rules for this project | Repo |
| `docs/conventions.md` | Naming, git flow, cleanup interface | Repo |
| `docs/lessons/for-ai.md` | **Read first. Append when you learn a gotcha** | Repo |
| `docs/lessons/for-humans.md` | Same, for people | Repo |
| `scripts/cleanup/<work>.ps1` \| `.sh` | Cleanup scripts, written for the user to run, never run | Per work |

Any subdirectory whose purpose is not obvious from its name has its own `README.md`. If you are
about to guess what a directory is for, read that file instead.

## Starting work

1. Read `work/status/current.md`. That is the task. If it is empty, read `work/tasklist.md`.
2. Read `docs/lessons/for-ai.md` before touching anything — it is where the last session put
   what it wished it had known.
3. Branch off `dev`. Never commit to `dev` or `main`.

## Finishing a task

Every one of these, in the same branch as the work, before the merge:

1. Tick the task in `work/tasklist.md`.
2. Roll the window: `current.md` -> `previous.md`, `next.md` -> `current.md`, next unchecked
   tasklist entry -> `next.md`. `/status roll` does this.
3. Anything found and not fixed goes to `work/todo.md` as one line. Not a fix, not a silence.
4. Anything learned the hard way goes to `docs/lessons/for-ai.md`.
5. Check the files are still where this table says they are, and named the way it says:

   ```
   deno run -A ~/.claude/tools/structure-audit.js
   ```

   Dry run by default; it exits 0 only when the repo is clean. `--execute` applies the moves with
   exactly one correct destination and prints the rest. A home for everything is half of it;
   putting things back is the other half, and this is the half that gets skipped.

6. If `docs/laws/` exists, every enacted law still holds:

   ```
   deno run -A ~/.claude/tools/laws-check.js --run
   ```

   It prints `All laws hold.` or names the law that is open or failing. An open law is not
   merged around; it is proven with `/prove` or the change that broke it is undone.

When the whole tasklist is done: summarise it into `CHANGELOG.md` under `## [Unreleased]`, move
`work/tasklist.md` to `work/archive/YYYY-MM-DD-<slug>.md`, and start a fresh one.

## Flow

- **Quick** — one file, nothing to design: branch, edit, test, merge. The commit is the artifact.
- **Standard** — multi-file or a design call: plan mode -> approve -> `/implement --plan <file>`
  -> `/verify` + `/code-review` -> merge. Artifact in `docs/plans/`.
- **Durable** — someone reads it later: `/spec` -> plan mode -> `/plan` -> `/tasks` ->
  `/implement`. Artifact in `docs/specs/<NNN>-<slug>/`.

## Rules

- Husk is a separate product built on Pare (`../pare`), not a fork:
  [docs/decisions/2026-10-17-001-separate-product.md](docs/decisions/2026-10-17-001-separate-product.md).
  Plumbing Husk needs from Pare is added to Pare as a public extension point, in Pare's repo.
- No Node or Deno at build time in what Husk ships. Node may appear in research (reference runs).
- Ask before adding any package. Approved so far: ClearScript 7.5.1.1 and Jint 4.17.0, for
  `prototypes/001-engine-spike` only (2026-10-17). For the product (spec 001, 2026-10-17):
  ClearScript V8 7.5.1.1 with its six native packages, Microsoft.Build.Framework and
  Microsoft.Build.Utilities.Core 18.9.6, Extrode.Pare, .Build and .AspNetCore 0.6.0 (0.6.1 from
  2026-10-30, 0.7.0 from 2026-10-31, 0.7.2 from 2026-10-31, a dependency of Extrode.Husk.Build); tests: xunit.v3 4.0.2, Selenium.WebDriver 4.51.0 (4.52.0
  from 2026-10-31). Not
  Playwright: its .NET package bundles Node.
- The Svelte compiler is pinned to one version; outputs are checked byte for byte against Node
  running the same bundle.
- `prototypes/<NNN>-<slug>/` holds spikes: throwaway, a README each, results written up under
  `docs/architecture/`.
- C# sources stay ASCII.

- Cleanup is never performed, only written: `scripts/cleanup/<work>.ps1` (or `.sh`) for the user
  to run. Dry run is the default; `--execute` / `-e` is the only way to act.
- New files and directories are `kebab-case`, except names someone else's tooling reads.
- Touch only what the task requires.
