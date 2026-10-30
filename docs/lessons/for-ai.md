# Lessons Learned — for AI agents

READ THIS BEFORE STARTING WORK on this repo. Concrete, mistake-preventing facts an agent needs so
it does not repeat a past error. Distinct from docs/constitution.md (binding rules) — these are
hard-won gotchas.

When you (an agent) learn something that would have saved you a mistake — a build flag, a
non-obvious dependency, a trap in the codebase — APPEND it here (one terse imperative line) so the
next agent doesn't hit it. Keep entries true; delete stale ones.

- (nothing yet)
- `git merge`'s auto `pack-refs` (gc --auto) is refused by the dotfiles reference-transaction hook as "deleting 'main'"; the merge itself lands, refs stay intact. Ignore it; don't set GIT_ALLOW_PROTECTED to silence it.
- Inside a target, `<X Include="glob" Meta="%(Filename)" />` after an earlier `X` Include batches over the `X` items already added and adds each new file once per batch; the SDK's `DiscoverPrecompressedAssets` then fails with "An item with the same key has already been added". Put constant metadata on each Include and compute the `%(...)` metadata in a separate `<X Meta="..." />` update.
- A break-check on a file not yet committed cannot be undone with `git checkout --`, and stacked breaks go unnoticed. Commit the green state first.
- Svelte's compiler picks its base64 encoder (`window.btoa`, else `Buffer`) when the bundle loads, and bare V8 has neither: anything the engine needs from the host must be defined before the compiler runs, not in glue loaded after it. Found only by the corpus sweep (`scripts/node-reference/corpus.sh`), not the fixtures.
- An MSBuild target whose `Outputs` is a 1:1 transform of its `Inputs` items builds *partially*: the task gets only the stale items. A source with several output items (Husk's .js/.css/.map) then arrives with some outputs missing. Decide staleness per source inside the task instead.
- worker.js land refuses a worktree whose ignored bin/ files differ from main ("holds ignored files"); after gate pass, merge the chore/worker-<key> branch --no-ff into the feature branch by hand and list the worktree in a cleanup script.
