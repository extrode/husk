# Lessons Learned — for AI agents

READ THIS BEFORE STARTING WORK on this repo. Concrete, mistake-preventing facts an agent needs so
it does not repeat a past error. Distinct from docs/constitution.md (binding rules) — these are
hard-won gotchas.

When you (an agent) learn something that would have saved you a mistake — a build flag, a
non-obvious dependency, a trap in the codebase — APPEND it here (one terse imperative line) so the
next agent doesn't hit it. Keep entries true; delete stale ones.

- (nothing yet)
- `git merge`'s auto `pack-refs` (gc --auto) is refused by the dotfiles reference-transaction hook as "deleting 'main'"; the merge itself lands, refs stay intact. Ignore it; don't set GIT_ALLOW_PROTECTED to silence it.
