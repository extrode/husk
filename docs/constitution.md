# Constitution — husk

Binding project rules. These are the WON'T-change decisions. Agents and humans obey them;
changing one is a deliberate governance act, not a casual edit. This is NOT lessons-learned
(see docs/lessons/) and NOT mechanical conventions (see docs/conventions.md).

Status: active · Last reviewed: 2026-10-31

## Stack constraints
- No Node or Deno at build time in what Husk ships. Node may appear in research (reference runs).
- The Svelte compiler is pinned to one version (5.57.2). Outputs are checked byte for byte against Node running the same bundle.
- Husk is a separate product built on Pare, not a fork (decision 2026-10-17-001). Plumbing Husk needs from Pare is added to Pare as a public extension point, in Pare's repo.
- No package is added without asking. The approved set is in `AGENTS.md`. Playwright is excluded: its .NET package bundles Node.
- C# sources stay ASCII.

## Architecture invariants
- The compiler runs in embedded V8 (ClearScript), never in a child process.
- A build with no source changed compiles nothing (L002).
- A failed compile never replaces the last good output (L003).
- Husk's compile result is identical to Node's for the same Svelte version (L001).

## Security / data
- Publishing to nuget.org uses trusted publishing (decision 008): no API key exists in the repo or in GitHub secrets.
- Third-party actions in workflows are pinned by commit SHA.
- The deploy tool sends only to the Cloudflare API over HTTPS; a non-HTTPS base URL is a test-only switch.

## Testing
- Every code change has tests on the same branch, failure paths included.
- Enacted laws in `docs/laws/` hold before every merge: `deno run -A ~/.claude/tools/laws-check.js --run`.
  Laws: L001 identical to Node, L002 no change no compile, L003 failed compile keeps output.
- A test is verified by breaking the code it covers.

## Governance
- Supersedes ad-hoc practices. To change a rule: record the change + date + why, right here.
- 2026-10-31: filled in from `AGENTS.md`, decisions and laws; no rule changed.
