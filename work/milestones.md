# Milestones — husk

Where this project is going, coarse. A milestone is a destination worth naming, not a task: it
survives many sprints, it is the thing you would tell someone who asked "what is this for", and
it changes rarely. If an entry here moves week to week, it belongs in `todo.md` or `status/`.

This is the one file in `work/` that is allowed to go a month without an edit. Editing it
means the plan changed, which is worth noticing.

**Distinction from the neighbours.** `milestones.md` is where we are going. `todo.md` is
everything not yet scheduled. `status/current.md` is what is being worked on right now. A single
item legitimately appears in all three at different times in its life, and should be in exactly
one of them at any given moment.

| # | Milestone | Target | Status | Notes |
|---|-----------|--------|--------|-------|
| 1 | First build: `.svelte` to modules on `dotnet build`, served with an import map | 2026-10-30 | shipped | spec 001; 0.1.0-preview.7 on nuget.org 2026-11-01 |
| 2 | saaaat.com web on Husk, no Deno | 2026-11-02 | shipped | spec 003: port on saaaat `feat/husk-port` (unmerged, the user's call); page text equals the Vite build in 3 browsers. Recorded gaps: `import.meta.env` in saved `.ts` under watch, Firefox and Safari console unread, service worker, Capacitor (stand-ins) and CSP out of scope |
| 3 | riwalqalb.com frontend on Husk | — | planned | waits on the older-browser add-on (decision 003) or a moved floor |

Status is one of `planned`, `in progress`, `shipped`, `dropped`. A dropped milestone stays in the
table with a note saying why: the reason it was dropped is the most useful thing on the row, and
deleting it means re-litigating the decision in six months.
