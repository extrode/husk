# husk

Svelte components for .NET projects without Node or Deno: the Svelte compiler runs in an
embedded V8 inside MSBuild, components compile to ES modules, and the `svelte` runtime is served
unbundled behind an import map. Built on [Pare](https://github.com/extrode/pare).

**Status: research.** No product code yet. Start with
[docs/handoffs/2026-10-17-start-here.md](docs/handoffs/2026-10-17-start-here.md).

## Run the spike

```sh
npm --prefix prototypes/001-engine-spike/js ci --ignore-scripts
```

Then follow [prototypes/001-engine-spike/README.md](prototypes/001-engine-spike/README.md).

## Layout

| Path | What |
|---|---|
| `prototypes/` | Spikes, numbered; throwaway code with its own README |
| `work/` | Current work: tasklist, status window, backlog, milestones |
| `docs/` | Specs, plans, architecture, decisions, conventions, lessons |
| `scripts/` | Operational scripts. `scripts/cleanup/` is written but never run |

Agent instructions are in `AGENTS.md`. `CLAUDE.md` is a one-line shim that imports it.

## Status

See `work/status/current.md` for what is being worked on right now, and `CHANGELOG.md` for
what has shipped.

---

**Local READMEs.** Any directory whose purpose is not obvious from its name gets its own
`README.md`, kept to a few lines: what belongs here, what does not, and the naming rule if
there is one. The test is simple — if someone would have to open three files to work out what
a directory is for, it needs one.
