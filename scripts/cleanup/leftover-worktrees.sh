#!/usr/bin/env bash
# Removes the four leftover worktrees (base-check, base-safari, worker-t6-hashed, worker-t7-deploy)
# found after the Safari fix, 2026-11-01. Dry run unless --execute or -e. Branches are kept.
set -euo pipefail
exec_=0; case "${1:-}" in --execute|-e) exec_=1;; esac
repo=$(git rev-parse --show-toplevel)
[ "$(git -C "$repo" branch --show-current)" = dev ] || { echo "refuse: run from dev"; exit 1; }
for name in base-check base-safari worker-t6-hashed worker-t7-deploy; do
  d="$repo/.worktrees/$name"
  [ -d "$d" ] || { echo "skip $name (gone)"; continue; }
  if [ -n "$(git -C "$d" status --porcelain)" ]; then echo "refuse $name: uncommitted changes"; continue; fi
  if [ "$exec_" = 1 ]; then git worktree remove "$d"; else echo "would remove $d"; fi
done
git worktree list
