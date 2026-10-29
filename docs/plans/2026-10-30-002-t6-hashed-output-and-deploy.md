# T6: hashed output, deploy command, public readiness

## Context

Spec 001 is done (compiler, build, dev, laws L001 to L003). What remains for milestone 1 is T6:
content-hashed names, an import map that points at them, `modulepreload`, and a Cloudflare deploy
without Node. Architecture 002 has the API sequence. This plan also lists what is needed to make
Husk public and what only the user can do.

## Work split

| Task | Who | Done when |
|---|---|---|
| T6 hashed output, map, modulepreload | `codegen` (opus), own worktree, gated | Build writes `name.<hash>.js`, the map points at hashed URLs, marked pages get `modulepreload` for the page's module graph; L001 to L003 still hold; no-change build still compiles nothing |
| T7 deploy command | `codegen` (opus), parallel with T6 | `Extrode.Husk.Deploy` (console tool, no new package) runs the three-step Workers assets upload against a fake server in tests; unchanged files are skipped by manifest hash |
| T8 Cloudflare limits vs app output | `search`/`research` then inline | Limits (files per deployment, file size) written in architecture 002 and compared with the sample, saaaat web and riwalqalb frontend output counts |
| T9 backlog bug batch | `codegen-lite`, one key per bug | Each listed bug has a failing test first, then a fix: HuskPage glob vs Content Remove, mtime cache, OrdinalIgnoreCase on Linux, free-port race, shared globalPackagesFolder |
| T10 CI workflows | `codegen-lite` | `.github/workflows/ci.yml` runs build, unit, integration (Chrome, Firefox) and laws on macOS, Linux, Windows; written, not run |
| T11 public-readiness docs | `author` | README drops the local-feed-only note once published, `CHANGELOG.md` has a 0.1.0 draft, `docs/release.md` lists the steps |
| T12 saaaat router plan | `plan` (fable) when user asks, else inline | Spec for the client router over `src/routes/` and `$app/*` shims, after T6 lands |
| T13 user-only items | user | License, real deploy token, NuGet publish, visibility, test approach without jsdom |

## Order

T6 and T7 in parallel (distinct keys), T8 inline meanwhile. T9 and T10 in parallel after. Each
writer is gated against this plan with `worker.js gate <key> --criteria <this file>`, then landed.
T11 after T6 and T7 land so the docs describe what exists. T12 last.

## T6 design question for the writer

Pare places an output at its source's path, so hashed names cannot come from Pare's output path
alone. Two options: the SDK's content fingerprinting and its endpoint routes, or Husk-written
hashed copies registered as static web assets. The writer picks one in its first step and records
it in `docs/decisions/` before building on it. Constraint: compiled bytes stay identical to
Node's (R4, L001); only file names and map entries change.

## Risks

- Hashing changes every import map entry; the browser tests (T20) must pass in all three
  browsers after.
- The deploy command cannot be proven against Cloudflare here; the fake server pins the
  documented request shapes only.
- A fingerprinted name changes when any imported module changes, so the map, not the module
  text, carries the hash (no rewriting of compiled code).

## Not in this plan

Router implementation, type checking, older-browser add-on, Linux and Windows runs (CI is written
in T10; running it needs a pushed repo, the user's call).
